using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientPaymentDetail
    {
        public PT_PatientPaymentDetail()
        {
            PT_CouponUsages = new HashSet<PT_CouponUsage>();
        }

        public long PatientPaymentId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public decimal? TotalPrice { get; set; }
        public string? CouponCode { get; set; }
        public decimal? DiscountPrice { get; set; }
        public string? ShipmentAddress { get; set; }
        public string? ShipmentStreet { get; set; }
        public int? ShipmentCityId { get; set; }
        public int? ShipmentStateId { get; set; }
        public string? ShipmentZipCode { get; set; }
        public string? PaymentStatus { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Guid { get; set; }

        public virtual ICollection<PT_CouponUsage> PT_CouponUsages { get; set; }
    }
}
