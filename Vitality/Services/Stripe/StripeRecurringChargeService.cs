using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stripe;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Services.Stripe
{

    public class StripeRecurringChargeService : IStripeRecurringCharge
    {
        private readonly StripeClient _stripeClient;

        public StripeRecurringChargeService(StripeClient stripeClient)
        {
            _stripeClient = stripeClient ?? throw new ArgumentNullException(nameof(stripeClient));
        }

        public async Task<StripeRecurringChargeResult> ChargeAsync(
            string stripeAccountId,
            string paymentMethodId,
            long amountInCents,
            string currency,
            string? customerId,
            int applicationFeeAmountCents,
            CancellationToken ct = default,
            string? idempotencyKey = null)
        {
            var empty = new StripeRecurringChargeResult();
            if (string.IsNullOrWhiteSpace(stripeAccountId) || string.IsNullOrWhiteSpace(paymentMethodId) || amountInCents <= 0)
                return empty;

            try
            {
                var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
                var pmService = new PaymentMethodService(_stripeClient);
                var customerService = new CustomerService(_stripeClient);

                var pm = await pmService.GetAsync(paymentMethodId, null, requestOptions, ct).ConfigureAwait(false);
                if (pm == null)
                    return empty;

                string resolvedCustomerId;
                if (!string.IsNullOrWhiteSpace(pm.CustomerId))
                {
                    resolvedCustomerId = pm.CustomerId;
                }
                else
                {
                    resolvedCustomerId = string.IsNullOrWhiteSpace(customerId) ? string.Empty : customerId.Trim();
                    if (string.IsNullOrWhiteSpace(resolvedCustomerId))
                    {
                        var customer = await customerService.CreateAsync(new CustomerCreateOptions(), requestOptions, ct).ConfigureAwait(false);
                        resolvedCustomerId = customer.Id;
                    }

                    try
                    {
                        await pmService.AttachAsync(
                            paymentMethodId,
                            new PaymentMethodAttachOptions { Customer = resolvedCustomerId },
                            requestOptions,
                            ct).ConfigureAwait(false);
                    }
                    catch (StripeException ex) when (IsAlreadyAttached(ex))
                    {
                        pm = await pmService.GetAsync(paymentMethodId, null, requestOptions, ct).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(pm?.CustomerId))
                            resolvedCustomerId = pm.CustomerId;
                        else
                            return empty;
                    }
                }

                var service = new PaymentIntentService(_stripeClient);
                var createOptions = new PaymentIntentCreateOptions
                {
                    Amount = amountInCents,
                    Currency = currency.Trim().ToLowerInvariant(),
                    PaymentMethodTypes = new List<string> { "card" },
                    Customer = resolvedCustomerId,
                    PaymentMethod = paymentMethodId,
                    Confirm = true,
                    ApplicationFeeAmount = applicationFeeAmountCents,
                    OffSession = true
                };

                var paymentIntentOptions = new RequestOptions
                {
                    StripeAccount = stripeAccountId,
                    IdempotencyKey = idempotencyKey
                };

                var intent = await service.CreateAsync(createOptions, paymentIntentOptions, ct).ConfigureAwait(false);
                var status = (intent.Status ?? "").Trim().ToLowerInvariant();
                if (status == "succeeded" || status == "requires_capture")
                {
                    return new StripeRecurringChargeResult
                    {
                        PaymentIntentId = intent.Id,
                        CustomerId = resolvedCustomerId
                    };
                }

                return new StripeRecurringChargeResult
                {
                    CustomerId = resolvedCustomerId,
                    FailureReason = $"Stripe PaymentIntent status was '{status}' (not 'succeeded' or 'requires_capture')."
                };
            }
            catch (StripeException sex)
            {

                var code = sex.StripeError?.Code;
                var msg = sex.StripeError?.Message ?? sex.Message;
                var combined = string.IsNullOrWhiteSpace(code) ? msg : $"[{code}] {msg}";
                return new StripeRecurringChargeResult
                {

                    FailureReason = combined
                };
            }
        }

        private static bool IsAlreadyAttached(StripeException ex)
        {
            var code = ex.StripeError?.Code;
            if (code == "resource_already_exists")
                return true;
            var msg = ex.Message ?? string.Empty;
            return msg.Contains("already been attached", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("already attached", StringComparison.OrdinalIgnoreCase);
        }
    }
}
