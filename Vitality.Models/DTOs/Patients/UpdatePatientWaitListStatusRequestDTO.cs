using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Patients
{
    public class UpdatePatientWaitListStatusRequestDTO
    {
        public long? PatientId { get; set; }
        public string? WaitListReason { get; set; }
    }
}
