using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stripe;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Services.Stripe
{

    public class StripePlatformChargeService : IStripePlatformCharge
    {
        private readonly StripeClient _stripeClient;

        public StripePlatformChargeService(StripeClient stripeClient)
        {
            _stripeClient = stripeClient ?? throw new ArgumentNullException(nameof(stripeClient));
        }

        public async Task<string?> ChargeCustomerAsync(
            string platformCustomerId,
            long amountInCents,
            string currency,
            CancellationToken ct = default,
            StripePlatformGaChargeContext? gaContext = null)
        {
            if (string.IsNullOrWhiteSpace(platformCustomerId) || amountInCents <= 0)
                return null;

            try
            {
                var paymentMethodId = await GetDefaultPaymentMethodAsync(platformCustomerId, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(paymentMethodId))
                    return null;

                var service = new PaymentIntentService(_stripeClient);
                var options = new PaymentIntentCreateOptions
                {
                    Amount = amountInCents,
                    Currency = currency.Trim().ToLowerInvariant(),
                    Customer = platformCustomerId,
                    PaymentMethod = paymentMethodId,
                    Confirm = true,
                    OffSession = true,
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                    Description = gaContext?.FacilityId is long fid
                        ? $"Clinic-to-global (facility {fid})"
                        : "Clinic-to-global"
                };

                if (gaContext != null &&
                    (!string.IsNullOrWhiteSpace(gaContext.ConnectedAccountId) || gaContext.FacilityId.HasValue))
                {
                    options.Metadata = new Dictionary<string, string>
                    {
                        ["billing_type"] = "clinic_to_global"
                    };
                    if (gaContext.FacilityId.HasValue)
                        options.Metadata["facility_id"] = gaContext.FacilityId.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(gaContext.ConnectedAccountId))
                        options.Metadata["stripe_connected_account_id"] = gaContext.ConnectedAccountId.Trim();
                }

                var intent = await service.CreateAsync(options, null, ct).ConfigureAwait(false);
                var status = (intent.Status ?? "").Trim().ToLowerInvariant();
                if (status == "succeeded" || status == "requires_capture")
                    return intent.Id;
                return null;
            }
            catch (StripeException)
            {
                return null;
            }
        }

        private async Task<string?> GetDefaultPaymentMethodAsync(string customerId, CancellationToken ct)
        {
            var customerService = new CustomerService(_stripeClient);
            var customer = await customerService.GetAsync(customerId, null, null, ct).ConfigureAwait(false);
            var defaultPm = customer.InvoiceSettings?.DefaultPaymentMethodId;
            if (!string.IsNullOrWhiteSpace(defaultPm))
                return defaultPm;

            var pmService = new PaymentMethodService(_stripeClient);
            var listOptions = new PaymentMethodListOptions { Customer = customerId, Type = "card" };
            var paymentMethods = await pmService.ListAsync(listOptions, null, ct).ConfigureAwait(false);
            return paymentMethods.Data?.Count > 0 ? paymentMethods.Data[0].Id : null;
        }
    }
}
