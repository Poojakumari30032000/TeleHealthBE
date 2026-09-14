using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetAllPatientAppointmentsByDaysResponseDTO
    {
        public long PatientAppointmentSlotId { get; set; }
        public long? FacilityId { get; set; }
        public string? ClinicName { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public string? Title { get; set; }
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public long? PatientId { get; set; }
        public string? Status { get; set; }
        public string? PatientName { get; set; }
        public DateTime? StartDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? Duration { get; set; }
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }

    }
}
