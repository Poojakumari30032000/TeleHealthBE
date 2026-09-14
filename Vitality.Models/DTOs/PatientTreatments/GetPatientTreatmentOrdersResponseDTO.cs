using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetPatientTreatmentOrdersResponseDTO
    {
        public string? PatientName { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? MRN { get; set; }
        public string? Pharmacy { get; set; }
        public string? VisitGuid { get; set; }
        public string? VisitStatus { get; set; }
        public long? OrdeId { get; set; }
        public string? OrderStatus { get; set; }
        public string? Address { get; set; }
    }
}
