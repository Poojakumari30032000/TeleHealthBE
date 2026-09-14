using System;

namespace Vitality.Models.DTOs.PatientTreatments
{
    public class GetTreatmentDocumentResponseDTO
    {
        public long PatientTreatmentDocumentId { get; set; }
        public long PatientTreatmentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DocumentUrl { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Guid { get; set; }
    }
}
