using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vitality.Helper;
using Vitality.Filters;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Was anonymous (AllowAnonymous). trigger-recurring-payments charges real saved cards
    // through Stripe and Square, so as an anonymous endpoint anyone who knew the
    // route could bill every patient whose next payment date had arrived.
    // Restricted to Super Admin, matching NotificationTestController's treatment
    // of the other manual-trigger surface. See TEL-36.
    [AuthorizeRoles(UserRole.SuperAdmin)]
    public class TestRecurringController : ControllerBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TestRecurringController> _logger;

        public TestRecurringController(
            IServiceScopeFactory scopeFactory,
            ILogger<TestRecurringController> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpPost("trigger-recurring-payments")]
        public async Task<ApiResponse<string>> TriggerRecurringPayments()
        {
            var response = new ApiResponse<string>();
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                    var invoiceRepo = scope.ServiceProvider.GetRequiredService<Vitality.Models.Repos.Interfaces.IInvoiceRepo>();
                    var paymentService = scope.ServiceProvider.GetRequiredService<Vitality.Models.Repos.Interfaces.ISquarePaymentRepo>();
                    var productsRepo = scope.ServiceProvider.GetRequiredService<DudeMeds.Models.Repos.Interfaces.IProductsRepo>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<Vitality.Models.Repos.Services.INotificationService>();

                    var today = DateTime.UtcNow.Date;
                    var results = new List<string>();

                    var treatmentsDueForPayment = await db.PT_PatientTreatments
                        .Where(t => t.IsRecurring == true &&
                                   t.NextRecurringPaymentDate.HasValue &&
                                   t.NextRecurringPaymentDate.Value.Date <= today &&
                                   (t.Status != "Completed" && t.Status != "Cancelled") &&
                                   (t.IsActive == true || t.IsActive == null))
                        .ToListAsync();

                    results.Add($"Found {treatmentsDueForPayment.Count} treatments due for recurring payment.");

                    foreach (var treatment in treatmentsDueForPayment)
                    {
                        try
                        {
                            await ProcessRecurringPaymentAsync(
                                treatment,
                                db,
                                invoiceRepo,
                                paymentService,
                                productsRepo,
                                notificationService);
                            results.Add($"✓ Processed treatment {treatment.PatientTreatmentId}");
                        }
                        catch (Exception ex)
                        {
                            results.Add($"✗ Error processing treatment {treatment.PatientTreatmentId}: {ex.Message}");
                            _logger.LogError(ex, $"Error processing recurring payment for treatment {treatment.PatientTreatmentId}");
                        }
                    }

                    var expiredNonRecurringTreatments = await db.PT_PatientTreatments
                        .Where(t => (t.IsRecurring == false || t.IsRecurring == null) &&
                                   t.RecurringStartDate.HasValue &&
                                   t.RecurringDurationMonths.HasValue &&
                                   t.RecurringStartDate.Value.AddMonths(t.RecurringDurationMonths.Value) <= today &&
                                   (t.Status != "Completed" && t.Status != "Cancelled") &&
                                   (t.IsActive == true || t.IsActive == null))
                        .ToListAsync();

                    results.Add($"Found {expiredNonRecurringTreatments.Count} non-recurring treatments that have expired.");

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
                                .ToListAsync();

                            foreach (var order in orders)
                            {
                                order.SubscriptionStatus = "Expired";
                                order.ModifiedDate = DateTime.UtcNow;
                            }

                            await db.SaveChangesAsync();
                            results.Add($"✓ Updated expired treatment {treatment.PatientTreatmentId}");
                        }
                        catch (Exception ex)
                        {
                            results.Add($"✗ Error updating expired treatment {treatment.PatientTreatmentId}: {ex.Message}");
                            _logger.LogError(ex, $"Error updating expired treatment {treatment.PatientTreatmentId}");
                        }
                    }

                    response.Data = string.Join("\n", results);
                    response.Message = "Recurring payment processing completed. Check results for details.";
                }
            }
            catch (Exception ex)
            {
                response.Message = $"Error: {ex.Message}";
                _logger.LogError(ex, "Error in test recurring payment endpoint");
            }
            return response;
        }

        private async Task ProcessRecurringPaymentAsync(
            PT_PatientTreatment treatment,
            MainContext db,
            Vitality.Models.Repos.Interfaces.IInvoiceRepo invoiceRepo,
            Vitality.Models.Repos.Interfaces.ISquarePaymentRepo paymentService,
            DudeMeds.Models.Repos.Interfaces.IProductsRepo productsRepo,
            Vitality.Models.Repos.Services.INotificationService notificationService)
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

            decimal paymentAmount = treatment.OriginalPaymentAmount ?? bundle.Price ?? 0m;

            if (treatment.FacilityId.HasValue)
            {

                var facilityPrice = await db.PD_FacilityBundlePrices
                    .Where(fbp => fbp.FacilityId == treatment.FacilityId.Value &&
                                 fbp.BundleId == treatment.ProductId.Value)
                    .FirstOrDefaultAsync();

                if (facilityPrice != null)
                {

                    paymentAmount = facilityPrice.ClinicPrice;
                    _logger.LogInformation($"Using updated clinic price {paymentAmount} for treatment {treatment.PatientTreatmentId}.");
                }
                else
                {

                    if (treatment.OriginalPaymentAmount.HasValue)
                    {
                        paymentAmount = treatment.OriginalPaymentAmount.Value;
                        _logger.LogInformation($"Using original payment amount {paymentAmount} for treatment {treatment.PatientTreatmentId}.");
                    }
                }
            }

            if (paymentAmount <= 0)
            {
                _logger.LogWarning($"Invalid payment amount {paymentAmount} for treatment {treatment.PatientTreatmentId}.");
                return;
            }

            try
            {

                var facilityCreds = paymentService.GetFacilitySquareCredentialsByFacilityId(treatment.FacilityId.Value);
                if (facilityCreds == null || string.IsNullOrWhiteSpace(facilityCreds.ApplicationId))
                {
                    _logger.LogWarning($"No square credentials found for facility {treatment.FacilityId.Value}.");
                    return;
                }

                long userIdForPayment = 0;
                Vitality.Models.EntityClasses.SYS_UserCard? userCard = null;

                if (treatment.PatientId.HasValue)
                {

                    var patient = await db.PT_Patients
                        .Where(p => p.PatientId == treatment.PatientId.Value)
                        .FirstOrDefaultAsync();

                    if (patient != null && patient.LoginId.HasValue)
                    {

                        var patientUser = await db.SYS_UserDetails
                            .Include(u => u.SYS_UserCards)
                            .Where(u => u.LoginId == patient.LoginId.Value &&
                                       (u.IsActive == true || u.IsActive == null))
                            .FirstOrDefaultAsync();

                        if (patientUser != null && patientUser.UserId > 0)
                        {

                            if (patientUser.SYS_UserCards == null || !patientUser.SYS_UserCards.Any())
                            {
                                _logger.LogWarning($"Patient {treatment.PatientId.Value} has user account {patientUser.UserId} but no saved cards in SYS_UserCards.");
                            }
                            else
                            {

                                userCard = patientUser.SYS_UserCards
                                    .Where(c => c.IsDefault == true && (c.IsActive == true || c.IsActive == null))
                                    .FirstOrDefault();

                                if (userCard == null)
                                {
                                    userCard = patientUser.SYS_UserCards
                                        .Where(c => c.IsActive == true || c.IsActive == null)
                                        .FirstOrDefault();
                                }

                                if (userCard != null)
                                {
                                    userIdForPayment = patientUser.UserId;
                                    _logger.LogInformation($"Found user account {userIdForPayment} for patient {treatment.PatientId.Value} with card (CardId: {userCard.CardId}, Last4: {userCard.Last4}, IsDefault: {userCard.IsDefault}).");
                                }
                                else
                                {
                                    _logger.LogWarning($"Patient {treatment.PatientId.Value} has user account {patientUser.UserId} with cards, but none are active.");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"No user account found for patient {treatment.PatientId.Value} (LoginId: {patient.LoginId ?? 0}).");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Patient {treatment.PatientId.Value} does not have a LoginId linked to a user account.");
                    }
                }

                if (userIdForPayment == 0 || userCard == null)
                {
                    _logger.LogError($"Cannot process recurring payment for treatment {treatment.PatientTreatmentId}: Patient does not have a user account with an active payment card in SYS_UserCards.");

                    treatment.IsRecurring = false;
                    treatment.ModifiedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    return;
                }

                var invoiceRequest = new Vitality.Models.DTOs.Invoices.SaveInvoiceRequestDTO
                {
                    Amount = paymentAmount,
                    InvoiceType = Vitality.Models.Enums.InvoiceType.PatientToClinic.ToString(),
                    Status = Vitality.Models.Enums.InvoiceStatus.Pending.ToString(),
                    FacilityId = treatment.FacilityId,
                    PatientId = treatment.PatientId,
                    InvoiceNumber = Guid.NewGuid().ToString(),
                    IsActive = true
                };

                var invoice = invoiceRepo.SaveInvoiceReturnInoice(invoiceRequest, 1);
                if (invoice == null)
                {
                    _logger.LogError($"Failed to create invoice for recurring payment for treatment {treatment.PatientTreatmentId}.");
                    return;
                }

                bool paymentSuccess = await invoiceRepo.PayInvoice(userIdForPayment, invoice.InvoiceId);

                if (paymentSuccess)
                {

                    invoice.Status = Vitality.Models.Enums.InvoiceStatus.Paid.ToString();
                    await db.SaveChangesAsync();

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
                    await db.SaveChangesAsync();

                    if (treatment.RecurringDurationMonths.HasValue)
                    {
                        treatment.NextRecurringPaymentDate = DateTime.UtcNow.AddMonths(treatment.RecurringDurationMonths.Value);

                        treatment.ModifiedDate = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }

                    try
                    {
                        await notificationService.SendPaymentConfirmationAsync(
                            patientId: treatment.PatientId.Value,
                            amount: paymentAmount,
                            transactionId: invoice.InvoiceNumber ?? invoice.InvoiceId.ToString(),
                            ct: default);
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx, $"Failed to send payment confirmation notification for treatment {treatment.PatientTreatmentId}.");
                    }

                    _logger.LogInformation($"Successfully processed recurring payment for treatment {treatment.PatientTreatmentId}. Amount: {paymentAmount}, Next payment: {treatment.NextRecurringPaymentDate}.");
                }
                else
                {
                    _logger.LogWarning($"Payment failed for recurring payment for treatment {treatment.PatientTreatmentId}. Invoice ID: {invoice.InvoiceId}.");

                    treatment.IsRecurring = false;
                    treatment.ModifiedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception processing recurring payment for treatment {treatment.PatientTreatmentId}: {ex.Message}");

                treatment.IsRecurring = false;
                treatment.ModifiedDate = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
    }
}
