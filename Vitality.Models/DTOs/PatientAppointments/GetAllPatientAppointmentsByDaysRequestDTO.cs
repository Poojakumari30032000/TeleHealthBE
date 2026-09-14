using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetAllPatientAppointmentsByDaysRequestDTO
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public long? FacilityId { get; set; }
        public long? PatientId { get; set; }
        public long? ProviderId { get; set; }

        public string? Status { get; set; }

        public int? ClientTimezoneOffsetMinutes { get; set; }
    }
}
