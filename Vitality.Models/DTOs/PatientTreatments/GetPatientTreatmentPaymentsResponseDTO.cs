using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetPatientTreatmentPaymentsResponseDTO
    {
        public string? Id { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal? FinalPrice { get; set; }
        public decimal? Discount {  get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? CapturedDate { get; set; }
        public string? paymentStatus { get; set; }
        public string? Coupon {  get; set; }
    }
}
