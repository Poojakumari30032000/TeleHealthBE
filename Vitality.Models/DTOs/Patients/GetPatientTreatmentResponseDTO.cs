using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Patients
{
    public class GetPatientTreatmentResponseDTO
    {
        public long? PatientTreatmentId { get; set; }
        public string? Name { get; set; }
        public string? BundleName { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? Frequency { get; set; }
        public int? Refills { get; set; }
        public decimal? Price { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? NextShippingDate { get; set; }
        public DateTime? NextPaymentDate { get; set; }
        public int? NumberOfProducts { get; set; }
        public int? Prescriptions { get; set; }
        public int? PrescriptionsCount { get; set; }
        public int? OrderCount { get; set; }
        public string? Status { get; set; }

        public string? RefillStatus { get; set; }
    }

}
