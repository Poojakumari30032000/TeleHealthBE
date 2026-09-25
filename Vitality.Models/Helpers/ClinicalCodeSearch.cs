using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Helpers
{
    /// <summary>
    /// How well a code matched a search. Lower is better; results are ordered by it.
    /// </summary>
    public enum ClinicalCodeMatchRank
    {
        /// <summary>The code itself, with or without the dot: 'E11.65' or 'E1165'.</summary>
        ExactCode = 0,
        /// <summary>The code starts with what was typed: 'E11.6' finds E11.6, E11.61, E11.65 ...</summary>
        CodePrefix = 1,
        /// <summary>The description starts with the phrase.</summary>
        DescriptionStartsWith = 2,
        /// <summary>A word in the description starts with the phrase: 'diab' in 'Type 2 diabetes'.</summary>
        DescriptionWordStartsWith = 3,
        /// <summary>The phrase appears anywhere in the description.</summary>
        DescriptionContains = 4,
        /// <summary>Every word appears somewhere in the description, in any order.</summary>
        DescriptionAllWords = 5
    }

    /// <summary>A search query broken into the parts the SQL needs. Built by <see cref="ClinicalCodeSearch.Parse"/>.</summary>
    public sealed class ClinicalCodeSearchTerms
    {
        /// <summary>
        /// The query as a code, without the dot and upper-cased, or null when it
        /// cannot be one (it has spaces, or characters no code has).
        /// </summary>
        public string? Code { get; init; }

        /// <summary>The whole query upper-cased with runs of whitespace collapsed, matched as one phrase.</summary>
        public string Phrase { get; init; } = string.Empty;

        /// <summary>
        /// The individual words, upper-cased, de-duplicated, at most
        /// <see cref="ClinicalCodeSearch.MaxWords"/>. More than one only when the
        /// query has several words.
        /// </summary>
        public IReadOnlyList<string> Words { get; init; } = Array.Empty<string>();

        public bool IsSearchable => Phrase.Length >= ClinicalCodeSearch.MinQueryLength;
    }

    /// <summary>
    /// TEL-21 - the pure part of code search: normalising what the user typed and
    /// the paging bounds. The SQL that ranks with it lives in ClinicalCodesRepo.
    ///
    /// Deliberately no SQL Server Full-Text Search: it is an optional feature that
    /// is not guaranteed on the target server, and LocalDB cannot run it. A
    /// search reads one release's rows through a covering index
    /// (Sql/Create_SYS_ClinicalCodeSearchIndexes.sql) and matches codes by prefix
    /// and descriptions by LIKE.
    /// </summary>
    public static class ClinicalCodeSearch
    {
        public const int MinQueryLength = 2;
        public const int MaxQueryLength = 100;
        public const int MaxWords = 6;
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;

        // ICD-10-CM: a letter, then up to six letters or digits (no dot) - 'E1165',
        // and the FY2026 'QA0' category with a letter in the second position.
        private static readonly Regex Icd10CodeShape = new(@"^[A-Z][0-9A-Z]{0,6}$", RegexOptions.CultureInvariant);

        // CPT: five letters or digits - '99213', '0001F', '0503T'.
        private static readonly Regex CptCodeShape = new(@"^[0-9A-Z]{1,5}$", RegexOptions.CultureInvariant);

        private static readonly char[] WordSeparators = { ' ', '\t', ',', ';', '/' };

        /// <param name="query">What the user typed: a code, part of a code, or words from the description.</param>
        /// <param name="codeSystem">A <see cref="ClinicalCodeSystem"/> value; decides what a code looks like.</param>
        public static ClinicalCodeSearchTerms Parse(string? query, string codeSystem)
        {
            var text = CollapseWhitespace(query ?? string.Empty).ToUpperInvariant();
            if (text.Length > MaxQueryLength)
                text = text.Substring(0, MaxQueryLength).TrimEnd();

            if (text.Length == 0)
                return new ClinicalCodeSearchTerms();

            string? code = null;
            if (!text.Contains(' '))
            {
                var candidate = text.Replace(".", string.Empty);
                var shape = codeSystem == ClinicalCodeSystem.Cpt ? CptCodeShape : Icd10CodeShape;
                if (candidate.Length > 0 && shape.IsMatch(candidate))
                    code = candidate;
            }

            var words = text
                .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim('.', '(', ')', '[', ']', '"', '\'', ':', '-'))
                .Where(w => w.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxWords)
                .ToList();

            return new ClinicalCodeSearchTerms { Code = code, Phrase = text, Words = words };
        }

        /// <summary>
        /// Escapes the LIKE wildcards in user input so '%', '_' and '[' match
        /// themselves. Used with the default escape rules (no ESCAPE clause).
        /// </summary>
        public static string EscapeLike(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var sb = new StringBuilder(value.Length + 8);
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '[': sb.Append("[[]"); break;
                    case '%': sb.Append("[%]"); break;
                    case '_': sb.Append("[_]"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        public static int ClampPageSize(int pageSize) =>
            pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        public static int ClampPageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;

        private static string CollapseWhitespace(string value)
        {
            var sb = new StringBuilder(value.Length);
            var pendingSpace = false;
            foreach (var ch in value)
            {
                if (char.IsWhiteSpace(ch))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }
                if (pendingSpace) sb.Append(' ');
                pendingSpace = false;
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
