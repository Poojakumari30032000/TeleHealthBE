using DudeMeds.Models.DTOs.ClinicalCodes;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Vitality.Models.EntityClasses;
using Vitality.Models.Helpers;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    /// <summary>
    /// TEL-20 - Category, Service and Package mapping to ICD-10 / CPT codes.
    ///
    /// Takes the DI-registered <see cref="MainContext"/> rather than deriving from
    /// BaseRepo, matching <see cref="ClinicalCodesRepo"/> (TEL-19).
    ///
    /// Two things shape the queries here.
    ///
    /// First, reference codes are stored per release, so "the code E11.65" is not
    /// one row but one row per fiscal year. Every read therefore resolves a
    /// mapping's code string against the release in force on a date, exactly as
    /// <see cref="ClinicalCodesRepo.GetIcd10Code"/> does.
    ///
    /// Second, no query here uses <c>list.Contains(column)</c>. EF Core 8 compiles
    /// that to <c>OPENJSON</c>, which needs database compatibility level 130 or
    /// higher (TEL-10). Reads use ordinary joins, and the bulk review-flag pass is
    /// one set-based statement rather than a list parameter.
    /// </summary>
    public class ClinicalCodeMappingsRepo : IClinicalCodeMappingsRepo
    {
        /// <summary>
        /// A save resolves each code with its own indexed lookup, so the request
        /// is capped to keep that bounded. No Category, Service or Package
        /// legitimately carries hundreds of codes.
        /// </summary>
        public const int MaxCodesPerSave = 200;

        private readonly MainContext _db;
        private readonly IAuditService _auditService;

        public ClinicalCodeMappingsRepo(MainContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        // ------------------------------------------------------------ reads

        public List<ClinicalCodeMappingDTO> GetMappings(GetClinicalCodeMappingsRequestDTO request)
        {
            var targetType = ClinicalCodeFormat.NormalizeTargetType(request?.TargetType);
            if (targetType is null || request!.TargetId <= 0) return new List<ClinicalCodeMappingDTO>();

            var day = (request.OnDate ?? DateTime.UtcNow).Date;
            var includeInactive = request.IncludeInactive;

            var mappings = _db.SYS_ClinicalCodeMappings.AsNoTracking()
                .Where(m => m.TargetType == targetType
                         && m.TargetId == request.TargetId
                         && (includeInactive || m.IsActive))
                .OrderBy(m => m.CodeSystem)
                .ThenBy(m => m.Code)
                .ToList();

            return Resolve(mappings, day);
        }

        public List<ClinicalCodeMappingDTO> GetMappingsNeedingReview(DateTime onDate)
        {
            var day = onDate.Date;

            var mappings = _db.SYS_ClinicalCodeMappings.AsNoTracking()
                .Where(m => m.IsActive && m.NeedsReview)
                .OrderBy(m => m.TargetType)
                .ThenBy(m => m.TargetId)
                .ThenBy(m => m.Code)
                .ToList();

            return Resolve(mappings, day);
        }

        public ClinicalCodeUsageDTO GetCodeUsage(string? codeSystem, string? code)
        {
            var system = ClinicalCodeFormat.NormalizeCodeSystem(codeSystem);
            var normalized = ClinicalCodeFormat.Normalize(code);

            var usage = new ClinicalCodeUsageDTO
            {
                CodeSystem = system ?? string.Empty,
                Code = normalized
            };

            if (system is null || normalized.Length == 0) return usage;

            var users = _db.SYS_ClinicalCodeMappings.AsNoTracking()
                .Where(m => m.IsActive && m.CodeSystem == system && m.Code == normalized)
                .Select(m => new { m.TargetType, m.TargetId })
                .Distinct()
                .ToList();

            usage.MappingCount = users.Count;
            usage.Targets = users
                .Select(u => new ClinicalCodeUsageTargetDTO
                {
                    TargetType = u.TargetType,
                    TargetId = u.TargetId,
                    TargetName = TargetName(u.TargetType, u.TargetId)
                })
                .OrderBy(t => t.TargetType)
                .ThenBy(t => t.TargetId)
                .ToList();

            return usage;
        }

        // ------------------------------------------------------------ writes

        public SaveClinicalCodeMappingsResultDTO SaveMappings(SaveClinicalCodeMappingsRequestDTO request, long userId)
        {
            var result = new SaveClinicalCodeMappingsResultDTO();

            var targetType = ClinicalCodeFormat.NormalizeTargetType(request?.TargetType);
            if (targetType is null)
                return Fail(result, $"Target type must be one of {ClinicalCodeTargetType.Category}, "
                                  + $"{ClinicalCodeTargetType.Service} or {ClinicalCodeTargetType.Package}.");

            var targetId = request!.TargetId;
            if (targetId <= 0 || !TargetExists(targetType, targetId))
                return Fail(result, $"No {targetType.ToLowerInvariant()} with id {targetId} exists.");

            var requested = request.Codes ?? new List<ClinicalCodeRefDTO>();
            if (requested.Count > MaxCodesPerSave)
                return Fail(result, $"A target takes at most {MaxCodesPerSave} codes; {requested.Count} were sent.");

            var day = (request.OnDate ?? DateTime.UtcNow).Date;

            // ---- resolve the whole request first: one bad code refuses all of it,
            //      so a partially coded target is never written.

            var desired = new Dictionary<(string System, string Code), ResolvedCode>();

            foreach (var reference in requested)
            {
                var system = ClinicalCodeFormat.NormalizeCodeSystem(reference?.CodeSystem);
                if (system is null)
                {
                    result.Rejected.Add($"'{reference?.CodeSystem}' is not a code system this application knows.");
                    continue;
                }

                var normalized = ClinicalCodeFormat.Normalize(reference!.Code);
                if (!ClinicalCodeFormat.IsValid(system, normalized))
                {
                    result.Rejected.Add($"'{reference.Code}' is not a well formed {system} code.");
                    continue;
                }

                var key = (system, normalized);
                if (desired.ContainsKey(key)) continue;   // the same code twice is not an error

                var resolved = ResolveCode(system, normalized, day);
                if (resolved is null)
                {
                    result.Rejected.Add(
                        $"{system} {ClinicalCodeFormat.ToDisplayCode(system, normalized)} was not in force on "
                      + $"{day:yyyy-MM-dd}, so it cannot be mapped.");
                    continue;
                }

                desired.Add(key, resolved);
            }

            if (result.Rejected.Count > 0)
                return Fail(result, $"Nothing was changed: {result.Rejected.Count} code(s) were refused.");

            // ---- apply

            var existing = _db.SYS_ClinicalCodeMappings
                .Where(m => m.TargetType == targetType && m.TargetId == targetId)
                .ToList();

            var before = Describe(existing.Where(m => m.IsActive));
            var now = DateTime.UtcNow;

            foreach (var mapping in existing)
            {
                var key = (mapping.CodeSystem, mapping.Code);

                if (desired.TryGetValue(key, out var resolved))
                {
                    // Still wanted. Re-point it at the release it now resolves to,
                    // revive it if it had been removed, and clear any review flag -
                    // the code was just checked to be in force.
                    var wasInactive = !mapping.IsActive;

                    mapping.Icd10CodeId = key.CodeSystem == ClinicalCodeSystem.Icd10Cm ? resolved.CodeRowId : null;
                    mapping.CptCodeId = key.CodeSystem == ClinicalCodeSystem.Cpt ? resolved.CodeRowId : null;
                    mapping.IsActive = true;
                    mapping.NeedsReview = false;
                    mapping.ReviewReason = null;
                    mapping.ModifiedBy = userId;
                    mapping.ModifiedDate = now;

                    if (wasInactive) result.Added++; else result.Unchanged++;

                    desired.Remove(key);
                }
                else if (mapping.IsActive)
                {
                    mapping.IsActive = false;
                    mapping.ModifiedBy = userId;
                    mapping.ModifiedDate = now;
                    result.Removed++;
                }
            }

            foreach (var entry in desired)
            {
                var system = entry.Key.System;
                var code = entry.Key.Code;

                _db.SYS_ClinicalCodeMappings.Add(new SYS_ClinicalCodeMapping
                {
                    TargetType = targetType,
                    TargetId = targetId,
                    CodeSystem = system,
                    Code = code,
                    Icd10CodeId = system == ClinicalCodeSystem.Icd10Cm ? entry.Value.CodeRowId : null,
                    CptCodeId = system == ClinicalCodeSystem.Cpt ? entry.Value.CodeRowId : null,
                    NeedsReview = false,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = now
                });
                result.Added++;
            }

            _db.SaveChanges();

            var after = Describe(_db.SYS_ClinicalCodeMappings.AsNoTracking()
                .Where(m => m.TargetType == targetType && m.TargetId == targetId && m.IsActive)
                .ToList());

            _auditService.LogEntityChange(
                action: "Update",
                entityType: "SYS_ClinicalCodeMapping",
                entityId: targetId,
                oldValues: new { TargetType = targetType, TargetId = targetId, Codes = before },
                newValues: new { TargetType = targetType, TargetId = targetId, Codes = after },
                userId: userId,
                description: $"{targetType} {targetId} clinical codes: {result.Added} added, "
                           + $"{result.Removed} removed, {result.Unchanged} unchanged",
                module: "ClinicalCodes");

            result.Success = true;
            result.Message = $"{result.Added} added, {result.Removed} removed, {result.Unchanged} unchanged.";
            return result;
        }

        public SaveClinicalCodeMappingsResultDTO DeleteMapping(long clinicalCodeMappingId, long userId)
        {
            var result = new SaveClinicalCodeMappingsResultDTO();

            var mapping = _db.SYS_ClinicalCodeMappings
                .FirstOrDefault(m => m.ClinicalCodeMappingId == clinicalCodeMappingId);

            if (mapping is null)
                return Fail(result, $"No mapping with id {clinicalCodeMappingId} exists.");

            if (!mapping.IsActive)
            {
                result.Success = true;
                result.Message = "The mapping had already been removed.";
                return result;
            }

            mapping.IsActive = false;
            mapping.ModifiedBy = userId;
            mapping.ModifiedDate = DateTime.UtcNow;
            _db.SaveChanges();

            result.Removed = 1;

            _auditService.LogEntityChange(
                action: "Delete",
                entityType: "SYS_ClinicalCodeMapping",
                entityId: mapping.ClinicalCodeMappingId,
                oldValues: new { mapping.TargetType, mapping.TargetId, mapping.CodeSystem, mapping.Code, IsActive = true },
                newValues: new { mapping.TargetType, mapping.TargetId, mapping.CodeSystem, mapping.Code, IsActive = false },
                userId: userId,
                description: $"{mapping.CodeSystem} {mapping.Code} unmapped from {mapping.TargetType} {mapping.TargetId}",
                module: "ClinicalCodes");

            result.Success = true;
            result.Message = $"{mapping.CodeSystem} {ClinicalCodeFormat.ToDisplayCode(mapping.CodeSystem, mapping.Code)} "
                           + $"was removed from {mapping.TargetType} {mapping.TargetId}.";
            return result;
        }

        /// <summary>
        /// One statement per direction rather than a row-by-row pass: a code
        /// release can terminate thousands of codes at once, and pulling every
        /// mapping into memory to compare it would make an import's tail longer
        /// than the import.
        /// </summary>
        public RefreshClinicalCodeReviewFlagsResultDTO RefreshReviewFlags(DateTime onDate, long userId)
        {
            var day = onDate.Date;
            var reason = $"The mapped code was not in force on {day:yyyy-MM-dd}.";

            var flagged = _db.Database.ExecuteSqlRaw(FlagSql,
                new SqlParameter("@on", day),
                new SqlParameter("@user", userId),
                new SqlParameter("@reason", reason));

            var cleared = _db.Database.ExecuteSqlRaw(ClearSql,
                new SqlParameter("@on", day),
                new SqlParameter("@user", userId));

            if (flagged > 0 || cleared > 0)
            {
                _auditService.LogEntityChange(
                    action: "Update",
                    entityType: "SYS_ClinicalCodeMapping",
                    entityId: null,
                    userId: userId,
                    description: $"Review flags refreshed against {day:yyyy-MM-dd}: {flagged} flagged, {cleared} cleared",
                    module: "ClinicalCodes");
            }

            return new RefreshClinicalCodeReviewFlagsResultDTO { Flagged = flagged, Cleared = cleared };
        }

        // ------------------------------------------------------------ SQL

        // A mapping is in force when some active code row, in an active release,
        // carries its code string on the date. The CodeSystem test inside each
        // EXISTS is what makes one predicate cover both systems: for a CPT
        // mapping the ICD arm matches nothing, and vice versa.
        private const string InForceExists = @"
    EXISTS (
        SELECT 1
        FROM dbo.SYS_Icd10Code c
        JOIN dbo.SYS_CodeSetVersion v ON v.CodeSetVersionId = c.CodeSetVersionId
        WHERE m.CodeSystem = 'ICD10CM' AND c.Code = m.Code
          AND c.IsActive = 1 AND v.IsActive = 1
          AND c.EffectiveDate <= @on
          AND (c.TerminationDate IS NULL OR c.TerminationDate >= @on))
 OR EXISTS (
        SELECT 1
        FROM dbo.SYS_CptCode c
        JOIN dbo.SYS_CodeSetVersion v ON v.CodeSetVersionId = c.CodeSetVersionId
        WHERE m.CodeSystem = 'CPT' AND c.Code = m.Code
          AND c.IsActive = 1 AND v.IsActive = 1
          AND c.EffectiveDate <= @on
          AND (c.TerminationDate IS NULL OR c.TerminationDate >= @on))";

        private const string FlagSql = @"
UPDATE m
   SET NeedsReview  = 1,
       ReviewReason = @reason,
       ModifiedBy   = @user,
       ModifiedDate = GETUTCDATE()
FROM dbo.SYS_ClinicalCodeMapping m
WHERE m.IsActive = 1
  AND m.NeedsReview = 0
  AND NOT (" + InForceExists + @");";

        private const string ClearSql = @"
UPDATE m
   SET NeedsReview  = 0,
       ReviewReason = NULL,
       ModifiedBy   = @user,
       ModifiedDate = GETUTCDATE()
FROM dbo.SYS_ClinicalCodeMapping m
WHERE m.IsActive = 1
  AND m.NeedsReview = 1
  AND (" + InForceExists + @");";

        // ------------------------------------------------------------ helpers

        private sealed record ResolvedCode(
            long CodeRowId,
            string DisplayCode,
            string? ShortDescription,
            string LongDescription,
            bool? IsBillable,
            DateTime EffectiveDate,
            DateTime? TerminationDate,
            string VersionLabel);

        private static SaveClinicalCodeMappingsResultDTO Fail(SaveClinicalCodeMappingsResultDTO result, string message)
        {
            result.Success = false;
            result.Message = message;
            return result;
        }

        private static List<string> Describe(IEnumerable<SYS_ClinicalCodeMapping> mappings) =>
            mappings
                .Select(m => $"{m.CodeSystem} {ClinicalCodeFormat.ToDisplayCode(m.CodeSystem, m.Code)}")
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToList();

        private bool TargetExists(string targetType, long targetId) => targetType switch
        {
            ClinicalCodeTargetType.Category => _db.PD_Categories.AsNoTracking().Any(c => c.CategoryId == targetId),
            ClinicalCodeTargetType.Service => _db.SYS_Products.AsNoTracking().Any(p => p.ProductId == targetId),
            ClinicalCodeTargetType.Package => _db.PD_Bundles.AsNoTracking().Any(b => b.BundleId == targetId),
            _ => false
        };

        private string? TargetName(string targetType, long targetId) => targetType switch
        {
            ClinicalCodeTargetType.Category => _db.PD_Categories.AsNoTracking()
                .Where(c => c.CategoryId == targetId).Select(c => c.CategoryName).FirstOrDefault(),
            ClinicalCodeTargetType.Service => _db.SYS_Products.AsNoTracking()
                .Where(p => p.ProductId == targetId).Select(p => p.ProductName).FirstOrDefault(),
            ClinicalCodeTargetType.Package => _db.PD_Bundles.AsNoTracking()
                .Where(b => b.BundleId == targetId).Select(b => b.Name).FirstOrDefault(),
            _ => null
        };

        /// <summary>The code as it stood on the day, or null if it was not in force then.</summary>
        private ResolvedCode? ResolveCode(string codeSystem, string normalizedCode, DateTime day) =>
            codeSystem == ClinicalCodeSystem.Icd10Cm
                ? ResolveIcd10(normalizedCode, day)
                : ResolveCpt(normalizedCode, day);

        private ResolvedCode? ResolveIcd10(string normalizedCode, DateTime day)
        {
            var row = (
                from c in _db.SYS_Icd10Codes.AsNoTracking()
                join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
                where c.Code == normalizedCode
                   && c.IsActive && v.IsActive
                   && c.EffectiveDate <= day
                   && (c.TerminationDate == null || c.TerminationDate >= day)
                orderby c.EffectiveDate descending
                select new
                {
                    c.Icd10CodeId,
                    c.DisplayCode,
                    c.ShortDescription,
                    c.LongDescription,
                    c.IsBillable,
                    c.EffectiveDate,
                    c.TerminationDate,
                    v.VersionLabel
                }).FirstOrDefault();

            return row is null
                ? null
                : new ResolvedCode(row.Icd10CodeId, row.DisplayCode, row.ShortDescription, row.LongDescription,
                                   row.IsBillable, row.EffectiveDate, row.TerminationDate, row.VersionLabel);
        }

        private ResolvedCode? ResolveCpt(string normalizedCode, DateTime day)
        {
            var row = (
                from c in _db.SYS_CptCodes.AsNoTracking()
                join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
                where c.Code == normalizedCode
                   && c.IsActive && v.IsActive
                   && c.EffectiveDate <= day
                   && (c.TerminationDate == null || c.TerminationDate >= day)
                orderby c.EffectiveDate descending
                select new
                {
                    c.CptCodeId,
                    c.Code,
                    c.ShortDescription,
                    c.LongDescription,
                    c.EffectiveDate,
                    c.TerminationDate,
                    v.VersionLabel
                }).FirstOrDefault();

            // CPT has no billable flag; every published CPT code is claimable.
            return row is null
                ? null
                : new ResolvedCode(row.CptCodeId, row.Code, row.ShortDescription, row.LongDescription,
                                   null, row.EffectiveDate, row.TerminationDate, row.VersionLabel);
        }

        /// <summary>
        /// Attaches the code in force on the day to each mapping. One join per code
        /// system rather than a lookup per mapping, and an inner join rather than a
        /// list parameter, so nothing here depends on OPENJSON.
        /// </summary>
        private List<ClinicalCodeMappingDTO> Resolve(List<SYS_ClinicalCodeMapping> mappings, DateTime day)
        {
            var result = mappings.Select(m => new ClinicalCodeMappingDTO
            {
                ClinicalCodeMappingId = m.ClinicalCodeMappingId,
                TargetType = m.TargetType,
                TargetId = m.TargetId,
                CodeSystem = m.CodeSystem,
                Code = m.Code,
                DisplayCode = ClinicalCodeFormat.ToDisplayCode(m.CodeSystem, m.Code),
                NeedsReview = m.NeedsReview,
                ReviewReason = m.ReviewReason,
                IsActive = m.IsActive,
                CreatedDate = m.CreatedDate,
                ModifiedDate = m.ModifiedDate
            }).ToList();

            if (result.Count == 0) return result;

            var byId = result.ToDictionary(r => r.ClinicalCodeMappingId);
            var minId = result.Min(r => r.ClinicalCodeMappingId);
            var maxId = result.Max(r => r.ClinicalCodeMappingId);

            // Joining on the code string, not on a list parameter: EF Core 8
            // compiles list.Contains(column) to OPENJSON, which needs database
            // compatibility level 130+ (TEL-10). The id range narrows the join to
            // roughly the rows already loaded, and the dictionary drops the rest.
            var icd10 = (
                from m in _db.SYS_ClinicalCodeMappings.AsNoTracking()
                where m.ClinicalCodeMappingId >= minId && m.ClinicalCodeMappingId <= maxId
                   && m.CodeSystem == ClinicalCodeSystem.Icd10Cm
                join c in _db.SYS_Icd10Codes.AsNoTracking() on m.Code equals c.Code
                join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
                where c.IsActive && v.IsActive
                   && c.EffectiveDate <= day
                   && (c.TerminationDate == null || c.TerminationDate >= day)
                select new
                {
                    m.ClinicalCodeMappingId,
                    c.DisplayCode,
                    c.ShortDescription,
                    c.LongDescription,
                    c.IsBillable,
                    c.EffectiveDate,
                    c.TerminationDate,
                    v.VersionLabel
                }).ToList();

            var cpt = (
                from m in _db.SYS_ClinicalCodeMappings.AsNoTracking()
                where m.ClinicalCodeMappingId >= minId && m.ClinicalCodeMappingId <= maxId
                   && m.CodeSystem == ClinicalCodeSystem.Cpt
                join c in _db.SYS_CptCodes.AsNoTracking() on m.Code equals c.Code
                join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
                where c.IsActive && v.IsActive
                   && c.EffectiveDate <= day
                   && (c.TerminationDate == null || c.TerminationDate >= day)
                select new
                {
                    m.ClinicalCodeMappingId,
                    c.Code,
                    c.ShortDescription,
                    c.LongDescription,
                    c.EffectiveDate,
                    c.TerminationDate,
                    v.VersionLabel
                }).ToList();

            // Releases do not overlap, so at most one row comes back per mapping.
            // Taking the latest effective date makes that an assumption the data
            // cannot break rather than a duplicated field in the response.
            foreach (var row in icd10.GroupBy(r => r.ClinicalCodeMappingId)
                                     .Select(g => g.OrderByDescending(r => r.EffectiveDate).First()))
            {
                if (!byId.TryGetValue(row.ClinicalCodeMappingId, out var dto)) continue;

                dto.DisplayCode = row.DisplayCode;
                dto.ShortDescription = row.ShortDescription;
                dto.LongDescription = row.LongDescription;
                dto.IsBillable = row.IsBillable;
                dto.EffectiveDate = row.EffectiveDate;
                dto.TerminationDate = row.TerminationDate;
                dto.VersionLabel = row.VersionLabel;
                dto.IsInForce = true;
            }

            foreach (var row in cpt.GroupBy(r => r.ClinicalCodeMappingId)
                                   .Select(g => g.OrderByDescending(r => r.EffectiveDate).First()))
            {
                if (!byId.TryGetValue(row.ClinicalCodeMappingId, out var dto)) continue;

                dto.DisplayCode = row.Code;
                dto.ShortDescription = row.ShortDescription;
                dto.LongDescription = row.LongDescription;
                dto.EffectiveDate = row.EffectiveDate;
                dto.TerminationDate = row.TerminationDate;
                dto.VersionLabel = row.VersionLabel;
                dto.IsInForce = true;
            }

            return result;
        }
    }
}
