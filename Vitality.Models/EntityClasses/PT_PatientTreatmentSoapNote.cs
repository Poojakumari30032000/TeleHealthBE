using System;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientTreatmentSoapNote
    {
        public long SoapNoteId { get; set; }
        public long PatientTreatmentId { get; set; }
        public long? PatientId { get; set; }
        public long? ProviderId { get; set; }
        public int? FacilityId { get; set; }
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
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual PT_PatientTreatment PatientTreatment { get; set; } = null!;
    }
}
