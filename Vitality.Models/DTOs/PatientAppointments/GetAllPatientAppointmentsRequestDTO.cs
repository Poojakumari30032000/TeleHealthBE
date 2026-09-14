using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetAllPatientAppointmentsRequestDTO
    {
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }
}
