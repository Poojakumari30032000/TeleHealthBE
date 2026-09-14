using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientPrescription
    {
        public PT_PatientPrescription()
        {
            PT_PatientPrescriptionSoapNotes = new HashSet<PT_PatientPrescriptionSoapNote>();
            PT_PrescriptionMedicines = new HashSet<PT_PrescriptionMedicine>();
            Sys_EmpowerOrders = new HashSet<Sys_EmpowerOrder>();
        }

        public long PatientPrescriptionId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public string? ProductType { get; set; }
        public long? ProductId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public long? PatientOrderId { get; set; }
        public long? PatientAppointmentId { get; set; }
        public string? PrescriptionInstruction { get; set; }
        public string? PrescriptionStatus { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Guid { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? PresImage { get; set; }
        public DateTime? PrescritionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
        public string? SOAPNotes { get; set; }
        public string? ControlledSubImage { get; set; }

        public virtual ICollection<PT_PatientPrescriptionSoapNote> PT_PatientPrescriptionSoapNotes { get; set; }
        public virtual ICollection<PT_PrescriptionMedicine> PT_PrescriptionMedicines { get; set; }
        public virtual ICollection<Sys_EmpowerOrder> Sys_EmpowerOrders { get; set; }
    }
}
