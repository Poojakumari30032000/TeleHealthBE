using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.Coupons;
using Vitality.Models.Enums;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Services.Stripe;
using DudeMeds.Models.Repos.Interfaces;

namespace Vitality.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StripeConnectController : ControllerBase
    {
        private readonly IStripeConnectService _stripeConnect;
        private readonly MainContext _db;
        private readonly IConfiguration _configuration;
        private readonly Stripe.StripeClient _stripeClient;
        private readonly ICouponRepo _couponRepo;
        private readonly IOnboardingCompletePlatformCustomerService? _onboardingCompletePlatformCustomer;

        public StripeConnectController(
            IStripeConnectService stripeConnect,
            MainContext db,
            IConfiguration configuration,
            Stripe.StripeClient stripeClient,
            ICouponRepo couponRepo,
            IOnboardingCompletePlatformCustomerService? onboardingCompletePlatformCustomer = null)
        {
            _stripeConnect = stripeConnect;
            _db = db;
            _configuration = configuration;
            _stripeClient = stripeClient;
            _couponRepo = couponRepo;
            _onboardingCompletePlatformCustomer = onboardingCompletePlatformCustomer;
        }

        private async Task<long?> GetFacilityIdAsync()
        {
            var facilityIdClaim = User.FindFirst("FacilityId");
            if (facilityIdClaim != null && long.TryParse(facilityIdClaim.Value, out var fid))
                return fid;
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ?? User.FindFirst("UserId");
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
                return null;
            return await _db.FC_UsersInFacilities
                .Where(uf => uf.UserId == userId && uf.IsAssign == true)
                .Select(uf => uf.FacilityId)
                .FirstOrDefaultAsync();
        }

        [HttpPost("account/create")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeAccountResult>> CreateAccount([FromBody] CreateStripeAccountRequest request)
        {
            var response = new ApiResponse<StripeAccountResult>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue || facilityId.Value <= 0)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var displayName = request?.DisplayName?.Trim() ?? "";
            var contactEmail = request?.ContactEmail?.Trim() ?? "";
            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(contactEmail))
            {
                response.Status = 0;
                response.Message = "DisplayName and ContactEmail are required.";
                response.Data = null;
                return response;
            }
            try
            {
                var result = await _stripeConnect.CreateConnectedAccountAsync(displayName, contactEmail, facilityId.Value);
                response.Status = result.Success ? 1 : 0;
                response.Message = result.Success ? "Account created." : (result.Error ?? "Failed.");
                response.Data = result;
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeAccountResult { Success = false, Error = ex.Message };
                return response;
            }
        }

        private static bool IsStripeConfigError(InvalidOperationException ex)
        {
            var msg = ex.Message ?? "";
            return msg.Contains("SecretKey", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("not set", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("not configured", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("PlatformPriceId", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet("account/onboard-link")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeOnboardLinkResponse>> GetOnboardLink()
        {
            var response = new ApiResponse<StripeOnboardLinkResponse>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account found for this facility. Create an account first.";
                response.Data = null;
                return response;
            }
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var frontendBase = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
            var refreshUrl = $"{frontendBase}/integrate-getting-started?stripe=refresh";
            var returnUrl = $"{frontendBase}/integrate-getting-started?stripe=return&accountId={accountId}";
            try
            {
                var url = await _stripeConnect.CreateAccountLinkAsync(accountId, refreshUrl, returnUrl);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeOnboardLinkResponse { Url = url };
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpGet("account/account-session")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeAccountSessionResponse>> GetAccountSession()
        {
            var response = new ApiResponse<StripeAccountSessionResponse>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account found for this facility. Create an account first.";
                response.Data = null;
                return response;
            }
            try
            {
                var clientSecret = await _stripeConnect.CreateAccountSessionAsync(accountId);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeAccountSessionResponse { ClientSecret = clientSecret };
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpGet("account/account-session-facility-payments")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeAccountSessionResponse>> GetAccountSessionForFacilityPayments([FromQuery] bool readOnly = true)
        {
            var response = new ApiResponse<StripeAccountSessionResponse>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account found for this facility.";
                response.Data = null;
                return response;
            }
            try
            {
                var clientSecret = await _stripeConnect.CreateAccountSessionForFacilityPaymentsAsync(accountId, readOnly);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeAccountSessionResponse { ClientSecret = clientSecret, StripeAccountId = accountId, Component = "payments" };
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpGet("account/account-session-connected")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<StripeAccountSessionResponse>> GetAccountSessionForConnectedFacility(
            [FromQuery] long facilityId,
            [FromQuery] string component)
        {
            var response = new ApiResponse<StripeAccountSessionResponse>();
            if (facilityId <= 0)
            {
                response.Status = 0;
                response.Message = "facilityId is required.";
                response.Data = null;
                return response;
            }
            if (string.IsNullOrWhiteSpace(component))
            {
                response.Status = 0;
                response.Message = "component is required (payments, payment-details, payouts-list, or disputes-list).";
                response.Data = null;
                return response;
            }
            try
            {
                var clientSecret = await _stripeConnect.CreateAccountSessionForConnectedFacilityAsync(facilityId, component);
                response.Status = 1;
                response.Message = "OK";
                var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId);
                response.Data = new StripeAccountSessionResponse { ClientSecret = clientSecret, StripeAccountId = accountId, Component = component?.Trim() };
                return response;
            }
            catch (InvalidOperationException ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (ArgumentException ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpGet("account/account-session-platform")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<StripeAccountSessionResponse>> GetAccountSessionForPlatform([FromQuery] string component)
        {
            var response = new ApiResponse<StripeAccountSessionResponse>();
            if (string.IsNullOrWhiteSpace(component))
            {
                response.Status = 0;
                response.Message = "component is required (payments, payment-details, payouts-list, or disputes-list).";
                response.Data = null;
                return response;
            }
            try
            {
                var clientSecret = await _stripeConnect.CreateAccountSessionForPlatformAsync(component);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeAccountSessionResponse { ClientSecret = clientSecret, Component = component?.Trim() };
                return response;
            }
            catch (ArgumentException ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpGet("account/status")]
        [AllowAnonymous]

        public async Task<ApiResponse<StripeAccountStatusResult>> GetAccountStatus(long? patientFacilityId)
        {
            var response = new ApiResponse<StripeAccountStatusResult>();
            var facilityId = await GetFacilityIdAsync() ?? patientFacilityId;
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 1;
                response.Message = "No Stripe account.";
                response.Data = new StripeAccountStatusResult();
                return response;
            }
            try
            {
                var status = await _stripeConnect.GetAccountStatusAsync(accountId);
                response.Status = 1;
                response.Message = "OK";
                response.Data = status;
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeAccountStatusResult { Error = ex.Message };
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeAccountStatusResult { Error = ex.Message };
                return response;
            }
        }

        [HttpPost("onboarding-complete")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<bool>> OnboardingComplete()
        {
            var response = new ApiResponse<bool>();
            if (_onboardingCompletePlatformCustomer == null)
            {
                response.Data = false;
                response.Message = "Service not configured.";
                return response;
            }
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Data = false;
                response.Message = "Facility not found for current user.";
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Data = false;
                response.Message = "No Stripe Connect account for this facility.";
                return response;
            }
            try
            {
                var created = await _onboardingCompletePlatformCustomer.EnsurePlatformCustomerForConnectedAccountAsync(accountId, HttpContext.RequestAborted);
                response.Data = created;
                response.Message = created ? "Platform customer set for GA billing. Add a payment method next to pay invoices." : "Could not set platform customer (e.g. no clinic admin).";
            }
            catch (Exception ex)
            {
                response.Data = false;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost("create-ga-billing-setup-session")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeCheckoutResponse>> CreateGaBillingSetupSession([FromBody] CreateGaBillingSetupSessionRequest request)
        {
            var response = new ApiResponse<StripeCheckoutResponse>();
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                response.Status = 0;
                response.Message = "User not found.";
                response.Data = null;
                return response;
            }
            var user = await _db.SYS_UserDetails.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.StripePlatformCustomerId })
                .FirstOrDefaultAsync();
            if (user == null || string.IsNullOrWhiteSpace(user.StripePlatformCustomerId))
            {
                response.Status = 0;
                response.Message = "Complete Stripe Connect onboarding first so we can create your clinic billing profile. Then you can add a payment method here.";
                response.Data = null;
                return response;
            }
            var successUrl = request?.SuccessUrl?.Trim();
            var cancelUrl = request?.CancelUrl?.Trim();
            if (string.IsNullOrEmpty(successUrl) || string.IsNullOrEmpty(cancelUrl))
            {
                response.Status = 0;
                response.Message = "SuccessUrl and CancelUrl are required.";
                response.Data = null;
                return response;
            }
            try
            {
                var url = await _stripeConnect.CreatePlatformSetupSessionAsync(user.StripePlatformCustomerId, successUrl, cancelUrl);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeCheckoutResponse { CheckoutUrl = url };
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
            }
            return response;
        }

        [HttpGet("ga-billing-status")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<GaBillingStatusResponse>> GetGaBillingStatus()
        {
            var response = new ApiResponse<GaBillingStatusResponse>();
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                response.Data = new GaBillingStatusResponse { HasPlatformCustomer = false, HasPaymentMethod = false };
                return response;
            }
            var user = await _db.SYS_UserDetails.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.StripePlatformCustomerId })
                .FirstOrDefaultAsync();
            if (user == null || string.IsNullOrWhiteSpace(user.StripePlatformCustomerId))
            {
                response.Data = new GaBillingStatusResponse { HasPlatformCustomer = false, HasPaymentMethod = false };
                return response;
            }
            bool hasPaymentMethod = false;
            try
            {
                var customerService = new Stripe.CustomerService(_stripeClient);
                var customer = await customerService.GetAsync(user.StripePlatformCustomerId, null, null, HttpContext.RequestAborted);
                var defaultPm = customer.InvoiceSettings?.DefaultPaymentMethodId;
                if (!string.IsNullOrWhiteSpace(defaultPm))
                {
                    hasPaymentMethod = true;
                }
                else
                {
                    var pmService = new Stripe.PaymentMethodService(_stripeClient);
                    var listOptions = new Stripe.PaymentMethodListOptions { Customer = user.StripePlatformCustomerId, Type = "card" };
                    var list = await pmService.ListAsync(listOptions, null, HttpContext.RequestAborted);
                    hasPaymentMethod = list?.Data?.Count > 0;
                }
            }
            catch
            {

            }
            response.Data = new GaBillingStatusResponse { HasPlatformCustomer = true, HasPaymentMethod = hasPaymentMethod };
            return response;
        }

        [HttpPost("products")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeProductResult>> CreateProduct([FromBody] CreateStripeProductRequest request)
        {
            var response = new ApiResponse<StripeProductResult>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                response.Data = null;
                return response;
            }
            var name = request?.Name?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                response.Status = 0;
                response.Message = "Name is required.";
                response.Data = null;
                return response;
            }
            var priceCents = request?.PriceInCents ?? 0;
            if (priceCents <= 0) priceCents = 100;
            try
            {
                var result = await _stripeConnect.CreateProductAsync(accountId, name, request?.Description ?? "", priceCents, request?.Currency ?? "usd");
                response.Status = result.Success ? 1 : 0;
                response.Message = result.Success ? "Product created." : (result.Error ?? "Failed.");
                response.Data = result;
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeProductResult { Success = false, Error = ex.Message };
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeProductResult { Success = false, Error = ex.Message };
                return response;
            }
        }

        [HttpGet("products")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeProductListResult>> ListProducts()
        {
            var response = new ApiResponse<StripeProductListResult>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 1;
                response.Message = "No Stripe account.";
                response.Data = new StripeProductListResult { Success = true, Products = new List<StripeProductItem>() };
                return response;
            }
            try
            {
                var result = await _stripeConnect.ListProductsAsync(accountId);
                response.Status = result.Success ? 1 : 0;
                response.Message = result.Success ? "OK" : (result.Error ?? "Failed.");
                response.Data = result;
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeProductListResult { Success = false, Error = ex.Message };
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeProductListResult { Success = false, Error = ex.Message };
                return response;
            }
        }

        [HttpGet("storefront/products/{accountId}")]
        [AllowAnonymous]
        public async Task<ApiResponse<StripeProductListResult>> ListStorefrontProducts(string accountId)
        {
            var response = new ApiResponse<StripeProductListResult>();
            if (string.IsNullOrWhiteSpace(accountId))
            {
                response.Status = 0;
                response.Message = "Account ID is required.";
                response.Data = null;
                return response;
            }
            try
            {
                var result = await _stripeConnect.ListProductsAsync(accountId.Trim(), 20);
                response.Status = result.Success ? 1 : 0;
                response.Message = result.Success ? "OK" : (result.Error ?? "Failed.");
                response.Data = result;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = new StripeProductListResult { Success = false, Error = ex.Message };
                return response;
            }
        }

        [HttpPost("storefront/checkout")]
        [AllowAnonymous]
        public async Task<ApiResponse<StripeCheckoutResponse>> CreateStorefrontCheckout([FromBody] StorefrontCheckoutRequest request)
        {
            var response = new ApiResponse<StripeCheckoutResponse>();

            var accountId = request?.StripeAccountId?.Trim();
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "StripeAccountId is required.";
                response.Data = null;
                return response;
            }
            var priceId = request?.PriceId?.Trim();
            if (string.IsNullOrEmpty(priceId))
            {
                response.Status = 0;
                response.Message = "PriceId is required.";
                response.Data = null;
                return response;
            }
            var quantity = request?.Quantity ?? 1;
            if (quantity < 1) quantity = 1;
            var appFeeCents = request?.ApplicationFeeAmountCents ?? 123;
            var frontendBase = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
            var successUrl = $"{frontendBase}/storefront/success?session_id={{CHECKOUT_SESSION_ID}}";
            var cancelUrl = request?.CancelUrl?.Trim();
            if (string.IsNullOrEmpty(cancelUrl))
                cancelUrl = $"{frontendBase}/storefront/{accountId}";
            try
            {
                var url = await _stripeConnect.CreateCheckoutSessionAsync(accountId, priceId, quantity, appFeeCents, successUrl, cancelUrl);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeCheckoutResponse { CheckoutUrl = url };
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpPost("payment-intent")]
        [AllowAnonymous]
        public async Task<ApiResponse<PaymentIntentResponse>> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
        {
            var response = new ApiResponse<PaymentIntentResponse>();
            var facilityId = request?.FacilityId ?? 0;
            if (facilityId <= 0)
            {
                response.Status = 0;
                response.Message = "FacilityId is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                response.Data = null;
                return response;
            }

            var currency = (request?.Currency ?? "usd").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(currency)) currency = "usd";

            string? customerName = null;
            string? customerEmail = null;
            if (request?.PatientId.HasValue == true && request.PatientId.Value > 0)
            {
                var patient = await _db.PT_Patients.AsNoTracking()
                    .Where(x => x.PatientId == request.PatientId.Value)
                    .Select(x => new { x.FirstName, x.LastName, x.Email })
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);
                if (patient != null)
                {
                    customerName = string.Join(" ", new[] { patient.FirstName, patient.LastName }
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!.Trim()));
                    customerEmail = string.IsNullOrWhiteSpace(patient.Email) ? null : patient.Email.Trim();
                }
            }

            decimal bundlePriceFromDb = 0m;
            var hasDbBundlePrice = false;
            if (request.ProductId.HasValue && request.ProductId.Value > 0)
            {
                var facilityPrice = await _db.PD_FacilityBundlePrices
                    .AsNoTracking()
                    .Where(fbp => fbp.FacilityId == facilityId && fbp.BundleId == request.ProductId.Value)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);
                if (facilityPrice != null)
                {
                    bundlePriceFromDb = facilityPrice.ClinicPrice;
                    hasDbBundlePrice = true;
                }
                else
                {
                    var bundle = await _db.PD_Bundles
                        .AsNoTracking()
                        .Where(b => b.BundleId == request.ProductId.Value)
                        .Select(b => new { b.Price })
                        .FirstOrDefaultAsync(HttpContext.RequestAborted);
                    if (bundle?.Price != null)
                    {
                        bundlePriceFromDb = bundle.Price.Value;
                        hasDbBundlePrice = true;
                    }
                }
            }

            var bundlePrice = hasDbBundlePrice ? bundlePriceFromDb : (request is { Amount: > 0 } ? request.Amount / 100m : 0m);
            long amountCents = (long)Math.Round(Math.Max(0m, bundlePrice) * 100, MidpointRounding.AwayFromZero);

            if (!string.IsNullOrWhiteSpace(request?.CouponCode))
            {
                if (!request.ProductId.HasValue || request.ProductId.Value <= 0 ||
                    !request.PatientId.HasValue || request.PatientId.Value <= 0)
                {
                    response.Status = 0;
                    response.Message = "Coupon code requires ProductId and PatientId so the discounted amount can be calculated before creating a payment.";
                    response.Data = null;
                    return response;
                }

                if (!hasDbBundlePrice)
                {
                    response.Status = 0;
                    response.Message = "Bundle price must exist in this facility/product catalog before a coupon can be applied (prevents charging full price before discount).";
                    response.Data = null;
                    return response;
                }

                var validateRequest = new ValidateCouponRequestDTO
                {
                    FacilityId = facilityId,
                    BundleId = request.ProductId.Value,
                    BundlePrice = bundlePriceFromDb,
                    CouponCode = request.CouponCode.Trim(),
                    PatientId = request.PatientId
                };
                var couponValidation = await _couponRepo.ValidateCouponAsync(validateRequest, HttpContext.RequestAborted);
                if (!couponValidation.IsValid || !couponValidation.DiscountedPrice.HasValue)
                {
                    response.Status = 0;
                    response.Message = couponValidation.Message ?? "Invalid or already used coupon code. Each coupon can only be used once per patient.";
                    response.Data = null;
                    return response;
                }

                var discounted = Math.Max(0m, Math.Round(couponValidation.DiscountedPrice.Value, 2, MidpointRounding.AwayFromZero));
                amountCents = (long)Math.Round(discounted * 100, MidpointRounding.AwayFromZero);

                if (amountCents <= 0)
                {
                    try
                    {
                        var setupSecret = await _stripeConnect.CreateSetupIntentAsync(
                            accountId,
                            currency,
                            request?.PatientId?.ToString(),
                            customerName,
                            customerEmail);
                        response.Status = 1;
                        response.Message = "OK";
                        response.Data = new PaymentIntentResponse
                        {
                            ClientSecret = setupSecret,
                            StripeAccountId = accountId,
                            StripeIntentKind = "setup",
                        };
                        return response;
                    }
                    catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
                    {
                        response.Status = 0;
                        response.Message = ex.Message;
                        response.Data = null;
                        return response;
                    }
                    catch (Exception ex)
                    {
                        response.Status = 0;
                        response.Message = ex.Message;
                        response.Data = null;
                        return response;
                    }
                }
            }
            else
            {

                if (amountCents <= 0 && request is { Amount: > 0 })
                    amountCents = request.Amount;

                if (amountCents <= 0)
                {
                    if (hasDbBundlePrice && bundlePriceFromDb <= 0m)
                    {
                        try
                        {
                            var setupSecret = await _stripeConnect.CreateSetupIntentAsync(
                                accountId,
                                currency,
                                request?.PatientId?.ToString(),
                                customerName,
                                customerEmail);
                            response.Status = 1;
                            response.Message = "OK";
                            response.Data = new PaymentIntentResponse
                            {
                                ClientSecret = setupSecret,
                                StripeAccountId = accountId,
                                StripeIntentKind = "setup",
                            };
                            return response;
                        }
                        catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
                        {
                            response.Status = 0;
                            response.Message = ex.Message;
                            response.Data = null;
                            return response;
                        }
                        catch (Exception ex)
                        {
                            response.Status = 0;
                            response.Message = ex.Message;
                            response.Data = null;
                            return response;
                        }
                    }

                    response.Status = 0;
                    response.Message = "Amount is required when bundle catalog price is unavailable, or pass ProductId so the bundle price can be resolved.";
                    response.Data = null;
                    return response;
                }
            }

            var appFeeCents = request?.ApplicationFeeAmountCents ?? Math.Max(1, (int)(amountCents * 0.10));
            try
            {
                var clientSecret = await _stripeConnect.CreatePaymentIntentAsync(
                    accountId,
                    amountCents,
                    currency,
                    appFeeCents,
                    request?.OrderId?.ToString(),
                    request?.PatientId?.ToString(),
                    customerName,
                    customerEmail);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new PaymentIntentResponse
                {
                    ClientSecret = clientSecret,
                    StripeAccountId = accountId,
                    StripeIntentKind = "payment",
                };
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpPost("patient/setup-intent")]
        [AllowAnonymous]
        public async Task<ApiResponse<PaymentIntentResponse>> CreatePatientSetupIntent([FromBody] PatientSetupIntentRequest request)
        {
            var response = new ApiResponse<PaymentIntentResponse>();
            var facilityId = request?.FacilityId ?? 0;
            var patientId = request?.PatientId ?? 0;
            if (facilityId <= 0 || patientId <= 0)
            {
                response.Status = 0;
                response.Message = "FacilityId and PatientId are required.";
                return response;
            }

            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                return response;
            }

            var (customerName, customerEmail) = await GetPatientNameEmailAsync(patientId);

            try
            {
                var clientSecret = await _stripeConnect.CreateSetupIntentAsync(
                    accountId, "usd", patientId.ToString(), customerName, customerEmail);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new PaymentIntentResponse
                {
                    ClientSecret = clientSecret,
                    StripeAccountId = accountId,
                    StripeIntentKind = "setup",
                };
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpPost("patient/payment-methods/save")]
        [AllowAnonymous]
        public async Task<ApiResponse<bool>> SavePatientPaymentMethod([FromBody] SavePatientPaymentMethodRequest request)
        {
            var response = new ApiResponse<bool>();
            if (request == null || request.FacilityId <= 0 || request.UserId <= 0 || string.IsNullOrWhiteSpace(request.SetupIntentId))
            {
                response.Status = 0;
                response.Message = "FacilityId, UserId and SetupIntentId are required.";
                return response;
            }

            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(request.FacilityId);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                return response;
            }

            try
            {
                var result = await _stripeConnect.SavePatientStripeCardAsync(accountId, request.SetupIntentId.Trim(), request.UserId);
                response.Status = result.Success ? 1 : 0;
                response.Message = result.Message;
                response.Data = result.Success;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet("patient/payment-methods")]
        [AllowAnonymous]
        public async Task<ApiResponse<List<PatientPaymentMethodDto>>> GetPatientPaymentMethods(
            [FromQuery] long facilityId, [FromQuery] long userId)
        {
            var response = new ApiResponse<List<PatientPaymentMethodDto>>
            {
                Data = new List<PatientPaymentMethodDto>()
            };
            if (facilityId <= 0 || userId <= 0)
            {
                response.Status = 0;
                response.Message = "FacilityId and UserId are required.";
                return response;
            }

            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                return response;
            }

            try
            {
                var cards = await _db.SYS_UserCards.AsNoTracking()
                    .Where(c => c.UserId == userId
                        && c.StripeAccountId == accountId
                        && c.StripePaymentMethodId != null
                        && (c.IsActive == true || c.IsActive == null))
                    .OrderByDescending(c => c.IsDefault)
                    .ThenBy(c => c.CardId)
                    .Select(c => new PatientPaymentMethodDto
                    {
                        CardId = c.CardId,
                        PaymentMethodId = c.StripePaymentMethodId,
                        CardBrand = c.CardBrand,
                        Last4 = c.Last4,
                        ExpirationMonth = c.ExpirationMonth,
                        ExpirationYear = c.ExpirationYear,
                        CardHolderName = c.CardHolderName,
                        IsDefault = c.IsDefault,
                    })
                    .ToListAsync(HttpContext.RequestAborted);

                response.Status = 1;
                response.Message = "OK";
                response.Data = cards;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpPost("patient/payment-methods/detach")]
        [AllowAnonymous]
        public async Task<ApiResponse<bool>> DetachPatientPaymentMethod([FromBody] DetachPaymentMethodRequest request)
        {
            var response = new ApiResponse<bool>();
            if (request == null || request.FacilityId <= 0 || request.UserId <= 0 || request.CardId <= 0)
            {
                response.Status = 0;
                response.Message = "FacilityId, UserId and CardId are required.";
                return response;
            }

            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(request.FacilityId);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                return response;
            }

            var card = await _db.SYS_UserCards
                .FirstOrDefaultAsync(c => c.CardId == request.CardId && c.UserId == request.UserId, HttpContext.RequestAborted);
            if (card == null || string.IsNullOrWhiteSpace(card.StripePaymentMethodId))
            {
                response.Status = 0;
                response.Message = "Card not found.";
                return response;
            }
            if (card.IsDefault)
            {
                response.Status = 0;
                response.Message = "Default card cannot be removed. Set another card as default first.";
                return response;
            }

            try
            {
                try
                {
                    await _stripeConnect.DetachConnectedPaymentMethodAsync(accountId, card.StripePaymentMethodId);
                }
                catch
                {

                }

                card.IsActive = false;
                card.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(HttpContext.RequestAborted);

                response.Status = 1;
                response.Message = "Card removed successfully.";
                response.Data = true;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpPost("patient/payment-methods/default")]
        [AllowAnonymous]
        public async Task<ApiResponse<bool>> SetPatientDefaultPaymentMethod([FromBody] SetDefaultPaymentMethodRequest request)
        {
            var response = new ApiResponse<bool>();
            if (request == null || request.FacilityId <= 0 || request.UserId <= 0 || request.CardId <= 0)
            {
                response.Status = 0;
                response.Message = "FacilityId, UserId and CardId are required.";
                return response;
            }

            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(request.FacilityId);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                return response;
            }

            var cards = await _db.SYS_UserCards
                .Where(c => c.UserId == request.UserId
                    && c.StripeAccountId == accountId
                    && c.StripePaymentMethodId != null
                    && (c.IsActive == true || c.IsActive == null))
                .ToListAsync(HttpContext.RequestAborted);

            var target = cards.FirstOrDefault(c => c.CardId == request.CardId);
            if (target == null)
            {
                response.Status = 0;
                response.Message = "Card not found.";
                return response;
            }

            try
            {
                foreach (var c in cards)
                    c.IsDefault = c.CardId == request.CardId;
                target.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(HttpContext.RequestAborted);

                if (!string.IsNullOrWhiteSpace(target.StripeCustomerId))
                {
                    try
                    {
                        await _stripeConnect.SetConnectedCustomerDefaultPaymentMethodAsync(
                            accountId, target.StripeCustomerId, target.StripePaymentMethodId!);
                    }
                    catch
                    {

                    }
                }

                response.Status = 1;
                response.Message = "Default card updated successfully.";
                response.Data = true;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                return response;
            }
        }

        private async Task<(string? Name, string? Email)> GetPatientNameEmailAsync(long patientId)
        {
            var patient = await _db.PT_Patients.AsNoTracking()
                .Where(x => x.PatientId == patientId)
                .Select(x => new { x.FirstName, x.LastName, x.Email })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            if (patient == null) return (null, null);
            var name = string.Join(" ", new[] { patient.FirstName, patient.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
            var email = string.IsNullOrWhiteSpace(patient.Email) ? null : patient.Email.Trim();
            return (string.IsNullOrWhiteSpace(name) ? null : name, email);
        }

        [HttpPost("subscription/checkout")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeCheckoutResponse>> CreateSubscriptionCheckout([FromBody] SubscriptionCheckoutRequest request)
        {
            var response = new ApiResponse<StripeCheckoutResponse>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                response.Data = null;
                return response;
            }

            var priceId = _configuration["Stripe:PlatformPriceId"]?.Trim();
            if (string.IsNullOrEmpty(priceId))
            {
                response.Status = 0;
                response.Message = "Stripe:PlatformPriceId is not configured. Add a subscription price ID in appsettings.";
                response.Data = null;
                return response;
            }
            var frontendBase = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
            var successUrl = $"{frontendBase}/dashboard?stripe_sub=success&session_id={{CHECKOUT_SESSION_ID}}";
            var cancelUrl = $"{frontendBase}/integrate-getting-started";
            try
            {
                var url = await _stripeConnect.CreateSubscriptionCheckoutSessionAsync(accountId, priceId, successUrl, cancelUrl);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeCheckoutResponse { CheckoutUrl = url };
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }

        [HttpGet("subscription/portal")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<StripeCheckoutResponse>> CreateBillingPortal()
        {
            var response = new ApiResponse<StripeCheckoutResponse>();
            var facilityId = await GetFacilityIdAsync();
            if (!facilityId.HasValue)
            {
                response.Status = 0;
                response.Message = "Facility is required.";
                response.Data = null;
                return response;
            }
            var accountId = await _stripeConnect.GetStripeAccountIdForFacilityAsync(facilityId.Value);
            if (string.IsNullOrEmpty(accountId))
            {
                response.Status = 0;
                response.Message = "No Stripe account for this facility.";
                response.Data = null;
                return response;
            }
            var returnUrl = _configuration["Stripe:DashboardReturnUrl"] ?? _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200/dashboard";
            try
            {
                var url = await _stripeConnect.CreateBillingPortalSessionAsync(accountId, returnUrl);
                response.Status = 1;
                response.Message = "OK";
                response.Data = new StripeCheckoutResponse { CheckoutUrl = url };
                return response;
            }
            catch (InvalidOperationException ex) when (IsStripeConfigError(ex))
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
                return response;
            }
        }
    }

    public class CreateStripeAccountRequest
    {
        public string? DisplayName { get; set; }
        public string? ContactEmail { get; set; }
    }

    public class StripeOnboardLinkResponse
    {
        public string? Url { get; set; }
    }

    public class StripeAccountSessionResponse
    {
        public string? ClientSecret { get; set; }
        public string? StripeAccountId { get; set; }
        public string? Component { get; set; }
    }

    public class CreateStripeProductRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public long PriceInCents { get; set; }
        public string? Currency { get; set; }
    }

    public class StorefrontCheckoutRequest
    {
        public string? StripeAccountId { get; set; }
        public string? PriceId { get; set; }
        public int Quantity { get; set; }
        public int ApplicationFeeAmountCents { get; set; }
        public string? CancelUrl { get; set; }
    }

    public class SubscriptionCheckoutRequest { }

    public class CreateGaBillingSetupSessionRequest
    {
        public string? SuccessUrl { get; set; }
        public string? CancelUrl { get; set; }
    }

    public class StripeCheckoutResponse
    {
        public string? CheckoutUrl { get; set; }
    }

    public class GaBillingStatusResponse
    {
        public bool HasPlatformCustomer { get; set; }
        public bool HasPaymentMethod { get; set; }
    }

    public class CreatePaymentIntentRequest
    {
        public long FacilityId { get; set; }

        public long Amount { get; set; }
        public string? Currency { get; set; }
        public int ApplicationFeeAmountCents { get; set; }
        public long? OrderId { get; set; }
        public long? PatientId { get; set; }

        public long? ProductId { get; set; }

        public string? CouponCode { get; set; }
    }

    public class PaymentIntentResponse
    {
        public string? ClientSecret { get; set; }
        public string? StripeAccountId { get; set; }

        public string StripeIntentKind { get; set; } = "payment";
    }

    public class PatientSetupIntentRequest
    {
        public long FacilityId { get; set; }
        public long PatientId { get; set; }
    }

    public class SavePatientPaymentMethodRequest
    {
        public long FacilityId { get; set; }

        public long UserId { get; set; }
        public string? SetupIntentId { get; set; }
    }

    public class PatientPaymentMethodDto
    {
        public long CardId { get; set; }
        public string? PaymentMethodId { get; set; }
        public string? CardBrand { get; set; }
        public string? Last4 { get; set; }
        public string? ExpirationMonth { get; set; }
        public string? ExpirationYear { get; set; }
        public string? CardHolderName { get; set; }
        public bool IsDefault { get; set; }
    }

    public class DetachPaymentMethodRequest
    {
        public long FacilityId { get; set; }
        public long UserId { get; set; }
        public long CardId { get; set; }
    }

    public class SetDefaultPaymentMethodRequest
    {
        public long FacilityId { get; set; }
        public long UserId { get; set; }
        public long CardId { get; set; }
    }
}
