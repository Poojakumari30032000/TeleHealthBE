using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class Sys_EmpowerOrder
    {
        public long EmpowerOrdersId { get; set; }
        public long? PatientPrescriptionId { get; set; }
        public long? PatientId { get; set; }
        public long? FacilityId { get; set; }
        public string? ClientOrderId { get; set; }
        public int? EipOrderId { get; set; }
        public string? LfOrderId { get; set; }
        public string? LfPatientId { get; set; }
        public string? LfReferenceId { get; set; }
        public string? MessageId { get; set; }
        public int? CanonicalOrderId { get; set; }
        public string? SalesForceOrderId { get; set; }
        public string? SalesForceClinicAccountId { get; set; }
        public int? LifeFilePracticeId { get; set; }
        public string? OrderStatus { get; set; }
        public string? Error { get; set; }
        public DateTime? OrderStatusLastUpdatedTime { get; set; }
        public string? Reference1 { get; set; }
        public string? Reference2 { get; set; }
        public string? Reference3 { get; set; }
        public string? Reference4 { get; set; }
        public string? Reference5 { get; set; }
        public string? ShipmentStatus { get; set; }
        public string? ShipmentTrackingNumber { get; set; }
        public string? ShipmentTrackingUrl { get; set; }
        public string? ShipmentProvider { get; set; }
        public DateTime? ShipmentStatusLastUpdatedTime { get; set; }
        public string? PrescriptionPdfBase64 { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? CreatedBy { get; set; }
        public long? ModifiedBy { get; set; }
        public bool? IsActive { get; set; }

        public virtual PT_Patient? Patient { get; set; }
        public virtual PT_PatientPrescription? PatientPrescription { get; set; }
    }
}
