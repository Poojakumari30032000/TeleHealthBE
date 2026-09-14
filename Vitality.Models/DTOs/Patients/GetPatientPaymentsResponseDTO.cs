using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Patients
{
    public class GetPatientPaymentsResponseDTO
    {
        public long? PaymentId { get; set; }
        public string? Id { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal? FinalPrice { get; set; }
        public int? Discount { get; set; }
        public DateTime? Date { get; set; }
        public string? PaymentStatus { get; set; }
        public string? Coupon { get; set; }
    }
    public class GetPatientInvoicesResponseDTO
    {
        public long? PaymentId { get; set; }
        public string? Id { get; set; }
        public string? TotalPrice { get; set; }
        public decimal? FinalPrice { get; set; }
        public string? Discount { get; set; }
        public DateTime? Date { get; set; }
        public string? PaymentStatus { get; set; }
        public string? Coupon { get; set; }
    }
}
