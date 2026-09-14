using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Square.Models;

namespace Vitality.Models.DTOs.Square
{

    public class PaymentRequest
    {
        public string SourceId { get; set; } = default!;
        public int Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? IdempotencyKey { get; set; } = default!;
        public string? BillingAddress { get; set; }
        public string? CardholderName { get; set; }
        public string? InvoiceType { get; set; }
        public long? SubscriptionId { get; set; }
        public string? SquareCustomerId { get; set; }
        public long? UserId { get; set; }
        public long? FacilityId { get; set; }
        public long? LocationId { get; set; }
        public bool SaveCard { get; set; } = false;
    }

    public class SaveCardRequest
    {
        public string SourceId { get; set; } = default!;
        public string? CardholderName { get; set; }
        public long? UserId { get; set; }
        public long? FacilityId { get; set; }
        public string? Currency { get; set; }

    }
    public class PatientPaymentRequestRequest
    {
        public string SourceId { get; set; } = default!;
        public string? CardholderName { get; set; }
        public string? Currency { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? FacilityId { get; set; }
        public decimal? Price { get; set; }
        public string? CouponCode { get; set; }
        public bool? IsRecurring { get; set; }

    }

    public class CreatePaymentAndAppointmentRequest
    {

        public string SourceId { get; set; } = default!;
        public string? CardholderName { get; set; }
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

    public class CreatePaymentAndAppointmentWithSavedCardRequest
    {

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

    public class CreatePaymentAndAppointmentWithSavedCardResponseDTO
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public long? PatientTreatmentId { get; set; }
        public long? CategoryId { get; set; }
    }

}
