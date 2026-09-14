using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Patients
{
    public class ClinicSquareKeys
    {
        public string? ApplicationId { get; set; }
        public string? LocationId { get; set; }
        public long? PatientId { get; set; }
        public long? UserId { get; set; }

    }

    public class UnauthorizedPatientDto
    {
        public long? PatientId { get; set; }
        public long? UserId { get; set; }

    }
}
