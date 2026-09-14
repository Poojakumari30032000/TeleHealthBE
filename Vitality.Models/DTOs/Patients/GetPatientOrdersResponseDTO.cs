using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Patients
{
    public class GetPatientOrdersResponseDTO
    {
        public long? PatientOrderId { get; set; }
        public string? PatientName { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? MRN { get; set; }
        public string? Pharmacy { get; set; }
        public string? VisitId { get; set; }
        public string? VisitStatus { get; set; }
        public long? OrderId { get; set; }
        public string? OrderGuid { get; set; }
        public string? OrderStatus { get; set; }
        public string? Address { get; set; }
    }
}
