using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.PatientAppointments
{
    public class GetPatientAppointmentVideoCallIdRespnseDTO
    {
        public string? SessionId { get; set; }
        public string? Token { get; set; }
    }
}
