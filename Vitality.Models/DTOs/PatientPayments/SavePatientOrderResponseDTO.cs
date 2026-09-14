using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPayments
{
    public class SavePatientOrderResponseDTO
    {
        public long? PatientPrescriptionId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? ProviderId { get; set; }
        public long? PatientTreamentId { get; set; }
        public long? PatientOrderId { get; set; }
        public long? ProductId { get; set; }
        public string? PrescriptionInstruction { get; set; }
        public long? PatientPaymentId { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public long? PatientAppointmentId { get; set; }
        public string? OrderStatus { get; set; }
        public string? Address { get; set; }
        public string? CouponCode { get; set; }
        public long? PatientTreatmentId { get; set; }
        public long? FacilityId { get; set; }

        public decimal? OrderTotal { get; set; }
        public decimal? OrderDiscount { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? ShippedDate { get; set; }
        public string? SubscriptionStatus { get; set; }
        public string? VisitStatus { get; set; }
        public string? LabelStatus { get; set; }
    }

    public class SavePrescriptionResponseDTO
    {
        public long? PatientPrescriptionId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? PatientTreamentId { get; set; }
        public long? PatientOrderId { get; set; }
        public long? ProductId { get; set; }
        public string? PrescriptionInstruction { get; set; }
        public long? PatientPaymentId { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public long? PatientAppointmentId { get; set; }
        public long? ProviderId { get; set; }

    }

    public class UpdatePrescriptionImageRequestDTO
    {
        public long PatientPrescriptionId { get; set; }
        public string? PresImage { get; set; }
        public string? ControlledSubImage { get; set; }
    }

    public class UpdatePrescriptionDateRequestDTO
    {
        public long PatientPrescriptionId { get; set; }
        public DateTime? PrescritionDate { get; set; }
    }

    public class UpdatePrescriptionWrittenDateRequestDTO
    {
        public long PatientPrescriptionId { get; set; }
        public DateTime WrittenDate { get; set; }
    }
    public class UpdateSOAPNotesRequestDTO
    {
        public long PatientPrescriptionId { get; set; }
        public long UserId { get; set; }
        public string? SOAPNotes { get; set; }
    }
}
