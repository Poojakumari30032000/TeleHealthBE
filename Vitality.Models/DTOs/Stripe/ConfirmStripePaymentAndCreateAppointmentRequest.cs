using System;

namespace Vitality.Models.DTOs.Stripe
{

    public class ConfirmStripePaymentAndCreateAppointmentRequest
    {

        public string? PaymentIntentId { get; set; }

        public string? SetupIntentId { get; set; }
        public string? Currency { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? FacilityId { get; set; }
        public decimal? Price { get; set; }
        public string? CouponCode { get; set; }
        public bool? IsRecurring { get; set; }

        public long? ProviderScheduledSlotId { get; set; }
        public string? Title { get; set; }
        public long? ProviderId { get; set; }
        public DateTime? StartDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? Duration { get; set; }
    }
}
