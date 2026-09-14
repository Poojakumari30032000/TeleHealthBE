using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllPatientTreatmentsDropDownResponseDTO
    {
        public long? PatientTreatmentId { get; set; }
        public DateTime? TreatmentDate { get; set; }
    }
    public class GetAllPatientTreatmentsResponseDTO
    {
        public long? PatientTreatmentId { get; set; }
        public DateTime? TreatmentDate { get; set; }
        public string? BundleName { get; set; }
    }
}
