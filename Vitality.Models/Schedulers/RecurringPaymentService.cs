using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.DTOs.FacilitySquareCred;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;
using Vitality.Models.Repos.Services.Audit;
using Vitality.Models.Schedulers;
using DudeMeds.Models.Repos.Interfaces;
using System;

public class RecurringPaymentService : BackgroundService
{
    private const string LockName = "SCHEDULER:RecurringPayment";

    private readonly ILogger<RecurringPaymentService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SchedulerSettings _settings;

    public RecurringPaymentService(
        ILogger<RecurringPaymentService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<SchedulerSettings> settings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _settings = settings?.Value ?? new SchedulerSettings();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        TimeSpan tickInterval;
        if (_settings.RecurringPaymentIntervalMinutes is int qaMinutes && qaMinutes > 0)
        {
            tickInterval = TimeSpan.FromMinutes(qaMinutes);
            _logger.LogWarning(
                "RecurringPaymentService: using QA minutes-based interval ({Minutes}m). This should NOT be set in production.",
                qaMinutes);
        }
        else
        {
            var intervalHours = Math.Max(1, _settings.RecurringPaymentIntervalHours);
            tickInterval = TimeSpan.FromHours(intervalHours);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MainContext>();

                    var connectionString = db.Database.GetDbConnection().ConnectionString;
                    await using var distributedLock = await SchedulerDistributedLock.TryAcquireAsync(connectionString, LockName, stoppingToken).ConfigureAwait(false);
                    if (distributedLock is null)
                    {
                        _logger.LogInformation("RecurringPaymentService: another instance holds the {LockName} lock â€” skipping this iteration.", LockName);
                    }
                    else
                    {
                    var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepo>();
                    var paymentService = scope.ServiceProvider.GetRequiredService<ISquarePaymentRepo>();
                    var productsRepo = scope.ServiceProvider.GetRequiredService<IProductsRepo>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var auditService = scope.ServiceProvider.GetRequiredService<Vitality.Models.Repos.Services.Audit.IAuditService>();
                    var couponRepo = scope.ServiceProvider.GetRequiredService<ICouponRepo>();
                    var facilityStatusService = new Vitality.Models.Repos.Services.FacilityStatusService(db);
                    var stripeAccountResolver = scope.ServiceProvider.GetService<Vitality.Models.Repos.Interfaces.IStripeAccountResolver>();
                    var stripeRecurringCharge = scope.ServiceProvider.GetRequiredService<Vitality.Models.Repos.Interfaces.IStripeRecurringCharge>();

                    var today = DateTime.UtcNow.Date;

                    var treatmentsDueForPayment = await db.PT_PatientTreatments
                        .Where(t => t.IsRecurring == true &&
                                   t.NextRecurringPaymentDate.HasValue &&
                                   t.NextRecurringPaymentDate.Value.Date <= today &&
                                   (t.Status != "Completed" && t.Status != "Cancelled") &&
                                   (t.TreatmentStatus == null || t.TreatmentStatus != "Paused") &&
                                   (t.IsActive == true || t.IsActive == null))
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation($"Found {treatmentsDueForPayment.Count} treatments due for recurring payment.");

                    foreach (var treatment in treatmentsDueForPayment)
                    {
                        try
                        {

                            if (treatment.FacilityId.HasValue && !facilityStatusService.IsFacilityActive(treatment.FacilityId.Value))
                            {
                                _logger.LogWarning($"Skipping recurring payment for treatment {treatment.PatientTreatmentId}: Facility {treatment.FacilityId.Value} is inactive. Recurring payment has been automatically disabled.");

                                if (treatment.IsRecurring == true)
                                {
                                    treatment.IsRecurring = false;
                                    treatment.ModifiedDate = DateTime.UtcNow;
                                    await db.SaveChangesAsync(stoppingToken);
                                    _logger.LogInformation($"Recurring payment disabled for treatment {treatment.PatientTreatmentId} due to inactive facility {treatment.FacilityId.Value}.");
                                }
                                continue;
                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing recurring payment for treatment {treatment.PatientTreatmentId}: {ex.Message}");
                        }
                    }

                    var expiredNonRecurringTreatments = await db.PT_PatientTreatments
                        .Where(t => (t.IsRecurring == false || t.IsRecurring == null) &&
                                   t.RecurringStartDate.HasValue &&
                                   t.RecurringDurationMonths.HasValue &&
                                   t.RecurringStartDate.Value.AddMonths(t.RecurringDurationMonths.Value) <= today &&
                                   (t.Status != "Completed" && t.Status != "Cancelled") &&
                                   (t.IsActive == true || t.IsActive == null))
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation($"Found {expiredNonRecurringTreatments.Count} non-recurring treatments that have expired.");

                    foreach (var treatment in expiredNonRecurringTreatments)
                    {
                        try
                        {
                            treatment.Status = "Completed";
                            treatment.TreatmentStatus = "Completed";
                            treatment.ModifiedDate = DateTime.UtcNow;

                            var orders = await db.PT_PatientOrders
                                .Where(o => o.PatientTreamentId == treatment.PatientTreatmentId &&
                                           (o.IsActive == true || o.IsActive == null))
                                .ToListAsync(stoppingToken);

                            foreach (var order in orders)
                            {
                                order.SubscriptionStatus = "Expired";
                                order.ModifiedDate = DateTime.UtcNow;
                            }

                            await db.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Updated treatment {treatment.PatientTreatmentId} and related orders to Completed/Expired status.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error updating expired treatment {treatment.PatientTreatmentId}: {ex.Message}");
                        }
                    }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in recurring payment service: {ex.Message}");
            }

            await Task.Delay(tickInterval, stoppingToken);
        }
    }

    private async Task ProcessRecurringPaymentAsync(
        PT_PatientTreatment treatment,
        MainContext db,
        IInvoiceRepo invoiceRepo,
        ISquarePaymentRepo paymentService,
        IProductsRepo productsRepo,
        INotificationService notificationService,
        Vitality.Models.Repos.Services.Audit.IAuditService auditService,
        Vitality.Models.Repos.Interfaces.IStripeAccountResolver? stripeAccountResolver,
        Vitality.Models.Repos.Interfaces.IStripeRecurringCharge? stripeRecurringCharge,
        ICouponRepo couponRepo,
        CancellationToken ct)
    {
        if (!treatment.PatientId.HasValue || !treatment.ProductId.HasValue || !treatment.FacilityId.HasValue)
        {
            _logger.LogWarning($"Treatment {treatment.PatientTreatmentId} missing required fields for recurring payment.");
            return;
        }

        var bundle = productsRepo.GetBundleByBundleId(treatment.ProductId.Value);
        if (bundle == null)
        {
            _logger.LogWarning($"Bundle {treatment.ProductId.Value} not found for treatment {treatment.PatientTreatmentId}.");
            return;
        }

        decimal baseBundlePrice = treatment.OriginalPaymentAmount ?? bundle.Price ?? 0m;

        if (treatment.FacilityId.HasValue)
        {
            var facilityPrice = await db.PD_FacilityBundlePrices
                .Where(fbp => fbp.FacilityId == treatment.FacilityId.Value &&
                             fbp.BundleId == treatment.ProductId.Value)
                .FirstOrDefaultAsync(ct);

            if (facilityPrice != null && facilityPrice.ClinicPrice > 0)
            {
                baseBundlePrice = facilityPrice.ClinicPrice;
                _logger.LogInformation($"Using updated clinic price {baseBundlePrice} for treatment {treatment.PatientTreatmentId}.");
            }
            else if (treatment.OriginalPaymentAmount.HasValue)
            {
                baseBundlePrice = treatment.OriginalPaymentAmount.Value;
                _logger.LogInformation($"Using original payment amount {baseBundlePrice} for treatment {treatment.PatientTreatmentId}.");
            }
        }

        var hadStoredCouponId = treatment.RecurringCouponCodeId;
        var (discountedCharge, couponApplied) = await couponRepo.ComputeRecurringChargeAmountAsync(
            treatment.FacilityId.Value,
            treatment.ProductId.Value,
            baseBundlePrice,
            treatment.RecurringCouponCodeId,
            ct);

        if (hadStoredCouponId.HasValue && !couponApplied)
        {
            treatment.RecurringCouponCodeId = null;
            treatment.RecurringAmountAfterCoupon = null;
            treatment.ModifiedDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation($"Cleared recurring coupon on treatment {treatment.PatientTreatmentId} (inactive, expired, or no longer valid for this bundle).");
        }

        decimal paymentAmount = discountedCharge;

        if (!treatment.RecurringDurationMonths.HasValue || treatment.RecurringDurationMonths.Value <= 0)
        {
            _logger.LogWarning(
                "Treatment {TreatmentId} has invalid RecurringDurationMonths ({Months}); disabling recurring without charging.",
                treatment.PatientTreatmentId, treatment.RecurringDurationMonths);
            treatment.IsRecurring = false;
            treatment.NextRecurringPaymentDate = null;
            treatment.ModifiedDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        bool complimentaryCycle = paymentAmount <= 0m;

        try
        {
            string? stripeAccountId = null;
            SaveSquareCredRequestDto? facilityCreds = null;
            bool facilityHasSquare = false;
            var facilityPaymentMode = (int)FacilityPaymentMode.Square;
            var useStripe = false;
            var useSquare = false;

            long userIdForPayment = 0;
            SYS_UserCard? userCard = null;
            SYS_UserCard? stripeCard = null;

            if (!complimentaryCycle)
            {
                if (stripeAccountResolver != null && treatment.FacilityId.HasValue)
                    stripeAccountId = await stripeAccountResolver.GetStripeAccountIdForFacilityAsync(treatment.FacilityId.Value, ct);
                facilityCreds = paymentService.GetFacilitySquareCredentialsByFacilityId(treatment.FacilityId!.Value);
                facilityHasSquare = !string.IsNullOrWhiteSpace(facilityCreds.AccessToken);
                var facilityPaymentModeRow = await db.SYS_Facilities.AsNoTracking()
                    .Where(f => f.FacilityId == treatment.FacilityId.Value)
                    .Select(f => (int?)f.PaymentModeId)
                    .FirstOrDefaultAsync(ct);
                facilityPaymentMode = facilityPaymentModeRow ?? (int)FacilityPaymentMode.Square;
                useStripe = facilityPaymentMode == (int)FacilityPaymentMode.Stripe;
                useSquare = facilityPaymentMode == (int)FacilityPaymentMode.Square;

                if (useStripe && string.IsNullOrEmpty(stripeAccountId))
                {
                    _logger.LogError(
                        "Recurring treatment {TreatmentId}: facility is in Stripe payment mode but has no Stripe Connect account linked.",
                        treatment.PatientTreatmentId);
                    treatment.IsRecurring = false;
                    treatment.ModifiedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return;
                }

                if (useSquare && !facilityHasSquare)
                {
                    _logger.LogError(
                        "Recurring treatment {TreatmentId}: facility is in Square payment mode but has no Square AccessToken (Square not connected).",
                        treatment.PatientTreatmentId);
                    treatment.IsRecurring = false;
                    treatment.ModifiedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return;
                }

                if (treatment.PatientId.HasValue)
                {
                    var patient = await db.PT_Patients
                        .Where(p => p.PatientId == treatment.PatientId.Value)
                        .FirstOrDefaultAsync(ct);
                    if (patient != null && patient.LoginId.HasValue)
                    {
                        var patientUser = await db.SYS_UserDetails
                            .Include(u => u.SYS_UserCards)
                            .Where(u => u.LoginId == patient.LoginId.Value && (u.IsActive == true || u.IsActive == null))
                            .FirstOrDefaultAsync(ct);
                        if (patientUser != null && patientUser.UserId > 0 && patientUser.SYS_UserCards != null && patientUser.SYS_UserCards.Any())
                        {
                            userIdForPayment = patientUser.UserId;
                            if (useStripe && !string.IsNullOrEmpty(stripeAccountId))
                                stripeCard = patientUser.SYS_UserCards
                                    .Where(c => (c.IsActive == true || c.IsActive == null) && c.StripeAccountId == stripeAccountId && !string.IsNullOrEmpty(c.StripePaymentMethodId))
                                    .OrderByDescending(c => c.IsDefault)
                                    .ThenByDescending(c => string.IsNullOrEmpty(c.StripeCustomerId) ? 0 : 1)
                                    .ThenBy(c => c.CardId)
                                    .FirstOrDefault();
                            var squareCards = patientUser.SYS_UserCards.Where(c => (c.IsActive == true || c.IsActive == null) && !string.IsNullOrEmpty(c.SquareCardId)).ToList();
                            userCard = squareCards.FirstOrDefault(c => c.IsDefault) ?? squareCards.FirstOrDefault();
                            if (useStripe && stripeCard != null)
                                _logger.LogInformation($"Recurring treatment {treatment.PatientTreatmentId}: Stripe mode; using Stripe card for facility {treatment.FacilityId}.");
                            else if (useSquare && userCard != null)
                                _logger.LogInformation($"Recurring treatment {treatment.PatientTreatmentId}: Square mode; user {userIdForPayment} has Square card (CardId: {userCard.CardId}).");
                        }
                    }
                }

                if (userIdForPayment == 0)
                {
                    _logger.LogError($"Cannot process recurring payment for treatment {treatment.PatientTreatmentId}: Patient has no linked user with cards.");
                    treatment.IsRecurring = false;
                    treatment.ModifiedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return;
                }

                if (useStripe && stripeCard == null)
                {
                    _logger.LogError(
                        "Recurring payment for treatment {TreatmentId}: facility is in Stripe payment mode but patient has no saved Stripe card for this clinic.",
                        treatment.PatientTreatmentId);
                    treatment.IsRecurring = false;
                    treatment.ModifiedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return;
                }

                if (useSquare && userCard == null)
                {
                    _logger.LogError(
                        "Recurring payment for treatment {TreatmentId}: facility is in Square payment mode but patient has no saved Square card.",
                        treatment.PatientTreatmentId);
                    treatment.IsRecurring = false;
                    treatment.ModifiedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return;
                }
            }
            else
            {
                _logger.LogInformation(
                    "Recurring treatment {TreatmentId}: complimentary/zero cycle (amount {Amount}); skipping Stripe/Square charge paths and card-on-file requirements.",
                    treatment.PatientTreatmentId, paymentAmount);
                userIdForPayment = await TryResolvePatientUserIdAsync(db, treatment.PatientId, ct).ConfigureAwait(false);
            }

            DateTime? originalDueDate = treatment.NextRecurringPaymentDate;
            var dueTimestampForKey = originalDueDate ?? DateTime.UtcNow;
            var recurringIdempotencyKey = $"RECURRING-{treatment.PatientTreatmentId}-{dueTimestampForKey:yyyyMMddHHmmss}";
            var deterministicInvoiceNumber = $"REC-{treatment.PatientTreatmentId}-{dueTimestampForKey:yyyyMMddHHmmss}";

            bool chargeSucceeded = false;
            string? chargeTransactionId = null;
            string? squarePaymentId = null;
            string chargePaymentMethod = "Unknown";
            long? chargeCardId = null;
            string? chargeFailureReason = null;
            string? chargeNotesContext = null;

            try
            {
                if (paymentAmount <= 0m)
                {

                    chargeSucceeded = true;
                    chargeTransactionId = $"REC-COMPL-{Guid.NewGuid():N}";
                    chargePaymentMethod = "Complimentary";
                    chargeCardId = null;
                    chargeNotesContext = "Recurring â€” no charge (coupon / zero balance).";
                }
                else if (useStripe && stripeCard != null && !string.IsNullOrEmpty(stripeAccountId) && !string.IsNullOrEmpty(stripeCard.StripePaymentMethodId))
                {
                    if (stripeRecurringCharge == null)
                    {
                        chargeFailureReason = "Internal: IStripeRecurringCharge service not registered.";
                        _logger.LogError(
                            "Recurring payment for treatment {TreatmentId}: facility has Stripe Connect but IStripeRecurringCharge is not registered.",
                            treatment.PatientTreatmentId);
                    }
                    else
                    {
                        var amountCents = (long)Math.Round(paymentAmount * 100, MidpointRounding.AwayFromZero);
                        var appFeeCents = Math.Max(1, (int)(amountCents * 0.10));

                        var chargeResult = await stripeRecurringCharge.ChargeAsync(
                            stripeAccountId,
                            stripeCard.StripePaymentMethodId,
                            amountCents,
                            "usd",
                            stripeCard.StripeCustomerId,
                            appFeeCents,
                            ct,
                            recurringIdempotencyKey).ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(chargeResult.CustomerId) && string.IsNullOrWhiteSpace(stripeCard.StripeCustomerId))
                        {
                            stripeCard.StripeCustomerId = chargeResult.CustomerId;
                            try { await db.SaveChangesAsync(ct).ConfigureAwait(false); }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not persist StripeCustomerId for user card {CardId} after recurring charge setup.", stripeCard.CardId);
                            }
                        }

                        if (!string.IsNullOrEmpty(chargeResult.PaymentIntentId))
                        {
                            chargeSucceeded = true;
                            chargeTransactionId = chargeResult.PaymentIntentId;
                            chargePaymentMethod = "Stripe";
                            chargeCardId = stripeCard.CardId;
                            chargeNotesContext = $"Recurring payment via Stripe - PaymentIntent {chargeResult.PaymentIntentId}";
                        }
                        else
                        {

                            chargeFailureReason = string.IsNullOrWhiteSpace(chargeResult.FailureReason)
                                ? "Stripe charge did not return a PaymentIntent - card likely declined or off-session authentication required."
                                : chargeResult.FailureReason;
                        }
                    }
                }
                else if (useSquare && userCard != null && facilityHasSquare && !string.IsNullOrWhiteSpace(userCard.SquareCardId))
                {

                    var amountCents = (int)Math.Round(paymentAmount * 100, MidpointRounding.AwayFromZero);
                    try
                    {
                        var response = await paymentService.ChargeCustomerWithSavedCard(
                            userCard.SquareClientId,
                            userCard.SquareCardId!.ToString(),
                            amountCents,
                            "USD",
                            treatment.FacilityId.Value,
                            recurringIdempotencyKey).ConfigureAwait(false);

                        if (response?.Payment != null && response.Payment.Status == "COMPLETED")
                        {
                            chargeSucceeded = true;
                            chargeTransactionId = response.Payment.Id ?? Guid.NewGuid().ToString();
                            squarePaymentId = response.Payment.Id;
                            chargePaymentMethod = "Card";
                            chargeCardId = userCard.CardId;
                            chargeNotesContext = $"Recurring payment via Square - Card ending in {userCard.Last4}";
                        }
                        else
                        {
                            chargeFailureReason = $"Square charge status was '{response?.Payment?.Status ?? "Unknown"}'.";
                        }
                    }
                    catch (Exception sqEx)
                    {
                        chargeFailureReason = $"Square charge threw: {sqEx.Message}";
                    }
                }
                else
                {
                    chargeFailureReason = "No valid payment method available for this treatment's facility payment mode.";
                    _logger.LogWarning(
                        "Recurring treatment {TreatmentId}: no eligible charge path. useStripe={UseStripe} useSquare={UseSquare} stripeCard={StripeCard} userCard={UserCard}",
                        treatment.PatientTreatmentId, useStripe, useSquare, stripeCard != null, userCard != null);
                }
            }
            catch (Exception chargeEx)
            {
                chargeFailureReason = $"Unhandled charge exception: {chargeEx.Message}";
                _logger.LogError(chargeEx, "Recurring charge attempt threw for treatment {TreatmentId}.", treatment.PatientTreatmentId);
            }

            if (chargeSucceeded)
            {

                try
                {

                    var existingInvoice = await db.Sys_Invoices
                        .Where(i => i.InvoiceNumber == deterministicInvoiceNumber)
                        .FirstOrDefaultAsync(ct);

                    Sys_Invoice? invoice = existingInvoice;
                    if (invoice == null)
                    {
                        var invoiceRequest = new SaveInvoiceRequestDTO
                        {
                            Amount = paymentAmount,
                            InvoiceType = InvoiceType.PatientToClinic.ToString(),
                            Status = InvoiceStatus.Paid.ToString(),
                            FacilityId = treatment.FacilityId,
                            PatientId = treatment.PatientId,
                            InvoiceNumber = deterministicInvoiceNumber,
                            IsActive = true
                        };
                        invoice = invoiceRepo.SaveInvoiceReturnInoice(invoiceRequest, 1);
                    }

                    if (invoice == null)
                    {
                        _logger.LogError(
                            "Recurring charge succeeded for treatment {TreatmentId} but post-charge invoice creation returned null. Manual reconciliation required. TransactionId={Tx}",
                            treatment.PatientTreatmentId, chargeTransactionId);
                    }
                    else
                    {
                        var payUserId = userIdForPayment > 0 ? userIdForPayment : 1L;
                        await invoiceRepo.RecordPayment(
                            invoice.InvoiceId,
                            paymentAmount,
                            chargeTransactionId!,
                            squarePaymentId,
                            payUserId,
                            treatment.PatientId,
                            treatment.FacilityId,
                            chargeCardId,
                            chargePaymentMethod,
                            chargeNotesContext).ConfigureAwait(false);

                        var patientPayment = new PT_PatientPaymentDetail
                        {
                            PatientPaymentId = 0,
                            PatientId = treatment.PatientId,
                            ProductId = treatment.ProductId,
                            PatientTreatmentId = treatment.PatientTreatmentId,
                            TotalPrice = paymentAmount,
                            PaymentStatus = "Paid",
                            IsActive = true,
                            CreatedBy = 1,
                            CreatedDate = DateTime.UtcNow,
                            Guid = Guid.NewGuid().ToString()
                        };
                        db.PT_PatientPaymentDetails.Add(patientPayment);
                        await db.SaveChangesAsync(ct);

                        if (treatment.ProductId.HasValue)
                        {
                            try
                            {
                                string? couponLineCode = null;
                                if (treatment.RecurringCouponCodeId.HasValue && couponApplied)
                                {
                                    couponLineCode = await db.SYS_CouponCodes.AsNoTracking()
                                        .Where(c => c.CoupanCodeId == treatment.RecurringCouponCodeId.Value)
                                        .Select(c => c.CoupanCode)
                                        .FirstOrDefaultAsync(ct);
                                }

                                await invoiceRepo.CreateInvoiceLineItemForBundle(
                                    invoiceId: invoice.InvoiceId,
                                    bundleId: treatment.ProductId.Value,
                                    couponCode: couponLineCode,
                                    originalPrice: baseBundlePrice,
                                    discountedPrice: paymentAmount,
                                    userId: 1);
                            }
                            catch (Exception lineEx)
                            {
                                _logger.LogWarning(lineEx, "Failed to create invoice line item for recurring payment treatment {TreatmentId}.", treatment.PatientTreatmentId);
                            }
                        }

                        if (treatment.PatientId.HasValue)
                        {
                            try
                            {
                                await notificationService.SendPaymentConfirmationAsync(
                                    patientId: treatment.PatientId.Value,
                                    amount: paymentAmount,
                                    transactionId: invoice.InvoiceId.ToString(),
                                    ct: ct);
                            }
                            catch (Exception notifEx)
                            {
                                _logger.LogWarning(notifEx, "Failed to send payment confirmation notification for treatment {TreatmentId}.", treatment.PatientTreatmentId);
                            }
                        }
                    }
                }
                catch (Exception postChargeEx)
                {

                    _logger.LogError(
                        postChargeEx,
                        "Recurring charge succeeded but post-charge bookkeeping failed for treatment {TreatmentId} (transactionId={Tx}). Recurring will continue; manual recon needed.",
                        treatment.PatientTreatmentId, chargeTransactionId);
                }

                treatment.NextRecurringPaymentDate = DateTime.UtcNow.AddMonths(treatment.RecurringDurationMonths.Value);
                treatment.RecurringAmountAfterCoupon = paymentAmount;
                treatment.RetryCount = null;
                treatment.LastFailureMessage = null;
                treatment.LastFailureAt = null;
                treatment.ModifiedDate = DateTime.UtcNow;
                try { await db.SaveChangesAsync(ct); }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to advance NextRecurringPaymentDate after successful charge for treatment {TreatmentId}.", treatment.PatientTreatmentId);
                }

                try
                {
                    await auditService.LogEntityChangeAsync(
                        action: "RecurringPaymentExecuted",
                        entityType: "PT_PatientTreatment",
                        entityId: treatment.PatientTreatmentId,
                        oldValues: new { OriginalDueDate = originalDueDate },
                        newValues: new { Amount = paymentAmount, TransactionId = chargeTransactionId, Method = chargePaymentMethod, NextDate = treatment.NextRecurringPaymentDate },
                        userId: 1,
                        patientId: treatment.PatientId,
                        facilityId: treatment.FacilityId,
                        description: $"Recurring charge succeeded for treatment {treatment.PatientTreatmentId} via {chargePaymentMethod}; amount {paymentAmount}; next due {treatment.NextRecurringPaymentDate:yyyy-MM-dd}.",
                        module: "RecurringPayment");
                }
                catch (Exception auditEx) { _logger.LogWarning(auditEx, "Failed to write RecurringPaymentExecuted audit row for treatment {TreatmentId}.", treatment.PatientTreatmentId); }

                _logger.LogInformation("Successfully processed recurring payment for treatment {TreatmentId}. Amount: {Amount}, Next payment: {Next}.", treatment.PatientTreatmentId, paymentAmount, treatment.NextRecurringPaymentDate);
            }
            else
            {

                var previousRetryCount = treatment.RetryCount ?? 0;
                var newRetryCount = previousRetryCount + 1;
                var maxRetries = Math.Max(1, _settings.RecurringMaxRetries);
                var truncatedReason = string.IsNullOrWhiteSpace(chargeFailureReason)
                    ? "Charge failed (no reason returned)."
                    : (chargeFailureReason.Length > 500 ? chargeFailureReason.Substring(0, 500) : chargeFailureReason);

                _logger.LogWarning(
                    "Recurring charge failed for treatment {TreatmentId} (retry {Retry}/{Max}): {Reason}",
                    treatment.PatientTreatmentId, newRetryCount, maxRetries, truncatedReason);

                treatment.RetryCount = newRetryCount;
                treatment.LastFailureAt = DateTime.UtcNow;
                treatment.LastFailureMessage = truncatedReason;

                bool giveUp = newRetryCount >= maxRetries;

                if (giveUp)
                {
                    treatment.IsRecurring = false;
                    treatment.NextRecurringPaymentDate = null;
                }
                else
                {

                    treatment.NextRecurringPaymentDate = DateTime.UtcNow.AddDays(1);
                }

                treatment.ModifiedDate = DateTime.UtcNow;
                try { await db.SaveChangesAsync(ct); }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to persist retry-state for treatment {TreatmentId} after failure.", treatment.PatientTreatmentId);
                }

                var notifyOn = _settings.RecurringRetryNotificationCounts ?? Array.Empty<int>();
                bool shouldNotify = giveUp || notifyOn.Contains(newRetryCount);

                if (shouldNotify && treatment.PatientId.HasValue)
                {
                    try
                    {
                        if (giveUp)
                        {
                            await notificationService.SendSubscriptionEndedDueToFailureAsync(
                                patientId: treatment.PatientId.Value,
                                treatmentId: treatment.PatientTreatmentId,
                                totalRetries: newRetryCount,
                                ct: ct).ConfigureAwait(false);
                        }
                        else
                        {
                            await notificationService.SendPaymentFailedAsync(
                                patientId: treatment.PatientId.Value,
                                amount: paymentAmount,
                                treatmentId: treatment.PatientTreatmentId,
                                failureReason: truncatedReason,
                                retryCount: newRetryCount,
                                ct: ct).ConfigureAwait(false);
                        }
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx, "Failed to send payment-failure notification for treatment {TreatmentId} (retry {Retry}).", treatment.PatientTreatmentId, newRetryCount);
                    }
                }

