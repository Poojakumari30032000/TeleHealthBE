using System;
using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.ClinicalCodes
{
    // TEL-20 - Category / Service / Package to ICD-10 / CPT mapping.

    /// <summary>One code as the caller names it: 'ICD10CM' + 'E11.65' or 'E1165'.</summary>
    public class ClinicalCodeRefDTO
    {
        public string CodeSystem { get; set; } = string.Empty;

        /// <summary>With or without the dot; it is normalised before use.</summary>
        public string Code { get; set; } = string.Empty;
    }

    public class GetClinicalCodeMappingsRequestDTO
    {
        /// <summary>'Category', 'Service' or 'Package'.</summary>
        public string TargetType { get; set; } = string.Empty;

        public long TargetId { get; set; }

        /// <summary>
        /// The date the codes are resolved against, so an encounter coded last
        /// year still reads back the wording that was current then. Defaults to
        /// today (UTC).
        /// </summary>
        public DateTime? OnDate { get; set; }

        /// <summary>Include mappings that have been removed. Off by default.</summary>
        public bool IncludeInactive { get; set; }
    }

    /// <summary>
    /// A mapping together with the code it resolves to on the requested date.
    /// The description fields are null when the code was not in force then -
    /// <see cref="IsInForce"/> says which case this is.
    /// </summary>
    public class ClinicalCodeMappingDTO
    {
        public long ClinicalCodeMappingId { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public long TargetId { get; set; }

        public string CodeSystem { get; set; } = string.Empty;

        /// <summary>Normalised, without the dot: 'E1165'.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>For display: 'E11.65'. CPT codes are identical either way.</summary>
        public string DisplayCode { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }

        /// <summary>ICD-10-CM only. A header code such as 'E11' is not valid on a claim.</summary>
        public bool? IsBillable { get; set; }

        /// <summary>The release the code resolved against on the requested date, if any.</summary>
        public string? VersionLabel { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        /// <summary>False when no release in force on the requested date carries this code.</summary>
        public bool IsInForce { get; set; }

        public bool NeedsReview { get; set; }
        public string? ReviewReason { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    /// <summary>
    /// The complete set of codes for one target. This is a replace, not a merge:
    /// codes absent from <see cref="Codes"/> are removed from the target. Send
    /// the list the administrator is looking at, not a delta.
    /// </summary>
    public class SaveClinicalCodeMappingsRequestDTO
    {
        public string TargetType { get; set; } = string.Empty;
        public long TargetId { get; set; }
        public List<ClinicalCodeRefDTO> Codes { get; set; } = new();

        /// <summary>The date codes must be in force on. Defaults to today (UTC).</summary>
        public DateTime? OnDate { get; set; }
    }

    public class SaveClinicalCodeMappingsResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public int Added { get; set; }
        public int Removed { get; set; }
        public int Unchanged { get; set; }

        /// <summary>
        /// Why the request was refused - an unknown code, or one that was not in
        /// force on the date. Nothing is written when this is non-empty.
        /// </summary>
        public List<string> Rejected { get; set; } = new();
    }

    public class DeleteClinicalCodeMappingRequestDTO
    {
        public long ClinicalCodeMappingId { get; set; }
    }

    /// <summary>
    /// Which targets use a reference code. The removal guard reads this before
    /// anything deletes or retires a code - TEL-20 acceptance criterion 6.
    /// </summary>
    public class ClinicalCodeUsageDTO
    {
        public string CodeSystem { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        /// <summary>Active mappings using the code. Removal is refused while this is above zero.</summary>
        public int MappingCount { get; set; }

        public bool IsInUse => MappingCount > 0;

        public List<ClinicalCodeUsageTargetDTO> Targets { get; set; } = new();
    }

    public class ClinicalCodeUsageTargetDTO
    {
        public string TargetType { get; set; } = string.Empty;
        public long TargetId { get; set; }
        public string? TargetName { get; set; }
    }

    public class RefreshClinicalCodeReviewFlagsResultDTO
    {
        /// <summary>Mappings newly flagged because their code is no longer in force.</summary>
        public int Flagged { get; set; }

        /// <summary>Mappings whose flag was cleared because their code is in force again.</summary>
        public int Cleared { get; set; }
    }
}
