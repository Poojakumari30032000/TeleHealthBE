using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetPatientTreatmentProductsResponseDTO
    {
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Type { get; set; }
        public string? Schedule {  get; set; }
        public int? Refills { get; set; }
        public int? Refillsremaining { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity {  get; set; }
        public DateTime? NextShippingDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }
}
