using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{

    public interface IStripePaymentVerifier
    {

        Task<StripePaymentVerificationResult?> VerifyPaymentIntentAsync(
            string paymentIntentId,
            string stripeAccountId,
            CancellationToken ct = default);

        Task<StripeSetupVerificationResult?> VerifySetupIntentAsync(
            string setupIntentId,
            string stripeAccountId,
            CancellationToken ct = default);

        Task<bool> RefundPaymentIntentAsync(
            string paymentIntentId,
            string? stripeAccountId,
            string? reason = null,
            CancellationToken ct = default);
    }

    public class StripePaymentVerificationResult
    {
        public bool Succeeded { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public string? StripeAccountId { get; set; }
    }

    public class StripeSetupVerificationResult
    {
        public bool Succeeded { get; set; }
        public string? StripeAccountId { get; set; }
    }
}
