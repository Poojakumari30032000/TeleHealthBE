using System;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientOrderManualFulfillment
    {
        public long PatientOrderManualFulfillmentId { get; set; }
        public long PatientOrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? TrackingNumber { get; set; }
        public string? TrackingUrl { get; set; }
        public string? ShippingProvider { get; set; }
        public DateTime? DateShipped { get; set; }
        public string? Notes { get; set; }
        public string? FulfilledBy { get; set; }
        public long? FulfilledByUserId { get; set; }
        public DateTime FulfilledAt { get; set; }

        public virtual PT_PatientOrder PatientOrder { get; set; } = null!;
    }
}