                try
                {
                    await auditService.LogEntityChangeAsync(
                        action: giveUp ? "RecurringSubscriptionEnded" : "RecurringPaymentFailed",
                        entityType: "PT_PatientTreatment",
                        entityId: treatment.PatientTreatmentId,
                        oldValues: new { RetryCount = previousRetryCount },
                        newValues: new { RetryCount = newRetryCount, NextDate = treatment.NextRecurringPaymentDate, FailureReason = truncatedReason },
                        userId: 1,
                        patientId: treatment.PatientId,
                        facilityId: treatment.FacilityId,
                        description: giveUp
                            ? $"Recurring disabled for treatment {treatment.PatientTreatmentId} after {newRetryCount} consecutive failures."
                            : $"Recurring charge failed for treatment {treatment.PatientTreatmentId} (retry {newRetryCount}); next attempt {treatment.NextRecurringPaymentDate:yyyy-MM-dd}.",
                        module: "RecurringPayment");
                }
                catch (Exception auditEx) { _logger.LogWarning(auditEx, "Failed to write RecurringPaymentFailed audit row for treatment {TreatmentId}.", treatment.PatientTreatmentId); }
            }
        }
        catch (Exception ex)
        {

            _logger.LogError(ex, "Exception processing recurring payment for treatment {TreatmentId}: {Message}. StackTrace: {Stack}", treatment.PatientTreatmentId, ex.Message, ex.StackTrace);
        }
    }

    private static async Task<long> TryResolvePatientUserIdAsync(MainContext db, long? patientId, CancellationToken ct)
    {
        if (!patientId.HasValue)
            return 0;

        var loginId = await db.PT_Patients.AsNoTracking()
            .Where(p => p.PatientId == patientId.Value)
            .Select(p => p.LoginId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (!loginId.HasValue)
            return 0;

        var userId = await db.SYS_UserDetails.AsNoTracking()
            .Where(u => u.LoginId == loginId.Value && (u.IsActive == true || u.IsActive == null))
            .Select(u => (long?)u.UserId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return userId ?? 0;
    }
}
