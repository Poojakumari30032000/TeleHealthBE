using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class GetAllPatientOrdersResponseDTO
    {

        public long PatientOrderId { get; set; }
        public long ProductId { get; set; }
        public long PatientId { get; set; }
        public long? FacilityId { get; set; }

        public string? PatientName { get; set; }
        public string? FacilityName { get; set; }
        public string? PatientEmail { get; set; }
        public string? PharmacyName { get; set; }

        public DateTime? OrderDate { get; set; }
        public DateTime? DatePrescribed { get; set; }
        public DateTime? ShippedDate { get; set; }
        public DateTime? PaymentDate { get; set; }

        public string? MRN { get; set; }
        public string? PaymentStatus { get; set; }
        public string? VisitStatus { get; set; }
        public string? Address { get; set; }
        public string? OrderStatus { get; set; }
        public decimal? OrderTotal { get; set; }
        public decimal? OrderDiscount { get; set; }
        public string? VisitId { get; set; }
        public string? CouponCode { get; set; }
        public bool? IsAutoRefill { get; set; }
        public bool? SendToPharmacy { get; set; }

        public List<string> VarientIds { get; set; } = new();
        public List<string> Products { get; set; } = new();
    }
}
