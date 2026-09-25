using DudeMeds.Models.DTOs.ClinicalCodes;
using System;
using System.Collections.Generic;
using System.IO;

namespace DudeMeds.Models.Repos.Interfaces
{
    /// <summary>TEL-19 - ICD-10-CM and CPT reference data.</summary>
    public interface IClinicalCodesRepo
    {
        /// <summary>
        /// Loads one release of a code system from its published file. Re-running
        /// with the same code system and version label updates that release in
        /// place; it never duplicates codes.
        /// </summary>
        ImportCodeSetResultDTO ImportCodeSet(ImportCodeSetRequestDTO request, string? fileName, Stream content, long userId);

        List<CodeSetVersionDTO> GetCodeSetVersions(string? codeSystem);

        /// <summary>The ICD-10-CM code as it stood on the given date, or null if it was not in force then.</summary>
        Icd10CodeDTO? GetIcd10Code(string code, DateTime onDate);
    }
}
