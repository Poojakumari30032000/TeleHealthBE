using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly StripeClient _stripeClient;

        public StripeWebhookController(
            IConfiguration configuration,
            ILogger<StripeWebhookController> logger,
            StripeClient stripeClient)
        {
            _configuration = configuration;
            _logger = logger;
            _stripeClient = stripeClient;
        }

        [HttpPost("thin")]
        public async Task<IActionResult> ThinWebhook()
        {
            var secret = _configuration["Stripe:WebhookSecretThin"]?.Trim();
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogWarning("Stripe:WebhookSecretThin is not set. Thin webhooks will fail.");
                return BadRequest("WebhookSecretThin not configured.");
            }
            var body = await ReadBodyAsync().ConfigureAwait(false);
            var sig = Request.Headers["Stripe-Signature"].ToString();
            if (string.IsNullOrEmpty(sig))
            {
                _logger.LogWarning("Thin webhook: missing Stripe-Signature header.");
                return BadRequest("Missing signature.");
            }
            Event stripeEvent;
            try
            {

                stripeEvent = EventUtility.ConstructEvent(body, sig, secret);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Thin webhook signature verification failed.");
                return BadRequest(ex.Message);
            }

            try
            {
                var eventService = new EventService(_stripeClient);
                var fullEvent = await eventService.GetAsync(stripeEvent.Id);
                await HandleThinEventType(fullEvent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thin webhook handle failed for event {EventId}", stripeEvent.Id);
                return Ok();
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Webhook()
        {
            var secret = _configuration["Stripe:WebhookSecret"]?.Trim();
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogWarning("Stripe:WebhookSecret is not set. Webhooks will fail.");
                return BadRequest("WebhookSecret not configured.");
            }
            var body = await ReadBodyAsync().ConfigureAwait(false);
            var sig = Request.Headers["Stripe-Signature"].ToString();
            if (string.IsNullOrEmpty(sig))
            {
                _logger.LogWarning("Webhook: missing Stripe-Signature header.");
                return BadRequest("Missing signature.");
            }
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(body, sig, secret);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Webhook signature verification failed.");
                return BadRequest(ex.Message);
            }
            try
            {
                await HandleSubscriptionEventAsync(stripeEvent).ConfigureAwait(false);
                await HandlePaymentIntentEventAsync(stripeEvent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook handle failed for event {EventId} type {Type}", stripeEvent.Id, stripeEvent.Type);
                return Ok();
            }
            return Ok();
        }

        private async Task HandlePaymentIntentEventAsync(Event stripeEvent)
        {
            if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var paymentIntent = stripeEvent.Data?.Object as PaymentIntent;
                if (paymentIntent != null)
                {
                    _logger.LogInformation(
                        "Stripe payment_intent.succeeded: PaymentIntentId={PaymentIntentId}, Amount={Amount}, Currency={Currency}, Account={Account}",
                        paymentIntent.Id,
                        paymentIntent.Amount,
                        paymentIntent.Currency,
                        stripeEvent.Account ?? "platform");
                }
                await Task.CompletedTask.ConfigureAwait(false);
                return;
            }
            if (stripeEvent.Type == "payment_intent.payment_failed")
            {
                var paymentIntent = stripeEvent.Data?.Object as PaymentIntent;
                if (paymentIntent != null)
                {
                    _logger.LogWarning(
                        "Stripe payment_intent.payment_failed: PaymentIntentId={PaymentIntentId}, Error={Error}, Account={Account}",
                        paymentIntent.Id,
                        paymentIntent.LastPaymentError?.Message ?? "unknown",
                        stripeEvent.Account ?? "platform");
                }
                await Task.CompletedTask.ConfigureAwait(false);
                return;
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task HandleThinEventType(Event fullEvent)
        {
            switch (fullEvent.Type)
            {
                case "v2.core.account.requirements.updated":

                    _logger.LogInformation("V2 account requirements updated: event {EventId} account {Account}", fullEvent.Id, fullEvent.Account);

                    break;
                case "v2.core.account.recipient.capability_status_updated":
                case "v2.core.account.configuration.merchant.capability_status_updated":
                case "v2.core.account.configuration.customer.capability_status_updated":

                    _logger.LogInformation("V2 account capability status updated: {Type} account {Account}", fullEvent.Type, fullEvent.Account);

                    break;
                default:
                    _logger.LogInformation("Unhandled thin event type: {Type}", fullEvent.Type);
                    break;
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task HandleSubscriptionEventAsync(Event stripeEvent)
        {
            switch (stripeEvent.Type)
            {
                case "customer.subscription.updated":

                    var subUpdated = stripeEvent.Data?.Object as Stripe.Subscription;
                    var accountId = subUpdated?.CustomerAccount ?? subUpdated?.CustomerId;
                    _logger.LogInformation("Subscription updated: {SubId} customer_account {AccountId}", subUpdated?.Id, accountId);

                    break;
                case "customer.subscription.deleted":
                    var subDeleted = stripeEvent.Data?.Object as Stripe.Subscription;
                    _logger.LogInformation("Subscription deleted: {SubId} customer_account {AccountId}", subDeleted?.Id, subDeleted?.CustomerAccount ?? subDeleted?.CustomerId);

                    break;
                case "payment_method.attached":
                case "payment_method.detached":
                case "customer.updated":
                case "customer.tax_id.created":
                case "customer.tax_id.deleted":
                case "customer.tax_id.updated":
                case "billing_portal.configuration.created":
                case "billing_portal.configuration.updated":
                case "billing_portal.session.created":
                    _logger.LogInformation("Stripe event received: {Type}", stripeEvent.Type);
                    break;
                case "payment_intent.succeeded":
                case "payment_intent.payment_failed":

                    break;
                case "account.updated":

                    break;
                default:
                    _logger.LogInformation("Unhandled webhook event type: {Type}", stripeEvent.Type);
                    break;
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task<string> ReadBodyAsync()
        {
            using var reader = new StreamReader(Request.Body);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
    }
}
