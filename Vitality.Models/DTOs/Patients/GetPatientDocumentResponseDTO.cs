using System;

namespace Vitality.Models.DTOs.Patients
{
    public class GetPatientDocumentResponseDTO
    {
        public long PatientDocumentId { get; set; }
        public long PatientId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DocumentUrl { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Guid { get; set; }
    }
}
