using DudeMeds.Models.DTOs.ClinicalCodes;
using System;
using System.Collections.Generic;

namespace DudeMeds.Models.Repos.Interfaces
{
    /// <summary>
    /// TEL-20 - mapping a Category, Service or Package to its ICD-10 / CPT codes.
    /// The codes themselves are reference data owned by TEL-19.
    /// </summary>
    public interface IClinicalCodeMappingsRepo
    {
        /// <summary>
        /// The codes mapped to one target, each resolved against the release in
        /// force on the requested date.
        /// </summary>
        List<ClinicalCodeMappingDTO> GetMappings(GetClinicalCodeMappingsRequestDTO request);

        /// <summary>
        /// Every mapping an import has flagged, across all targets - the queue an
        /// administrator works through after a new code release lands.
        /// </summary>
        List<ClinicalCodeMappingDTO> GetMappingsNeedingReview(DateTime onDate);

        /// <summary>
        /// Replaces the complete set of codes on one target. Refuses the whole
        /// request - writing nothing - if any code is unknown or was not in force
        /// on the date.
        /// </summary>
        SaveClinicalCodeMappingsResultDTO SaveMappings(SaveClinicalCodeMappingsRequestDTO request, long userId);

        /// <summary>Removes one mapping. The mapping row is deactivated, not deleted.</summary>
        SaveClinicalCodeMappingsResultDTO DeleteMapping(long clinicalCodeMappingId, long userId);

        /// <summary>
        /// Which targets use a reference code. Anything that would remove or
        /// retire a code calls this first and refuses while the code is in use.
        /// </summary>
        ClinicalCodeUsageDTO GetCodeUsage(string? codeSystem, string? code);

        /// <summary>
        /// Re-evaluates every active mapping against the releases in force on the
        /// date: flags the ones whose code is no longer in force and clears the
        /// flag on any that is in force again. Called after a code-set import.
        /// </summary>
        RefreshClinicalCodeReviewFlagsResultDTO RefreshReviewFlags(DateTime onDate, long userId);
    }
}
