using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class GetPatientOrderByIdResponseDTO
    {
        public long PatientOrderId { get; set; }
        public long? FacilityId { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? DrugVarientId { get; set; }
        public long? DrugIngredientId { get; set; }
        public long? TreamentId { get; set; }
        public string? Status { get; set; }
        public string? Address { get; set; }
        public string? CouponCode { get; set; }
        public decimal? OrderTotal { get; set; }
        public decimal? OrderDiscount { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? ShippedDate { get; set; }
        public string? SubscriptionStatus { get; set; }
        public string? VisitStatus { get; set; }
        public string? LabelStatus { get; set; }
    }
}
