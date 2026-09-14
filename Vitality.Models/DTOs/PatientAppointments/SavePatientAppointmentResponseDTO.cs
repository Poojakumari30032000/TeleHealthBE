using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class SavePatientAppointmentResponseDTO
    {
        public long PatientAppointmentSlotId { get; set; }

        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
