using DudeMeds.Models.DTOs.ClinicalCodes;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Vitality.Models.EntityClasses;
using Vitality.Models.Helpers;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    /// <summary>
    /// TEL-19 - ICD-10-CM and CPT reference data.
    ///
    /// Takes the DI-registered <see cref="MainContext"/> rather than deriving from
    /// BaseRepo, so it does not add to the repositories that build their own
    /// context (docs/project-map.md, quirk 1).
    ///
    /// An import bulk-copies the parsed file into a temp table and MERGEs it into
    /// the release's rows in one transaction. A full ICD-10-CM release is about
    /// 98,000 codes, which row-by-row EF inserts would take minutes to write.
    ///
    /// Every statement that touches the release's rows works in batches of
    /// <see cref="WriteBatchSize"/>. The hosted development database enforces
    /// a query governor cost limit of 3000, and a single MERGE of a full release
    /// is estimated just above it (3054), so the server refuses it. The import
    /// stays within whatever limit the host sets instead of lifting it.
    /// </summary>
    public class ClinicalCodesRepo : IClinicalCodesRepo
    {
        private const int CommandTimeoutSeconds = 300;
        private const int WriteBatchSize = 5000;
        private const int Icd10LongDescriptionMax = 400;
        private const int CptLongDescriptionMax = 1000;

        private readonly MainContext _db;
        private readonly IAuditService _auditService;

        public ClinicalCodesRepo(MainContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        public ImportCodeSetResultDTO ImportCodeSet(ImportCodeSetRequestDTO request, string? fileName, Stream content, long userId)
        {
            ArgumentNullException.ThrowIfNull(content);

            var system = request?.CodeSystem?.Trim().ToUpperInvariant();
            if (system != ClinicalCodeSystem.Icd10Cm && system != ClinicalCodeSystem.Cpt)
                return Fail($"Code system must be '{ClinicalCodeSystem.Icd10Cm}' or '{ClinicalCodeSystem.Cpt}'.");

            var label = request!.VersionLabel?.Trim();
            if (string.IsNullOrEmpty(label) || label.Length > 32)
                return Fail("A version label of up to 32 characters is required, e.g. 'FY2026'.");

            // ---- the window this release is in force

            DateTime? effective = request.EffectiveDate?.Date;
            DateTime? termination = request.TerminationDate?.Date;

            if (effective is null && system == ClinicalCodeSystem.Icd10Cm
                && ClinicalCodeFileParser.Icd10CmFiscalYearWindow(label) is { } window)
            {
                effective = window.Effective;
                termination ??= window.Termination;
            }

            if (effective is null)
                return Fail("An effective date is required for this version label.");

            if (termination is not null && termination < effective)
                return Fail("The termination date cannot be before the effective date.");

            var otherVersions = _db.SYS_CodeSetVersions.AsNoTracking()
                .Where(v => v.CodeSystem == system && v.VersionLabel != label)
                .Select(v => new { v.VersionLabel, v.EffectiveDate })
                .ToList();

            var clash = otherVersions.FirstOrDefault(v => v.EffectiveDate == effective);
            if (clash is not null)
                return Fail($"Release '{clash.VersionLabel}' already takes effect on {effective:yyyy-MM-dd}.");

            // Loading an older release after a newer one: end it where the newer begins.
            var nextEffective = otherVersions
                .Where(v => v.EffectiveDate > effective)
                .Select(v => (DateTime?)v.EffectiveDate)
                .Min();
            if (nextEffective is not null && (termination is null || termination >= nextEffective))
                termination = nextEffective.Value.AddDays(-1);

            // ---- parse; any problem rejects the whole file

            ClinicalCodeParseResult parsed;
            using (var reader = new StreamReader(content, detectEncodingFromByteOrderMarks: true))
            {
                parsed = system == ClinicalCodeSystem.Icd10Cm
                    ? ClinicalCodeFileParser.ParseIcd10CmOrderFile(reader)
                    : ClinicalCodeFileParser.ParseCptTabDelimited(reader);
            }

            var maxLong = system == ClinicalCodeSystem.Icd10Cm ? Icd10LongDescriptionMax : CptLongDescriptionMax;
            var tooLong = parsed.Codes.FirstOrDefault(c => c.LongDescription.Length > maxLong);
            if (tooLong is not null)
                return Fail($"Code {tooLong.Code} has a description longer than {maxLong} characters.");

            if (!parsed.IsValid)
            {
                var result = Fail($"The file was not imported: {parsed.ErrorCount} problem(s) found. Nothing was changed.");
                result.ErrorCount = parsed.ErrorCount;
                result.Errors = parsed.Errors;
                return result;
            }

            // ---- load

            var now = DateTime.UtcNow;
            using var tx = _db.Database.BeginTransaction();
            var connection = (SqlConnection)_db.Database.GetDbConnection();
            var sqlTx = (SqlTransaction)tx.GetDbTransaction();

            var version = _db.SYS_CodeSetVersions
                .FirstOrDefault(v => v.CodeSystem == system && v.VersionLabel == label);

            if (version is null)
            {
                version = new SYS_CodeSetVersion
                {
                    CodeSystem = system,
                    VersionLabel = label,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = now
                };
                _db.SYS_CodeSetVersions.Add(version);
            }
            else
            {
                version.ModifiedBy = userId;
                version.ModifiedDate = now;
            }

            version.EffectiveDate = effective.Value;
            version.TerminationDate = termination;
            _db.SaveChanges();

            var codeTable = system == ClinicalCodeSystem.Icd10Cm ? "SYS_Icd10Code" : "SYS_CptCode";

            Execute(connection, sqlTx, StagingTableSql);
            BulkCopyToStaging(connection, sqlTx, parsed.Codes);

            var (inserted, updated) = MergeInBatches(
                connection, sqlTx,
                system == ClinicalCodeSystem.Icd10Cm ? MergeIcd10BatchSql : MergeCptBatchSql,
                version, parsed.Codes.Count);

            var deactivated = DeactivateCodesNotInFile(connection, sqlTx, codeTable, version);

            var closed = CloseEarlierReleases(connection, sqlTx, codeTable, version, userId);

            Execute(connection, sqlTx, "DROP TABLE #CodeStaging;");

            version.CodeCount = parsed.Codes.Count;
            version.SourceFileName = fileName is null ? null : Path.GetFileName(fileName);
            version.ImportedBy = userId;
            version.ImportedDate = now;
            _db.SaveChanges();

            tx.Commit();

            _auditService.LogEntityChange(
                action: "Import",
                entityType: "SYS_CodeSetVersion",
                entityId: version.CodeSetVersionId,
                userId: userId,
                description: $"{system} {label} imported from '{version.SourceFileName}': {parsed.Codes.Count} codes, "
                           + $"{inserted} inserted, {updated} updated, {deactivated} deactivated, {closed} earlier release(s) closed",
                module: "ClinicalCodes");

            return new ImportCodeSetResultDTO
            {
                Success = true,
                Message = $"{system} {label} imported: {parsed.Codes.Count} codes.",
                CodeSetVersionId = version.CodeSetVersionId,
                CodesInFile = parsed.Codes.Count,
                Inserted = inserted,
                Updated = updated,
                Deactivated = deactivated,
                ReleasesClosed = closed
            };
        }

        public List<CodeSetVersionDTO> GetCodeSetVersions(string? codeSystem)
        {
            var system = codeSystem?.Trim().ToUpperInvariant();

            return _db.SYS_CodeSetVersions.AsNoTracking()
                .Where(v => string.IsNullOrEmpty(system) || v.CodeSystem == system)
                .OrderBy(v => v.CodeSystem)
                .ThenByDescending(v => v.EffectiveDate)
                .Select(v => new CodeSetVersionDTO
                {
                    CodeSetVersionId = v.CodeSetVersionId,
                    CodeSystem = v.CodeSystem,
                    VersionLabel = v.VersionLabel,
                    EffectiveDate = v.EffectiveDate,
                    TerminationDate = v.TerminationDate,
                    CodeCount = v.CodeCount,
                    SourceFileName = v.SourceFileName,
                    ImportedDate = v.ImportedDate,
                    IsActive = v.IsActive
                })
                .ToList();
        }

        public Icd10CodeDTO? GetIcd10Code(string code, DateTime onDate)
        {
            var normalized = (code ?? string.Empty).Replace(".", string.Empty).Trim().ToUpperInvariant();
            if (normalized.Length == 0) return null;

            var day = onDate.Date;

            return (
                from c in _db.SYS_Icd10Codes.AsNoTracking()
                join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
                where c.Code == normalized
                   && c.IsActive
                   && v.IsActive
                   && c.EffectiveDate <= day
                   && (c.TerminationDate == null || c.TerminationDate >= day)
                orderby c.EffectiveDate descending
                select new Icd10CodeDTO
                {
                    Icd10CodeId = c.Icd10CodeId,
                    Code = c.Code,
                    DisplayCode = c.DisplayCode,
                    ShortDescription = c.ShortDescription,
                    LongDescription = c.LongDescription,
                    IsBillable = c.IsBillable,
                    EffectiveDate = c.EffectiveDate,
                    TerminationDate = c.TerminationDate,
                    CodeSetVersionId = v.CodeSetVersionId,
                    VersionLabel = v.VersionLabel
                }).FirstOrDefault();
        }

        // ------------------------------------------------------------ SQL

        // COLLATE DATABASE_DEFAULT: a temp table otherwise takes tempdb's
        // collation, and the MERGE join on Code would fail on a mismatch.
        // RowNo is the clustered key so each batch reads one contiguous range.
        private const string StagingTableSql = @"
CREATE TABLE #CodeStaging (
    RowNo            INT            NOT NULL PRIMARY KEY,
    Code             NVARCHAR(8)    COLLATE DATABASE_DEFAULT NOT NULL UNIQUE,
    DisplayCode      NVARCHAR(9)    COLLATE DATABASE_DEFAULT NULL,
    ShortDescription NVARCHAR(60)   COLLATE DATABASE_DEFAULT NULL,
    LongDescription  NVARCHAR(1000) COLLATE DATABASE_DEFAULT NOT NULL,
    IsBillable       BIT            NOT NULL,
    SortOrder        INT            NULL
);";

        // One batch of the file (RowNo @from..@to) into the release's rows.
        // Codes the file no longer has are handled by DeactivateCodesNotInFile.
        private const string MergeIcd10BatchSql = @"
DECLARE @actions TABLE (ActionName NVARCHAR(10));

WITH t AS (SELECT * FROM dbo.SYS_Icd10Code WHERE CodeSetVersionId = @v)
MERGE t
USING (SELECT * FROM #CodeStaging WHERE RowNo BETWEEN @from AND @to) AS s
   ON t.Code = s.Code
WHEN MATCHED AND (
       t.DisplayCode <> s.DisplayCode
    OR ISNULL(t.ShortDescription, N'') <> ISNULL(s.ShortDescription, N'')
    OR t.LongDescription <> s.LongDescription
    OR t.IsBillable <> s.IsBillable
    OR ISNULL(t.SortOrder, -1) <> ISNULL(s.SortOrder, -1)
    OR t.IsActive = 0
    OR t.EffectiveDate <> @eff
    OR ISNULL(t.TerminationDate, '99991231') <> ISNULL(@term, '99991231'))
  THEN UPDATE SET
       DisplayCode = s.DisplayCode,
       ShortDescription = s.ShortDescription,
       LongDescription = s.LongDescription,
       IsBillable = s.IsBillable,
       SortOrder = s.SortOrder,
       EffectiveDate = @eff,
       TerminationDate = @term,
       IsActive = 1,
       ModifiedDate = GETUTCDATE()
WHEN NOT MATCHED BY TARGET
  THEN INSERT (CodeSetVersionId, Code, DisplayCode, ShortDescription, LongDescription,
               IsBillable, EffectiveDate, TerminationDate, IsActive, SortOrder, CreatedDate)
       VALUES (@v, s.Code, s.DisplayCode, s.ShortDescription, s.LongDescription,
               s.IsBillable, @eff, @term, 1, s.SortOrder, GETUTCDATE())
OUTPUT $action INTO @actions (ActionName);

SELECT
    ISNULL(SUM(CASE WHEN ActionName = 'INSERT' THEN 1 ELSE 0 END), 0),
    ISNULL(SUM(CASE WHEN ActionName = 'UPDATE' THEN 1 ELSE 0 END), 0)
FROM @actions;";

        private const string MergeCptBatchSql = @"
DECLARE @actions TABLE (ActionName NVARCHAR(10));

WITH t AS (SELECT * FROM dbo.SYS_CptCode WHERE CodeSetVersionId = @v)
MERGE t
USING (SELECT * FROM #CodeStaging WHERE RowNo BETWEEN @from AND @to) AS s
   ON t.Code = s.Code
WHEN MATCHED AND (
       ISNULL(t.ShortDescription, N'') <> ISNULL(s.ShortDescription, N'')
    OR t.LongDescription <> s.LongDescription
    OR t.IsActive = 0
    OR t.EffectiveDate <> @eff
    OR ISNULL(t.TerminationDate, '99991231') <> ISNULL(@term, '99991231'))
  THEN UPDATE SET
       ShortDescription = s.ShortDescription,
       LongDescription = s.LongDescription,
       EffectiveDate = @eff,
       TerminationDate = @term,
       IsActive = 1,
       ModifiedDate = GETUTCDATE()
WHEN NOT MATCHED BY TARGET
  THEN INSERT (CodeSetVersionId, Code, ShortDescription, LongDescription,
               EffectiveDate, TerminationDate, IsActive, CreatedDate)
       VALUES (@v, s.Code, s.ShortDescription, s.LongDescription,
               @eff, @term, 1, GETUTCDATE())
OUTPUT $action INTO @actions (ActionName);

SELECT
    ISNULL(SUM(CASE WHEN ActionName = 'INSERT' THEN 1 ELSE 0 END), 0),
    ISNULL(SUM(CASE WHEN ActionName = 'UPDATE' THEN 1 ELSE 0 END), 0)
FROM @actions;";

        // ------------------------------------------------------------ helpers

        private static void Execute(SqlConnection connection, SqlTransaction tx, string sql)
        {
            using var cmd = new SqlCommand(sql, connection, tx) { CommandTimeout = CommandTimeoutSeconds };
            cmd.ExecuteNonQuery();
        }

        private static void BulkCopyToStaging(SqlConnection connection, SqlTransaction tx, IReadOnlyList<ParsedClinicalCode> codes)
        {
            var table = new DataTable();
            table.Columns.Add("RowNo", typeof(int));
            table.Columns.Add("Code", typeof(string));
            table.Columns.Add("DisplayCode", typeof(string));
            table.Columns.Add("ShortDescription", typeof(string));
            table.Columns.Add("LongDescription", typeof(string));
            table.Columns.Add("IsBillable", typeof(bool));
            table.Columns.Add("SortOrder", typeof(int));

            var rowNo = 0;
            foreach (var c in codes)
            {
                table.Rows.Add(
                    ++rowNo,
                    c.Code,
                    (object?)c.DisplayCode ?? DBNull.Value,
                    (object?)c.ShortDescription ?? DBNull.Value,
                    c.LongDescription,
                    c.IsBillable,
                    (object?)c.SortOrder ?? DBNull.Value);
            }

            using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, tx)
            {
                DestinationTableName = "#CodeStaging",
                BulkCopyTimeout = CommandTimeoutSeconds,
                BatchSize = 5000
            };
            foreach (DataColumn col in table.Columns)
                bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);

            bulk.WriteToServer(table);
        }

        private static (int Inserted, int Updated) MergeInBatches(
            SqlConnection connection, SqlTransaction tx, string sql, SYS_CodeSetVersion version, int rowCount)
        {
            int inserted = 0, updated = 0;

            for (var from = 1; from <= rowCount; from += WriteBatchSize)
            {
                using var cmd = new SqlCommand(sql, connection, tx) { CommandTimeout = CommandTimeoutSeconds };
                cmd.Parameters.Add("@v", SqlDbType.BigInt).Value = version.CodeSetVersionId;
                cmd.Parameters.Add("@eff", SqlDbType.Date).Value = version.EffectiveDate;
                cmd.Parameters.Add("@term", SqlDbType.Date).Value = (object?)version.TerminationDate ?? DBNull.Value;
                cmd.Parameters.Add("@from", SqlDbType.Int).Value = from;
                cmd.Parameters.Add("@to", SqlDbType.Int).Value = from + WriteBatchSize - 1;

                using var reader = cmd.ExecuteReader();
                reader.Read();
                inserted += reader.GetInt32(0);
                updated += reader.GetInt32(1);
            }

            return (inserted, updated);
        }

        /// <summary>
        /// Codes an earlier import of this release had but the file no longer has.
        /// Deactivated rather than deleted - later work (TEL-20) will reference them.
        /// </summary>
        private static int DeactivateCodesNotInFile(
            SqlConnection connection, SqlTransaction tx, string codeTable, SYS_CodeSetVersion version)
        {
            // codeTable is one of two constants, never input.
            var sql = $@"
UPDATE TOP (@batch) t
   SET IsActive = 0, ModifiedDate = GETUTCDATE()
  FROM dbo.{codeTable} t
 WHERE t.CodeSetVersionId = @v
   AND t.IsActive = 1
   AND NOT EXISTS (SELECT 1 FROM #CodeStaging s WHERE s.Code = t.Code);";

            return ExecuteUntilDone(connection, tx, sql, cmd =>
                cmd.Parameters.Add("@v", SqlDbType.BigInt).Value = version.CodeSetVersionId);
        }

        /// <summary>Runs an UPDATE TOP (@batch) repeatedly until a batch comes back short.</summary>
        private static int ExecuteUntilDone(
            SqlConnection connection, SqlTransaction tx, string sql, Action<SqlCommand> addParameters)
        {
            var total = 0;
            int affected;
            do
            {
                using var cmd = new SqlCommand(sql, connection, tx) { CommandTimeout = CommandTimeoutSeconds };
                cmd.Parameters.Add("@batch", SqlDbType.Int).Value = WriteBatchSize;
                addParameters(cmd);
                affected = cmd.ExecuteNonQuery();
                total += affected;
            } while (affected == WriteBatchSize);

            return total;
        }

        /// <summary>
        /// Ends every earlier release of the same code system that was still open
        /// on the day this one takes effect - including the CMS April update case,
        /// where a mid-year release supersedes the October one.
        /// </summary>
        private static int CloseEarlierReleases(
            SqlConnection connection, SqlTransaction tx, string codeTable, SYS_CodeSetVersion version, long userId)
        {
            const string closeReleasesSql = @"
UPDATE dbo.SYS_CodeSetVersion
   SET TerminationDate = DATEADD(day, -1, @eff), ModifiedBy = @u, ModifiedDate = GETUTCDATE()
OUTPUT inserted.CodeSetVersionId
 WHERE CodeSystem = @sys
   AND CodeSetVersionId <> @v
   AND EffectiveDate < @eff
   AND (TerminationDate IS NULL OR TerminationDate >= @eff);";

            var closedIds = new List<long>();
            using (var cmd = new SqlCommand(closeReleasesSql, connection, tx) { CommandTimeout = CommandTimeoutSeconds })
            {
                cmd.Parameters.Add("@v", SqlDbType.BigInt).Value = version.CodeSetVersionId;
                cmd.Parameters.Add("@eff", SqlDbType.Date).Value = version.EffectiveDate;
                cmd.Parameters.Add("@sys", SqlDbType.NVarChar, 16).Value = version.CodeSystem;
                cmd.Parameters.Add("@u", SqlDbType.BigInt).Value = userId;
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) closedIds.Add(reader.GetInt64(0));
            }

            // codeTable is one of two constants, never input.
            var closeCodesSql = $@"
UPDATE TOP (@batch) c
   SET TerminationDate = @close, ModifiedDate = GETUTCDATE()
  FROM dbo.{codeTable} c
 WHERE c.CodeSetVersionId = @id
   AND (c.TerminationDate IS NULL OR c.TerminationDate <> @close);";

            var closeDate = version.EffectiveDate.AddDays(-1);
            foreach (var id in closedIds)
            {
                ExecuteUntilDone(connection, tx, closeCodesSql, cmd =>
                {
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
                    cmd.Parameters.Add("@close", SqlDbType.Date).Value = closeDate;
                });
            }

            return closedIds.Count;
        }

        private static ImportCodeSetResultDTO Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
