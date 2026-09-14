using AutoMapper;
using DudeMeds.Models.DTOs.Users;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Square;
using Square.Exceptions;
using Square.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Chats;
using Vitality.Models.DTOs.FacilitySquareCred;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.DTOs.PatientProduct;
using Vitality.Models.DTOs.Square;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{
    public class SquarePaymentRepo : BaseRepo, ISquarePaymentRepo
    {
        private readonly SquareClient _squareClient;
        private readonly IFacilitySquareClientProvider _facilityClientProvider;
        private readonly ISquareOAuthService _squareOAuthService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly IUsersRepo _userRepo;
        private readonly IMapper IMapper;
        private readonly ILogger<SquarePaymentRepo> _logger;

        public SquarePaymentRepo(
            SquareClient squareClient,
            IFacilitySquareClientProvider facilityClientProvider,
            ISquareOAuthService squareOAuthService,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            IUsersRepo userRepo,
            IMapper IMapper,
            ILogger<SquarePaymentRepo> logger)
        {
            _squareClient = squareClient;
            _facilityClientProvider = facilityClientProvider;
            _squareOAuthService = squareOAuthService;
            _configuration = configuration;
            _userRepo = userRepo;
            this.IMapper = IMapper;
            _logger = logger;
        }

        private async Task<SquareClient> GetSquareClientAsync(long? facilityId, CancellationToken ct = default)
        {
            if (facilityId.HasValue && facilityId.Value > 0)
            {

                var isConnected = await _squareOAuthService.IsConnectedAsync(facilityId.Value);
                if (!isConnected)
                {
                    _logger.LogError("Facility {FacilityId} does not have Square OAuth connection. Connect via Integrations → Square.", facilityId.Value);
                    throw new InvalidOperationException(
                        "Square is not connected for this facility. Go to Integrations → Square and click Connect with Square.");
                }
                await _squareOAuthService.GetValidAccessTokenAsync(facilityId.Value);
                return await _facilityClientProvider.GetAsync(facilityId.Value, ct);
            }

            return _squareClient;
        }

        private async Task<string?> GetLocationIdAsync(long? facilityId, CancellationToken ct = default)
        {
            if (facilityId.HasValue && facilityId.Value > 0)
            {
                try
                {
                    var squareCreds = await GetSquareAppIdByFacilityIdAsync(facilityId.Value);
                    return squareCreds?.LocationId;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get location ID for facility {FacilityId}", facilityId.Value);
                    return null;
                }
            }

            return _configuration["Square:LocationId"]?.Trim();
        }

        public async Task<CreatePaymentResponse> CreatePaymentAsync(PaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceId))
                throw new ArgumentException("SourceId is required.", nameof(request));

            if (request.Amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.", nameof(request));

            if (string.IsNullOrWhiteSpace(request.Currency))
                request.Currency = "USD";

            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? Guid.NewGuid().ToString()
                : request.IdempotencyKey;

            try
            {
                var squareClient = await GetSquareClientAsync(request.FacilityId);
                var paymentsApi = squareClient.PaymentsApi;

                string? locationId = request.LocationId != null
                    ? request.LocationId.ToString()
                    : await GetLocationIdAsync(request.FacilityId);

                var money = new Money(amount: request.Amount, currency: request.Currency);

                var bodyBuilder = new CreatePaymentRequest.Builder(
                    sourceId: request.SourceId,
                    idempotencyKey: idempotencyKey,
                    amountMoney: money);

                if (!string.IsNullOrWhiteSpace(locationId))
                {
                    bodyBuilder.LocationId(locationId);
                }

                if (!string.IsNullOrWhiteSpace(request.SquareCustomerId))
                {
                    bodyBuilder.CustomerId(request.SquareCustomerId);
                }

                bodyBuilder.Autocomplete(true);

                var body = bodyBuilder.Build();
                var response = await paymentsApi.CreatePaymentAsync(body);

                _logger.LogInformation("Square payment created successfully. PaymentId: {PaymentId}, Amount: {Amount}, Status: {Status}",
                    response.Payment?.Id, request.Amount, response.Payment?.Status);

                return response;
            }
            catch (ApiException e)
            {

                var errorDetails = new StringBuilder();
                errorDetails.Append($"HTTP {e.ResponseCode}: {e.Message}");

                if (e.Errors != null && e.Errors.Count > 0)
                {
                    errorDetails.Append(" | Errors: ");
                    errorDetails.Append(string.Join("; ", e.Errors.Select(err =>
                        $"[{err.Category}] {err.Code}: {err.Detail ?? err.Field ?? "N/A"}")));
                }

                _logger.LogError(e,
                    "Square payment failed. HTTP {ResponseCode}, Amount: {Amount}, SourceId: {SourceId}, FacilityId: {FacilityId}, LocationId: {LocationId}, Error: {Error}",
                    e.ResponseCode, request.Amount, request.SourceId, request.FacilityId, request.LocationId, errorDetails.ToString());

                var errorMessage = $"Square payment failed (HTTP {e.ResponseCode}): {errorDetails}";
                throw new Exception(errorMessage, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating Square payment. Amount: {Amount}, FacilityId: {FacilityId}",
                    request.Amount, request.FacilityId);
                throw;
            }
        }

        public async Task<bool> SavePaymentCards(SaveCardRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceId))
                throw new ArgumentException("SourceId (nonce) is required.", nameof(request));

            if (!request.UserId.HasValue || request.UserId.Value <= 0)
                throw new ArgumentException("UserId is required.", nameof(request));

            try
            {

                var squareClient = await GetSquareClientAsync(request.FacilityId);
                var cardsApi = squareClient.CardsApi;
                var customersApi = squareClient.CustomersApi;

                var userDetails = _userRepo.GetUserByIdLinq((long)request.UserId);
                if (userDetails == null)
                    throw new ArgumentException($"User with ID {request.UserId} not found.");

                string? email = string.IsNullOrWhiteSpace(userDetails.Email) ? null : userDetails.Email.Trim();
                if (email != null && !email.Contains("@"))
                    email = null;

                string? givenName = string.IsNullOrWhiteSpace(userDetails.FirstName) ? null : userDetails.FirstName.Trim();
                string? familyName = string.IsNullOrWhiteSpace(userDetails.LastName) ? null : userDetails.LastName.Trim();
                string? phone = string.IsNullOrWhiteSpace(userDetails.Phone) ? null : userDetails.Phone.Trim();

                string squareCustomerId;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    try
                    {
                        var searchReq = new SearchCustomersRequest.Builder()
                            .Query(
                                new CustomerQuery.Builder()
                                    .Filter(
                                        new CustomerFilter.Builder()
                                            .EmailAddress(
                                                new CustomerTextFilter.Builder()
                                                    .Exact(email)
                                                    .Build()
                                            )
                                            .Build()
                                    )
                                    .Build()
                            )
                            .Build();

                        var searchResult = await customersApi.SearchCustomersAsync(searchReq);
                        var existingCustomer = searchResult.Customers?.FirstOrDefault();

                        if (existingCustomer != null)
                        {
                            squareCustomerId = existingCustomer.Id;
                            _logger.LogInformation("Found existing Square customer {CustomerId} for user {UserId}", squareCustomerId, request.UserId);
                        }
                        else
                        {

                            var createCustomerBuilder = new CreateCustomerRequest.Builder()
                                .IdempotencyKey(Guid.NewGuid().ToString());

                            if (email != null) createCustomerBuilder.EmailAddress(email);
                            if (givenName != null) createCustomerBuilder.GivenName(givenName);
                            if (familyName != null) createCustomerBuilder.FamilyName(familyName);
                            if (phone != null) createCustomerBuilder.PhoneNumber(phone);

                            var createCustomerResp = await customersApi.CreateCustomerAsync(createCustomerBuilder.Build());
                            if (createCustomerResp?.Customer == null)
                                throw new Exception("Square CreateCustomer returned no customer.");

                            squareCustomerId = createCustomerResp.Customer.Id;
                            _logger.LogInformation("Created new Square customer {CustomerId} for user {UserId}", squareCustomerId, request.UserId);
                        }
                    }
                    catch (ApiException ex)
                    {
                        _logger.LogError(ex, "Error searching/creating Square customer for user {UserId}", request.UserId);
                        throw new Exception($"Square customer operation failed: {ex.Message}", ex);
                    }
                }
                else
                {

                    var createCustomerBuilder = new CreateCustomerRequest.Builder()
                        .IdempotencyKey(Guid.NewGuid().ToString());

                    if (givenName != null) createCustomerBuilder.GivenName(givenName);
                    if (familyName != null) createCustomerBuilder.FamilyName(familyName);
                    if (phone != null) createCustomerBuilder.PhoneNumber(phone);

                    var createCustomerResp = await customersApi.CreateCustomerAsync(createCustomerBuilder.Build());
                    if (createCustomerResp?.Customer == null)
                        throw new Exception("Square CreateCustomer returned no customer.");

                    squareCustomerId = createCustomerResp.Customer.Id;
                    _logger.LogInformation("Created new Square customer {CustomerId} (no email) for user {UserId}", squareCustomerId, request.UserId);
                }

                var cardholderName = string.IsNullOrWhiteSpace(request.CardholderName)
                    ? string.Join(" ", new[] { givenName, familyName }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : request.CardholderName.Trim();

                var cardModel = new Card.Builder()
                    .CustomerId(squareCustomerId)
                    .CardholderName(string.IsNullOrWhiteSpace(cardholderName) ? null : cardholderName)
                    .BillingAddress(null)
                    .Build();

                var createCardReq = new CreateCardRequest.Builder(
                        idempotencyKey: Guid.NewGuid().ToString(),
                        sourceId: request.SourceId,
                        card: cardModel)
                    .Build();

                var createCardResp = await cardsApi.CreateCardAsync(createCardReq);
                if (createCardResp?.Card == null)
                    throw new Exception("Square CreateCard returned no card.");

                var card = createCardResp.Card;

                var userCard = new UpdateUserCardCredentialsRequestDTO
                {
                    UserId = (long)request.UserId,
                    SquareCardId = card.Id,
                    ExpirationMonth = card.ExpMonth?.ToString(),
                    ExpirationYear = card.ExpYear?.ToString(),
                    CardHolderName = card.CardholderName,
                    SquareClientId = squareCustomerId,
                    Last4 = card.Last4,
                    CardBrand = card.CardBrand,
                    Currency = request.Currency ?? "USD"
                };

                bool userCredentialsUpdated = _userRepo.UpdateUserCardCredentials(userCard);

                if (userCredentialsUpdated)
                {
                    _logger.LogInformation("Card saved successfully for user {UserId}. CardId: {CardId}, Last4: {Last4}",
                        request.UserId, card.Id, card.Last4);
                }
                else
                {
                    _logger.LogWarning("Card created in Square but failed to save to database for user {UserId}", request.UserId);
                }

                return userCredentialsUpdated;
            }
            catch (ApiException e)
            {
                var errorDetails = e.Errors != null && e.Errors.Count > 0
                    ? string.Join("; ", e.Errors.Select(err => $"{err.Code}: {err.Detail}"))
                    : e.Message;

                _logger.LogError(e, "Square save-card failed for user {UserId}. Error: {Error}", request.UserId, errorDetails);
                throw new Exception($"Square save-card failed: {errorDetails}", e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving card for user {UserId}", request.UserId);
                throw;
            }
        }

        public async Task<CreatePaymentResponse> ChargeCustomerWithSavedCard(string squareCustomerId, string cardId, int amount, string currency = "USD")
        {
            return await ChargeCustomerWithSavedCard(squareCustomerId, cardId, amount, currency, null);
        }

        private void DeactivateExpiredCard(string squareCardId)
        {
            try
            {
                var card = _db.SYS_UserCards
                    .FirstOrDefault(c => c.SquareCardId == squareCardId && c.IsActive == true);

                if (card != null)
                {
                    card.IsActive = false;
                    _db.SaveChanges();
                    _logger.LogInformation("Deactivated expired card {CardId} (SquareCardId: {SquareCardId})", card.CardId, squareCardId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating expired card {SquareCardId}", squareCardId);
                throw;
            }
        }

        private bool IsCardExpiredInDatabase(string squareCardId)
        {
            try
            {
                var card = _db.SYS_UserCards
                    .AsNoTracking()
                    .FirstOrDefault(c => c.SquareCardId == squareCardId && c.IsActive == true);

                if (card == null || string.IsNullOrWhiteSpace(card.ExpirationYear) || string.IsNullOrWhiteSpace(card.ExpirationMonth))
                    return false;

                if (!int.TryParse(card.ExpirationYear, out int expirationYear) ||
                    !int.TryParse(card.ExpirationMonth, out int expirationMonth))
                    return false;

                if (expirationYear < 100)
                    expirationYear += 2000;

                var expirationDate = new DateTime(expirationYear, expirationMonth, DateTime.DaysInMonth(expirationYear, expirationMonth));
                var currentDate = DateTime.Now;

                return expirationDate < currentDate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking card expiration in database for card {CardId}", squareCardId);
                return false;
            }
        }

        public async Task<CreatePaymentResponse> ChargeCustomerWithSavedCard(string squareCustomerId, string cardId, int amount, string currency, long? facilityId)
            => await ChargeCustomerWithSavedCard(squareCustomerId, cardId, amount, currency, facilityId, idempotencyKey: null);

        public async Task<CreatePaymentResponse> ChargeCustomerWithSavedCard(string squareCustomerId, string cardId, int amount, string currency, long? facilityId, string? idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(squareCustomerId))
                throw new ArgumentException("SquareCustomerId is required.", nameof(squareCustomerId));
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId is required.", nameof(cardId));
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.", nameof(amount));

            if (string.IsNullOrWhiteSpace(currency))
                currency = "USD";

            if (IsCardExpiredInDatabase(cardId))
            {
                _logger.LogWarning("Card {CardId} is expired in database. Payment attempt prevented.", cardId);

                try
                {
                    DeactivateExpiredCard(cardId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deactivate expired card {CardId}", cardId);
                }

                throw new Exception("The payment card has expired. Please use a different card or update your card information.");
            }

            try
            {

                var squareClient = await GetSquareClientAsync(facilityId);
                var paymentsApi = squareClient.PaymentsApi;

                string? locationId = await GetLocationIdAsync(facilityId);
                if (string.IsNullOrWhiteSpace(locationId))
                {
                    _logger.LogError("Location ID missing for payment. FacilityId: {FacilityId}. For global admin ensure Square:LocationId is set in appsettings.", facilityId);
                    throw new InvalidOperationException(
                        facilityId.HasValue
                            ? "Square location is not configured for this facility. Reconnect Square integration and try again."
                            : "Square location is not configured. Please set Square:LocationId in configuration and try again.");
                }

                var money = new Money.Builder()
                    .Amount(amount)
                    .Currency(currency)
                    .Build();

                var effectiveIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
                    ? Guid.NewGuid().ToString()
                    : idempotencyKey!;

                var payReqBuilder = new CreatePaymentRequest.Builder(
                        sourceId: cardId,
                        idempotencyKey: effectiveIdempotencyKey,
                        amountMoney: money)
                    .CustomerId(squareCustomerId)
                    .Autocomplete(true);

                if (!string.IsNullOrWhiteSpace(locationId))
                {
                    payReqBuilder.LocationId(locationId);
                }
                else
                {
                    _logger.LogWarning("Location ID is missing for payment. CustomerId: {CustomerId}, CardId: {CardId}, FacilityId: {FacilityId}",
                        squareCustomerId, cardId, facilityId);
                }

                var payReq = payReqBuilder.Build();

                _logger.LogInformation("Attempting Square payment. CustomerId: {CustomerId}, CardId: {CardId}, Amount: {Amount}, Currency: {Currency}, LocationId: {LocationId}, FacilityId: {FacilityId}",
                    squareCustomerId, cardId, amount, currency, locationId ?? "N/A", facilityId);

                var response = await paymentsApi.CreatePaymentAsync(payReq);

                _logger.LogInformation("Square charge completed successfully. PaymentId: {PaymentId}, Amount: {Amount}, Status: {Status}, CustomerId: {CustomerId}",
                    response.Payment?.Id, amount, response.Payment?.Status, squareCustomerId);

                return response;
            }
            catch (ApiException e)
            {

                var errorDetails = new StringBuilder();
                errorDetails.Append($"HTTP {e.ResponseCode}: {e.Message}");

                string? userFriendlyMessage = null;
                bool isCardExpired = false;

                if (e.Errors != null && e.Errors.Count > 0)
                {
                    errorDetails.Append(" | Errors: ");
                    var errorList = e.Errors.Select(err =>
                        $"[{err.Category}] {err.Code}: {err.Detail ?? err.Field ?? "N/A"}").ToList();
                    errorDetails.Append(string.Join("; ", errorList));

                    var firstError = e.Errors.FirstOrDefault();
                    if (firstError != null)
                    {
                        switch (firstError.Code)
                        {
                            case "CARD_EXPIRED":
                                isCardExpired = true;
                                userFriendlyMessage = "The payment card has expired. Please use a different card or update your card information.";
                                break;
                            case "CARD_DECLINED":
                                userFriendlyMessage = "The payment card was declined. Please use a different card or contact your bank.";
                                break;
                            case "INSUFFICIENT_FUNDS":
                                userFriendlyMessage = "Insufficient funds available on the payment card. Please use a different card.";
                                break;
                            case "INVALID_EXPIRATION":
                                userFriendlyMessage = "The card expiration date is invalid. Please update your card information.";
                                break;
                            case "CARD_NOT_SUPPORTED":
                                userFriendlyMessage = "This card type is not supported. Please use a different payment method.";
                                break;
                            case "INVALID_CARD":
                            case "CARD_TOKEN_EXPIRED":
                            case "NOT_FOUND":
                                userFriendlyMessage = "This saved card is no longer valid for the current payment account. Please remove it and add the card again.";
                                break;
                            case "VERIFY_CVV_FAILURE":
                                userFriendlyMessage = "Card verification failed. Please check your card details and try again.";
                                break;
                            case "VERIFY_AVS_FAILURE":
                                userFriendlyMessage = "Address verification failed. Please check your billing address.";
                                break;
                            case "PAYMENT_AMOUNT_MISMATCH":
                                userFriendlyMessage = "Payment amount mismatch. Please try again.";
                                break;
                            case "GENERIC_DECLINE":
                                userFriendlyMessage = "The payment was declined. Please use a different card or contact your bank.";
                                break;
                            case "UNAUTHORIZED":
                                userFriendlyMessage = "Square authorization failed for this facility. Please reconnect Square and try again.";
                                break;
                            default:

                                userFriendlyMessage = $"Payment processing failed: {firstError.Detail ?? firstError.Code}";
                                break;
                        }
                    }
                }

                if (isCardExpired)
                {
                    try
                    {
                        DeactivateExpiredCard(cardId);
                        _logger.LogInformation("Deactivated expired card {CardId} after Square API confirmation", cardId);
                    }
                    catch (Exception deactivateEx)
                    {
                        _logger.LogWarning(deactivateEx, "Failed to deactivate expired card {CardId} after Square API error", cardId);

                    }
                }

                _logger.LogError(e,
                    "Square charge failed. HTTP {ResponseCode}, Amount: {Amount}, CustomerId: {CustomerId}, CardId: {CardId}, FacilityId: {FacilityId}, Error: {Error}",
                    e.ResponseCode, amount, squareCustomerId, cardId, facilityId, errorDetails.ToString());

                var errorMessage = userFriendlyMessage ?? $"Square payment failed (HTTP {e.ResponseCode}): {errorDetails}";
                throw new Exception(errorMessage, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error charging customer. Amount: {Amount}, CustomerId: {CustomerId}, CardId: {CardId}, FacilityId: {FacilityId}",
                    amount, squareCustomerId, cardId, facilityId);
                throw;
            }
        }
        public List<SquareCardDTO> GetCardsByUserId(long userId)
        {
            var userCards = _db.SYS_UserCards
                                .Where(x => x.UserId == userId && x.IsActive == true)
                                .ToList();

            return userCards.Select(card => new SquareCardDTO
            {
                CardId = card.CardId,
                SquareCardId = card.SquareCardId,
                SquareClientId = card.SquareClientId,
                ExpirationYear = card.ExpirationYear,
                ExpirationMonth = card.ExpirationMonth,
                CardHolderName = card.CardHolderName,
                Last4 = card.Last4,
                CardBrand = card.CardBrand,
                IsDefault = card.IsDefault,
                IsActive = card.IsActive
            }).ToList() ?? new List<SquareCardDTO>();
        }
        public List<SquareCardDTO> GetCardsByPatientId(long patientId)
        {

            var loginId = _db.PT_Patients
                .Where(p => p.PatientId == patientId && (p.IsArchieved == null || p.IsArchieved == false))
                .Select(p => p.LoginId)
                .FirstOrDefault();

            if (loginId == null) return new List<SquareCardDTO>();

            var userIds = _db.SYS_UserDetails
                .Where(ud => ud.LoginId == loginId && (ud.IsActive == null || ud.IsActive == true))
                .Select(ud => ud.UserId)
                .ToList();

            if (userIds.Count == 0) return new List<SquareCardDTO>();

            var userCards = _db.SYS_UserCards
                .Where(c => userIds.Contains((long)c.UserId) && c.IsActive == true)
                .OrderByDescending(c => c.IsDefault)
                .ThenByDescending(c => c.CardId)
                .ToList();

            return userCards.Select(card => new SquareCardDTO
            {
                CardId = card.CardId,
                SquareCardId = card.SquareCardId,
                SquareClientId = card.SquareClientId,
                ExpirationYear = card.ExpirationYear,
                ExpirationMonth = card.ExpirationMonth,
                CardHolderName = card.CardHolderName,
                Last4 = card.Last4,
                CardBrand = card.CardBrand,
                IsDefault = card.IsDefault,
                IsActive = card.IsActive
            }).ToList();
        }

        public bool SetDefaultCard(long userId, long cardId)
        {
            try
            {

                var userCards = _db.SYS_UserCards.Where(x => x.UserId == userId).ToList();

                if (!userCards.Any())
                {
                    throw new Exception("No cards found for the user.");
                }

                var defaultCard = userCards.FirstOrDefault(x => x.CardId == cardId);
                if (defaultCard == null)
                {
                    throw new Exception("Card with the specified ID not found.");
                }

                foreach (var card in userCards)
                {
                    card.IsDefault = (card.CardId == cardId);
                }

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {

                throw new Exception("Error setting the default card: " + ex.Message);
            }
        }

        public bool DeleteCard(long cardId)
        {
            try
            {

                SYS_UserCard userCard = _db.SYS_UserCards.FirstOrDefault(x => x.CardId == cardId);

                if (userCard == null)
                {
                    throw new Exception("No cards found for the user.");
                }

                userCard.IsActive = false;

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {

                throw new Exception("Error deleting the card: " + ex.Message);
            }
        }

        public bool SavePatientProduct(SavePatientProductDTO request, long UserId)
        {
            try
            {
                Pt_PatientProduct PatientProduct = new();
                if (request.PatientProductId == 0)
                {
                    PatientProduct = IMapper.Map<Pt_PatientProduct>(request);

                    PatientProduct.IsActive = true;
                    _db.Pt_PatientProducts.Add(PatientProduct);
                    _db.SaveChanges();
                }
                else
                {
                    PatientProduct = _db.Pt_PatientProducts.Where(x => x.PatientProductId == request.PatientProductId).FirstOrDefault();
                    IMapper.Map(request, PatientProduct);

                    _db.SaveChanges();

                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool SaveFacilitySquareCredentials(SaveSquareCredRequestDto request)
        {
            try
            {

                var FacilitySquareCred = IMapper.Map<Sys_FacilitySquareCred>(request);

                FacilitySquareCred.IsActive = true;

                _db.Sys_FacilitySquareCreds.Add(FacilitySquareCred);
                _db.SaveChanges();

                if (request.FacilityId.HasValue && request.FacilityId.Value > 0)
                    _facilityClientProvider.Invalidate(request.FacilityId.Value);

                return true;
            }
            catch (Exception ex)
            {

                return false;
            }
        }

        public SaveSquareCredRequestDto GetFacilitySquareCredentialsByFacilityId(long FacilityId)
        {
            var credentials = _db.Sys_FacilitySquareCreds
                                .Where(x => x.FacilityId == FacilityId
                                && x.IsActive == true
                                ).OrderByDescending(x => x.FacilitySquareCredId)
                                .FirstOrDefault();

            if (credentials == null)
            {
                _logger.LogWarning("No active Square credentials found for facility {FacilityId}", FacilityId);
                return new SaveSquareCredRequestDto();
            }

            SaveSquareCredRequestDto card = new SaveSquareCredRequestDto
            {
                FacilityId = credentials.FacilityId,
                ApplicationId = credentials.ApplicationId,
                AccessToken = credentials.AccessToken,
                LocationId = credentials.LocationId,
                IsActive = credentials.IsActive
            };

            return card;
        }

        public SaveSquareCredRequestDto GetAllFacilitySquareCredentialsByIdWithoutToken(long FacilityId)
        {
            var credentials = _db.Sys_FacilitySquareCreds
                                .Where(x => x.FacilityId == FacilityId
                                && x.IsActive == true
                                ).OrderByDescending(x => x.FacilitySquareCredId)
                                .FirstOrDefault();

            if (credentials == null)
            {
                _logger.LogWarning("No active Square credentials found for facility {FacilityId}", FacilityId);
                return new SaveSquareCredRequestDto();
            }

            SaveSquareCredRequestDto card = new SaveSquareCredRequestDto
            {
                FacilityId = credentials.FacilityId,
                ApplicationId = credentials.ApplicationId,
                LocationId = credentials.LocationId,
                IsActive = credentials.IsActive
            };

            return card;
        }

        public async Task<SquareAppIdResponseDto?> GetSquareAppIdByFacilityIdAsync(long facilityId)
        {
            return await _db.Sys_FacilitySquareCreds
                .AsNoTracking()
                .Where(x => x.FacilityId == facilityId && x.IsActive == true)
                .OrderByDescending(x => x.FacilitySquareCredId)
                .Select(x => new SquareAppIdResponseDto
                {
                    ApplicationId = x.ApplicationId,
                    LocationId = x.LocationId
                })
                .FirstOrDefaultAsync();
        }

    }
}
