using System;
using System.Text.RegularExpressions;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Helpers
{
    /// <summary>
    /// How a clinical code is written, shared by the TEL-19 file import and the
    /// TEL-20 mapping API so the two cannot drift. A code that the importer
    /// would reject must not be storable as a mapping, and a code typed with the
    /// dot by an administrator must normalise to the same string the importer
    /// stored.
    /// <para>Pure, so it is unit tested in Vitality.Models.Tests without a database.</para>
    /// </summary>
    public static class ClinicalCodeFormat
    {
        // ICD-10-CM: a letter, then two to six letters or digits. The second
        // character is usually a digit, but FY2026 introduced QA0 (QA00101 etc.),
        // so it is not assumed. Covers e.g. A00, C4A, E1165, S72001A, U071, QA00101.
        internal static readonly Regex Icd10CmCode = new("^[A-Z][0-9A-Z][0-9A-Z]{1,5}$", RegexOptions.Compiled);

        // CPT: five digits (Category I), or four digits and F / T (Category II / III).
        internal static readonly Regex CptCode = new("^[0-9]{4}[0-9FTU]$", RegexOptions.Compiled);

        /// <summary>
        /// The stored form of a code as typed by a human: trimmed, upper-cased and
        /// with the dot and any internal whitespace removed. 'e11.65' becomes
        /// 'E1165'. Never null; an empty input gives an empty string.
        /// </summary>
        public static string Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;

            // A heap array, not stackalloc: the input is whatever an API caller
            // sent, and its length is not bounded here.
            var buffer = new char[code.Length];
            var length = 0;
            foreach (var ch in code)
            {
                if (ch == '.' || char.IsWhiteSpace(ch)) continue;
                buffer[length++] = char.ToUpperInvariant(ch);
            }

            return new string(buffer, 0, length);
        }

        /// <summary>
        /// Whether an already normalised code is well formed for its system.
        /// <paramref name="codeSystem"/> is one of <see cref="EntityClasses.ClinicalCodeSystem"/>;
        /// anything else is false.
        /// </summary>
        public static bool IsValid(string? codeSystem, string? normalizedCode)
        {
            if (string.IsNullOrEmpty(normalizedCode)) return false;

            return codeSystem switch
            {
                ClinicalCodeSystem.Icd10Cm => Icd10CmCode.IsMatch(normalizedCode),
                ClinicalCodeSystem.Cpt => CptCode.IsMatch(normalizedCode),
                _ => false
            };
        }

        /// <summary>
        /// The display form: 'E1165' to 'E11.65' for ICD-10-CM, unchanged for CPT.
        /// The dot follows the three-character category.
        /// </summary>
        public static string ToDisplayCode(string? codeSystem, string normalizedCode)
        {
            ArgumentNullException.ThrowIfNull(normalizedCode);

            if (codeSystem == ClinicalCodeSystem.Cpt) return normalizedCode;

            return normalizedCode.Length > 3
                ? normalizedCode.Substring(0, 3) + "." + normalizedCode.Substring(3)
                : normalizedCode;
        }

        /// <summary>
        /// The canonical spelling of a code system, or null when it is neither.
        /// Accepts the value in any case and with surrounding whitespace.
        /// </summary>
        public static string? NormalizeCodeSystem(string? codeSystem)
        {
            var value = codeSystem?.Trim().ToUpperInvariant();
            return value switch
            {
                ClinicalCodeSystem.Icd10Cm => ClinicalCodeSystem.Icd10Cm,
                ClinicalCodeSystem.Cpt => ClinicalCodeSystem.Cpt,
                _ => null
            };
        }

        /// <summary>
        /// The canonical spelling of a mapping target type, or null when it is
        /// none of Category, Service or Package. Accepts any case.
        /// </summary>
        public static string? NormalizeTargetType(string? targetType)
        {
            var value = targetType?.Trim();
            if (string.IsNullOrEmpty(value)) return null;

            if (string.Equals(value, ClinicalCodeTargetType.Category, StringComparison.OrdinalIgnoreCase))
                return ClinicalCodeTargetType.Category;
            if (string.Equals(value, ClinicalCodeTargetType.Service, StringComparison.OrdinalIgnoreCase))
                return ClinicalCodeTargetType.Service;
            if (string.Equals(value, ClinicalCodeTargetType.Package, StringComparison.OrdinalIgnoreCase))
                return ClinicalCodeTargetType.Package;

            return null;
        }
    }
}
