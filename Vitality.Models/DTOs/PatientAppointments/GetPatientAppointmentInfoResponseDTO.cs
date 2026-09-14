using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetPatientAppointmentInfoResponseDTO
    {

        public long? PatientId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Status { get; set; }
        public string? Gender { get; set; }
        public DateTime? DOB { get; set; }

        public long? PatientAppointmentId { get; set; }
        public DateTime? StartDate { get; set; }
        public int? Duration { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }

        public long? ProductId { get; set; }
        public string? ProductName { get; set; }

        public TreatmentInAppointmentDTO? Treatment { get; set; }

        public string? ZoomJoinUrl { get; set; }
        public string? ZoomPassword { get; set; }
        public string? ZoomMeetingId { get; set; }
        public string? ZoomStatus { get; set; }
    }

    public class TreatmentInAppointmentDTO
    {
        public long PatientTreatmentId { get; set; }
        public string? TreatmentGuid { get; set; }
        public string? TreatmentStatus { get; set; }

        public long? ProductId { get; set; }
        public string? BundleName { get; set; }

        public long? ProviderScheduledSlotId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public string? Name { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? Frequency { get; set; }
        public int? Refills { get; set; }
        public decimal? Price { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? NextShippingDate { get; set; }
        public DateTime? NextPaymentDate { get; set; }
        public int? NumberOfProducts { get; set; }
        public int? Prescriptions { get; set; }
        public int? PrescriptionsCount { get; set; }
        public int? OrderCount { get; set; }
    }
}
