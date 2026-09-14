using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientPrescriptionSoapNote
    {
        public long SoapNoteId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public long? PatientId { get; set; }
        public long? ProviderId { get; set; }
        public long? FacilityId { get; set; }
        public long? PatientAppointmentSlotId { get; set; }
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
        public string? Guid { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual PT_PatientPrescription PatientPrescription { get; set; } = null!;
    }
}
