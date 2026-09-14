using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{

    public sealed class StripeRecurringChargeResult
    {

        public string? PaymentIntentId { get; init; }

        public string? CustomerId { get; init; }

        public string? FailureReason { get; init; }
    }

    public interface IStripeRecurringCharge
    {

        Task<StripeRecurringChargeResult> ChargeAsync(
            string stripeAccountId,
            string paymentMethodId,
            long amountInCents,
            string currency,
            string? customerId,
            int applicationFeeAmountCents,
            CancellationToken ct = default,
            string? idempotencyKey = null);
    }
}
