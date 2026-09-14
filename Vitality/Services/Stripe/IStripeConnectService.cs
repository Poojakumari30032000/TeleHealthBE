using System.Collections.Generic;
using System.Threading.Tasks;

namespace Vitality.Services.Stripe
{

    public interface IStripeConnectService
    {

        Task<StripeAccountResult> CreateConnectedAccountAsync(string displayName, string contactEmail, long facilityId);

        Task<string> CreateAccountLinkAsync(string stripeAccountId, string refreshUrl, string returnUrl);

        Task<string> CreateAccountSessionAsync(string stripeAccountId);

        Task<string> CreateAccountSessionForPlatformAsync(string component);

        Task<string> CreateAccountSessionForConnectedFacilityAsync(long facilityId, string component);

        Task<string> CreateAccountSessionForFacilityPaymentsAsync(string stripeAccountId, bool readOnly = false);

        Task<StripeAccountStatusResult> GetAccountStatusAsync(string stripeAccountId);

        Task<StripeProductResult> CreateProductAsync(string stripeAccountId, string name, string description, long priceInCents, string currency = "usd");

        Task<StripeProductListResult> ListProductsAsync(string stripeAccountId, int limit = 20);

        Task<string> CreateCheckoutSessionAsync(string stripeAccountId, string priceId, int quantity, int applicationFeeAmountCents, string successUrl, string cancelUrl);

        Task<string> CreateSubscriptionCheckoutSessionAsync(string connectedAccountId, string priceId, string successUrl, string cancelUrl);

        Task<string> CreateBillingPortalSessionAsync(string connectedAccountId, string returnUrl);

        Task<string?> GetStripeAccountIdForFacilityAsync(long facilityId);

        Task<string> CreatePaymentIntentAsync(
            string stripeAccountId,
            long amountInCents,
            string currency,
            int applicationFeeAmountCents,
            string? metadataOrderId = null,
            string? metadataPatientId = null,
            string? customerName = null,
            string? customerEmail = null);

        Task<string> CreateSetupIntentAsync(
            string stripeAccountId,
            string currency,
            string? metadataPatientId = null,
            string? customerName = null,
            string? customerEmail = null);

        Task<string> CreatePlatformSetupSessionAsync(string platformCustomerId, string successUrl, string cancelUrl);

        Task<SavePatientCardResult> SavePatientStripeCardAsync(string stripeAccountId, string setupIntentId, long userId);

        Task DetachConnectedPaymentMethodAsync(string stripeAccountId, string paymentMethodId);

        Task SetConnectedCustomerDefaultPaymentMethodAsync(string stripeAccountId, string customerId, string paymentMethodId);
    }

    public class SavePatientCardResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public class StripeAccountResult
    {
        public bool Success { get; set; }
        public string? AccountId { get; set; }
        public string? Error { get; set; }
    }

    public class StripeAccountStatusResult
    {
        public bool ReadyToProcessPayments { get; set; }
        public bool OnboardingComplete { get; set; }
        public bool ChargesEnabled { get; set; }
        public bool PayoutsEnabled { get; set; }
        public string? RequirementsStatus { get; set; }
        public string? Error { get; set; }
    }

    public class StripeProductResult
    {
        public bool Success { get; set; }
        public string? ProductId { get; set; }
        public string? PriceId { get; set; }
        public string? Error { get; set; }
    }

    public class StripeProductListResult
    {
        public bool Success { get; set; }
        public List<StripeProductItem>? Products { get; set; }
        public string? Error { get; set; }
    }

    public class StripeProductItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? PriceId { get; set; }
        public long? UnitAmount { get; set; }
        public string? Currency { get; set; }
    }
}
