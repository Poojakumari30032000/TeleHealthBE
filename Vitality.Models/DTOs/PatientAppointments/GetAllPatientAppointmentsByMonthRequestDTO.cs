using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetAllPatientAppointmentsByMonthRequestDTO
    {
        public int ScheduledMonth { get; set; }
        public int ScheduledYear { get; set; }
        public long? FacilityId { get; set; }
        public long? PatientId { get; set; }
        public long? ProviderId { get; set; }
    }
}
