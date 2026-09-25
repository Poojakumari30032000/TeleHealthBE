using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Helpers
{
    /// <summary>One code read from a published code-set file.</summary>
    public sealed record ParsedClinicalCode(
        string Code,
        string? DisplayCode,
        string? ShortDescription,
        string LongDescription,
        bool IsBillable,
        int? SortOrder);

    public sealed class ClinicalCodeParseResult
    {
        /// <summary>At most this many errors are kept; the count is still exact.</summary>
        public const int MaxErrorsReported = 50;

        public List<ParsedClinicalCode> Codes { get; } = new();
        public List<string> Errors { get; } = new();
        public int ErrorCount { get; private set; }
        public bool IsValid => ErrorCount == 0 && Codes.Count > 0;

        internal void AddError(int lineNumber, string message)
        {
            ErrorCount++;
            if (Errors.Count < MaxErrorsReported)
                Errors.Add($"Line {lineNumber}: {message}");
        }
    }

    /// <summary>
    /// Parses published clinical code-set files for the TEL-19 import. Pure, so
    /// it is unit tested in Vitality.Models.Tests without a database.
    /// </summary>
    public static class ClinicalCodeFileParser
    {
        // The code shapes live in ClinicalCodeFormat so that the importer and the
        // TEL-20 mapping API accept exactly the same set of codes.
        private static readonly Regex Icd10CmCode = ClinicalCodeFormat.Icd10CmCode;
        private static readonly Regex CptCode = ClinicalCodeFormat.CptCode;

        /// <summary>
        /// The CMS ICD-10-CM "order file" (icd10cm_order_YYYY.txt), fixed width:
        /// <code>
        /// cols  1-5   order number, zero padded
        /// col   7-13  code without the dot, left justified
        /// col  15     1 = valid for HIPAA-covered transactions (billable), 0 = header
        /// cols 17-76  short description
        /// cols 78-    long description
        /// </code>
        /// Every error is reported against its line; the caller rejects the whole
        /// file if there is any, so a partial code set is never loaded.
        /// </summary>
        public static ClinicalCodeParseResult ParseIcd10CmOrderFile(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var result = new ClinicalCodeParseResult();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var lineNumber = 0;
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // A long description can be short enough that trailing padding was
                // trimmed, but the line must at least reach its first character.
                if (line.Length < 78)
                {
                    result.AddError(lineNumber, "line is shorter than the order file layout (78 characters).");
                    continue;
                }

                var orderText = line.Substring(0, 5);
                var code = line.Substring(6, 7).Trim().ToUpperInvariant();
                var flag = line[14];
                var shortDescription = line.Substring(16, 60).Trim();
                var longDescription = line.Substring(77).Trim();

                if (!int.TryParse(orderText, out var order))
                {
                    result.AddError(lineNumber, $"order number '{orderText}' is not a number.");
                    continue;
                }
                if (!Icd10CmCode.IsMatch(code))
                {
                    result.AddError(lineNumber, $"'{code}' is not an ICD-10-CM code.");
                    continue;
                }
                if (flag != '0' && flag != '1')
                {
                    result.AddError(lineNumber, $"billable flag '{flag}' must be 0 or 1.");
                    continue;
                }
                if (longDescription.Length == 0)
                {
                    result.AddError(lineNumber, $"code {code} has no description.");
                    continue;
                }
                if (!seen.Add(code))
                {
                    result.AddError(lineNumber, $"code {code} appears more than once.");
                    continue;
                }

                result.Codes.Add(new ParsedClinicalCode(
                    Code: code,
                    DisplayCode: ToIcd10DisplayCode(code),
                    ShortDescription: shortDescription.Length == 0 ? null : shortDescription,
                    LongDescription: longDescription,
                    IsBillable: flag == '1',
                    SortOrder: order));
            }

            if (result.Codes.Count == 0 && result.ErrorCount == 0)
                result.AddError(lineNumber, "the file contains no codes.");

            return result;
        }

        /// <summary>
        /// A CPT file as tab-separated <c>code, long description[, short description]</c>,
        /// with an optional header row. The layout of the AMA's licensed data files
        /// must be confirmed once a license is in place (TEL-19); this is the
        /// shape the importer accepts until then.
        /// </summary>
        public static ClinicalCodeParseResult ParseCptTabDelimited(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var result = new ClinicalCodeParseResult();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var lineNumber = 0;
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('\t');
                var code = parts[0].Trim().ToUpperInvariant();

                if (lineNumber == 1 && code == "CODE") continue;

                if (parts.Length < 2)
                {
                    result.AddError(lineNumber, "expected code and description separated by a tab.");
                    continue;
                }
                if (!CptCode.IsMatch(code))
                {
                    result.AddError(lineNumber, $"'{code}' is not a CPT code.");
                    continue;
                }

                var longDescription = parts[1].Trim();
                if (longDescription.Length == 0)
                {
                    result.AddError(lineNumber, $"code {code} has no description.");
                    continue;
                }
                if (!seen.Add(code))
                {
                    result.AddError(lineNumber, $"code {code} appears more than once.");
                    continue;
                }

                var shortDescription = parts.Length > 2 ? parts[2].Trim() : string.Empty;
                result.Codes.Add(new ParsedClinicalCode(
                    Code: code,
                    DisplayCode: null,
                    ShortDescription: shortDescription.Length == 0 ? null : Truncate(shortDescription, 60),
                    LongDescription: longDescription,
                    IsBillable: true,
                    SortOrder: null));
            }

            if (result.Codes.Count == 0 && result.ErrorCount == 0)
                result.AddError(lineNumber, "the file contains no codes.");

            return result;
        }

        /// <summary>'E1165' to 'E11.65'. The dot follows the three-character category.</summary>
        public static string ToIcd10DisplayCode(string code)
        {
            ArgumentNullException.ThrowIfNull(code);
            return ClinicalCodeFormat.ToDisplayCode(ClinicalCodeSystem.Icd10Cm, code);
        }

        /// <summary>
        /// The default in-force window for an ICD-10-CM fiscal-year release:
        /// 'FY2026' runs 1 October 2025 to 30 September 2026. Null when the label
        /// is not in that form, so the caller must supply dates explicitly.
        /// </summary>
        public static (DateTime Effective, DateTime Termination)? Icd10CmFiscalYearWindow(string? versionLabel)
        {
            if (string.IsNullOrWhiteSpace(versionLabel)) return null;
            var m = Regex.Match(versionLabel.Trim(), "^FY(\\d{4})$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var year = int.Parse(m.Groups[1].Value);
            return (new DateTime(year - 1, 10, 1), new DateTime(year, 9, 30));
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value.Substring(0, max);
    }
}
