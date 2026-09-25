using System;
using System.Collections.Generic;
using DudeMeds.Models.DTOs.ClinicalCodes;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Helpers
{
    /// <summary>One requested code after normalisation. DisplayOrder is 1-based, in request order.</summary>
    public sealed record NormalizedSoapNoteCode(string CodeSystem, long CodeSetVersionId, string Code, int DisplayOrder);

    /// <summary>
    /// TEL-22 - the shape checks on a "save SOAP note codes" request that need no
    /// database: known code system, a release id, a code, no duplicates, a sane
    /// count. Whether each code really exists and is in force is checked by
    /// SoapNoteCodesRepo against the code tables.
    /// </summary>
    public static class SoapNoteCodeRequestValidator
    {
        /// <summary>Well above any real note; bounds the per-code lookups a save does.</summary>
        public const int MaxCodesPerNote = 50;

        public static (List<NormalizedSoapNoteCode> Codes, List<string> Errors) Normalize(IReadOnlyList<SoapNoteCodeItemRequestDTO>? items)
        {
            var codes = new List<NormalizedSoapNoteCode>();
            var errors = new List<string>();

            if (items is null || items.Count == 0)
                return (codes, errors);

            if (items.Count > MaxCodesPerNote)
            {
                errors.Add($"A SOAP note can carry at most {MaxCodesPerNote} codes.");
                return (codes, errors);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var position = i + 1;

                if (item is null)
                {
                    errors.Add($"Code {position} is empty.");
                    continue;
                }

                var system = item.CodeSystem?.Trim().ToUpperInvariant();
                if (system != ClinicalCodeSystem.Icd10Cm && system != ClinicalCodeSystem.Cpt)
                {
                    errors.Add($"Code {position}: code system must be '{ClinicalCodeSystem.Icd10Cm}' or '{ClinicalCodeSystem.Cpt}'.");
                    continue;
                }

                var code = (item.Code ?? string.Empty).Replace(".", string.Empty).Trim().ToUpperInvariant();
                if (code.Length == 0 || code.Length > 8)
                {
                    errors.Add($"Code {position}: a code of 1 to 8 characters is required.");
                    continue;
                }

                if (item.CodeSetVersionId <= 0)
                {
                    errors.Add($"Code {position} ({code}): the code set version it was picked from is required.");
                    continue;
                }

                if (!seen.Add(system + ":" + code))
                {
                    errors.Add($"{code} is listed more than once.");
                    continue;
                }

                codes.Add(new NormalizedSoapNoteCode(system, item.CodeSetVersionId, code, codes.Count + 1));
            }

            return (codes, errors);
        }
    }
}
