using DudeMeds.Models.DTOs.ClinicalCodes;

namespace DudeMeds.Models.Repos.Interfaces
{
    /// <summary>TEL-22 - ICD-10-CM / CPT codes on a treatment SOAP note.</summary>
    public interface ISoapNoteCodesRepo
    {
        /// <summary>The note's codes, if the caller may reach the note's patient.</summary>
        SoapNoteCodesResultDTO GetSoapNoteCodes(long soapNoteId, ClinicalCodeCallerDTO caller);

        /// <summary>
        /// Replaces every code on the note. All-or-nothing: if any code is unknown,
        /// inactive, not in force on the note's date, or a header code, nothing changes.
        /// </summary>
        SoapNoteCodesResultDTO SaveSoapNoteCodes(SaveSoapNoteCodesRequestDTO request, ClinicalCodeCallerDTO caller);
    }
}
