using System;
using System.Threading;
using System.Threading.Tasks;
using Stripe;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Services.Stripe
{

    public class StripePaymentVerifier : IStripePaymentVerifier, IStripeAccountResolver
    {
        private readonly StripeClient _stripeClient;
        private readonly IStripeConnectService _stripeConnect;

        public StripePaymentVerifier(StripeClient stripeClient, IStripeConnectService stripeConnect)
        {
            _stripeClient = stripeClient ?? throw new ArgumentNullException(nameof(stripeClient));
            _stripeConnect = stripeConnect ?? throw new ArgumentNullException(nameof(stripeConnect));
        }

        public async Task<string?> GetStripeAccountIdForFacilityAsync(long facilityId, CancellationToken ct = default)
        {
            return await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId).ConfigureAwait(false);
        }

        public async Task<StripePaymentVerificationResult?> VerifyPaymentIntentAsync(
            string paymentIntentId,
            string stripeAccountId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(paymentIntentId) || string.IsNullOrWhiteSpace(stripeAccountId))
                return null;

            try
            {
                var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
                var service = new PaymentIntentService(_stripeClient);
                var intent = await service.GetAsync(paymentIntentId, null, requestOptions).ConfigureAwait(false);
                if (intent == null)
                    return null;

                var status = (intent.Status ?? "").Trim().ToLowerInvariant();
                if (status != "succeeded")
                    return new StripePaymentVerificationResult { Succeeded = false, Amount = 0, Currency = intent.Currency ?? "usd", StripeAccountId = stripeAccountId };

                var amountCents = intent.Amount;
                var amountDollars = amountCents / 100m;
                return new StripePaymentVerificationResult
                {
                    Succeeded = true,
                    Amount = amountDollars,
                    Currency = intent.Currency ?? "usd",
                    StripeAccountId = stripeAccountId
                };
            }
            catch (StripeException)
            {
                return null;
            }
        }

        public async Task<StripeSetupVerificationResult?> VerifySetupIntentAsync(
            string setupIntentId,
            string stripeAccountId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(setupIntentId) || string.IsNullOrWhiteSpace(stripeAccountId))
                return null;

            try
            {
                var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
                var service = new SetupIntentService(_stripeClient);
                var intent = await service.GetAsync(setupIntentId, null, requestOptions, ct).ConfigureAwait(false);
                if (intent == null)
                    return null;

                var status = (intent.Status ?? "").Trim().ToLowerInvariant();
                if (status != "succeeded")
                    return new StripeSetupVerificationResult { Succeeded = false, StripeAccountId = stripeAccountId };

                return new StripeSetupVerificationResult { Succeeded = true, StripeAccountId = stripeAccountId };
            }
            catch (StripeException)
            {
                return null;
            }
        }

        public async Task<bool> RefundPaymentIntentAsync(
            string paymentIntentId,
            string? stripeAccountId,
            string? reason = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(paymentIntentId))
                return false;

            try
            {
                var requestOptions = stripeAccountId != null ? new RequestOptions { StripeAccount = stripeAccountId } : null;
                var service = new RefundService(_stripeClient);
                var options = new RefundCreateOptions
                {
                    PaymentIntent = paymentIntentId,
                    Reason = reason == "duplicate" ? "duplicate" : "requested_by_customer"
                };
                await service.CreateAsync(options, requestOptions).ConfigureAwait(false);
                return true;
            }
            catch (StripeException)
            {
                return false;
            }
        }
    }
}
