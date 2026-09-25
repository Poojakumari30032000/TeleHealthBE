using System;
using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.ClinicalCodes
{
    // TEL-22 - ICD-10-CM / CPT codes on a treatment SOAP note.

    /// <summary>Who is asking. Always built from the signed token, never from the request.</summary>
    public class ClinicalCodeCallerDTO
    {
        public long UserId { get; set; }

        /// <summary>A <see cref="Vitality.Models.Enums.UserRole"/> value.</summary>
        public long? RoleId { get; set; }
        public long? OrganizationId { get; set; }
    }

    public class SoapNoteCodeDTO
    {
        public long SoapNoteCodeId { get; set; }

        /// <summary>'ICD10CM' or 'CPT'.</summary>
        public string CodeSystem { get; set; } = string.Empty;

        /// <summary>The release the code was coded against.</summary>
        public long CodeSetVersionId { get; set; }
        public string? VersionLabel { get; set; }

        /// <summary>Icd10CodeId or CptCodeId, depending on the code system.</summary>
        public long CodeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsBillable { get; set; }

        /// <summary>1 is the primary code.</summary>
        public int DisplayOrder { get; set; }
    }

    public class SoapNoteCodesDTO
    {
        public long SoapNoteId { get; set; }

        /// <summary>
        /// The date codes on this note must be in force on: the note's created date
        /// (UTC). The note has no separate date of service. Use it as the picker's date.
        /// </summary>
        public DateTime CodingDate { get; set; }

        /// <summary>Whether this caller may change the note's codes.</summary>
        public bool CanEdit { get; set; }

        public List<SoapNoteCodeDTO> Codes { get; set; } = new();
    }

    public class SoapNoteCodeItemRequestDTO
    {
        /// <summary>'ICD10CM' or 'CPT'.</summary>
        public string CodeSystem { get; set; } = string.Empty;

        /// <summary>The release the code was picked from (every search result carries it).</summary>
        public long CodeSetVersionId { get; set; }

        /// <summary>With or without the dot.</summary>
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Replaces every code on the note with <see cref="Codes"/>, in that order.
    /// An empty list removes them all.
    /// </summary>
    public class SaveSoapNoteCodesRequestDTO
    {
        public long SoapNoteId { get; set; }
        public List<SoapNoteCodeItemRequestDTO> Codes { get; set; } = new();
    }

    public class SoapNoteCodesResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>One entry per rejected code, when validation fails.</summary>
        public List<string> Errors { get; set; } = new();
        public SoapNoteCodesDTO? Data { get; set; }
    }
}
