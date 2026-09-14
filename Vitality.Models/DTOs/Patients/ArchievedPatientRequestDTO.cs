using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Patients
{
    public class ArchievedPatientRequestDTO
    {
        public long? PatientId { get; set; }
        public bool? IsArchieved { get; set; }
    }
}
