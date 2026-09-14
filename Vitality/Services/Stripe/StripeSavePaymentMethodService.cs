using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Services.Stripe
{

    public class StripeSavePaymentMethodService : IStripeSavePaymentMethod
    {
        private readonly StripeClient _stripeClient;
        private readonly MainContext _db;

        public StripeSavePaymentMethodService(StripeClient stripeClient, MainContext db)
        {
            _stripeClient = stripeClient ?? throw new ArgumentNullException(nameof(stripeClient));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task SavePaymentMethodFromPaymentIntentAsync(string paymentIntentId, string stripeAccountId, long userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(paymentIntentId) || string.IsNullOrWhiteSpace(stripeAccountId) || userId <= 0)
                return;

            try
            {
                var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
                var piService = new PaymentIntentService(_stripeClient);
                var intent = await piService.GetAsync(paymentIntentId, null, requestOptions, ct).ConfigureAwait(false);
                if (intent?.Status != "succeeded")
                    return;

                var paymentMethodId = intent.PaymentMethodId;
                if (string.IsNullOrWhiteSpace(paymentMethodId))
                    return;

                await PersistConnectedPaymentMethodAsync(
                    paymentMethodId,
                    stripeAccountId,
                    userId,
                    intent.CustomerId,
                    ct).ConfigureAwait(false);
            }
            catch (StripeException)
            {

            }
        }

        public async Task SavePaymentMethodFromSetupIntentAsync(string setupIntentId, string stripeAccountId, long userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(setupIntentId) || string.IsNullOrWhiteSpace(stripeAccountId) || userId <= 0)
                return;

            try
            {
                var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
                var siService = new SetupIntentService(_stripeClient);
                var intent = await siService.GetAsync(setupIntentId, null, requestOptions, ct).ConfigureAwait(false);
                if (intent?.Status != "succeeded")
                    return;

                var paymentMethodId = intent.PaymentMethodId;
                if (string.IsNullOrWhiteSpace(paymentMethodId))
                    return;

                await PersistConnectedPaymentMethodAsync(
                    paymentMethodId,
                    stripeAccountId,
                    userId,
                    intent.CustomerId,
                    ct).ConfigureAwait(false);
            }
            catch (StripeException)
            {

            }
        }

        private async Task PersistConnectedPaymentMethodAsync(
            string paymentMethodId,
            string stripeAccountId,
            long userId,
            string? intentCustomerId,
            CancellationToken ct)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };

            string? customerId = intentCustomerId;
            if (string.IsNullOrWhiteSpace(customerId))
            {
                var existingCard = await _db.SYS_UserCards
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.StripeAccountId == stripeAccountId && c.StripeCustomerId != null, ct).ConfigureAwait(false);
                if (existingCard != null && !string.IsNullOrWhiteSpace(existingCard.StripeCustomerId))
                    customerId = existingCard.StripeCustomerId;
                else
                {
                    var customerService = new CustomerService(_stripeClient);
                    var customer = await customerService.CreateAsync(new CustomerCreateOptions
                    {
                        Metadata = new Dictionary<string, string> { { "user_id", userId.ToString() } }
                    }, requestOptions, ct).ConfigureAwait(false);
                    customerId = customer.Id;
                }
            }

            var pmService = new PaymentMethodService(_stripeClient);
            var pm = await pmService.GetAsync(paymentMethodId, null, requestOptions, ct).ConfigureAwait(false);
            if (pm == null)
                return;

            if (string.IsNullOrWhiteSpace(pm.CustomerId))
            {
                try
                {
                    await pmService.AttachAsync(paymentMethodId, new PaymentMethodAttachOptions { Customer = customerId }, requestOptions, ct).ConfigureAwait(false);
                }
                catch (StripeException)
                {

                }

                pm = await pmService.GetAsync(paymentMethodId, null, requestOptions, ct).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(pm?.CustomerId))
                customerId = pm.CustomerId;

            if (string.IsNullOrWhiteSpace(customerId))
                return;

            var card = await _db.SYS_UserCards
                .FirstOrDefaultAsync(c => c.UserId == userId && c.StripeAccountId == stripeAccountId, ct).ConfigureAwait(false);
            if (card != null)
            {
                card.StripePaymentMethodId = paymentMethodId;
                card.StripeCustomerId = customerId;
                card.StripeAccountId = stripeAccountId;
                card.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var newCard = new SYS_UserCard
                {
                    UserId = userId,
                    StripePaymentMethodId = paymentMethodId,
                    StripeCustomerId = customerId,
                    StripeAccountId = stripeAccountId,
                    IsActive = true,
                    IsDefault = !await _db.SYS_UserCards.AnyAsync(c => c.UserId == userId && c.IsDefault, ct).ConfigureAwait(false),
                    CreatedAt = DateTime.UtcNow
                };
                _db.SYS_UserCards.Add(newCard);
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
