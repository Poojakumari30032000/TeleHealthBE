using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientOrder
    {
        public PT_PatientOrder()
        {
            PT_CouponUsages = new HashSet<PT_CouponUsage>();
        }

        public long PatientOrderId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? PatientTreamentId { get; set; }
        public long? PatientPaymentId { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public long? PatientAppointmentId { get; set; }
        public string? OrderStatus { get; set; }
        public string? Address { get; set; }
        public string? CouponCode { get; set; }
        public decimal? OrderTotal { get; set; }
        public decimal? OrderDiscount { get; set; }
        public decimal? OrderPayableAmount { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? ShippedDate { get; set; }
        public string? SubscriptionStatus { get; set; }
        public string? VisitStatus { get; set; }
        public string? LabelStatus { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Guid { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }

        public virtual ICollection<PT_CouponUsage> PT_CouponUsages { get; set; }
    }
}
