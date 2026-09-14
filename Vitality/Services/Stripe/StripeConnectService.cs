using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using Vitality.Models.EntityClasses;

namespace Vitality.Services.Stripe
{

    public class StripeConnectService : IStripeConnectService
    {
        private readonly StripeClient _stripeClient;
        private readonly HttpClient _httpClient;
        private readonly MainContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripeConnectService> _logger;

        private static readonly JsonSerializerOptions StripeV2JsonOptions = new() { PropertyNamingPolicy = null, PropertyNameCaseInsensitive = true };

        public StripeConnectService(
            StripeClient stripeClient,
            IHttpClientFactory httpClientFactory,
            MainContext db,
            IConfiguration configuration,
            ILogger<StripeConnectService> logger)
        {
            _stripeClient = stripeClient ?? throw new ArgumentNullException(nameof(stripeClient));
            _httpClient = httpClientFactory.CreateClient("StripeV2");
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<StripeAccountResult> CreateConnectedAccountAsync(string displayName, string contactEmail, long facilityId)
        {

            var existingAccountId = await GetStripeAccountIdForFacilityAsync(facilityId).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(existingAccountId))
            {
                return new StripeAccountResult { Success = true, AccountId = existingAccountId };
            }

            var payload = new
            {
                display_name = displayName ?? "",
                contact_email = contactEmail ?? "",
                identity = new { country = "us" },
                dashboard = "full",
                defaults = new
                {
                    responsibilities = new
                    {
                        fees_collector = "stripe",
                        losses_collector = "stripe"
                    }
                },
                configuration = new
                {
                    customer = new { },
                    merchant = new
                    {
                        capabilities = new
                        {
                            card_payments = new { requested = true }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload, StripeV2JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v2/core/accounts") { Content = content };
            AddStripeAuth(request);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe V2 account create request failed");
                return new StripeAccountResult { Success = false, Error = ex.Message };
            }

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe V2 account create failed: {Code} {Body}", response.StatusCode, responseBody);
                return new StripeAccountResult { Success = false, Error = responseBody };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var accountId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(accountId))
            {
                return new StripeAccountResult { Success = false, Error = "Stripe returned no account id." };
            }

            var existing = await _db.Sys_FacilityStripeConnects
                .FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.IsActive == true).ConfigureAwait(false);
            if (existing != null)
            {
                existing.StripeAccountId = accountId;
                existing.ModifiedDate = DateTime.UtcNow;
            }
            else
            {
                _db.Sys_FacilityStripeConnects.Add(new Sys_FacilityStripeConnect
                {
                    FacilityId = facilityId,
                    StripeAccountId = accountId,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync().ConfigureAwait(false);

            return new StripeAccountResult { Success = true, AccountId = accountId };
        }

        public async Task<string> CreateAccountLinkAsync(string stripeAccountId, string refreshUrl, string returnUrl)
        {

            var returnUrlWithAccount = returnUrl + (returnUrl.Contains("?") ? "&" : "?") + "accountId=" + Uri.EscapeDataString(stripeAccountId);
            var form = new Dictionary<string, string>
            {
                ["account"] = stripeAccountId,
                ["refresh_url"] = refreshUrl,
                ["return_url"] = returnUrlWithAccount,
                ["type"] = "account_onboarding"
            };
            var formContent = new FormUrlEncodedContent(form);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/account_links") { Content = formContent };
            AddStripeAuthV1Only(request);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe account link create failed: {Code} {Body}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"Stripe account link failed: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var url = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("Stripe returned no account link url.");
            return url;
        }

        public async Task<string> CreateAccountSessionAsync(string stripeAccountId)
        {
            var service = new AccountSessionService(_stripeClient);
            var options = new AccountSessionCreateOptions
            {
                Account = stripeAccountId,
                Components = new AccountSessionComponentsOptions
                {
                    AccountOnboarding = new AccountSessionComponentsAccountOnboardingOptions
                    {
                        Enabled = true
                    }
                }
            };
            var session = await service.CreateAsync(options).ConfigureAwait(false);
            if (string.IsNullOrEmpty(session.ClientSecret))
                throw new InvalidOperationException("Stripe did not return a client_secret for the Account Session.");
            return session.ClientSecret;
        }

        private const string PlatformAccountId = "acct_1SyvRtJUpSek4giC";

        public async Task<string> CreateAccountSessionForPlatformAsync(string component)
        {
            var service = new AccountSessionService(_stripeClient);
            var components = BuildAccountSessionComponents(component, allowManagement: true);

            var options = new AccountSessionCreateOptions
            {
                Account = GetConfiguredPlatformAccountId(),
                Components = components
            };
            var session = await service.CreateAsync(options).ConfigureAwait(false);
            if (string.IsNullOrEmpty(session.ClientSecret))
                throw new InvalidOperationException("Stripe did not return a client_secret for the Account Session.");
            return session.ClientSecret;
        }

        public async Task<string> CreateAccountSessionForConnectedFacilityAsync(long facilityId, string component)
        {
            var stripeAccountId = await GetStripeAccountIdForFacilityAsync(facilityId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(stripeAccountId))
                throw new InvalidOperationException($"No Stripe account found for facility {facilityId}.");

            var service = new AccountSessionService(_stripeClient);
            var components = BuildAccountSessionComponents(component, allowManagement: true);

            var options = new AccountSessionCreateOptions
            {
                Account = stripeAccountId,
                Components = components
            };
            var session = await service.CreateAsync(options).ConfigureAwait(false);
            if (string.IsNullOrEmpty(session.ClientSecret))
                throw new InvalidOperationException("Stripe did not return a client_secret for the Account Session.");
            return session.ClientSecret;
        }

        public async Task<string> CreateAccountSessionForFacilityPaymentsAsync(string stripeAccountId, bool readOnly = false)
        {
            if (string.IsNullOrEmpty(stripeAccountId))
                throw new ArgumentException("Stripe account ID is required.", nameof(stripeAccountId));

            var service = new AccountSessionService(_stripeClient);
            var components = BuildAccountSessionComponents("payments", allowManagement: !readOnly);
            var options = new AccountSessionCreateOptions
            {
                Account = stripeAccountId,
                Components = components
            };
            var session = await service.CreateAsync(options).ConfigureAwait(false);
            if (string.IsNullOrEmpty(session.ClientSecret))
                throw new InvalidOperationException("Stripe did not return a client_secret for the Account Session.");
            return session.ClientSecret;
        }

        private AccountSessionComponentsOptions BuildAccountSessionComponents(string? component, bool allowManagement)
        {
            var components = new AccountSessionComponentsOptions();

            switch ((component ?? "").Trim().ToLowerInvariant())
            {
                case "payments":
                    components.Payments = new AccountSessionComponentsPaymentsOptions
                    {
                        Enabled = true,
                        Features = new AccountSessionComponentsPaymentsFeaturesOptions
                        {
                            RefundManagement = allowManagement,
                            DisputeManagement = allowManagement,
                            CapturePayments = allowManagement,
                            DestinationOnBehalfOfChargeManagement = allowManagement
                        }
                    };
                    break;
                case "payment-details":
                    components.PaymentDetails = new AccountSessionComponentsPaymentDetailsOptions
                    {
                        Enabled = true,
                        Features = new AccountSessionComponentsPaymentDetailsFeaturesOptions
                        {
                            RefundManagement = allowManagement,
                            DisputeManagement = allowManagement,
                            CapturePayments = allowManagement,
                            DestinationOnBehalfOfChargeManagement = allowManagement
                        }
                    };
                    break;
                case "payouts-list":
                    components.PayoutsList = new AccountSessionComponentsPayoutsListOptions { Enabled = true };
                    break;
                case "disputes-list":
                    components.DisputesList = new AccountSessionComponentsDisputesListOptions
                    {
                        Enabled = true,
                        Features = new AccountSessionComponentsDisputesListFeaturesOptions
                        {
                            RefundManagement = allowManagement,
                            DisputeManagement = allowManagement,
                            CapturePayments = allowManagement
                        }
                    };
                    break;
                default:
                    throw new ArgumentException($"Unknown component: {component}. Use payments, payment-details, payouts-list, or disputes-list.");
            }

            return components;
        }

        private string GetConfiguredPlatformAccountId()
        {
            var configuredAccountId = _configuration["Stripe:PlatformAccountId"]?.Trim();
            return string.IsNullOrWhiteSpace(configuredAccountId) ? PlatformAccountId : configuredAccountId;
        }

        public async Task<StripeAccountStatusResult> GetAccountStatusAsync(string stripeAccountId)
        {

            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.stripe.com/v1/accounts/" + Uri.EscapeDataString(stripeAccountId));
            AddStripeAuthV1Only(request);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new StripeAccountStatusResult { Error = responseBody };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            bool chargesEnabled = GetBoolOrTrueString(root, "charges_enabled");
            bool detailsSubmitted = GetBoolOrTrueString(root, "details_submitted");
            bool payoutsEnabled = GetBoolOrTrueString(root, "payouts_enabled");

            bool readyToProcessPayments = chargesEnabled;
            bool onboardingComplete = detailsSubmitted;

            string? requirementsStatus = null;
            if (root.TryGetProperty("requirements", out var req))
            {
                if (req.TryGetProperty("currently_due", out var due) && due.GetArrayLength() > 0)
                    requirementsStatus = "currently_due";
                else if (req.TryGetProperty("past_due", out var past) && past.GetArrayLength() > 0)
                    requirementsStatus = "past_due";
            }

            return new StripeAccountStatusResult
            {
                ReadyToProcessPayments = readyToProcessPayments,
                OnboardingComplete = onboardingComplete,
                ChargesEnabled = chargesEnabled,
                PayoutsEnabled = payoutsEnabled,
                RequirementsStatus = requirementsStatus
            };
        }

        private static bool GetBoolOrTrueString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var el)) return false;
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString() ?? "";
                return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public async Task<StripeProductResult> CreateProductAsync(string stripeAccountId, string name, string description, long priceInCents, string currency = "usd")
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var service = new ProductService(_stripeClient);
            var options = new ProductCreateOptions
            {
                Name = name,
                Description = description ?? "",
                DefaultPriceData = new ProductDefaultPriceDataOptions
                {
                    UnitAmount = priceInCents,
                    Currency = currency
                }
            };
            try
            {
                var product = await service.CreateAsync(options, requestOptions).ConfigureAwait(false);
                var priceId = product.DefaultPriceId ?? (product.DefaultPrice as global::Stripe.Price)?.Id;
                return new StripeProductResult { Success = true, ProductId = product.Id, PriceId = priceId };
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe product create failed for account {AccountId}", stripeAccountId);
                return new StripeProductResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<StripeProductListResult> ListProductsAsync(string stripeAccountId, int limit = 20)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var service = new ProductService(_stripeClient);
            var options = new ProductListOptions { Limit = limit, Active = true, Expand = new List<string> { "data.default_price" } };
            try
            {
                var list = await service.ListAsync(options, requestOptions).ConfigureAwait(false);
                var products = list.Data.Select(p =>
                {
                    long? unitAmount = null;
                    string? priceId = null;
                    string? curr = null;
                    if (p.DefaultPrice is global::Stripe.Price price)
                    {
                        priceId = price.Id;
                        unitAmount = price.UnitAmount;
                        curr = price.Currency;
                    }
                    else if (!string.IsNullOrEmpty(p.DefaultPriceId))
                    {
                        priceId = p.DefaultPriceId;
                    }
                    return new StripeProductItem
                    {
                        Id = p.Id,
                        Name = p.Name ?? "",
                        Description = p.Description,
                        PriceId = priceId,
                        UnitAmount = unitAmount,
                        Currency = curr
                    };
                }).ToList();
                return new StripeProductListResult { Success = true, Products = products };
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe product list failed for account {AccountId}", stripeAccountId);
                return new StripeProductListResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<string> CreateCheckoutSessionAsync(string stripeAccountId, string priceId, int quantity, int applicationFeeAmountCents, string successUrl, string cancelUrl)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var service = new SessionService(_stripeClient);
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = quantity
                    }
                },
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    ApplicationFeeAmount = applicationFeeAmountCents
                },

                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };
            var session = await service.CreateAsync(options, requestOptions).ConfigureAwait(false);
            return session.Url ?? throw new InvalidOperationException("Stripe did not return a checkout URL.");
        }

