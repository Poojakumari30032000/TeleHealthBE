using System;
using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.ClinicalCodes
{
    // TEL-19 - ICD-10-CM and CPT code-set import and lookup.

    /// <summary>Metadata for an uploaded code-set file. The file travels separately.</summary>
    public class ImportCodeSetRequestDTO
    {
        /// <summary>'ICD10CM' or 'CPT'.</summary>
        public string CodeSystem { get; set; } = string.Empty;

        /// <summary>The publisher's release name, e.g. 'FY2026'.</summary>
        public string VersionLabel { get; set; } = string.Empty;

        /// <summary>
        /// When omitted for an ICD-10-CM 'FYyyyy' label, 1 October of the prior
        /// year to 30 September is used. Required otherwise.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }
    }

    public class ImportCodeSetResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long? CodeSetVersionId { get; set; }
        public int CodesInFile { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }

        /// <summary>Codes in an earlier import of this release that the file no longer has; marked inactive, not deleted.</summary>
        public int Deactivated { get; set; }

        /// <summary>Earlier releases whose termination date this import set.</summary>
        public int ReleasesClosed { get; set; }

        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class CodeSetVersionDTO
    {
        public long CodeSetVersionId { get; set; }
        public string CodeSystem { get; set; } = string.Empty;
        public string VersionLabel { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public int CodeCount { get; set; }
        public string? SourceFileName { get; set; }
        public DateTime? ImportedDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class GetIcd10CodeRequestDTO
    {
        /// <summary>With or without the dot: 'E11.65' or 'E1165'.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>The date the code must have been in force on. Defaults to today (UTC).</summary>
        public DateTime? OnDate { get; set; }
    }

    public class Icd10CodeDTO
    {
        public long Icd10CodeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayCode { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string LongDescription { get; set; } = string.Empty;
        public bool IsBillable { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public long CodeSetVersionId { get; set; }
        public string VersionLabel { get; set; } = string.Empty;
    }
}

namespace DudeMeds.Models.DTOs.ClinicalCodes
{
    // TEL-21 - code search.

    public class SearchClinicalCodesRequestDTO
    {
        /// <summary>'ICD10CM' (the default) or 'CPT'.</summary>
        public string? CodeSystem { get; set; }

        /// <summary>A code, part of a code (with or without the dot), or words from the description.</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>Only codes in force on this date are returned. Defaults to today (UTC).</summary>
        public DateTime? OnDate { get; set; }

        /// <summary>Leave out ICD-10-CM header (category) codes, which are not valid on a claim.</summary>
        public bool BillableOnly { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SearchClinicalCodesResultDTO
    {
        public string CodeSystem { get; set; } = string.Empty;

        /// <summary>The release searched: the one in force on <see cref="OnDate"/>. Null when none was.</summary>
        public long? CodeSetVersionId { get; set; }
        public string? VersionLabel { get; set; }
        public DateTime OnDate { get; set; }

        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<ClinicalCodeSearchItemDTO> Items { get; set; } = new();
    }

    public class ClinicalCodeSearchItemDTO
    {
        /// <summary>Icd10CodeId or CptCodeId, depending on the code system.</summary>
        public long CodeId { get; set; }
        public long CodeSetVersionId { get; set; }

        /// <summary>As stored, without the dot: 'E1165'.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>As people write it: 'E11.65'. Equal to <see cref="Code"/> for CPT.</summary>
        public string DisplayCode { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }
        public string LongDescription { get; set; } = string.Empty;

        /// <summary>Always true for CPT, which has no header codes.</summary>
        public bool IsBillable { get; set; }

        public DateTime EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        /// <summary>A ClinicalCodeMatchRank value: 0 exact code ... 5 all words somewhere in the description.</summary>
        public int MatchRank { get; set; }
    }
}
