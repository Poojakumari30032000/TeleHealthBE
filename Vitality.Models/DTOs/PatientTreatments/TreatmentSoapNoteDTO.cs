using System;

namespace Vitality.Models.DTOs.PatientTreatments
{

    public class TreatmentSoapNoteDTO
    {
        public long SoapNoteId { get; set; }
        public long PatientTreatmentId { get; set; }
        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? AllergiesJson { get; set; }
        public string? SignaturePath { get; set; }
        public string? Signature { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? CreatedByName { get; set; }
    }

    public class GetSoapNoteDetailsResponseDTO
    {
        public long PatientTreatmentId { get; set; }
        public TreatmentSoapNoteDTO? SoapNote { get; set; }
    }

    public class SaveTreatmentSoapNoteRequestDTO
    {
        public long SoapNoteId { get; set; }
        public long PatientTreatmentId { get; set; }
        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? AllergiesJson { get; set; }
        public string? SignaturePath { get; set; }
        public string? Signature { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? Status { get; set; }
    }
}