        public async Task<string> CreateSubscriptionCheckoutSessionAsync(string connectedAccountId, string priceId, string successUrl, string cancelUrl)
        {

            var service = new SessionService(_stripeClient);
            var options = new SessionCreateOptions
            {
                CustomerAccount = connectedAccountId,
                Mode = "subscription",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions { Price = priceId, Quantity = 1 }
                },
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };
            var session = await service.CreateAsync(options).ConfigureAwait(false);
            return session.Url ?? throw new InvalidOperationException("Stripe did not return a checkout URL.");
        }

        public async Task<string> CreatePlatformSetupSessionAsync(string platformCustomerId, string successUrl, string cancelUrl)
        {
            if (string.IsNullOrWhiteSpace(platformCustomerId) || !platformCustomerId.TrimStart().StartsWith("cus_", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A valid Stripe platform customer id is required.", nameof(platformCustomerId));
            var service = new SessionService(_stripeClient);
            var options = new SessionCreateOptions
            {
                Mode = "setup",
                Customer = platformCustomerId.Trim(),
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Currency = "usd"
            };
            var session = await service.CreateAsync(options).ConfigureAwait(false);
            return session.Url ?? throw new InvalidOperationException("Stripe did not return a checkout URL.");
        }

        public async Task<string> CreateBillingPortalSessionAsync(string connectedAccountId, string returnUrl)
        {
            var service = new global::Stripe.BillingPortal.SessionService(_stripeClient);
            var options = new global::Stripe.BillingPortal.SessionCreateOptions
            {
                CustomerAccount = connectedAccountId,
                ReturnUrl = returnUrl
            };
            var session = await service.CreateAsync(options).ConfigureAwait(false);
            return session.Url ?? throw new InvalidOperationException("Stripe did not return a portal URL.");
        }

        public async Task<string?> GetStripeAccountIdForFacilityAsync(long facilityId)
        {
            var row = await _db.Sys_FacilityStripeConnects
                .Where(x => x.FacilityId == facilityId && x.IsActive == true)
                .Select(x => x.StripeAccountId)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            return row;
        }

        public async Task<string> CreatePaymentIntentAsync(
            string stripeAccountId,
            long amountInCents,
            string currency,
            int applicationFeeAmountCents,
            string? metadataOrderId = null,
            string? metadataPatientId = null,
            string? customerName = null,
            string? customerEmail = null)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var service = new PaymentIntentService(_stripeClient);
            var options = new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = currency.ToLowerInvariant(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                ApplicationFeeAmount = applicationFeeAmountCents,

                SetupFutureUsage = "off_session"
            };
            var customerId = await GetOrCreateConnectedCustomerAsync(
                stripeAccountId,
                customerName,
                customerEmail,
                metadataPatientId).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(customerId))
                options.Customer = customerId;
            if (!string.IsNullOrWhiteSpace(customerEmail))
                options.ReceiptEmail = customerEmail.Trim();
            if (!string.IsNullOrWhiteSpace(customerName))
                options.Description = $"Payment from {customerName.Trim()}";
            if (!string.IsNullOrEmpty(metadataOrderId) || !string.IsNullOrEmpty(metadataPatientId))
            {
                options.Metadata = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(metadataOrderId)) options.Metadata["order_id"] = metadataOrderId;
                if (!string.IsNullOrEmpty(metadataPatientId)) options.Metadata["patient_id"] = metadataPatientId;
            }
            if (!string.IsNullOrWhiteSpace(customerName) || !string.IsNullOrWhiteSpace(customerEmail))
            {
                options.Metadata ??= new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(customerName)) options.Metadata["customer_name"] = customerName.Trim();
                if (!string.IsNullOrWhiteSpace(customerEmail)) options.Metadata["customer_email"] = customerEmail.Trim();
            }
            var intent = await service.CreateAsync(options, requestOptions).ConfigureAwait(false);
            if (string.IsNullOrEmpty(intent.ClientSecret))
                throw new InvalidOperationException("Stripe did not return a client_secret for the PaymentIntent.");
            return intent.ClientSecret;
        }

        public async Task<string> CreateSetupIntentAsync(
            string stripeAccountId,
            string currency,
            string? metadataPatientId = null,
            string? customerName = null,
            string? customerEmail = null)
        {
            _ = currency;
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var customerId = await GetOrCreateConnectedCustomerAsync(
                    stripeAccountId,
                    customerName,
                    customerEmail,
                    metadataPatientId).ConfigureAwait(false);

            var options = new SetupIntentCreateOptions
            {
                Usage = "off_session",
                AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions { Enabled = true },
            };
            if (!string.IsNullOrWhiteSpace(customerId))
                options.Customer = customerId;

            if (!string.IsNullOrEmpty(metadataPatientId))
                options.Metadata = new Dictionary<string, string> { ["patient_id"] = metadataPatientId };
            if (!string.IsNullOrWhiteSpace(customerName) || !string.IsNullOrWhiteSpace(customerEmail))
            {
                options.Metadata ??= new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(customerName)) options.Metadata["customer_name"] = customerName.Trim();
                if (!string.IsNullOrWhiteSpace(customerEmail)) options.Metadata["customer_email"] = customerEmail.Trim();
            }

            var service = new SetupIntentService(_stripeClient);
            var intent = await service.CreateAsync(options, requestOptions).ConfigureAwait(false);
            if (string.IsNullOrEmpty(intent.ClientSecret))
                throw new InvalidOperationException("Stripe did not return a client_secret for the SetupIntent.");
            return intent.ClientSecret;
        }

        private async Task<string?> GetOrCreateConnectedCustomerAsync(
            string stripeAccountId,
            string? customerName,
            string? customerEmail,
            string? patientId)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var customerService = new CustomerService(_stripeClient);

            if (string.IsNullOrWhiteSpace(customerEmail) && string.IsNullOrWhiteSpace(customerName))
            {
                if (!string.IsNullOrWhiteSpace(patientId))
                {
                    var patientCustomer = await customerService.CreateAsync(new CustomerCreateOptions
                    {
                        Metadata = new Dictionary<string, string> { ["patient_id"] = patientId.Trim() }
                    }, requestOptions).ConfigureAwait(false);
                    return patientCustomer.Id;
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(customerEmail))
            {
                var existingCustomers = await customerService.ListAsync(new CustomerListOptions
                {
                    Email = customerEmail.Trim(),
                    Limit = 1
                }, requestOptions).ConfigureAwait(false);

                var existingCustomer = existingCustomers.Data?.FirstOrDefault();
                if (existingCustomer != null && !string.IsNullOrWhiteSpace(existingCustomer.Id))
                    return existingCustomer.Id;
            }

            var createOptions = new CustomerCreateOptions();
            if (!string.IsNullOrWhiteSpace(customerEmail))
                createOptions.Email = customerEmail.Trim();
            if (!string.IsNullOrWhiteSpace(customerName))
                createOptions.Name = customerName.Trim();
            if (!string.IsNullOrWhiteSpace(patientId))
            {
                createOptions.Metadata = new Dictionary<string, string>
                {
                    ["patient_id"] = patientId.Trim()
                };
            }

            var customer = await customerService.CreateAsync(createOptions, requestOptions).ConfigureAwait(false);
            return customer.Id;
        }

        public async Task<SavePatientCardResult> SavePatientStripeCardAsync(
            string stripeAccountId,
            string setupIntentId,
            long userId)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };

            var setupIntentService = new SetupIntentService(_stripeClient);
            var intent = await setupIntentService.GetAsync(setupIntentId, null, requestOptions).ConfigureAwait(false);
            if (intent == null || intent.Status != "succeeded")
                return new SavePatientCardResult { Success = false, Message = "Card setup was not completed. Please try again." };

            var paymentMethodId = intent.PaymentMethodId;
            if (string.IsNullOrWhiteSpace(paymentMethodId))
                return new SavePatientCardResult { Success = false, Message = "No payment method was returned by Stripe." };

            var paymentMethodService = new PaymentMethodService(_stripeClient);
            var pm = await paymentMethodService.GetAsync(paymentMethodId, null, requestOptions).ConfigureAwait(false);

            var customerId = intent.CustomerId;
            if (string.IsNullOrWhiteSpace(customerId) && !string.IsNullOrWhiteSpace(pm?.CustomerId))
                customerId = pm.CustomerId;

            if (!string.IsNullOrWhiteSpace(customerId) && string.IsNullOrWhiteSpace(pm?.CustomerId))
            {
                try
                {
                    await paymentMethodService.AttachAsync(paymentMethodId,
                        new PaymentMethodAttachOptions { Customer = customerId }, requestOptions).ConfigureAwait(false);
                }
                catch (StripeException) { }
                pm = await paymentMethodService.GetAsync(paymentMethodId, null, requestOptions).ConfigureAwait(false);
            }

            var isFirstCard = !await _db.SYS_UserCards
                .AnyAsync(c => c.UserId == userId && c.IsDefault && (c.IsActive == true || c.IsActive == null))
                .ConfigureAwait(false);

            var existing = await _db.SYS_UserCards
                .FirstOrDefaultAsync(c => c.UserId == userId
                    && c.StripeAccountId == stripeAccountId
                    && c.StripePaymentMethodId == paymentMethodId)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.StripeCustomerId = customerId;
                existing.StripeAccountId = stripeAccountId;
                existing.Last4 = pm?.Card?.Last4;
                existing.CardBrand = pm?.Card?.Brand;
                existing.ExpirationMonth = pm?.Card != null ? pm.Card.ExpMonth.ToString("D2") : null;
                existing.ExpirationYear = pm?.Card != null ? pm.Card.ExpYear.ToString() : null;
                existing.CardHolderName = pm?.BillingDetails?.Name;
                existing.Currency = "usd";
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.SYS_UserCards.Add(new SYS_UserCard
                {
                    UserId = userId,
                    StripePaymentMethodId = paymentMethodId,
                    StripeCustomerId = customerId,
                    StripeAccountId = stripeAccountId,
                    Last4 = pm?.Card?.Last4,
                    CardBrand = pm?.Card?.Brand,
                    ExpirationMonth = pm?.Card != null ? pm.Card.ExpMonth.ToString("D2") : null,
                    ExpirationYear = pm?.Card != null ? pm.Card.ExpYear.ToString() : null,
                    CardHolderName = pm?.BillingDetails?.Name,
                    Currency = "usd",
                    IsActive = true,
                    IsDefault = isFirstCard,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync().ConfigureAwait(false);

            if (isFirstCard && existing == null && !string.IsNullOrWhiteSpace(customerId))
            {
                try
                {
                    await SetConnectedCustomerDefaultPaymentMethodAsync(stripeAccountId, customerId, paymentMethodId)
                        .ConfigureAwait(false);
                }
                catch { }
            }

            return new SavePatientCardResult { Success = true, Message = "Card saved successfully." };
        }

        public async Task DetachConnectedPaymentMethodAsync(string stripeAccountId, string paymentMethodId)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var paymentMethodService = new PaymentMethodService(_stripeClient);
            await paymentMethodService.DetachAsync(paymentMethodId, null, requestOptions).ConfigureAwait(false);
        }

        public async Task SetConnectedCustomerDefaultPaymentMethodAsync(
            string stripeAccountId,
            string customerId,
            string paymentMethodId)
        {
            var requestOptions = new RequestOptions { StripeAccount = stripeAccountId };
            var customerService = new CustomerService(_stripeClient);
            await customerService.UpdateAsync(customerId, new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = paymentMethodId
                }
            }, requestOptions).ConfigureAwait(false);
        }

        private void AddStripeAuth(HttpRequestMessage request)
        {
            var key = _configuration["Stripe:SecretKey"]?.Trim();
            if (string.IsNullOrEmpty(key) || key.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new InvalidOperationException(
                    "Stripe:SecretKey is not set or invalid. Add your Stripe secret key (sk_...) in appsettings.json under Stripe:SecretKey.");
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            request.Headers.TryAddWithoutValidation("Stripe-Version", "2025-03-31.preview");
        }

        private void AddStripeAuthV1Only(HttpRequestMessage request)
        {
            var key = _configuration["Stripe:SecretKey"]?.Trim();
            if (string.IsNullOrEmpty(key) || key.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new InvalidOperationException(
                    "Stripe:SecretKey is not set or invalid. Add your Stripe secret key (sk_...) in appsettings.json under Stripe:SecretKey.");
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
    }
}
