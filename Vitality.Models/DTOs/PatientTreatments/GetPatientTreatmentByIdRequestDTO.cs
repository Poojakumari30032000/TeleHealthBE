using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetPatientTreatmentByIdRequestDTO
    {
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
    }
}
