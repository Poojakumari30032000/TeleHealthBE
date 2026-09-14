using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.ClinicToPatient
{
    public class ClinicToPatientCreateResultDTO
    {
        public bool Success { get; set; }
        public long? ClinicToPatientId { get; set; }
        public string Message { get; set; } = "";
    }
}
