using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vitality.Models.CommonMethods;
using Vitality.Models.DTOs.Reminders;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services;
using Vitality.Models.Repos.Services.Audit;
using Vitality.Models.Repos.Services.Email;
using Vitality.Services.Email;
using Vitality.Services.Sms;

namespace Vitality.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private const string LegacyBrandName = "TelehealthUS";
    private const string PreviousBrandName = "ImpactHealthUSA";
    private const string RequestedBrandName = "TelehealthUS";
    private static readonly Regex LegacyBrandRegex = new(@"\bImpact Health\b(?!\s*USA\b)", RegexOptions.CultureInvariant);

    private readonly MainContext _db;
    private readonly IMailSender _mailSender;
    private readonly ISmsSender _smsSender;
    private readonly ILogger<NotificationService> _logger;
    private readonly BackgroundEmailService _backgroundEmailService;
    private readonly IAuditService _auditService;
    private const string FrontendBaseUrl = "https://www.telehealthus.com";

    private static string FormatCurrency(decimal amount)
    {
        return $"${amount:N2}";
    }

    private static bool IsInvoicePaymentPending(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        return string.Equals(status, InvoiceStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "PartiallyPaid", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ResolveDrugCatalogDisplayAsync(long? drugId, bool isCustomFallback, CancellationToken ct)
    {
        var isCustom = isCustomFallback;
        long? catalogId = null;
        string? catalogName = null;
        if (drugId.HasValue && drugId.Value > 0)
        {
            var drug = await _db.PD_Drugs.AsNoTracking()
                .Where(d => d.DrugId == drugId.Value)
                .Select(d => new { d.IsCustom, d.CatalogId })
                .FirstOrDefaultAsync(ct);
            if (drug != null)
            {
                isCustom = drug.IsCustom == true;
                catalogId = drug.CatalogId;
            }
        }

        if (catalogId.HasValue && catalogId.Value > 0)
        {
            catalogName = await _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.CatalogId == catalogId.Value && c.IsActive == true)
                .Select(c => c.CatalogName)
                .FirstOrDefaultAsync(ct);
        }

        var parts = new List<string>
        {
            !string.IsNullOrWhiteSpace(catalogName)
                ? $"{catalogName} (Catalog {(catalogId ?? 0)})"
                : (isCustom ? "Custom Drug Catalog" : "Empower Drug Catalog")
        };

        if (drugId.HasValue && drugId.Value > 0)
        {
            var inGlobalListings = await _db.PC_PHARMTOGLOBALs.AsNoTracking()
                .AnyAsync(p => p.DrugId == drugId.Value && p.IsActive == true, ct);
            if (inGlobalListings)
                parts.Add("Clinic / GA package catalog (product listings)");
        }

        return string.Join(" · ", parts);
    }

    public NotificationService(
        MainContext db,
        IMailSender mailSender,
        ISmsSender smsSender,
        ILogger<NotificationService> logger,
        BackgroundEmailService backgroundEmailService,
        IAuditService auditService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mailSender = new BrandedMailSender(mailSender ?? throw new ArgumentNullException(nameof(mailSender)));
        _smsSender = new BrandedSmsSender(smsSender ?? throw new ArgumentNullException(nameof(smsSender)));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backgroundEmailService = backgroundEmailService ?? throw new ArgumentNullException(nameof(backgroundEmailService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    private static string? NormalizeBranding(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalized = text.Replace(PreviousBrandName, RequestedBrandName, StringComparison.Ordinal);
        normalized = LegacyBrandRegex.Replace(normalized, RequestedBrandName);
        return normalized.Replace($"{RequestedBrandName} USA", RequestedBrandName, StringComparison.Ordinal);
    }

    private sealed class BrandedMailSender : IMailSender
    {
        private readonly IMailSender _inner;

        public BrandedMailSender(IMailSender inner)
        {
            _inner = inner;
        }

        public Task SendAsync(MailTemplateModel message, CancellationToken cancellationToken = default)
        {
            var bodyParagraphs = new List<string>();
            if (message.BodyParagraphs != null)
            {
                foreach (var paragraph in message.BodyParagraphs)
                {
                    bodyParagraphs.Add(NormalizeBranding(paragraph) ?? string.Empty);
                }
            }

            var branded = new MailTemplateModel
            {
                ToEmail = message.ToEmail,
                ToName = message.ToName,
                Subject = NormalizeBranding(message.Subject) ?? string.Empty,
                Greeting = NormalizeBranding(message.Greeting) ?? "Hi there,",
                PreviewText = NormalizeBranding(message.PreviewText),
                BodyParagraphs = bodyParagraphs,
                HighlightText = NormalizeBranding(message.HighlightText),
                ButtonText = NormalizeBranding(message.ButtonText),
                ButtonUrl = message.ButtonUrl,
                FooterNote = NormalizeBranding(message.FooterNote)
            };

            return _inner.SendAsync(branded, cancellationToken);
        }
    }

    private sealed class BrandedSmsSender : ISmsSender
    {
        private readonly ISmsSender _inner;

        public BrandedSmsSender(ISmsSender inner)
        {
            _inner = inner;
        }

        public Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
        {
            return _inner.SendAsync(toPhoneNumber, NormalizeBranding(message) ?? string.Empty, cancellationToken);
        }
    }

    public async Task SendPaymentConfirmationAsync(long patientId, decimal amount, string transactionId, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for payment confirmation", patientId);
                return;
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var formattedAmount = FormatCurrency(amount);

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Payment Confirmed - TelehealthUS",
                PreviewText = $"Your payment of {formattedAmount} has been successfully processed.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"We're pleased to confirm that your payment of {formattedAmount} has been successfully processed.",
                    $"Invoice ID: {transactionId}",
                    "Thank you for your payment. Your account has been updated accordingly."
                },
                ButtonText = "View Payment History",
                ButtonUrl = $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{transactionId}",
                FooterNote = "If you have any questions about this payment, please contact our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                var smsMessage = $"TelehealthUS: Payment of {formattedAmount} confirmed. Invoice ID: {transactionId}. Thank you!";
                await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
            }

            _logger.LogInformation("Payment confirmation sent to patient {PatientId}", patientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment confirmation to patient {PatientId}", patientId);

        }
    }

    public async Task SendPaymentFailedAsync(long patientId, decimal amount, long? treatmentId, string? failureReason, int retryCount = 1, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null)
            {
                _logger.LogWarning("Patient {PatientId} not found for payment failure notification", patientId);
                return;
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var formattedAmount = FormatCurrency(amount);
            var reasonClause = string.IsNullOrWhiteSpace(failureReason)
                ? "We weren't able to charge your saved card."
                : $"We weren't able to charge your saved card. Reason from the bank: {failureReason}.";

            var updateCardUrl = treatmentId.HasValue && treatmentId.Value > 0
                ? $"{FrontendBaseUrl}/myTreatments/{treatmentId.Value}?action=update-card"
                : $"{FrontendBaseUrl}/profile/paymentMethods";

            var subjectAttemptSuffix = retryCount > 1 ? $" (attempt {retryCount})" : string.Empty;

            if (!string.IsNullOrWhiteSpace(patient.Email))
            {
                var emailModel = new MailTemplateModel
                {
                    ToEmail = patient.Email!.Trim(),
                    ToName = patientName,
                    Subject = $"Payment Failed - Action Required{subjectAttemptSuffix}",
                    PreviewText = $"Your scheduled payment of {formattedAmount} could not be processed.",
                    Greeting = $"Hi {patient.FirstName},",
                    BodyParagraphs = new List<string>
                    {
                        $"Your recurring payment of {formattedAmount} could not be processed.",
                        reasonClause,
                        "We'll keep retrying this charge daily. To avoid an interruption to your treatment, please update or add a new payment method as soon as possible.",
                        "Once your payment method is updated the next daily retry will pick it up automatically."
                    },
                    ButtonText = "Update Payment Method",
                    ButtonUrl = updateCardUrl,
                    FooterNote = "If you have any questions or believe this is in error, please contact our support team."
                };

                try { await _mailSender.SendAsync(emailModel, ct); }
                catch (Exception mailEx)
                {
                    _logger.LogWarning(mailEx, "Failed to send payment-failure email to patient {PatientId}", patientId);
                }
            }
            else
            {
                _logger.LogWarning("Patient {PatientId} has no email — skipping payment-failure email (in-app still recorded).", patientId);
            }

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                try
                {
                    var sms = $"TelehealthUS: payment of {formattedAmount} failed{subjectAttemptSuffix}. Please update your card to continue service: {updateCardUrl}";
                    await _smsSender.SendAsync(patient.Phone, sms, ct);
                }
                catch (Exception smsEx)
                {
                    _logger.LogWarning(smsEx, "Failed to send payment-failure SMS to patient {PatientId}", patientId);
                }
            }

            try
            {
                _db.SYS_Notifications.Add(new SYS_Notification
                {
                    NotificationType = "PaymentFailed",
                    UserId = null,
                    PatientId = patientId,
                    FacilityId = null,
                    Description =
                        $"Your recurring payment of {formattedAmount} could not be processed{(retryCount > 1 ? $" (attempt {retryCount})" : string.Empty)}. " +
                        $"Please update your card: {updateCardUrl}",
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception inAppEx)
            {
                _logger.LogWarning(inAppEx, "Failed to write in-app payment-failure notification for patient {PatientId}", patientId);
            }

            _logger.LogInformation("Payment failure notification dispatched to patient {PatientId} (amount {Amount}, treatment {TreatmentId}, retry {RetryCount})", patientId, amount, treatmentId, retryCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending payment failure notification to patient {PatientId}", patientId);

        }
    }

    public async Task SendSubscriptionEndedDueToFailureAsync(long patientId, long? treatmentId, int totalRetries, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);
            if (patient == null) return;

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var url = treatmentId.HasValue && treatmentId.Value > 0
                ? $"{FrontendBaseUrl}/myTreatments/{treatmentId.Value}"
                : $"{FrontendBaseUrl}/myTreatments";

            if (!string.IsNullOrWhiteSpace(patient.Email))
            {
                var emailModel = new MailTemplateModel
                {
                    ToEmail = patient.Email!.Trim(),
                    ToName = patientName,
                    Subject = "Recurring billing has been paused on your treatment",
                    PreviewText = "We've stopped automatic billing after repeated payment failures.",
                    Greeting = $"Hi {patient.FirstName},",
                    BodyParagraphs = new List<string>
                    {
                        $"After {totalRetries} failed attempts to charge your saved card, we've paused recurring billing on your treatment to avoid further declined charges.",
                        "Your treatment record is still on file. To resume, please update your payment method and turn recurring back on from your treatment screen — or purchase the package again if you prefer a fresh start.",
                    },
                    ButtonText = "Manage My Treatment",
                    ButtonUrl = url,
                    FooterNote = "If you need help, our support team is happy to assist."
                };
                try { await _mailSender.SendAsync(emailModel, ct); }
                catch (Exception mailEx) { _logger.LogWarning(mailEx, "Failed to send subscription-ended email to patient {PatientId}", patientId); }
            }

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                try
                {
                    var sms = $"TelehealthUS: recurring billing on your treatment has been paused after {totalRetries} failed attempts. Update your card and re-enable recurring: {url}";
                    await _smsSender.SendAsync(patient.Phone, sms, ct);
                }
                catch (Exception smsEx) { _logger.LogWarning(smsEx, "Failed to send subscription-ended SMS to patient {PatientId}", patientId); }
            }

            try
            {
                _db.SYS_Notifications.Add(new SYS_Notification
                {
                    NotificationType = "SubscriptionEndedDueToFailure",
                    PatientId = patientId,
                    Description = $"Recurring billing has been paused after {totalRetries} failed payment attempts. Update your card and re-enable recurring: {url}",
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception inAppEx) { _logger.LogWarning(inAppEx, "Failed to write in-app subscription-ended notification for patient {PatientId}", patientId); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending subscription-ended notification to patient {PatientId}", patientId);
        }
    }

    public async Task SendSubscriptionPausedAsync(long patientId, long treatmentId, CancellationToken ct = default)
    {
        await SendSubscriptionStateChangeEmailAndInAppAsync(
            patientId, treatmentId,
            notificationType: "SubscriptionPaused",
            subject: "Your treatment has been paused",
            body: "Your treatment has been paused. Recurring billing is suspended while it's paused, and will resume when the treatment is reactivated.",
            ct).ConfigureAwait(false);
    }

    public async Task SendSubscriptionCancelledAsync(long patientId, long treatmentId, CancellationToken ct = default)
    {
        await SendSubscriptionStateChangeEmailAndInAppAsync(
            patientId, treatmentId,
            notificationType: "SubscriptionCancelled",
            subject: "Your treatment has been cancelled",
            body: "Your treatment has been cancelled. Recurring billing has been stopped. If you would like to continue, please purchase the package again from your dashboard.",
            ct).ConfigureAwait(false);
    }

    private async Task SendSubscriptionStateChangeEmailAndInAppAsync(
        long patientId, long treatmentId, string notificationType, string subject, string body, CancellationToken ct)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);
            if (patient == null) return;

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var url = $"{FrontendBaseUrl}/myTreatments/{treatmentId}";

            if (!string.IsNullOrWhiteSpace(patient.Email))
            {
                var emailModel = new MailTemplateModel
                {
                    ToEmail = patient.Email!.Trim(),
                    ToName = patientName,
                    Subject = subject,
                    PreviewText = subject,
                    Greeting = $"Hi {patient.FirstName},",
                    BodyParagraphs = new List<string> { body },
                    ButtonText = "View My Treatments",
                    ButtonUrl = url,
                    FooterNote = "If you have questions, please contact our support team."
                };
                try { await _mailSender.SendAsync(emailModel, ct); }
                catch (Exception mailEx) { _logger.LogWarning(mailEx, "Failed to send {Type} email to patient {PatientId}", notificationType, patientId); }
            }

            try
            {
                _db.SYS_Notifications.Add(new SYS_Notification
                {
                    NotificationType = notificationType,
                    PatientId = patientId,
                    Description = $"{body} {url}",
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception inAppEx) { _logger.LogWarning(inAppEx, "Failed to write in-app {Type} notification for patient {PatientId}", notificationType, patientId); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending {Type} notification to patient {PatientId}", notificationType, patientId);
        }
    }

    public async Task SendBundleRecurringStatusChangeAsync(long patientId, long treatmentId, long bundleId, bool isNowEnabled, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null)
            {
                _logger.LogWarning("Patient {PatientId} not found for bundle recurring status change notification", patientId);
                return;
            }

            var bundleName = await _db.PD_Bundles
                .AsNoTracking()
                .Where(b => b.BundleId == bundleId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct) ?? "your package";

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var stateWord = isNowEnabled ? "enabled" : "disabled";
            var treatmentUrl = $"{FrontendBaseUrl}/myTreatments/{treatmentId}";

            if (!string.IsNullOrWhiteSpace(patient.Email))
            {
                var emailModel = new MailTemplateModel
                {
                    ToEmail = patient.Email!.Trim(),
                    ToName = patientName,
                    Subject = $"Recurring billing {stateWord} for {bundleName}",
                    PreviewText = $"Your clinic {stateWord} recurring billing on {bundleName}.",
                    Greeting = $"Hi {patient.FirstName},",
                    BodyParagraphs = new List<string>
                    {
                        $"Your clinic has {stateWord} recurring billing for the package \"{bundleName}\", so we've {stateWord} recurring on your treatment to match.",
                        "You can override this choice at any time from your treatment screen — turning recurring on or off as you prefer."
                    },
                    ButtonText = "Manage Recurring on My Treatment",
                    ButtonUrl = treatmentUrl,
                    FooterNote = "If you have any questions about this change, please contact your clinic or our support team."
                };

                try { await _mailSender.SendAsync(emailModel, ct); }
                catch (Exception mailEx)
                {
                    _logger.LogWarning(mailEx, "Failed to send bundle-recurring-status email to patient {PatientId}", patientId);
                }
            }

            try
            {
                _db.SYS_Notifications.Add(new SYS_Notification
                {
                    NotificationType = "BundleRecurringStatusChanged",
                    UserId = null,
                    PatientId = patientId,
                    FacilityId = null,
                    Description =
                        $"Recurring billing has been {stateWord} for \"{bundleName}\" by your clinic. " +
                        $"You can manage recurring manually on your treatment: {treatmentUrl}",
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception inAppEx)
            {
                _logger.LogWarning(inAppEx, "Failed to write in-app bundle-recurring-status notification for patient {PatientId}", patientId);
            }

            _logger.LogInformation("Bundle recurring status change notification dispatched to patient {PatientId} (treatment {TreatmentId}, bundle {BundleId}, nowEnabled={Enabled})", patientId, treatmentId, bundleId, isNowEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending bundle recurring status change notification to patient {PatientId}", patientId);

        }
    }

    public async Task SendInvoicePaidNotificationAsync(long? patientId, long? facilityId, decimal amount, string invoiceNumber, long? adminUserId = null, CancellationToken ct = default)
    {
        try
        {
            if (!facilityId.HasValue || facilityId.Value <= 0)
            {
                _logger.LogWarning("Facility ID is required for invoice paid notification");
                return;
            }

            string? patientName = null;
            if (patientId.HasValue && patientId.Value > 0)
            {
                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.PatientId == patientId.Value)
                    .Select(p => new { p.FirstName, p.LastName })
                    .FirstOrDefaultAsync(ct);

                if (patient != null)
                {
                    patientName = $"{patient.FirstName} {patient.LastName}".Trim();
                }
            }

            var formattedAmount = FormatCurrency(amount);
            var patientInfo = !string.IsNullOrWhiteSpace(patientName) ? $" by {patientName}" : "";

            SYS_UserDetail? specificAdmin = null;

            if (adminUserId.HasValue && adminUserId.Value > 0)
            {

                specificAdmin = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => u.UserId == adminUserId.Value)
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        u => u.LoginId,
                        l => l.LoginId,
                        (u, l) => new { User = u, RoleId = l.RoleId })
                    .Where(x => x.RoleId == 3 && x.User.IsActive == true && x.User.Status == "Active")
                    .Join(_db.FC_UsersInFacilities.AsNoTracking(),
                        x => x.User.UserId,
                        uf => uf.UserId,
                        (x, uf) => new { x.User, uf.FacilityId, uf.IsAssign })
                    .Where(x => x.FacilityId == facilityId.Value && x.IsAssign == true)
                    .Select(x => x.User)
                    .FirstOrDefaultAsync(ct);
            }

            if (specificAdmin == null && !string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var invoice = await _db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber)
                    .FirstOrDefaultAsync(ct);

                if (invoice != null && invoice.CreatedBy.HasValue)
                {

                    specificAdmin = await _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u => u.UserId == invoice.CreatedBy.Value)
                        .Join(_db.SYS_Logins.AsNoTracking(),
                            u => u.LoginId,
                            l => l.LoginId,
                            (u, l) => new { User = u, RoleId = l.RoleId })
                        .Where(x => x.RoleId == 3 && x.User.IsActive == true && x.User.Status == "Active")
                        .Join(_db.FC_UsersInFacilities.AsNoTracking(),
                            x => x.User.UserId,
                            uf => uf.UserId,
                            (x, uf) => new { x.User, uf.FacilityId, uf.IsAssign })
                        .Where(x => x.FacilityId == facilityId.Value && x.IsAssign == true)
                        .Select(x => x.User)
                        .FirstOrDefaultAsync(ct);
                }
            }

            if (specificAdmin == null)
            {
                specificAdmin = await _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == facilityId.Value && uf.IsAssign == true && uf.UserId.HasValue)
                    .Join(_db.SYS_UserDetails.AsNoTracking(),
                        uf => uf.UserId,
                        u => u.UserId,
                        (uf, u) => new { User = u, uf.FacilityId })
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        x => x.User.LoginId,
                        l => l.LoginId,
                        (x, l) => new { x.User, l.RoleId })
                    .Where(x => x.RoleId == 3 && x.User.IsActive == true && x.User.Status == "Active")
                    .Select(x => x.User)
                    .FirstOrDefaultAsync(ct);
            }

            if (specificAdmin == null || string.IsNullOrWhiteSpace(specificAdmin.Email))
            {
                _logger.LogWarning("No clinic admin found for invoice paid notification for facility {FacilityId}", facilityId);
                return;
            }

            long? invoiceId = null;
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var invoice = await _db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber)
                    .Select(i => (long?)i.InvoiceId)
                    .FirstOrDefaultAsync(ct);
                invoiceId = invoice;
            }

            var adminName = $"{specificAdmin.FirstName} {specificAdmin.LastName}".Trim();
            var adminEmail = specificAdmin.Email?.Trim();

            var invoiceIdLine = invoiceId.HasValue
                ? $"Invoice ID: {invoiceId.Value}"
                : $"Invoice Number: {invoiceNumber}";

            var emailModel = new MailTemplateModel
            {
                ToEmail = adminEmail ?? string.Empty,
                ToName = adminName,
                Subject = "Invoice Paid - TelehealthUS",
                PreviewText = $"An invoice of {formattedAmount} has been paid{patientInfo}.",
                Greeting = $"Hi {adminName},",
                BodyParagraphs = new List<string>
                {
                    $"This is to notify you that an invoice has been successfully paid.",
                    invoiceIdLine,
                    $"Amount Paid: {formattedAmount}",
                    !string.IsNullOrWhiteSpace(patientName)
                        ? $"Paid by: {patientName}"
                        : null,
                    "The payment has been processed and your records have been updated accordingly."
                }.Where(p => p != null).Cast<string>().ToList(),
                ButtonText = "View Invoice",
                ButtonUrl = invoiceId.HasValue
                    ? $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoiceId.Value}"
                    : $"{FrontendBaseUrl}/billing/clinicInvoices",
                FooterNote = "This is an automated notification for invoice payments."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Invoice paid notification sent to clinic admin {AdminEmail} for invoice {InvoiceNumber}", adminEmail, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invoice paid notification for invoice {InvoiceNumber}", invoiceNumber);

        }
    }

    public async Task SendInvoiceCancelledNotificationAsync(long invoiceId, string? cancellationReason = null, CancellationToken ct = default)
    {
        try
        {
            if (invoiceId <= 0)
            {
                _logger.LogWarning("Invalid invoice id {InvoiceId} for cancellation notification", invoiceId);
                return;
            }

            var invoiceData = await (from inv in _db.Sys_Invoices.AsNoTracking()
                                     join pat in _db.PT_Patients.AsNoTracking()
                                         on inv.PatientId equals pat.PatientId into patJoin
                                     from pat in patJoin.DefaultIfEmpty()
                                     where inv.InvoiceId == invoiceId
                                     select new
                                     {
                                         inv.InvoiceId,
                                         inv.InvoiceNumber,
                                         inv.Amount,
                                         inv.Status,
                                         inv.PatientId,
                                         PatientFirstName = pat != null ? pat.FirstName : null,
                                         PatientLastName = pat != null ? pat.LastName : null,
                                         PatientEmail = pat != null ? pat.Email : null
                                     }).FirstOrDefaultAsync(ct);

            if (invoiceData == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found for cancellation notification", invoiceId);
                return;
            }

            if (!invoiceData.PatientId.HasValue || string.IsNullOrWhiteSpace(invoiceData.PatientEmail))
            {
                _logger.LogWarning("Invoice {InvoiceId} has no patient email for cancellation notification", invoiceId);
                return;
            }

            var formattedAmount = invoiceData.Amount.HasValue ? FormatCurrency(invoiceData.Amount.Value) : "N/A";
            var patientName = $"{invoiceData.PatientFirstName} {invoiceData.PatientLastName}".Trim();
            if (string.IsNullOrWhiteSpace(patientName))
                patientName = "Patient";

            var invoiceLabel =  invoiceData.InvoiceId.ToString();

            var normalizedReason = string.IsNullOrWhiteSpace(cancellationReason) ? null : cancellationReason.Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = invoiceData.PatientEmail.Trim(),
                ToName = patientName,
                Subject = "Invoice Cancelled - TelehealthUS",
                PreviewText = $"Invoice {invoiceLabel} has been cancelled.",
                Greeting = $"Hi {patientName},",
                BodyParagraphs = new List<string>
                {
                    $"We wanted to let you know that your invoice has been cancelled.",
                    $"Invoice Number: {invoiceLabel}",
                    $"Invoice Amount: {formattedAmount}",
                    $"Status: {(string.IsNullOrWhiteSpace(invoiceData.Status) ? "Cancelled" : invoiceData.Status)}",
                },
                ButtonText = "View Billing",
                ButtonUrl = $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoiceData.InvoiceId}",
                FooterNote = "If you have any questions, please contact support."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Invoice cancellation email sent to patient {PatientId} for invoice {InvoiceId}", invoiceData.PatientId, invoiceData.InvoiceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invoice cancellation notification for invoice {InvoiceId}", invoiceId);

        }
    }

    public async Task SendOrderToPharmacyAsync(long patientId, long orderId, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for order notification", patientId);
                return;
            }

            var order = await _db.PT_PatientOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.PatientOrderId == orderId, ct);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", orderId);
                return;
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var orderTotal = order.OrderPayableAmount.HasValue ? FormatCurrency(order.OrderPayableAmount.Value) : "N/A";

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Your Order Has Been Sent to Pharmacy - TelehealthUS",
                PreviewText = $"Order #{orderId} has been sent to the pharmacy for processing.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Great news! Your order #{orderId} has been successfully sent to the pharmacy.",
                    $"Order Total: {orderTotal}",
                    "The pharmacy will process your order and you'll receive updates as it progresses.",
                    "You'll be notified once your order is confirmed, dispatched, and delivered."
                },
                ButtonText = "Track Order",
                ButtonUrl = $"{FrontendBaseUrl}/order/detail/{orderId}",
                FooterNote = "If you have any questions about your order, please contact our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                var smsMessage = $"TelehealthUS: Your order #{orderId} has been sent to the pharmacy. Total: {orderTotal}. Track at {FrontendBaseUrl}";
                await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
            }

            _logger.LogInformation("Order to pharmacy notification sent to patient {PatientId} for order {OrderId}", patientId, orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending order to pharmacy notification to patient {PatientId} for order {OrderId}", patientId, orderId);

        }
    }

    public async Task SendBillingReminderAsync(long? patientId, long? facilityId, decimal amount, string invoiceNumber, CancellationToken ct = default)
    {
        try
        {
            var recipients = new List<(string Email, string Phone, string Name)>();
            var isClinicToGlobalInvoice = false;

            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var invoiceType = await _db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber)
                    .Select(i => i.InvoiceType)
                    .FirstOrDefaultAsync(ct);

                isClinicToGlobalInvoice = string.Equals(
                    invoiceType,
                    InvoiceType.ClinicToGlobal.ToString(),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!isClinicToGlobalInvoice && patientId.HasValue)
            {
                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PatientId == patientId.Value, ct);

                if (patient != null && !string.IsNullOrWhiteSpace(patient.Email))
                {
                    recipients.Add((
                        patient.Email?.Trim() ?? string.Empty,
                        patient.Phone ?? string.Empty,
                        $"{patient.FirstName} {patient.LastName}".Trim()
                    ));
                }
            }

            if (facilityId.HasValue)
            {
                var clinicAdmins = await _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == facilityId.Value && uf.IsAssign == true && uf.UserId.HasValue)
                    .Join(_db.SYS_UserDetails.AsNoTracking(),
                        uf => uf.UserId,
                        u => u.UserId,
                        (uf, u) => new { u.UserId, u.LoginId, u.Email, u.Phone, u.FirstName, u.LastName, u.IsActive, u.Status })
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        x => x.LoginId,
                        l => l.LoginId,
                        (x, l) => new { x, l.RoleId })
                    .Where(x => x.RoleId == (int)UserRole.ClinicAdmin &&
                                x.x.IsActive == true &&
                                x.x.Status == "Active" &&
                                !string.IsNullOrWhiteSpace(x.x.Email))
                    .Select(x => new { x.x.Email, x.x.Phone, x.x.FirstName, x.x.LastName })
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var admin in clinicAdmins)
                {
                    recipients.Add((
                        admin.Email?.Trim() ?? string.Empty,
                        admin.Phone ?? string.Empty,
                        $"{admin.FirstName} {admin.LastName}".Trim()
                    ));
                }
            }

            var formattedAmount = FormatCurrency(amount);

            long? invoiceId = null;
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var invoice = await _db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber)
                    .Select(i => (long?)i.InvoiceId)
                    .FirstOrDefaultAsync(ct);
                invoiceId = invoice;
            }

            foreach (var recipient in recipients)
            {
                try
                {

                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = "Billing Reminder - Payment Due - TelehealthUS",
                        PreviewText = $"Reminder: Payment of {formattedAmount} is due for invoice {invoiceNumber}.",
                        Greeting = $"Hi {recipient.Name},",
                        BodyParagraphs = new List<string>
                        {
                            $"This is a friendly reminder that a payment of {formattedAmount} is due.",
                            $"Invoice Number: {invoiceNumber}",
                            "Please make the payment at your earliest convenience to avoid any service interruptions.",
                            "If you've already made this payment, please disregard this reminder."
                        },
                        ButtonText = "Make Payment",
                        ButtonUrl = invoiceId.HasValue
                            ? $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoiceId.Value}"
                            : $"{FrontendBaseUrl}/billing/clinicInvoices",
                        FooterNote = "If you have any questions about this invoice, please contact our billing department."
                    };

                    await _mailSender.SendAsync(emailModel, ct);

                    if (!isClinicToGlobalInvoice && !string.IsNullOrWhiteSpace(recipient.Phone))
                    {
                        var smsMessage = $"TelehealthUS: Payment reminder - {formattedAmount} due for invoice {invoiceNumber}. Pay at {FrontendBaseUrl}";
                        await _smsSender.SendAsync(recipient.Phone, smsMessage, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending billing reminder to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("Billing reminder sent to {Count} recipients for invoice {InvoiceNumber}", recipients.Count, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending billing reminder for invoice {InvoiceNumber}", invoiceNumber);

        }
    }

    public async Task SendOverdueClinicBillAsync(long facilityId, decimal amount, string invoiceNumber, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null)
            {
                _logger.LogWarning("Facility {FacilityId} not found for overdue bill notification", facilityId);
                return;
            }

            var formattedAmount = FormatCurrency(amount);
            var facilityName = facility.TitleShort ?? facility.TitleLong ?? "Facility";

            long? invoiceId = null;
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var invoice = await _db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber)
                    .Select(i => (long?)i.InvoiceId)
                    .FirstOrDefaultAsync(ct);
                invoiceId = invoice;
            }

            var clinicAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId && uf.IsAssign == true && uf.UserId.HasValue)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { u.UserId, u.LoginId, u.Email, u.FirstName, u.LastName, u.IsActive, u.Status })
                .Join(_db.SYS_Logins.AsNoTracking(),
                    x => x.LoginId,
                    l => l.LoginId,
                    (x, l) => new { x, l.RoleId })
                .Where(x => x.RoleId == (int)UserRole.ClinicAdmin &&
                            x.x.IsActive == true &&
                            x.x.Status == "Active" &&
                            !string.IsNullOrWhiteSpace(x.x.Email))
                .Select(x => new { x.x.UserId, x.x.Email, x.x.FirstName, x.x.LastName })
                .Distinct()
                .ToListAsync(ct);

            foreach (var admin in clinicAdmins)
            {
                try
                {
                    var adminName = $"{admin.FirstName} {admin.LastName}".Trim();
                    var adminEmailModel = new MailTemplateModel
                    {
                        ToEmail = admin.Email?.Trim(),
                        ToName = adminName,
                        Subject = $"Overdue Bill Alert: {facilityName} - TelehealthUS",
                        PreviewText = $"Facility {facilityName} has an overdue bill of {formattedAmount}.",
                        Greeting = $"Hi {adminName},",
                        BodyParagraphs = new List<string>
                        {
                            $"This is to notify you that {facilityName} has an overdue monthly bill.",
                            $"Invoice Number: {invoiceNumber}",
                            $"Amount Due: {formattedAmount}",
                            "Please follow up with the facility to ensure payment is made promptly."
                        },
                        ButtonText = "View Invoice",
                        ButtonUrl = invoiceId.HasValue
                            ? $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoiceId.Value}"
                            : $"{FrontendBaseUrl}/billing/clinicInvoices",
                        FooterNote = "This is an automated alert for administrative purposes."
                    };

                    await _mailSender.SendAsync(adminEmailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending overdue bill alert to Clinic Admin {Email}", admin.Email);

                }
            }

            _logger.LogInformation("Overdue clinic bill notification sent for facility {FacilityId}, invoice {InvoiceNumber}", facilityId, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending overdue clinic bill notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendPatientSignUpAsync(long patientId, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for sign-up notification", patientId);
                return;
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Welcome to TelehealthUS!",
                PreviewText = "Thank you for joining TelehealthUS. We're excited to have you!",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    "Welcome to TelehealthUS! We're thrilled to have you as part of our community.",
                    "Your account has been successfully created and you can now access all our services.",
                    "Here's what you can do next:",
                    "• Complete your profile",
                    "• Schedule appointments with our providers",
                    "• Browse available treatments",
                    "• Access your health records"
                },
                ButtonText = "Get Started",
                ButtonUrl = $"{FrontendBaseUrl}/dashboard/patient",
                FooterNote = "If you have any questions, our support team is here to help. Welcome aboard!"
            };

            await _mailSender.SendAsync(emailModel, ct);

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                var smsMessage = $"Welcome to TelehealthUS, {patient.FirstName}! Your account is ready. Visit {FrontendBaseUrl} to get started.";
                await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
            }

            _logger.LogInformation("Patient sign-up notification sent to patient {PatientId}", patientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending patient sign-up notification to patient {PatientId}", patientId);

        }
    }

    public async Task SendManualPatientWelcomeWithPasswordAsync(long patientId, string temporaryPassword, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for manual welcome notification", patientId);
                return;
            }

            string? clinicName = null;
            if (patient.FacilityId.HasValue)
            {
                var facility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.FacilityId == patient.FacilityId.Value)
                    .Select(f => new { f.TitleLong, f.TitleShort })
                    .FirstOrDefaultAsync(ct);
                clinicName = !string.IsNullOrWhiteSpace(facility?.TitleLong) ? facility.TitleLong : facility?.TitleShort;
            }
            var clinicText = !string.IsNullOrWhiteSpace(clinicName)
                ? $" by {clinicName.Trim()}"
                : " by your administrator";

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var loginUrl = $"{FrontendBaseUrl}/login";

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email.Trim(),
                ToName = patientName,
                Subject = "Your TelehealthUS Patient Account",
                PreviewText = "Your account has been created. Use the temporary password below to sign in.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Your patient account has been created{clinicText}.",
                    $"Your temporary password is: {temporaryPassword}",
                    "Please sign in to the patient portal and change your password after your first login.",
                    "Keep this password secure and do not share it with anyone."
                },
                ButtonText = "Sign In",
                ButtonUrl = loginUrl,
                FooterNote = "If you did not expect this email, please contact your clinic or administrator."
            };

            await _mailSender.SendAsync(emailModel, ct);
            _logger.LogInformation("Manual patient welcome (with temp password) sent to patient {PatientId}", patientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending manual patient welcome to patient {PatientId}", patientId);

        }
    }

    public async Task SendPasswordResetSmsAsync(string email, string resetCode, CancellationToken ct = default)
    {
        try
        {

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == email.ToLower(), ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Phone))
            {
                _logger.LogWarning("Patient with email {Email} not found or has no phone for password reset SMS", email);
                return;
            }

            var smsMessage = $"TelehealthUS: Your password reset code is {resetCode}. This code expires in 10 minutes. Do not share this code with anyone.";
            await _smsSender.SendAsync(patient.Phone, smsMessage, ct);

            _logger.LogInformation("Password reset SMS sent to patient with email {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset SMS to {Email}", email);

        }
    }

    public async Task SendAppointmentConfirmationAsync(long appointmentId, bool isPatient, CancellationToken ct = default)
    {
        try
        {
            var appointment = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId, ct);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found", appointmentId);
                return;
            }

            if (isPatient)
            {

                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId, ct);

                if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
                {
                    _logger.LogWarning("Patient {PatientId} not found or has no email for appointment confirmation", appointment.PatientId);
                    return;
                }

                var provider = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == appointment.ProviderId, ct);

                var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "Your Provider";

                string appointmentDate = "TBD";
                string appointmentTimeWithTz = "TBD";
                if (appointment.StartDate.HasValue)
                {
                    var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);
                    var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);
                    appointmentDate = appointmentDateTimeLocal.ToString("MMMM dd, yyyy");
                    var offset = TimeZoneInfo.Local.GetUtcOffset(appointmentDateTimeLocal);
                    var tzSuffix = $"(UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset:hh\\:mm})";
                    appointmentTimeWithTz = appointmentDateTimeLocal.ToString(@"hh\:mm tt") + " " + tzSuffix;
                }
                var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

                var emailModel = new MailTemplateModel
                {
                    ToEmail = patient.Email?.Trim(),
                    ToName = patientName,
                    Subject = "Appointment Confirmed - TelehealthUS",
                    PreviewText = $"Your appointment with {providerName} on {appointmentDate} at {appointmentTimeWithTz} has been confirmed.",
                    Greeting = $"Hi {patient.FirstName},",
                    BodyParagraphs = new List<string>
                    {
                        $"Your appointment with {providerName} has been confirmed.",
                        $"Date: {appointmentDate}",
                        $"Time: {appointmentTimeWithTz}",
                        "Please arrive on time for your appointment. If you need to reschedule or cancel, please do so at least 24 hours in advance."
                    },
                    ButtonText = "View Appointment",
                    ButtonUrl = $"{FrontendBaseUrl}/schedule/appointment/details/{appointment.PatientAppointmentSlotId}",
                    FooterNote = appointment.ZoomJoinUrl != null
                        ? $"Your video appointment link: {appointment.ZoomJoinUrl}"
                        : "If you have any questions, please contact our support team."
                };

                await _mailSender.SendAsync(emailModel, ct);

                if (!string.IsNullOrWhiteSpace(patient.Phone))
                {
                    var smsMessage = $"TelehealthUS: Appointment confirmed with {providerName} on {appointmentDate} at {appointmentTimeWithTz}. See details at {FrontendBaseUrl}";
                    await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
                }
            }
            else
            {

                var provider = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == appointment.ProviderId, ct);

                if (provider == null || string.IsNullOrWhiteSpace(provider.Email))
                {
                    _logger.LogWarning("Provider {ProviderId} not found or has no email for appointment confirmation", appointment.ProviderId);
                    return;
                }

                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId, ct);

                var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}".Trim() : "Patient";

                string appointmentDateProvider = "TBD";
                string appointmentTimeWithTzProvider = "TBD";
                if (appointment.StartDate.HasValue)
                {
                    var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);
                    var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);
                    appointmentDateProvider = appointmentDateTimeLocal.ToString("MMMM dd, yyyy");
                    var offset = TimeZoneInfo.Local.GetUtcOffset(appointmentDateTimeLocal);
                    var tzSuffix = $"(UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset:hh\\:mm})";
                    appointmentTimeWithTzProvider = appointmentDateTimeLocal.ToString(@"hh\:mm tt") + " " + tzSuffix;
                }
                var providerName = $"{provider.FirstName} {provider.LastName}".Trim();

                var emailModel = new MailTemplateModel
                {
                    ToEmail = provider.Email?.Trim(),
                    ToName = providerName,
                    Subject = "Appointment Confirmed - TelehealthUS",
                    PreviewText = $"Your appointment with {patientName} on {appointmentDateProvider} at {appointmentTimeWithTzProvider} has been confirmed.",
                    Greeting = $"Hi {provider.FirstName},",
                    BodyParagraphs = new List<string>
                    {
                        $"Your appointment with {patientName} has been confirmed.",
                        $"Date: {appointmentDateProvider}",
                        $"Time: {appointmentTimeWithTzProvider}",
                        "Please ensure you're available for this appointment. The patient will be notified separately."
                    },
                    ButtonText = "View Appointment",
                    ButtonUrl = $"{FrontendBaseUrl}/schedule/appointment/details/{appointment.PatientAppointmentSlotId}",
                    FooterNote = appointment.ZoomJoinUrl != null
                        ? $"Video appointment link: {appointment.ZoomJoinUrl}"
                        : "If you need to make any changes, please contact support."
                };

                await _mailSender.SendAsync(emailModel, ct);
            }

            _logger.LogInformation("Appointment confirmation sent for appointment {AppointmentId}, isPatient: {IsPatient}", appointmentId, isPatient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending appointment confirmation for appointment {AppointmentId}", appointmentId);

        }
    }

    public async Task SendAppointmentConfirmationToClinicAdminAsync(long appointmentId, long? adminUserId = null, CancellationToken ct = default)
    {
        try
        {
            var appointment = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId, ct);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found for clinic admin notification", appointmentId);
                return;
            }

            if (!appointment.FacilityId.HasValue || appointment.FacilityId.Value <= 0)
            {
                _logger.LogWarning("Facility ID is missing for appointment {AppointmentId}", appointmentId);
                return;
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .Where(p => p.PatientId == appointment.PatientId)
                .Select(p => new { p.FirstName, p.LastName })
                .FirstOrDefaultAsync(ct);

            var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}".Trim() : "Patient";

            var provider = await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.UserId == appointment.ProviderId)
                .Select(u => new { u.FirstName, u.LastName })
                .FirstOrDefaultAsync(ct);

            var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "Provider";

            string appointmentDateAdmin = "TBD";
            string appointmentTimeWithTzAdmin = "TBD";
            if (appointment.StartDate.HasValue)
            {
                var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);
                var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);
                appointmentDateAdmin = appointmentDateTimeLocal.ToString("MMMM dd, yyyy");
                var offset = TimeZoneInfo.Local.GetUtcOffset(appointmentDateTimeLocal);
                var tzSuffix = $"(UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset:hh\\:mm})";
                appointmentTimeWithTzAdmin = appointmentDateTimeLocal.ToString(@"hh\:mm tt") + " " + tzSuffix;
            }

            SYS_UserDetail? specificAdmin = null;

            if (adminUserId.HasValue && adminUserId.Value > 0)
            {

                specificAdmin = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => u.UserId == adminUserId.Value)
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        u => u.LoginId,
                        l => l.LoginId,
                        (u, l) => new { User = u, RoleId = l.RoleId })
                    .Where(x => x.RoleId == 3 && x.User.IsActive == true && x.User.Status == "Active")
                    .Join(_db.FC_UsersInFacilities.AsNoTracking(),
                        x => x.User.UserId,
                        uf => uf.UserId,
                        (x, uf) => new { x.User, uf.FacilityId, uf.IsAssign })
                    .Where(x => x.FacilityId == appointment.FacilityId.Value && x.IsAssign == true)
                    .Select(x => x.User)
                    .FirstOrDefaultAsync(ct);
            }

            if (specificAdmin == null)
            {
                specificAdmin = await _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == appointment.FacilityId.Value && uf.IsAssign == true && uf.UserId.HasValue)
                    .Join(_db.SYS_UserDetails.AsNoTracking(),
                        uf => uf.UserId,
                        u => u.UserId,
                        (uf, u) => new { User = u, uf.FacilityId })
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        x => x.User.LoginId,
                        l => l.LoginId,
                        (x, l) => new { x.User, l.RoleId })
                    .Where(x => x.RoleId == 3 && x.User.IsActive == true && x.User.Status == "Active")
                    .Select(x => x.User)
                    .FirstOrDefaultAsync(ct);
            }

            if (specificAdmin == null || string.IsNullOrWhiteSpace(specificAdmin.Email))
            {
                _logger.LogWarning("No clinic admin found for appointment confirmation notification for appointment {AppointmentId}", appointmentId);
                return;
            }

            var adminName = $"{specificAdmin.FirstName} {specificAdmin.LastName}".Trim();
            var adminEmail = specificAdmin.Email?.Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = adminEmail ?? string.Empty,
                ToName = adminName,
                Subject = "New Appointment Confirmed - TelehealthUS",
                PreviewText = $"A new appointment has been confirmed: {patientName} with {providerName} on {appointmentDateAdmin} at {appointmentTimeWithTzAdmin}.",
                Greeting = $"Hi {adminName},",
                BodyParagraphs = new List<string>
                {
                    $"A new appointment has been confirmed for your facility:",
                    $"Patient: {patientName}",
                    $"Provider: {providerName}",
                    $"Date: {appointmentDateAdmin}",
                    $"Time: {appointmentTimeWithTzAdmin}",
                    "The appointment has been successfully scheduled and both the patient and provider have been notified."
                },
                ButtonText = "View Appointment",
                ButtonUrl = $"{FrontendBaseUrl}/schedule/appointment/details/{appointmentId}",
                FooterNote = appointment.ZoomJoinUrl != null
                    ? $"Video appointment link: {appointment.ZoomJoinUrl}"
                    : "This is an automated notification for appointment confirmations."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Appointment confirmation sent to clinic admin {AdminEmail} for appointment {AppointmentId}", adminEmail, appointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending appointment confirmation to clinic admin for appointment {AppointmentId}", appointmentId);

        }
    }

    public async Task SendAppointmentCancellationAsync(long appointmentId, bool isPatient, CancellationToken ct = default)
    {
        try
        {
            var appointment = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId, ct);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found", appointmentId);
                return;
            }

            if (isPatient)
            {

                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId, ct);

                if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
                {
                    _logger.LogWarning("Patient {PatientId} not found or has no email for appointment cancellation", appointment.PatientId);
                    return;
                }

                var provider = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == appointment.ProviderId, ct);

                var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "Your Provider";

                string appointmentDate = "TBD";
                string appointmentTime = "TBD";
                if (appointment.StartDate.HasValue)
                {

                    var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);

                    var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);

                    appointmentDate = appointmentDateTimeLocal.ToString("MMMM dd, yyyy");
                    appointmentTime = appointmentDateTimeLocal.ToString(@"hh\:mm tt");
                }

                var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

                var emailModel = new MailTemplateModel
                {
                    ToEmail = patient.Email?.Trim(),
                    ToName = patientName,
                    Subject = "Appointment Cancelled - TelehealthUS",
                    PreviewText = $"Your appointment with {providerName} on {appointmentDate} has been cancelled.",
                    Greeting = $"Hi {patient.FirstName},",
                    BodyParagraphs = new List<string>
                    {
                        $"We're writing to inform you that your appointment with {providerName} has been cancelled.",
                        $"Original Date: {appointmentDate}",
                        $"Original Time: {appointmentTime}",
                        "Please contact the clinic if you need help scheduling another appointment."
                    },
                    ButtonText = null,
                    ButtonUrl = null,
                    FooterNote = "If you have any questions or concerns, please contact our support team."
                };

                await _mailSender.SendAsync(emailModel, ct);
            }
            else
            {

                var provider = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == appointment.ProviderId, ct);

                if (provider == null || string.IsNullOrWhiteSpace(provider.Email))
                {
                    _logger.LogWarning("Provider {ProviderId} not found or has no email for appointment cancellation", appointment.ProviderId);
                    return;
                }

                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId, ct);

                var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}".Trim() : "Patient";

                string appointmentDate = "TBD";
                string appointmentTime = "TBD";
                if (appointment.StartDate.HasValue)
                {

                    var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);

                    var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);

                    appointmentDate = appointmentDateTimeLocal.ToString("MMMM dd, yyyy");
                    appointmentTime = appointmentDateTimeLocal.ToString(@"hh\:mm tt");
                }

                var providerName = $"{provider.FirstName} {provider.LastName}".Trim();

                var emailModel = new MailTemplateModel
                {
                    ToEmail = provider.Email?.Trim(),
                    ToName = providerName,
                    Subject = "Appointment Cancelled - TelehealthUS",
                    PreviewText = $"Your appointment with {patientName} on {appointmentDate} has been cancelled.",
                    Greeting = $"Hi {provider.FirstName},",
                    BodyParagraphs = new List<string>
                    {
                        $"We're writing to inform you that your appointment with {patientName} has been cancelled.",
                        $"Original Date: {appointmentDate}",
                        $"Original Time: {appointmentTime}",
                        "The patient has been notified separately. This time slot is now available."
                    },
                    ButtonText = "View Schedule",
                    ButtonUrl = $"{FrontendBaseUrl}/schedule/calendar",
                    FooterNote = "If you have any questions, please contact support."
                };

                await _mailSender.SendAsync(emailModel, ct);
            }

            _logger.LogInformation("Appointment cancellation sent for appointment {AppointmentId}, isPatient: {IsPatient}", appointmentId, isPatient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending appointment cancellation for appointment {AppointmentId}", appointmentId);

        }
    }

    public async Task SendAppointmentCancellationToClinicAdminAsync(long appointmentId, long? adminUserId = null, CancellationToken ct = default)
    {
        try
        {
            var appointment = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId, ct);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found for clinic admin cancellation notification", appointmentId);
                return;
            }

            if (!appointment.FacilityId.HasValue || appointment.FacilityId.Value <= 0)
            {
                _logger.LogWarning("Appointment {AppointmentId} has no associated facility for clinic admin notification", appointmentId);
                return;
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId, ct);

            var provider = await _db.SYS_UserDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == appointment.ProviderId, ct);

            var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}".Trim() : "A patient";
            var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "A provider";

            string appointmentDate = "TBD";
            string appointmentTime = "TBD";
            if (appointment.StartDate.HasValue)
            {

                var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);

                var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);

                appointmentDate = appointmentDateTimeLocal.ToString("MMMM dd, yyyy");
                appointmentTime = appointmentDateTimeLocal.ToString(@"hh\:mm");
            }

            SYS_UserDetail? specificAdmin = null;

            if (adminUserId.HasValue && adminUserId.Value > 0)
            {

                specificAdmin = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => u.UserId == adminUserId.Value)
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        u => u.LoginId,
                        l => l.LoginId,
                        (u, l) => new { User = u, RoleId = l.RoleId })
                    .Where(x => x.RoleId == (int)UserRole.ClinicAdmin && x.User.IsActive == true && x.User.Status == "Active")
                    .Join(_db.FC_UsersInFacilities.AsNoTracking(),
                        x => x.User.UserId,
                        uf => uf.UserId,
                        (x, uf) => new { x.User, uf.FacilityId, uf.IsAssign })
                    .Where(x => x.FacilityId == appointment.FacilityId.Value && x.IsAssign == true)
                    .Select(x => x.User)
                    .FirstOrDefaultAsync(ct);
            }

            if (specificAdmin == null)
            {
                specificAdmin = await _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == appointment.FacilityId.Value && uf.IsAssign == true && uf.UserId.HasValue)
                    .Join(_db.SYS_UserDetails.AsNoTracking(),
                        uf => uf.UserId,
                        u => u.UserId,
                        (uf, u) => new { User = u, uf.FacilityId })
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        x => x.User.LoginId,
                        l => l.LoginId,
                        (x, l) => new { x.User, l.RoleId })
                    .Where(x => x.RoleId == (int)UserRole.ClinicAdmin && x.User.IsActive == true && x.User.Status == "Active")
                    .Select(x => x.User)
                    .FirstOrDefaultAsync(ct);
            }

            if (specificAdmin == null || string.IsNullOrWhiteSpace(specificAdmin.Email))
            {
                _logger.LogWarning("No clinic admin found for appointment cancellation notification for facility {FacilityId}", appointment.FacilityId);
                return;
            }

            var adminName = $"{specificAdmin.FirstName} {specificAdmin.LastName}".Trim();
            var adminEmail = specificAdmin.Email?.Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = adminEmail ?? string.Empty,
                ToName = adminName,
                Subject = "Appointment Cancelled - TelehealthUS",
                PreviewText = $"An appointment has been cancelled: {patientName} with {providerName} on {appointmentDate}.",
                Greeting = $"Hi {adminName},",
                BodyParagraphs = new List<string>
                {
                    $"An appointment in your facility has been cancelled:",
                    $"Patient: {patientName}",
                    $"Provider: {providerName}",
                    $"Date: {appointmentDate}",
                    $"Time: {appointmentTime}",
                    "The patient and provider have been notified separately. This time slot is now available."
                },
                ButtonText = "View Appointments",
                ButtonUrl = $"{FrontendBaseUrl}/schedule/calendar",
                FooterNote = "This is an automated notification for appointment cancellations."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Appointment cancellation sent to clinic admin {AdminEmail} for appointment {AppointmentId}", adminEmail, appointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending appointment cancellation to clinic admin for appointment {AppointmentId}", appointmentId);

        }
    }

    public async Task SendTreatmentSelectionAsync(long patientId, long treatmentId, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for treatment selection notification", patientId);
                return;
            }

            var treatment = await _db.PT_PatientTreatments
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.PatientTreatmentId == treatmentId, ct);

            if (treatment == null)
            {
                _logger.LogWarning("Treatment {TreatmentId} not found", treatmentId);
                return;
            }

            var product = await _db.PD_Bundles
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BundleId == treatment.ProductId, ct);

            var productName = product?.Name ?? "Treatment";
            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "New Treatment Selected - TelehealthUS",
                PreviewText = $"You've selected a new treatment: {productName}",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Thank you for selecting {productName} as your treatment.",
                    "Your treatment plan has been set up and you'll receive updates as it progresses.",
                    "Next steps:",
                    "• Complete any required questionnaires",
                    "• Schedule your appointments",
                    "• Review your treatment details"
                },
                ButtonText = "View Treatment",
                ButtonUrl = $"{FrontendBaseUrl}/treatment/detail/{treatmentId}",
                FooterNote = "If you have any questions about your treatment, please contact your provider or support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                var smsMessage = $"TelehealthUS: You've selected {productName} as your treatment. View details at {FrontendBaseUrl}";
                await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
            }

            _logger.LogInformation("Treatment selection notification sent to patient {PatientId} for treatment {TreatmentId}", patientId, treatmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending treatment selection notification to patient {PatientId}", patientId);

        }
    }

    public async Task SendOrderStatusUpdateAsync(long patientId, long orderId, string status, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for order status update", patientId);
                return;
            }

            var order = await _db.PT_PatientOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.PatientOrderId == orderId, ct);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", orderId);
                return;
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var statusMessage = status switch
            {
                "Sent" => "has been sent",
                "Confirmed" => "has been confirmed",
                "Dispatched" => "has been dispatched",
                "Delivered" => "has been delivered",
                "Cancelled" => "has been cancelled",
                _ => $"status has been updated to {status}"
            };

            var subject = status switch
            {
                "Sent" => "Order Sent - TelehealthUS",
                "Confirmed" => "Order Confirmed - TelehealthUS",
                "Dispatched" => "Order Dispatched - TelehealthUS",
                "Delivered" => "Order Delivered - TelehealthUS",
                "Cancelled" => "Order Cancelled - TelehealthUS",
                _ => "Order Status Updated - TelehealthUS"
            };

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = subject,
                PreviewText = $"Your order #{orderId} {statusMessage}.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Your order #{orderId} {statusMessage}.",
                    status == "Dispatched" && !string.IsNullOrWhiteSpace(order.TrackingNumber)
                        ? $"Tracking Number: {order.TrackingNumber}"
                        : null,
                    status == "Delivered"
                        ? "Your order has been successfully delivered. We hope you're satisfied with your purchase!"
                        : status == "Cancelled"
                        ? "If you have any questions about this cancellation, please contact our support team."
                        : "You'll receive further updates as your order progresses.",
                }.Where(p => p != null).Cast<string>().ToList(),
                ButtonText = "Track Order",
                ButtonUrl = $"{FrontendBaseUrl}/order/detail/{orderId}",
                FooterNote = "If you have any questions about your order, please contact our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                var trackingInfo = status == "Dispatched" && !string.IsNullOrWhiteSpace(order.TrackingNumber)
                    ? $" Tracking: {order.TrackingNumber}."
                    : "";
                var smsMessage = $"TelehealthUS: Order #{orderId} {statusMessage}.{trackingInfo} Details: {FrontendBaseUrl}/order/detail/{orderId}";
                await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
            }

            _logger.LogInformation("Order status update sent to patient {PatientId} for order {OrderId}, status: {Status}", patientId, orderId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending order status update to patient {PatientId} for order {OrderId}", patientId, orderId);

        }
    }

    public async Task SendRefillNotificationAsync(long patientId, long treatmentId, CancellationToken ct = default)
    {
        try
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for refill notification", patientId);
                return;
            }

            var treatment = await _db.PT_PatientTreatments
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.PatientTreatmentId == treatmentId, ct);

            if (treatment == null)
            {
                _logger.LogWarning("Treatment {TreatmentId} not found", treatmentId);
                return;
            }

            var product = await _db.PD_Bundles
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BundleId == treatment.ProductId, ct);

            var productName = product?.Name ?? "Treatment";
            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Refill Reminder - TelehealthUS",
                PreviewText = $"It's time to refill your {productName} prescription.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"This is a reminder that it's time to refill your {productName} prescription.",
                    "To ensure continuity of your treatment, please place your refill order as soon as possible.",
                    "You can easily order your refill through your dashboard or contact your provider if you have any questions."
                },
                ButtonText = "Order Refill",
                ButtonUrl = $"{FrontendBaseUrl}/treatment/detail/{treatmentId}",
                FooterNote = "If you no longer need this medication, please contact your provider to update your treatment plan."
            };

            await _mailSender.SendAsync(emailModel, ct);

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                var smsMessage = $"TelehealthUS: Time to refill your {productName} prescription. Order at {FrontendBaseUrl}";
                await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
            }

            _logger.LogInformation("Refill notification sent to patient {PatientId} for treatment {TreatmentId}", patientId, treatmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending refill notification to patient {PatientId}", patientId);

        }
    }

    public async Task SendClinicSignUpAsync(long facilityId, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null || string.IsNullOrWhiteSpace(facility.Email))
            {
                _logger.LogWarning("Facility {FacilityId} not found or has no email for sign-up notification", facilityId);
                return;
            }

            var facilityName = facility.TitleShort ?? facility.TitleLong ?? "Facility";

            var emailModel = new MailTemplateModel
            {
                ToEmail = facility.Email?.Trim(),
                ToName = facilityName,
                Subject = "Welcome to TelehealthUS - Clinic Account Created",
                PreviewText = "Your clinic account has been successfully created. Welcome to TelehealthUS!",
                Greeting = $"Dear {facilityName},",
                BodyParagraphs = new List<string>
                {
                    "Welcome to TelehealthUS! Your clinic account has been successfully created.",
                    "You can now access all the features and services available to clinics:",
                    "• Manage your staff and providers",
                    "• View patient records and appointments",
                    "• Process orders and prescriptions",
                    "• Access analytics and reports",
                    "• Manage billing and invoices"
                },
                ButtonText = "Access Dashboard",
                ButtonUrl = $"{FrontendBaseUrl}/dashboard",
                FooterNote = "If you have any questions or need assistance setting up your account, please contact our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Clinic sign-up notification sent to facility {FacilityId}", facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending clinic sign-up notification to facility {FacilityId}", facilityId);

        }
    }

    public async Task SendDoctorSignUpAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var doctor = await _db.SYS_UserDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive == true, ct);

            if (doctor == null || string.IsNullOrWhiteSpace(doctor.Email))
            {
                _logger.LogWarning("Doctor {UserId} not found, inactive, or has no email for sign-up notification", userId);
                return;
            }

            var doctorName = $"{doctor.FirstName} {doctor.LastName}".Trim();
            var title = !string.IsNullOrWhiteSpace(doctor.Title) ? doctor.Title : "Doctor";

            var emailModel = new MailTemplateModel
            {
                ToEmail = doctor.Email?.Trim(),
                ToName = doctorName,
                Subject = "Welcome to TelehealthUS - Provider Account Created",
                PreviewText = "Your provider account has been successfully created. Welcome to TelehealthUS!",
                Greeting = $"Hi {doctor.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Welcome to TelehealthUS, {title} {doctor.LastName}! Your provider account has been successfully created.",
                    "As a provider on our platform, you can:",
                    "• Manage your schedule and availability",
                    "• View and confirm patient appointments",
                    "• Upload and manage prescriptions",
                    "• Access patient records and treatment plans",
                    "• View your analytics and earnings"
                },
                ButtonText = "Access Dashboard",
                ButtonUrl = $"{FrontendBaseUrl}/dashboard/provider",
                FooterNote = "If you have any questions or need assistance, please contact our support team. We're here to help!"
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Doctor sign-up notification sent to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending doctor sign-up notification to user {UserId}", userId);

        }
    }

    public async Task SendClinicAdminSignUpAsync(long userId, long? facilityId, CancellationToken ct = default)
    {
        try
        {
            var admin = await _db.SYS_UserDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive == true, ct);

            if (admin == null || string.IsNullOrWhiteSpace(admin.Email))
            {
                _logger.LogWarning("Clinic Admin {UserId} not found, inactive, or has no email for sign-up notification", userId);
                return;
            }

            var adminName = $"{admin.FirstName} {admin.LastName}".Trim();
            string? facilityName = null;

            if (facilityId.HasValue && facilityId.Value > 0)
            {
                var facility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FacilityId == facilityId.Value, ct);
                facilityName = facility != null ? (facility.TitleShort ?? facility.TitleLong) : null;
            }

            var emailModel = new MailTemplateModel
            {
                ToEmail = admin.Email?.Trim(),
                ToName = adminName,
                Subject = "Welcome to TelehealthUS - Clinic Admin Account Created",
                PreviewText = "You have been added as a clinic admin. Welcome to TelehealthUS!",
                Greeting = $"Hi {admin.FirstName},",
                BodyParagraphs = new List<string>
                {
                    !string.IsNullOrWhiteSpace(facilityName)
                        ? $"You have been added as a clinic admin for {facilityName}. Welcome to TelehealthUS!"
                        : "You have been added as a clinic admin. Welcome to TelehealthUS!",
                    "As a clinic admin, you can:",
                    "- Manage your clinic's staff.",
                    "- View patient records and appointments",
                    "- Access clinic analytics and reports",
                    "- Manage clinic settings and preferences"
                },
                ButtonText = "Access Dashboard",
                ButtonUrl = $"{FrontendBaseUrl}/dashboard/clinic",
                FooterNote = "If you have any questions or need assistance, please contact our support team. We're here to help!"
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Clinic admin sign-up notification sent to user {UserId} for facility {FacilityId}", userId, facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending clinic admin sign-up notification to user {UserId}", userId);

        }
    }

    public async Task SendPrescriptionUploadedAsync(long prescriptionId, CancellationToken ct = default)
    {
        try
        {
            var prescription = await _db.PT_PatientPrescriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientPrescriptionId == prescriptionId, ct);

            if (prescription == null)
            {
                _logger.LogWarning("Prescription {PrescriptionId} not found", prescriptionId);
                return;
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == prescription.PatientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for prescription upload notification", prescription.PatientId);
                return;
            }

            var provider = prescription.ProviderId.HasValue
                ? await _db.SYS_UserDetails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == prescription.ProviderId.Value, ct)
                : null;

            var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "Your Provider";
            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

            DateTime? dateToUse = prescription.PrescritionDate ?? prescription.CreatedDate;
            var prescriptionDate = dateToUse.HasValue
                ? CommonMethods.ToLocalTime(dateToUse.Value).ToString("MMMM dd, yyyy")
                : CommonMethods.ToLocalTime(DateTime.UtcNow).ToString("MMMM dd, yyyy");

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Prescription Uploaded - TelehealthUS",
                PreviewText = $"Your prescription has been uploaded by {providerName}.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Your prescription has been uploaded by {providerName} on {prescriptionDate}.",
                    "The prescription is now being processed and will be sent to the pharmacy soon.",
                    "You'll receive updates as your prescription progresses through the system."
                },
                    ButtonText = "View Prescription",
                    ButtonUrl = $"{FrontendBaseUrl}/prescription/detail/{prescriptionId}",
                    FooterNote = "If you have any questions about your prescription, please contact your provider or our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Prescription uploaded notification sent for prescription {PrescriptionId}", prescriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prescription uploaded notification for prescription {PrescriptionId}", prescriptionId);

        }
    }

    public async Task SendPrescriptionCreatedAsync(long prescriptionId, CancellationToken ct = default)
    {
        try
        {
            var prescription = await _db.PT_PatientPrescriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientPrescriptionId == prescriptionId, ct);

            if (prescription == null)
            {
                _logger.LogWarning("Prescription {PrescriptionId} not found for prescription created notification", prescriptionId);
                return;
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == prescription.PatientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for prescription created notification", prescription.PatientId);
                return;
            }

            var provider = prescription.ProviderId.HasValue
                ? await _db.SYS_UserDetails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == prescription.ProviderId.Value, ct)
                : null;

            var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "Your Provider";
            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            DateTime? dateToUse = prescription.PrescritionDate ?? prescription.CreatedDate;
            var prescriptionDate = dateToUse.HasValue
                ? CommonMethods.ToLocalTime(dateToUse.Value).ToString("MMMM dd, yyyy")
                : CommonMethods.ToLocalTime(DateTime.UtcNow).ToString("MMMM dd, yyyy");

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Prescription Created - TelehealthUS",
                PreviewText = $"A new prescription has been created for you by {providerName}.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"A new prescription has been created for you by {providerName}. Prescription ID: {prescriptionId}.",
                    "Your prescription is now being processed and will be sent to the pharmacy soon.",
                    "You'll receive updates as your prescription progresses through the system."
                },
                ButtonText = "View Prescription",
                ButtonUrl = $"{FrontendBaseUrl}/prescription/detail/{prescriptionId}",
                FooterNote = "If you have any questions about your prescription, please contact your provider or our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Prescription created notification sent for prescription {PrescriptionId}", prescriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prescription created notification for prescription {PrescriptionId}", prescriptionId);

        }
    }

    public async Task SendPrescriptionEditedAsync(long prescriptionId, CancellationToken ct = default)
    {
        try
        {

            var prescription = await _db.PT_PatientPrescriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientPrescriptionId == prescriptionId, ct);

            if (prescription == null)
            {
                _logger.LogWarning("Prescription {PrescriptionId} not found", prescriptionId);
                return;
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == prescription.PatientId, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for prescription edit notification", prescription.PatientId);
                return;
            }

            var provider = prescription.ProviderId.HasValue
                ? await _db.SYS_UserDetails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == prescription.ProviderId.Value, ct)
                : null;

            var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "Your Provider";
            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

            DateTime? dateToUse = prescription.ModifiedDate ?? prescription.PrescritionDate ?? prescription.CreatedDate;

            DateTime localDateToUse;
            if (dateToUse.HasValue)
            {
                localDateToUse = CommonMethods.ToLocalTime(dateToUse.Value);
            }
            else
            {
                localDateToUse = CommonMethods.ToLocalTime(DateTime.UtcNow);
            }

            var modifiedDate = localDateToUse.Date.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture);

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Prescription Updated - TelehealthUS",
                PreviewText = $"Your prescription has been updated by {providerName}.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Your prescription has been updated by {providerName}. Prescription ID: {prescriptionId}.",
                    "The changes have been saved and will be reflected in your prescription details.",
                    "If your prescription was already sent to the pharmacy, the updated version will be used for future orders."
                },
                ButtonText = "View Prescription",
                ButtonUrl = $"{FrontendBaseUrl}/prescription/detail/{prescriptionId}",
                FooterNote = "If you have any questions about these changes, please contact your provider or our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Prescription edited notification sent for prescription {PrescriptionId}", prescriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prescription edited notification for prescription {PrescriptionId}", prescriptionId);

        }
    }

    public async Task SendManualInvoiceCreatedNotificationAsync(
        long facilityId,
        long? patientId,
        decimal amount,
        string invoiceNumber,
        CancellationToken ct = default)
    {
        try
        {
            if (facilityId <= 0)
            {
                _logger.LogWarning("Facility ID is required for manual invoice created notification");
                return;
            }

            long? invoiceId = null;
            DateTime? dueDateLocal = null;
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var invoice = await _db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber)
                    .Select(i => new { i.InvoiceId, i.CreatedDate })
                    .FirstOrDefaultAsync(ct);

                if (invoice != null)
                {
                    invoiceId = invoice.InvoiceId;
                    dueDateLocal = invoice.CreatedDate.HasValue
                        ? CommonMethods.ToLocalTime(invoice.CreatedDate.Value.AddDays(29))
                        : (DateTime?)null;
                }
            }

            var formattedAmount = FormatCurrency(amount);

            if (patientId.HasValue && patientId.Value > 0)
            {

                _db.SYS_Notifications.Add(new SYS_Notification
                {
                    NotificationType = "ManualInvoiceCreated",
                    UserId = null,
                    PatientId = patientId,
                    FacilityId = facilityId,
                    Description = $"A manual invoice was created for {formattedAmount}. Invoice: {invoiceNumber}.",
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false
                });
                await _db.SaveChangesAsync(ct);

                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.PatientId == patientId.Value)
                    .Select(p => new { p.Email, p.FirstName, p.LastName })
                    .FirstOrDefaultAsync(ct);

                if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
                    return;

                var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

                var emailModel = new MailTemplateModel
                {
                    ToEmail = patient.Email.Trim(),
                    ToName = patientName,
                    Subject = "Invoice Generated - TelehealthUS",
                    PreviewText = $"Your invoice of {formattedAmount} has been generated.",
                    Greeting = $"Hi {patientName},",
                    BodyParagraphs = new List<string>
                    {
                        $"Your invoice has been generated.",
                        $"Invoice Number: {invoiceNumber}",
                        $"Total Amount: {formattedAmount}",
                        dueDateLocal.HasValue ? $"Due Date: {dueDateLocal.Value:MM/dd/yyyy}" : "Due Date: N/A",
                        "Please review the invoice details and make payment by the due date to avoid any service interruptions."
                    },
                    ButtonText = "View Invoice",
                    ButtonUrl = invoiceId.HasValue
                        ? $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoiceId.Value}"
                        : $"{FrontendBaseUrl}/billing/clinicInvoices",
                    FooterNote = "If you have any questions about this invoice, please contact our support team."
                };

                await _mailSender.SendAsync(emailModel, ct);
            }
            else
            {

                _db.SYS_Notifications.Add(new SYS_Notification
                {
                    NotificationType = "ManualInvoiceCreated",
                    UserId = null,
                    PatientId = null,
                    FacilityId = facilityId,
                    Description = $"A manual invoice was created for {formattedAmount}. Invoice: {invoiceNumber}.",
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false
                });
                await _db.SaveChangesAsync(ct);

                var admin = await _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == facilityId && uf.IsAssign == true && uf.UserId.HasValue)
                    .Join(_db.SYS_UserDetails.AsNoTracking(),
                        uf => uf.UserId,
                        u => u.UserId,
                        (uf, u) => u)
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        u => u.LoginId,
                        l => l.LoginId,
                        (u, l) => new { u, l.RoleId })
                    .Where(x => x.RoleId == 3 && x.u.IsActive == true && x.u.Status == "Active")
                    .Select(x => new { x.u.Email, x.u.FirstName, x.u.LastName })
                    .FirstOrDefaultAsync(ct);

                if (admin == null || string.IsNullOrWhiteSpace(admin.Email))
                    return;

                var adminName = $"{admin.FirstName} {admin.LastName}".Trim();
                var facility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

                var facilityName = facility?.TitleLong ?? facility?.TitleShort ?? "Clinic";

                var emailModel = new MailTemplateModel
                {
                    ToEmail = admin.Email.Trim(),
                    ToName = adminName,
                    Subject = "Invoice Generated - TelehealthUS",
                    PreviewText = $"A new invoice of {formattedAmount} has been generated for your clinic.",
                    Greeting = $"Hi {adminName},",
                    BodyParagraphs = new List<string>
                    {
                        $"A new invoice has been generated for {facilityName}.",
                        $"Invoice Number: {invoiceNumber}",
                        $"Total Amount: {formattedAmount}",
                        dueDateLocal.HasValue ? $"Due Date: {dueDateLocal.Value:MM/dd/yyyy}" : "Due Date: N/A"
                    },
                    ButtonText = "View Invoice",
                    ButtonUrl = invoiceId.HasValue
                        ? $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoiceId.Value}"
                        : $"{FrontendBaseUrl}/billing/clinicInvoices",
                    FooterNote = "If you have any questions about this invoice, please contact our billing team."
                };

                await _mailSender.SendAsync(emailModel, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending manual invoice created notification");

        }
    }

    public async Task SendMonthlyInvoiceGeneratedAsync(long facilityId, decimal amount, string invoiceNumber, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null)
            {
                _logger.LogWarning("Facility {FacilityId} not found for monthly invoice notification", facilityId);
                return;
            }

            var facilityName = facility.TitleShort ?? facility.TitleLong ?? "Facility";
            var formattedAmount = FormatCurrency(amount);

            long? invoiceId = null;
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var invoice = await _db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber)
                    .Select(i => (long?)i.InvoiceId)
                    .FirstOrDefaultAsync(ct);
                invoiceId = invoice;
            }

            var facilityAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { uf, u })
                .Join(_db.SYS_Logins.AsNoTracking(),
                    x => x.u.LoginId,
                    l => l.LoginId,
                    (x, l) => new { x.u.Email, x.u.FirstName, x.u.LastName, x.u.IsActive, l.RoleId })
                .Where(x => x.RoleId == (int)UserRole.ClinicAdmin
                         && x.IsActive == true
                         && !string.IsNullOrWhiteSpace(x.Email))
                .ToListAsync(ct);

            foreach (var admin in facilityAdmins)
            {
                try
                {
                    var adminName = $"{admin.FirstName} {admin.LastName}".Trim();
                    var adminEmailModel = new MailTemplateModel
                    {
                        ToEmail = admin.Email?.Trim(),
                        ToName = adminName,
                        Subject = "Monthly Invoice Generated - TelehealthUS",
                        PreviewText = $"Monthly invoice of {formattedAmount} has been generated for {facilityName}.",
                        Greeting = $"Hi {admin.FirstName},",
                        BodyParagraphs = new List<string>
                        {
                            $"A monthly invoice has been generated for {facilityName}.",
                            $"Invoice Number: {invoiceNumber}",
                            $"Total Amount: {formattedAmount}",
                            "Please review and ensure payment is made by the due date."
                        },
                        ButtonText = "View Invoice",
                        ButtonUrl = invoiceId.HasValue
                            ? $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoiceId.Value}"
                            : $"{FrontendBaseUrl}/billing/clinicInvoices",
                        FooterNote = "If you have any questions, please contact our billing department."
                    };

                    await _mailSender.SendAsync(adminEmailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending monthly invoice notification to admin {Email}", admin.Email);

                }
            }

            _logger.LogInformation("Monthly invoice notification sent for facility {FacilityId}, invoice {InvoiceNumber}", facilityId, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending monthly invoice notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendNewPatientAlertAsync(long patientId, long? facilityId, CancellationToken ct = default)
    {
        try
        {

            var patientTask = _db.PT_Patients
                .AsNoTracking()
                .Where(p => p.PatientId == patientId)
                .Select(p => new
                {
                    p.PatientId,
                    p.FirstName,
                    p.LastName,
                    p.Email,
                    p.Phone,
                    p.MRN,
                    p.FacilityId
                })
                .FirstOrDefaultAsync(ct);

            var patient = await patientTask;
            if (patient == null)
            {
                _logger.LogWarning("Patient {PatientId} not found for new patient alert", patientId);
                return;
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var targetFacilityId = facilityId ?? patient.FacilityId;

            if (!targetFacilityId.HasValue)
            {
                _logger.LogWarning("No facility ID available for new patient alert for patient {PatientId}", patientId);
                return;
            }

            var globalAdmins = await _db.SYS_UserDetails
                .AsNoTracking()
                .Join(_db.SYS_Logins.AsNoTracking(),
                    u => u.LoginId,
                    l => l.LoginId,
                    (u, l) => new { u, l.RoleId })
                .Where(x => x.RoleId == (int)UserRole.GlobalAdmin &&
                           x.u.IsActive == true &&
                           !string.IsNullOrWhiteSpace(x.u.Email))
                .Select(x => new { x.u.Email, x.u.FirstName, x.u.LastName })
                .ToListAsync(ct);

            var facilityAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == targetFacilityId.Value)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { u, uf })
                .Join(_db.SYS_Logins.AsNoTracking(),
                    x => x.u.LoginId,
                    l => l.LoginId,
                    (x, l) => new { x.u, l.RoleId })
                .Where(x => x.RoleId == (int)UserRole.ClinicAdmin &&
                           x.u.IsActive == true &&
                           !string.IsNullOrWhiteSpace(x.u.Email))
                .Select(x => new { x.u.Email, x.u.FirstName, x.u.LastName })
                .ToListAsync(ct);

            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .Where(f => f.FacilityId == targetFacilityId.Value)
                .Select(f => new { f.Email, f.TitleShort, f.TitleLong })
                .FirstOrDefaultAsync(ct);

            var allPatientEmails = await _db.PT_Patients
                .AsNoTracking()
                .Where(p => !string.IsNullOrWhiteSpace(p.Email))
                .Select(p => p.Email.Trim().ToLower())
                .Distinct()
                .ToListAsync(ct);

            var patientLoginEmails = await _db.SYS_Logins
                .AsNoTracking()
                .Where(l => l.RoleId == (int)UserRole.Patient && !string.IsNullOrWhiteSpace(l.Email))
                .Select(l => l.Email.Trim().ToLower())
                .Distinct()
                .ToListAsync(ct);

            var excludedPatientEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var email in allPatientEmails)
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    excludedPatientEmails.Add(email);
                }
            }
            foreach (var email in patientLoginEmails)
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    excludedPatientEmails.Add(email);
                }
            }

            var recipients = new List<(string Email, string Name)>();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var admin in facilityAdmins)
            {
                var adminEmail = admin.Email?.Trim();

                if (!string.IsNullOrWhiteSpace(adminEmail) &&
                    !excludedPatientEmails.Contains(adminEmail) &&
                    seenEmails.Add(adminEmail))
                {
                    recipients.Add((adminEmail, $"{admin.FirstName} {admin.LastName}".Trim()));
                }
            }

            if (facility != null && !string.IsNullOrWhiteSpace(facility.Email))
            {
                var facilityEmail = facility.Email.Trim();

                if (!excludedPatientEmails.Contains(facilityEmail) &&
                    seenEmails.Add(facilityEmail))
                {
                    recipients.Add((
                        facilityEmail,
                        facility.TitleShort ?? facility.TitleLong ?? "Facility"
                    ));
                }
            }

            if (recipients.Count == 0)
            {
                _logger.LogWarning("No recipients found for new patient alert for patient {PatientId}", patientId);
                return;
            }

            var genericBody = new List<string>
            {
                $"A new patient has been added to your facility:",
                $"Name: {patientName}",
            };
            if (!string.IsNullOrWhiteSpace(patient.Email)) genericBody.Add($"Email: {patient.Email}");
            if (!string.IsNullOrWhiteSpace(patient.Phone)) genericBody.Add($"Phone: {patient.Phone}");
            if (!string.IsNullOrWhiteSpace(patient.MRN)) genericBody.Add($"MRN: {patient.MRN}");
            genericBody.Add("Please review the patient's information and ensure all necessary setup steps are completed.");

            var emailModels = recipients.Select(recipient => new MailTemplateModel
            {
                ToEmail = recipient.Email?.Trim() ?? string.Empty,
                ToName = recipient.Name,
                Subject = "New Patient Alert - TelehealthUS",
                PreviewText = $"A new patient, {patientName}, has been added to your facility.",
                Greeting = $"Hi {recipient.Name},",
                BodyParagraphs = new List<string>(genericBody),
                ButtonText = "View Patient",
                ButtonUrl = $"{FrontendBaseUrl}/patient/detail/{patientId}",
                FooterNote = "This is an automated notification for new patient registrations."
            }).ToList();

            if (emailModels.Count > 3)
            {

                _backgroundEmailService.SendBatchInBackground(emailModels, ct);
                _logger.LogInformation("New patient alert queued for background sending to {Count} recipients for patient {PatientId}", emailModels.Count, patientId);
            }
            else
            {

                _ = Task.Run(async () =>
                {
                    try
                    {
                        foreach (var emailModel in emailModels)
                        {
                            try
                            {
                                await _mailSender.SendAsync(emailModel, ct);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending new patient alert to {Email}", emailModel.ToEmail);
                            }
                        }
                        _logger.LogInformation("New patient alert sent for patient {PatientId} to facility {FacilityId}", patientId, targetFacilityId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background task for new patient alert for patient {PatientId}", patientId);
                    }
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending new patient alert for patient {PatientId}", patientId);

        }
    }

    public async Task SendMessageNotificationAsync(string recipientEmail, string senderName, string messagePreview, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning("Recipient email is empty for message notification");
                return;
            }

            var preview = messagePreview.Length > 100
                ? messagePreview.Substring(0, 100) + "..."
                : messagePreview;

            var emailModel = new MailTemplateModel
            {
                ToEmail = recipientEmail,
                ToName = recipientEmail,
                Subject = $"New Message from {senderName} - TelehealthUS",
                PreviewText = $"You have a new message from {senderName}.",
                Greeting = "Hi there,",
                BodyParagraphs = new List<string>
                {
                    $"You have received a new message from {senderName}.",
                    $"Message: {preview}",
                    "Please log in to your dashboard to view and respond to this message."
                },
                ButtonText = "View Messages",
                ButtonUrl = $"{FrontendBaseUrl}/chat",
                FooterNote = "You're receiving this notification because you have unread messages in your TelehealthUS account."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Message notification sent to {RecipientEmail} from {SenderName}", recipientEmail, senderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message notification to {RecipientEmail}", recipientEmail);

        }
    }

    public async Task<bool> SendUnreadMessageReminderEmailAsync(string recipientEmail, string messagePreview, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning("Recipient email is empty for unread message reminder");
                return false;
            }

            var preview = string.IsNullOrWhiteSpace(messagePreview)
                ? "You have unread messages in your TelehealthUS inbox."
                : (messagePreview.Length > 120 ? messagePreview.Substring(0, 120) + "..." : messagePreview);

            var emailModel = new MailTemplateModel
            {
                ToEmail = recipientEmail,
                ToName = recipientEmail,
                Subject = "Unread Messages Reminder - TelehealthUS",
                PreviewText = "You have unread messages waiting in your chat inbox.",
                Greeting = "Hi there,",
                BodyParagraphs = new List<string>
                {
                    "You still have unread message(s) in your TelehealthUS chat inbox.",
                    preview,
                    "Please log in to review and respond."
                },
                ButtonText = "Open Chat",
                ButtonUrl = $"{FrontendBaseUrl}/chat",
                FooterNote = "This is an automated reminder for unread chat messages."
            };

            await _mailSender.SendAsync(emailModel, ct);
            _logger.LogInformation("Unread message reminder email sent to {RecipientEmail}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending unread message reminder email to {RecipientEmail}", recipientEmail);
            return false;
        }
    }

    public async Task SendPriceChangeNotificationAsync(long facilityId, string productName, decimal oldPrice, decimal newPrice, long? drugId, bool isCustom, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null || string.IsNullOrWhiteSpace(facility.Email))
            {
                _logger.LogWarning("Facility {FacilityId} not found or has no email for price change notification", facilityId);
                return;
            }

            var facilityName = facility.TitleShort ?? facility.TitleLong ?? "Facility";
            var catalogDisplay = await ResolveDrugCatalogDisplayAsync(drugId, isCustom, ct);
            var formattedOldPrice = FormatCurrency(oldPrice);
            var formattedNewPrice = FormatCurrency(newPrice);
            var priceChange = newPrice > oldPrice ? "increased" : "decreased";
            var changeAmount = FormatCurrency(Math.Abs(newPrice - oldPrice));

            _db.SYS_Notifications.Add(new SYS_Notification
            {
                NotificationType = "WholesalePriceUpdate",
                FacilityId = facilityId,
                Description = $"Wholesale price for {productName} was updated ({catalogDisplay}). Previous: {formattedOldPrice}, New: {formattedNewPrice}.",
                CreatedDate = DateTime.UtcNow,
                IsRead = false
            });
            await _db.SaveChangesAsync(ct);

            var facilityAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { uf, u })
                .Join(_db.SYS_Logins.AsNoTracking(),
                    x => x.u.LoginId,
                    l => l.LoginId,
                    (x, l) => new { x.u.Email, x.u.FirstName, x.u.LastName, RoleId = l.RoleId })
                .Where(x => x.RoleId == (int)UserRole.ClinicAdmin && !string.IsNullOrWhiteSpace(x.Email))
                .ToListAsync(ct);

            var recipients = new List<(string Email, string Name)>();

            foreach (var admin in facilityAdmins)
            {
                recipients.Add((admin.Email?.Trim() ?? string.Empty, $"{admin.FirstName} {admin.LastName}".Trim()));
            }

            if (!recipients.Any(r => r.Email.Equals(facility.Email, StringComparison.OrdinalIgnoreCase)))
            {
                recipients.Add((facility.Email?.Trim() ?? string.Empty, facilityName));
            }

            foreach (var recipient in recipients)
            {
                try
                {
                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = $"Wholesale Price Update - {productName} - TelehealthUS",
                        PreviewText = $"Catalog: {catalogDisplay}. The wholesale price for {productName} has {priceChange} from {formattedOldPrice} to {formattedNewPrice}.",
                        Greeting = $"Hi {recipient.Name},",
                        HighlightText = $"<strong>Catalog(s):</strong> {catalogDisplay}",
                        BodyParagraphs = new List<string>
                        {
                            $"This is to notify you that the wholesale price for {productName} has been updated. This product may appear under: {catalogDisplay}.",
                            $"Previous Wholesale Price: {formattedOldPrice}",
                            $"New Wholesale Price: {formattedNewPrice}",
                            $"Change: {priceChange} by {changeAmount}",
                            "Please review this change in your catalog and update your pricing strategy if needed."
                        },
                        ButtonText = "View Drug",
                        ButtonUrl = $"{FrontendBaseUrl}/product/view/Drugs",
                        FooterNote = "This is an automated notification for wholesale price updates."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending price change notification to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("Price change notification sent for facility {FacilityId}, product {ProductName}", facilityId, productName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending price change notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendMedicineCatalogUpdateAsync(long facilityId, string action, string productName, long? drugId, bool isCustom, string? previousDrugName = null, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null)
            {
                _logger.LogWarning("Facility {FacilityId} not found for catalog update notification", facilityId);
                return;
            }

            if (drugId.HasValue && drugId.Value > 0)
            {
                var excluded = await _db.PC_DRUGFACILITYEXCLUSIONS
                    .AsNoTracking()
                    .AnyAsync(x => x.DrugId == drugId.Value && x.FacilityId == facilityId && x.IsActive == true, ct);
                if (excluded)
                {
                    _logger.LogInformation(
                        "Skipping drug catalog update email for facility {FacilityId}: drug {DrugId} is not assigned (excluded).",
                        facilityId,
                        drugId.Value);
                    return;
                }
            }

            var facilityName = facility.TitleShort ?? facility.TitleLong ?? "Facility";
            var catalogDisplay = await ResolveDrugCatalogDisplayAsync(drugId, isCustom, ct);
            var actionMessage = action.ToLower() switch
            {
                "add" or "added" => "has been added to",
                "remove" or "removed" => "has been removed from",
                "update" or "updated" => "has been updated in",
                _ => $"has been {action} in"
            };

            var renameNote = string.Empty;
            if (!string.IsNullOrWhiteSpace(previousDrugName)
                && !string.Equals(previousDrugName.Trim(), productName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                renameNote = $" The medicine was renamed from \"{previousDrugName.Trim()}\" to \"{productName.Trim()}\".";
            }

            var facilityAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { uf, u })
                .Join(_db.SYS_Logins.AsNoTracking(),
                    x => x.u.LoginId,
                    l => l.LoginId,
                    (x, l) => new { x.u.Email, x.u.FirstName, x.u.LastName, l.RoleId, x.u.IsActive, x.u.Status })
                .Where(x => x.RoleId == (int)UserRole.ClinicAdmin
                            && x.IsActive == true
                            && x.Status == "Active"
                            && !string.IsNullOrWhiteSpace(x.Email))
                .ToListAsync(ct);

            var recipients = new List<(string Email, string Name)>();

            foreach (var admin in facilityAdmins)
            {
                recipients.Add((admin.Email?.Trim() ?? string.Empty, $"{admin.FirstName} {admin.LastName}".Trim()));
            }

            recipients = recipients
                .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                .GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            _db.SYS_Notifications.Add(new SYS_Notification
            {
                NotificationType = "DrugCatalogUpdate",
                FacilityId = facilityId,
                Description = string.IsNullOrEmpty(renameNote)
                    ? $"Drug catalog was updated ({catalogDisplay}). {productName} {actionMessage} the catalog."
                    : $"Drug catalog was updated ({catalogDisplay}).{renameNote}",
                CreatedDate = DateTime.UtcNow,
                IsRead = false
            });
            await _db.SaveChangesAsync(ct);

            if (recipients.Count == 0)
            {
                _logger.LogWarning("No active clinic admin recipients for catalog update notification (facility {FacilityId})", facilityId);
                return;
            }

            var subjectSuffix = string.IsNullOrEmpty(renameNote) ? productName : $"{productName} (name updated)";

            foreach (var recipient in recipients)
            {
                try
                {
                    var paragraphs = new List<string>
                    {
                        string.IsNullOrEmpty(renameNote)
                            ? $"This is to notify you that {productName} {actionMessage} the catalog. This medicine may appear under: {catalogDisplay}."
                            : $"This is to notify you that a medicine in your catalog has been updated.{renameNote} It may appear under: {catalogDisplay}.",
                        action.ToLower().Contains("add")
                            ? "The new medicine is now available for use in your facility."
                            : action.ToLower().Contains("remove")
                            ? "This medicine has been removed and is no longer available."
                            : "The medicine information has been updated in the catalog.",
                        "Please review the changes and update your records if necessary."
                    };

                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = $"Drug Catalog Update - {subjectSuffix} - TelehealthUS",
                        PreviewText = string.IsNullOrEmpty(renameNote)
                            ? $"Catalog: {catalogDisplay}. {productName} {actionMessage} the drug catalog."
                            : $"Catalog: {catalogDisplay}. Drug name updated.{renameNote}",
                        Greeting = $"Hi {recipient.Name},",
                        HighlightText = $"<strong>Catalog(s):</strong> {catalogDisplay}",
                        BodyParagraphs = paragraphs,
                        ButtonText = "View Catalog",
                        ButtonUrl = drugId.HasValue && drugId.Value > 0
                            ? $"{FrontendBaseUrl}/product/view/Drugs"
                            : $"{FrontendBaseUrl}/product/view/Drugs",
                        FooterNote = "This is an automated notification for catalog updates."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending catalog update notification to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("Medicine catalog update notification sent for facility {FacilityId}, action: {Action}, product: {ProductName}", facilityId, action, productName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending medicine catalog update notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendMonthlyAnalyticsAsync(long facilityId, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null)
            {
                _logger.LogWarning("Facility {FacilityId} not found for monthly analytics notification", facilityId);
                return;
            }

            var monthYear = CommonMethods.ToLocalTime(DateTime.UtcNow.AddMonths(-1)).ToString("MMMM yyyy");

            var clinicAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId && uf.UserId.HasValue && uf.IsAssign == true)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { User = u })
                .Join(_db.SYS_Logins.AsNoTracking(),
                    x => x.User.LoginId,
                    l => l.LoginId,
                    (x, l) => new { x.User, RoleId = l.RoleId })
                .Where(x => x.RoleId == 3 && x.User.IsActive == true && x.User.Status == "Active")
                .Select(x => new { x.User.Email, x.User.FirstName, x.User.LastName })
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .ToListAsync(ct);

            var recipients = new List<(string Email, string Name)>();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var admin in clinicAdmins)
            {
                var email = admin.Email?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(email) || !seenEmails.Add(email))
                    continue;
                recipients.Add((email, $"{admin.FirstName} {admin.LastName}".Trim()));
            }

            if (recipients.Count == 0)
            {
                _logger.LogWarning("No clinic admin recipients with email for monthly analytics notification for facility {FacilityId}", facilityId);
                return;
            }

            foreach (var recipient in recipients)
            {
                try
                {
                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = $"Monthly Analytics Report - {monthYear} - TelehealthUS",
                        PreviewText = $"Your monthly analytics report for {monthYear} is now available.",
                        Greeting = $"Hi {recipient.Name},",
                        BodyParagraphs = new List<string>
                        {
                            $"Your monthly analytics report for {monthYear} is now available.",
                            "This report includes:",
                            "• Patient statistics and growth",
                            "• Appointment metrics",
                            "• Revenue and billing summary",
                            "• Treatment and prescription analytics",
                            "• Staff performance metrics"
                        },
                        ButtonText = "View Analytics",
                        ButtonUrl = $"{FrontendBaseUrl}/analytics/charts",
                        FooterNote = "This is an automated monthly report. For detailed analysis, please log in to your dashboard."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending monthly analytics notification to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("Monthly analytics notification sent for facility {FacilityId}", facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending monthly analytics notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendStaffChangeNotificationAsync(long facilityId, string action, string staffName, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null || string.IsNullOrWhiteSpace(facility.Email))
            {
                _logger.LogWarning("Facility {FacilityId} not found or has no email for staff change notification", facilityId);
                return;
            }

            var facilityName = facility.TitleShort ?? facility.TitleLong ?? "Facility";
            var actionMessage = action.ToLower() switch
            {
                "add" or "added" => "has been added to",
                "remove" or "removed" => "has been removed from",
                _ => $"has been {action} in"
            };

            var facilityAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { u.Email, u.FirstName, u.LastName })
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .ToListAsync(ct);

            var recipients = new List<(string Email, string Name)>();

            foreach (var admin in facilityAdmins)
            {
                recipients.Add((admin.Email?.Trim() ?? string.Empty, $"{admin.FirstName} {admin.LastName}".Trim()));
            }

            if (!recipients.Any(r => r.Email.Equals(facility.Email, StringComparison.OrdinalIgnoreCase)))
            {
                recipients.Add((facility.Email?.Trim() ?? string.Empty, facilityName));
            }

            foreach (var recipient in recipients)
            {
                try
                {
                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = $"Staff Change Notification - {staffName} - TelehealthUS",
                        PreviewText = $"{staffName} {actionMessage} your facility staff.",
                        Greeting = $"Hi {recipient.Name},",
                        BodyParagraphs = new List<string>
                        {
                            $"This is to notify you that {staffName} {actionMessage} your facility staff.",
                            action.ToLower().Contains("add")
                                ? "The new staff member has been added and can now access the system."
                                : action.ToLower().Contains("remove")
                                ? "This staff member's access has been removed from the facility."
                                : "The staff member's information has been updated.",
                            "Please review the changes in your staff management section."
                        },
                        ButtonText = "View Staff",
                        ButtonUrl = $"{FrontendBaseUrl}/user/view",
                        FooterNote = "This is an automated notification for staff management changes."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending staff change notification to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("Staff change notification sent for facility {FacilityId}, action: {Action}, staff: {StaffName}", facilityId, action, staffName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending staff change notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendCouponCreatedAsync(long facilityId, string couponCode, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null)
            {
                _logger.LogWarning("Facility {FacilityId} not found for coupon creation notification", facilityId);
                return;
            }

            var clinicAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId && uf.UserId.HasValue)
                .Join(_db.SYS_UserDetails.AsNoTracking(), uf => uf.UserId, u => u.UserId, (uf, u) => new { u.LoginId, u.Email, u.FirstName, u.LastName })
                .Join(_db.SYS_Logins.AsNoTracking(), x => x.LoginId, l => l.LoginId, (x, l) => new { x.Email, x.FirstName, x.LastName, l.RoleId })
                .Where(x => !string.IsNullOrWhiteSpace(x.Email) && x.RoleId == (int)UserRole.ClinicAdmin)
                .Select(x => new { x.Email, x.FirstName, x.LastName })
                .ToListAsync(ct);

            var recipients = clinicAdmins
                .Select(a => (Email: (a.Email ?? string.Empty).Trim(), Name: $"{a.FirstName} {a.LastName}".Trim()))
                .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                .GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "Coupon creation notification skipped: no clinic admin with email for facility {FacilityId}, coupon {CouponCode}",
                    facilityId,
                    couponCode);
                return;
            }

            foreach (var recipient in recipients)
            {
                try
                {
                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = $"New Coupon Created - {couponCode} - TelehealthUS",
                        PreviewText = $"A new coupon code {couponCode} has been created for your facility.",
                        Greeting = $"Hi {recipient.Name},",
                        BodyParagraphs = new List<string>
                        {
                            $"A new coupon code has been created for your facility:",
                            $"Coupon Code: {couponCode}",
                            "This coupon is now active and can be used by patients during checkout.",
                            "You can manage and view all your coupons in the dashboard."
                        },
                        ButtonText = "View Coupons",
                        ButtonUrl = $"{FrontendBaseUrl}/product/coupons",
                        FooterNote = "This is an automated notification for coupon creation."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending coupon creation notification to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("Coupon creation notification sent for facility {FacilityId}, coupon: {CouponCode}", facilityId, couponCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending coupon creation notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendGlobalAdminSignUpAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var admin = await _db.SYS_UserDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive == true, ct);

            if (admin == null || string.IsNullOrWhiteSpace(admin.Email))
            {
                _logger.LogWarning("Global Admin {UserId} not found, inactive, or has no email for sign-up notification", userId);
                return;
            }

            var adminName = $"{admin.FirstName} {admin.LastName}".Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = admin.Email?.Trim(),
                ToName = adminName,
                Subject = "Welcome to TelehealthUS - Global Admin Account Created",
                PreviewText = "Your Global Admin account has been successfully created. Welcome to TelehealthUS!",
                Greeting = $"Hi {admin.FirstName},",
                BodyParagraphs = new List<string>
                {
                    "Welcome to TelehealthUS! Your Global Admin account has been successfully created.",
                    "As a Global Administrator, you have access to:",
                    "• Manage all facilities and clinics",
                    "• View system-wide analytics and reports",
                    "• Manage users and roles across the platform",
                    "• Access billing and invoice management",
                    "• Configure system settings and preferences"
                },
                ButtonText = "Access Dashboard",
                ButtonUrl = $"{FrontendBaseUrl}/dashboard/admin",
                FooterNote = "If you have any questions or need assistance, please contact our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Global Admin sign-up notification sent to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Global Admin sign-up notification to user {UserId}", userId);

        }
    }

    public async Task SendClinicManagementChangeAsync(string action, long? facilityId, string facilityName, CancellationToken ct = default)
    {
        try
        {

            var globalAdmins = await _db.SYS_UserDetails
                .AsNoTracking()
                .Join(_db.SYS_Logins.AsNoTracking(),
                    u => u.LoginId,
                    l => l.LoginId,
                    (u, l) => new { u, l.RoleId })
                .Where(x => x.RoleId == 2 && !string.IsNullOrWhiteSpace(x.u.Email))
                .Select(x => new { x.u.Email, x.u.FirstName, x.u.LastName })
                .ToListAsync(ct);

            var actionMessage = action.ToLower() switch
            {
                "add" or "added" => "has been added",
                "remove" or "removed" => "has been removed",
                "update" or "updated" => "has been updated",
                _ => $"has been {action}"
            };

            foreach (var admin in globalAdmins)
            {
                try
                {
                    var adminName = $"{admin.FirstName} {admin.LastName}".Trim();
                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = admin.Email?.Trim(),
                        ToName = adminName,
                        Subject = $"Clinic Management Change - {facilityName} - TelehealthUS",
                        PreviewText = $"Clinic {facilityName} {actionMessage}.",
                        Greeting = $"Hi {adminName},",
                        BodyParagraphs = new List<string>
                        {
                            $"This is to notify you that clinic {facilityName} {actionMessage}.",
                            action.ToLower().Contains("add")
                                ? "The new clinic has been added to the system and is now active."
                                : action.ToLower().Contains("remove")
                                ? "This clinic has been removed from the system."
                                : "The clinic information has been updated.",
                            facilityId.HasValue
                                ? $"Facility ID: {facilityId.Value}"
                                : null,
                            "Please review the changes in the clinic management section."
                        }.Where(p => p != null).Cast<string>().ToList(),
                        ButtonText = "View Clinics",
                        ButtonUrl = facilityId.HasValue
                            ? $"{FrontendBaseUrl}/clinic/detail/{facilityId.Value}"
                            : $"{FrontendBaseUrl}/clinic/view",
                        FooterNote = "This is an automated notification for clinic management changes."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending clinic management change notification to Global Admin {Email}", admin.Email);

                }
            }

            _logger.LogInformation("Clinic management change notification sent, action: {Action}, facility: {FacilityName}", action, facilityName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending clinic management change notification");

        }
    }

    public async Task SendPharmacyPriceUpdateAsync(long facilityId, string productName, CancellationToken ct = default)
    {
        try
        {
            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);

            if (facility == null || string.IsNullOrWhiteSpace(facility.Email))
            {
                _logger.LogWarning("Facility {FacilityId} not found or has no email for pharmacy price update notification", facilityId);
                return;
            }

            var facilityName = facility.TitleShort ?? facility.TitleLong ?? "Facility";

            var facilityAdmins = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.FacilityId == facilityId)
                .Join(_db.SYS_UserDetails.AsNoTracking(),
                    uf => uf.UserId,
                    u => u.UserId,
                    (uf, u) => new { u.Email, u.FirstName, u.LastName })
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .ToListAsync(ct);

            var recipients = new List<(string Email, string Name)>();

            foreach (var admin in facilityAdmins)
            {
                recipients.Add((admin.Email?.Trim() ?? string.Empty, $"{admin.FirstName} {admin.LastName}".Trim()));
            }

            if (!recipients.Any(r => r.Email.Equals(facility.Email, StringComparison.OrdinalIgnoreCase)))
            {
                recipients.Add((facility.Email?.Trim() ?? string.Empty, facilityName));
            }

            foreach (var recipient in recipients)
            {
                try
                {
                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = $"Pharmacy Price Update - {productName} - TelehealthUS",
                        PreviewText = $"The pharmacy price for {productName} has been updated.",
                        Greeting = $"Hi {recipient.Name},",
                        BodyParagraphs = new List<string>
                        {
                            $"This is to notify you that the pharmacy price for {productName} has been updated.",
                            "The updated price has been reflected in the system and will be used for future orders.",
                            "Please review the price change in your product catalog and update your pricing strategy if needed."
                        },
                        ButtonText = "View Product",
                        ButtonUrl = $"{FrontendBaseUrl}/product/view/Drugs",
                        FooterNote = "This is an automated notification for pharmacy price updates."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending pharmacy price update notification to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("Pharmacy price update notification sent for facility {FacilityId}, product: {ProductName}", facilityId, productName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pharmacy price update notification for facility {FacilityId}", facilityId);

        }
    }

    public async Task SendUserManagementChangeAsync(string action, string userName, long? facilityId, int? changedUserRoleId = null, CancellationToken ct = default)
    {
        try
        {
            var recipients = new List<(string Email, string Name)>();
            var notifyGlobalAdminsOnly = changedUserRoleId == (int)UserRole.GlobalAdmin;
            var notifyClinicAdminsOnly = changedUserRoleId == (int)UserRole.ClinicAdmin;

            if (!notifyClinicAdminsOnly)
            {
                var globalAdmins = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Join(_db.SYS_Logins.AsNoTracking(),
                        u => u.LoginId,
                        l => l.LoginId,
                        (u, l) => new { u, l.RoleId })
                    .Where(x => x.RoleId == (int)UserRole.GlobalAdmin &&
                               x.u.IsActive == true &&
                               !string.IsNullOrWhiteSpace(x.u.Email))
                    .Select(x => new { x.u.Email, x.u.FirstName, x.u.LastName })
                    .ToListAsync(ct);

                foreach (var admin in globalAdmins)
                {
                    recipients.Add((admin.Email?.Trim() ?? string.Empty, $"{admin.FirstName} {admin.LastName}".Trim()));
                }
            }

            if (!notifyGlobalAdminsOnly && facilityId.HasValue)
            {
                var facility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FacilityId == facilityId.Value, ct);

                if (facility != null)
                {
                    var facilityAdmins = await _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Where(uf => uf.FacilityId == facilityId.Value)
                        .Join(_db.SYS_UserDetails.AsNoTracking(),
                            uf => uf.UserId,
                            u => u.UserId,
                            (uf, u) => new { u })
                        .Join(_db.SYS_Logins.AsNoTracking(),
                            x => x.u.LoginId,
                            l => l.LoginId,
                            (x, l) => new { x.u, l.RoleId })
                        .Where(x => x.RoleId == (int)UserRole.ClinicAdmin &&
                                   x.u.IsActive == true &&
                                   !string.IsNullOrWhiteSpace(x.u.Email))
                        .Select(x => new { x.u.Email, x.u.FirstName, x.u.LastName })
                        .ToListAsync(ct);

                    foreach (var admin in facilityAdmins)
                    {
                        var adminEmail = admin.Email;
                        if (!recipients.Any(r => r.Email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase)))
                        {
                            recipients.Add((adminEmail?.Trim() ?? string.Empty, $"{admin.FirstName} {admin.LastName}".Trim()));
                        }
                    }

                    if (facility.Email != null && !recipients.Any(r => r.Email.Equals(facility.Email, StringComparison.OrdinalIgnoreCase)))
                    {
                        recipients.Add((facility.Email?.Trim() ?? string.Empty, facility.TitleShort ?? facility.TitleLong ?? "Facility"));
                    }
                }
            }

            var actionMessage = action.ToLower() switch
            {
                "add" or "added" => "has been added",
                "remove" or "removed" => "has been removed",
                "update" or "updated" => "has been updated",
                "delete" or "deleted" => "has been deleted",
                "edit" or "edited" => "has been edited",
                _ => $"has been {action}"
            };

            foreach (var recipient in recipients)
            {
                try
                {
                    var emailModel = new MailTemplateModel
                    {
                        ToEmail = recipient.Email?.Trim() ?? string.Empty,
                        ToName = recipient.Name,
                        Subject = $"User Management Change - {userName} - TelehealthUS",
                        PreviewText = $"User {userName} {actionMessage}.",
                        Greeting = $"Hi {recipient.Name},",
                        BodyParagraphs = new List<string>
                        {
                            $"This is to notify you that user {userName} {actionMessage}.",
                            action.ToLower().Contains("add")
                                ? "The new user has been added to the system and can now access their account."
                                : action.ToLower().Contains("remove") || action.ToLower().Contains("delete")
                                ? "This user's access has been removed from the system."
                                : "The user's information has been updated in the system.",
                            facilityId.HasValue
                                ? $"Facility ID: {facilityId.Value}"
                                : null,
                            "Please review the changes in the user management section."
                        }.Where(p => p != null).Cast<string>().ToList(),
                        ButtonText = "View Users",

                        ButtonUrl = facilityId.HasValue ? $"{FrontendBaseUrl}/user/view" : $"{FrontendBaseUrl}/user-management",
                        FooterNote = "This is an automated notification for user management changes."
                    };

                    await _mailSender.SendAsync(emailModel, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending user management change notification to {Email}", recipient.Email);

                }
            }

            _logger.LogInformation("User management change notification sent, action: {Action}, user: {UserName}, facility: {FacilityId}", action, userName, facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user management change notification");

        }
    }

    public async Task SendSupportTicketNotificationAsync(long ticketId, string recipientEmail, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning("Recipient email is empty for support ticket notification");
                return;
            }

            var ticket = await _db.SYS_Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);

            if (ticket == null)
            {
                _logger.LogWarning("Support ticket {TicketId} not found", ticketId);
                return;
            }

            var subject = ticket.Subject ?? "Support Ticket";
            var status = ticket.Status ?? "Open";
            var ticketType = ticket.Type ?? "General";
            string descriptionText = null;

            if (!string.IsNullOrWhiteSpace(ticket.Description))
            {
                var shortDesc = ticket.Description.Length > 200
                    ? ticket.Description.Substring(0, 200) + "..."
                    : ticket.Description;

                descriptionText = $"Description: {shortDesc}";
            }

            var emailModel = new MailTemplateModel
            {
                ToEmail = recipientEmail?.Trim(),
                ToName = recipientEmail?.Trim(),
                Subject = $"Support Ticket Update - #{ticketId} - TelehealthUS",
                PreviewText = $"Your support ticket #{ticketId} has been updated.",
                Greeting = "Hi there,",
                BodyParagraphs = new List<string>
                {
                    $"Your support ticket has been updated:",
                    $"Ticket ID: #{ticketId}",
                    $"Subject: {subject}",
                    $"Status: {status}",
                    $"Type: {ticketType}",
                    descriptionText,
                    "Please log in to your dashboard to view the full details and respond if needed."
                }.Where(p => p != null).Cast<string>().ToList(),
                ButtonText = "View Ticket",
                ButtonUrl = $"{FrontendBaseUrl}/support/detail/{ticketId}",
                FooterNote = "This is an automated notification for support ticket updates."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Support ticket notification sent for ticket {TicketId} to {RecipientEmail}", ticketId, recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending support ticket notification for ticket {TicketId}", ticketId);

        }
    }

    public async Task SendTicketAssignedToTechSupportAsync(long ticketId, string recipientEmail, string subject, string priority, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning("Recipient email is empty for ticket-assigned notification");
                return;
            }

            var ticketSubject = subject ?? "Support Ticket";
            var ticketPriority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority;

            var emailModel = new MailTemplateModel
            {
                ToEmail = recipientEmail.Trim(),
                ToName = recipientEmail.Trim(),
                Subject = $"New ticket assigned to you - #{ticketId} - TelehealthUS",
                PreviewText = $"A new ticket (Priority: {ticketPriority}) has been assigned to you.",
                Greeting = "Hi,",
                BodyParagraphs = new List<string>
                {
                    "A new support ticket has been assigned to you by Global Admin.",
                    $"Ticket ID: #{ticketId}",
                    $"Subject: {ticketSubject}",
                    $"Priority: {ticketPriority}",
                    "Please log in to your dashboard to view the ticket and add comments or update status."
                },
                ButtonText = "View Ticket",
                ButtonUrl = $"{FrontendBaseUrl}/support/detail/{ticketId}",
                FooterNote = "This is an automated notification. You can reply by adding comments on the ticket."
            };

            await _mailSender.SendAsync(emailModel, ct);
            _logger.LogInformation("Ticket-assigned notification sent for ticket {TicketId} to {RecipientEmail}", ticketId, recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ticket-assigned notification for ticket {TicketId}", ticketId);
        }
    }

    public async Task SendTicketUpdateToTechSupportAsync(long ticketId, string recipientEmail, string updateType, string updateDetail, string? ticketSubject, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;
            var subject = ticketSubject ?? "Support ticket";
            var emailModel = new MailTemplateModel
            {
                ToEmail = recipientEmail.Trim(),
                ToName = recipientEmail.Trim(),
                Subject = $"Ticket update - #{ticketId} - TelehealthUS",
                PreviewText = $"{updateType}: {updateDetail}",
                Greeting = "Hi,",
                BodyParagraphs = new List<string>
                {
                    $"Global Admin has updated the ticket.",
                    $"Ticket ID: #{ticketId}",
                    $"Subject: {subject}",
                    $"{updateType}: {updateDetail}",
                    "Please log in to your dashboard to view the ticket."
                },
                ButtonText = "View Ticket",
                ButtonUrl = $"{FrontendBaseUrl}/support/detail/{ticketId}",
                FooterNote = "This is an automated notification."
            };
            await _mailSender.SendAsync(emailModel, ct);
            _logger.LogInformation("Ticket-update notification sent for ticket {TicketId} to {RecipientEmail}", ticketId, recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ticket-update notification for ticket {TicketId}", ticketId);
        }
    }

    public async Task SendTicketCommentByTechSupportToGlobalAdminAsync(long ticketId, string recipientEmail, string techSupportUserName, string ticketSubject, string commentPreview, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;
            var preview = commentPreview != null && commentPreview.Length > 150 ? commentPreview.Substring(0, 150) + "..." : (commentPreview ?? "");
            var emailModel = new MailTemplateModel
            {
                ToEmail = recipientEmail.Trim(),
                ToName = recipientEmail.Trim(),
                Subject = $"Tech Support replied on ticket #{ticketId} - TelehealthUS",
                PreviewText = $"{techSupportUserName} added a comment.",
                Greeting = "Hi,",
                BodyParagraphs = new List<string>
                {
                    $"Tech Support user {techSupportUserName} has added a comment on the following ticket.",
                    $"Ticket ID: #{ticketId}",
                    $"Subject: {ticketSubject ?? "Support ticket"}",
                    $"Comment: {preview}",
                    "Please log in to your dashboard to view and respond."
                },
                ButtonText = "View Ticket",
                ButtonUrl = $"{FrontendBaseUrl}/support/detail/{ticketId}",
                FooterNote = "This is an automated notification."
            };
            await _mailSender.SendAsync(emailModel, ct);
            _logger.LogInformation("Ticket-comment-by-tech-support notification sent for ticket {TicketId} to {RecipientEmail}", ticketId, recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ticket-comment notification for ticket {TicketId}", ticketId);
        }
    }

    public async Task SendTimeSlotConfirmationAsync(long providerId, DateTime slotDate, TimeSpan slotTime, CancellationToken ct = default)
    {
        try
        {
            var provider = await _db.SYS_UserDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == providerId, ct);

            if (provider == null || string.IsNullOrWhiteSpace(provider.Email))
            {
                _logger.LogWarning("Provider {ProviderId} not found or has no email for time slot confirmation", providerId);
                return;
            }

            var providerName = $"{provider.FirstName} {provider.LastName}".Trim();
            var dateStr = CommonMethods.ToLocalTime(slotDate).ToString("MMMM dd, yyyy");
            var timeStr = slotTime.ToString(@"hh\:mm");

            var slot = await _db.UR_ProviderScheduledSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProviderId == providerId &&
                    s.SlotDate.HasValue && s.SlotDate.Value.Date == slotDate.Date &&
                    s.StartTime == slotTime, ct);

            var slotTitle = slot?.Title ?? "Time Slot";
            var slotStatus = slot?.Status ?? "Confirmed";

            var emailModel = new MailTemplateModel
            {
                ToEmail = provider.Email?.Trim(),
                ToName = providerName,
                Subject = "Time Slot Confirmed - TelehealthUS",
                PreviewText = $"Your time slot on {dateStr} at {timeStr} has been confirmed.",
                Greeting = $"Hi {provider.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Your time slot has been confirmed:",
                    $"Date: {dateStr}",
                    $"Time: {timeStr}",
                    $"Title: {slotTitle}",
                    $"Status: {slotStatus}",
                    "This time slot is now active and available for patient appointments.",
                    "You can view and manage all your time slots in your dashboard."
                },
                ButtonText = "View Schedule",
                ButtonUrl = $"{FrontendBaseUrl}/schedule/calendar",
                FooterNote = "If you need to make any changes to this time slot, please do so through your dashboard."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Time slot confirmation sent to provider {ProviderId} for {Date} at {Time}", providerId, slotDate, slotTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending time slot confirmation to provider {ProviderId}", providerId);

        }
    }

    public async Task SendRefillRequestNotificationAsync(long patientId, long treatmentId, long? facilityId, CancellationToken ct = default, bool includePatientRecipient = false, bool notifyGlobalAdminsInApp = false)
    {
        try
        {
            var patient = await (
                from p in _db.PT_Patients.AsNoTracking()
                join u in _db.SYS_UserDetails.AsNoTracking() on p.LoginId equals u.LoginId into uj
                from u in uj.DefaultIfEmpty()
                where p.PatientId == patientId
                select new
                {
                    p.PatientId,
                    p.FirstName,
                    p.LastName,
                    Email = !string.IsNullOrWhiteSpace(p.Email) ? p.Email : (u != null ? u.Email : null),
                    p.MRN,
                    PortalUserId = u != null ? (long?)u.UserId : null
                }).FirstOrDefaultAsync(ct);

            if (patient == null)
            {
                _logger.LogWarning("Patient {PatientId} not found for refill request notification", patientId);
                return;
            }

            var treatment = await _db.PT_PatientTreatments
                .AsNoTracking()
                .Where(t => t.PatientTreatmentId == treatmentId)
                .FirstOrDefaultAsync(ct);

            if (treatment == null)
            {
                _logger.LogWarning("Treatment {TreatmentId} not found for refill request notification", treatmentId);
                return;
            }

            var product = await _db.PD_Bundles
                .AsNoTracking()
                .Where(b => b.BundleId == treatment.ProductId)
                .Select(b => new { b.Name })
                .FirstOrDefaultAsync(ct);

            var productName = product?.Name ?? "Treatment";
            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var targetFacilityId = facilityId ?? treatment.FacilityId;

            var globalAdmins = (await _db.SYS_UserDetails
                .AsNoTracking()
                .Join(_db.SYS_Logins.AsNoTracking(),
                    u => u.LoginId,
                    l => l.LoginId,
                    (u, l) => new { User = u, RoleId = l.RoleId })
                .Where(x => (x.RoleId == (int)UserRole.GlobalAdmin || x.RoleId == (int)UserRole.SuperAdmin)
                            && x.User.IsActive == true && x.User.Status == "Active")
                .Select(x => new { x.User.UserId, x.User.Email, x.User.FirstName, x.User.LastName })
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .ToListAsync(ct))
                .DistinctBy(a => a.UserId)
                .ToList();

            var nowUtc = DateTime.UtcNow;
            var duplicateWindowStart = nowUtc.AddMinutes(-2);
            var hasNotificationInserts = false;

            var adminDescription = $"{patientName} requested a refill for {productName}.";
            long? adminNotificationPatientId = null;
            if (notifyGlobalAdminsInApp)
            {
                foreach (var admin in globalAdmins)
                {
                    var alreadyExists = await _db.SYS_Notifications
                        .AsNoTracking()
                        .AnyAsync(n =>
                            n.NotificationType == "RefillRequest"
                            && n.UserId == admin.UserId
                            && n.PatientId == adminNotificationPatientId
                            && n.FacilityId == targetFacilityId
                            && n.Description == adminDescription
                            && n.CreatedDate >= duplicateWindowStart, ct);

                    if (alreadyExists)
                        continue;

                    _db.SYS_Notifications.Add(new SYS_Notification
                    {
                        NotificationType = "RefillRequest",
                        UserId = admin.UserId,
                        PatientId = adminNotificationPatientId,
                        FacilityId = targetFacilityId,
                        Description = adminDescription,
                        CreatedDate = nowUtc,
                        IsRead = false
                    });
                    hasNotificationInserts = true;
                }
            }

            if (includePatientRecipient)
            {
                var patientDescription =
                    $"Our team submitted a refill request on your behalf for {productName}. We will review it and follow up if needed.";

                var patientAlreadyExists = await _db.SYS_Notifications
                    .AsNoTracking()
                    .AnyAsync(n =>
                        n.NotificationType == "RefillRequest"
                        && n.UserId == patient.PortalUserId
                        && n.PatientId == patientId
                        && n.FacilityId == targetFacilityId
                        && n.Description == patientDescription
                        && n.CreatedDate >= duplicateWindowStart, ct);

                if (!patientAlreadyExists)
                {
                    _db.SYS_Notifications.Add(new SYS_Notification
                    {
                        NotificationType = "RefillRequest",
                        UserId = patient.PortalUserId,
                        PatientId = patientId,
                        FacilityId = targetFacilityId,
                        Description = patientDescription,
                        CreatedDate = nowUtc,
                        IsRead = false
                    });
                    hasNotificationInserts = true;
                }
            }

            if (hasNotificationInserts)
            {
                await _db.SaveChangesAsync(ct);
            }

            var recipients = new List<(string Email, string Name)>();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var admin in globalAdmins)
            {
                if (!string.IsNullOrWhiteSpace(admin.Email) && seenEmails.Add(admin.Email))
                {
                    recipients.Add((admin.Email.Trim(), $"{admin.FirstName} {admin.LastName}".Trim()));
                }
            }

            string? facilityDisplay = null;
            if (targetFacilityId.HasValue)
            {
                facilityDisplay = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.FacilityId == targetFacilityId.Value)
                    .Select(f => f.TitleLong ?? f.TitleShort)
                    .FirstOrDefaultAsync(ct);
            }

            var facilityLine = string.IsNullOrWhiteSpace(facilityDisplay) ? "N/A" : facilityDisplay;

            var patientEmailForSend = !string.IsNullOrWhiteSpace(patient.Email) ? patient.Email.Trim() : null;

            var sendPatientEmail = includePatientRecipient && !string.IsNullOrWhiteSpace(patientEmailForSend);

            if (recipients.Count == 0 && !sendPatientEmail)
            {
                _logger.LogWarning("No recipients found for refill request notification for patient {PatientId}", patientId);
                return;
            }

            if (includePatientRecipient && !sendPatientEmail)
            {
                _logger.LogWarning(
                    "Refill patient email skipped for patient {PatientId}: no email on patient record or linked user",
                    patientId);
            }

            var adminEmailModels = recipients.Select(recipient => new MailTemplateModel
            {
                ToEmail = recipient.Email,
                ToName = recipient.Name,
                Subject = "Refill Request - TelehealthUS",
                PreviewText = $"{patientName} has requested a refill for {productName}.",
                Greeting = $"Hi {recipient.Name},",
                BodyParagraphs = new List<string>
                {
                    "A patient has requested a refill for their treatment:",
                    "",
                    $"Patient: {patientName}",
                    $"MRN: {patient.MRN ?? "N/A"}",
                    $"Treatment: {productName}",
                    $"Treatment ID: {treatmentId}",
                    $"Facility: {facilityLine}",
                    "",
                    "Please review and process this refill request as soon as possible."
                },
                ButtonText = "View Treatment Details",
                ButtonUrl = $"{FrontendBaseUrl}/treatment/detail/{treatmentId}",
                FooterNote = "This is an automated notification. Please log in to the system to process the refill request."
            }).ToList();

            if (adminEmailModels.Count > 0)
            {
                _backgroundEmailService.SendBatchInBackground(adminEmailModels, CancellationToken.None);
            }

            if (sendPatientEmail)
            {
                var patientFirst = string.IsNullOrWhiteSpace(patient.FirstName) ? "there" : patient.FirstName.Trim();
                var patientEmailModel = new MailTemplateModel
                {
                    ToEmail = patientEmailForSend!,
                    ToName = patientName,
                    Subject = "Refill request submitted - TelehealthUS",
                    PreviewText = $"Your refill request for {productName} has been submitted.",
                    Greeting = $"Hi {patientFirst},",
                    BodyParagraphs = new List<string>
                    {
                        "Our team submitted a refill request on your behalf for your treatment.",
                        "",
                        $"Treatment: {productName}",
                        $"Facility: {facilityLine}",
                        "",
                        "We will review your request and contact you if anything else is needed."
                    },
                    ButtonText = "View Treatment Details",
                    ButtonUrl = $"{FrontendBaseUrl}/treatment/detail/{treatmentId}",
                    FooterNote = "This is an automated message from TelehealthUS."
                };
                _backgroundEmailService.SendInBackground(patientEmailModel, CancellationToken.None);
            }

            _logger.LogInformation(
                "Refill request notification queued for {AdminCount} admin(s){PatientSuffix} (patient {PatientId}, treatment {TreatmentId})",
                recipients.Count,
                sendPatientEmail ? " and the patient" : "",
                patientId,
                treatmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending refill request notification for patient {PatientId}, treatment {TreatmentId}", patientId, treatmentId);

        }
    }

    public async Task SendRefillSubmittedByAdminToPatientAsync(long patientId, long treatmentId, long? facilityId, CancellationToken ct = default)
    {
        try
        {
            var patient = await (
                from p in _db.PT_Patients.AsNoTracking()
                join u in _db.SYS_UserDetails.AsNoTracking() on p.LoginId equals u.LoginId into uj
                from u in uj.DefaultIfEmpty()
                where p.PatientId == patientId
                select new
                {
                    p.PatientId,
                    p.FirstName,
                    p.LastName,
                    Email = !string.IsNullOrWhiteSpace(p.Email) ? p.Email : (u != null ? u.Email : null),
                    PortalUserId = u != null ? (long?)u.UserId : null
                }).FirstOrDefaultAsync(ct);

            if (patient == null)
            {
                _logger.LogWarning("Patient {PatientId} not found for refill-submitted-by-admin notification", patientId);
                return;
            }

            var treatment = await _db.PT_PatientTreatments
                .AsNoTracking()
                .Where(t => t.PatientTreatmentId == treatmentId)
                .FirstOrDefaultAsync(ct);

            if (treatment == null)
            {
                _logger.LogWarning("Treatment {TreatmentId} not found for refill-submitted-by-admin notification", treatmentId);
                return;
            }

            var product = await _db.PD_Bundles
                .AsNoTracking()
                .Where(b => b.BundleId == treatment.ProductId)
                .Select(b => new { b.Name })
                .FirstOrDefaultAsync(ct);

            var productName = product?.Name ?? "Treatment";
            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var targetFacilityId = facilityId ?? treatment.FacilityId;

            string? facilityDisplay = null;
            if (targetFacilityId.HasValue)
            {
                facilityDisplay = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.FacilityId == targetFacilityId.Value)
                    .Select(f => f.TitleLong ?? f.TitleShort)
                    .FirstOrDefaultAsync(ct);
            }

            var facilityLine = string.IsNullOrWhiteSpace(facilityDisplay) ? "N/A" : facilityDisplay;

            var nowUtc = DateTime.UtcNow;
            var duplicateWindowStart = nowUtc.AddMinutes(-2);
            var submittedDescription = $"A Global Administrator has submitted your refill for {productName}.";

            var alreadyExists = await _db.SYS_Notifications
                .AsNoTracking()
                .AnyAsync(n =>
                    n.NotificationType == "RefillSubmittedByAdmin"
                    && n.UserId == patient.PortalUserId
                    && n.PatientId == patientId
                    && n.FacilityId == targetFacilityId
                    && n.Description == submittedDescription
                    && n.CreatedDate >= duplicateWindowStart, ct);

            if (!alreadyExists)
            {
                _db.SYS_Notifications.Add(new SYS_Notification
                {
                    NotificationType = "RefillSubmittedByAdmin",
                    UserId = patient.PortalUserId,
                    PatientId = patientId,
                    FacilityId = targetFacilityId,
                    Description = submittedDescription,
                    CreatedDate = nowUtc,
                    IsRead = false
                });
                await _db.SaveChangesAsync(ct);
            }

            var patientEmailForSend = !string.IsNullOrWhiteSpace(patient.Email) ? patient.Email.Trim() : null;
            if (string.IsNullOrWhiteSpace(patientEmailForSend))
            {
                _logger.LogWarning(
                    "Refill-submitted email skipped for patient {PatientId}: no email on patient record or linked user",
                    patientId);
                return;
            }

            var patientFirst = string.IsNullOrWhiteSpace(patient.FirstName) ? "there" : patient.FirstName.Trim();
            var patientEmailModel = new MailTemplateModel
            {
                ToEmail = patientEmailForSend,
                ToName = patientName,
                Subject = "Your refill has been submitted - TelehealthUS",
                PreviewText = $"A Global Administrator has submitted your refill for {productName}.",
                Greeting = $"Hi {patientFirst},",
                BodyParagraphs = new List<string>
                {
                    "A Global Administrator has submitted your refill request for your treatment.",
                    "",
                    $"Treatment: {productName}",
                    $"Facility: {facilityLine}",
                    "",
                    "You can view details in your portal. Contact us if you have questions."
                },
                ButtonText = "View Treatment Details",
                ButtonUrl = $"{FrontendBaseUrl}/treatment/detail/{treatmentId}",
                FooterNote = "This is an automated message from TelehealthUS."
            };

            _backgroundEmailService.SendInBackground(patientEmailModel, CancellationToken.None);

            _logger.LogInformation(
                "Refill-submitted-by-admin notification saved and patient email queued for patient {PatientId}, treatment {TreatmentId}",
                patientId,
                treatmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending refill-submitted-by-admin notification for patient {PatientId}, treatment {TreatmentId}", patientId, treatmentId);
        }
    }

    public async Task SendIntakeReminderAsync(long appointmentId, CancellationToken ct = default)
    {
        try
        {
            var appointment = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId, ct);

            if (appointment == null || !appointment.PatientId.HasValue || !appointment.PatientTreatmentId.HasValue)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found or missing required fields for intake reminder", appointmentId);
                return;
            }

            var intakeFormFilled = await _db.PT_PatientTreatmentInTakeForms
                .AsNoTracking()
                .AnyAsync(f => f.PatientTreatmentId == appointment.PatientTreatmentId.Value &&
                             !string.IsNullOrWhiteSpace(f.Answer), ct);

            if (intakeFormFilled)
            {
                _logger.LogInformation("Intake form already filled for appointment {AppointmentId}, skipping reminder", appointmentId);
                return;
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId.Value, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                _logger.LogWarning("Patient {PatientId} not found or has no email for intake reminder", appointment.PatientId);
                return;
            }

            var provider = await _db.SYS_UserDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == appointment.ProviderId, ct);

            var providerName = provider != null ? $"{provider.FirstName} {provider.LastName}".Trim() : "Your Provider";

            string appointmentDate = "TBD";
            string appointmentTime = "TBD";
            if (appointment.StartDate.HasValue)
            {

                var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);

                var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);

                appointmentDate = appointmentDateTimeLocal.ToString("MMMM dd, yyyy");
                appointmentTime = appointmentDateTimeLocal.ToString(@"hh\:mm");
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();

            var treatment = await _db.PT_PatientTreatments
                .AsNoTracking()
                .Where(t => t.PatientTreatmentId == appointment.PatientTreatmentId.Value)
                .FirstOrDefaultAsync(ct);

            string? treatmentName = null;
            if (treatment != null && treatment.ProductId.HasValue)
            {

                var bundle = await _db.PD_Bundles
                    .AsNoTracking()
                    .Where(b => b.BundleId == treatment.ProductId.Value)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync(ct);

                if (bundle != null)
                {
                    treatmentName = bundle;
                }
                else
                {

                    var drug = await _db.PD_Drugs
                        .AsNoTracking()
                        .Where(d => d.ProductId == treatment.ProductId.Value)
                        .Select(d => d.CategoryId)
                        .FirstOrDefaultAsync(ct);

                    if (drug.HasValue)
                    {
                        treatmentName = await _db.PD_Categories
                            .AsNoTracking()
                            .Where(c => c.CategoryId == drug.Value)
                            .Select(c => c.CategoryName)
                            .FirstOrDefaultAsync(ct);
                    }
                }
            }

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Intake Form Reminder - TelehealthUS",
                PreviewText = $"Please complete your intake form before your appointment with {providerName}.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"This is a reminder that you have an upcoming appointment with {providerName}.",
                    $"Date: {appointmentDate}",
                    $"Time: {appointmentTime}",
                    !string.IsNullOrWhiteSpace(treatmentName)
                        ? $"Treatment: {treatmentName}"
                        : null,
                    "",
                    "⚠️ IMPORTANT: Please complete your intake form before your appointment.",
                    "If the intake form is not completed 5 minutes before your appointment time, your appointment will be automatically cancelled.",
                    "",
                    "Please complete the intake form as soon as possible to ensure your appointment proceeds as scheduled."
                }.Where(p => p != null).Cast<string>().ToList(),
                ButtonText = "Complete Intake Form",
                ButtonUrl = $"{FrontendBaseUrl}/treatment/detail/{appointment.PatientTreatmentId.Value}",
                FooterNote = "If you have any questions or need assistance, please contact our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Intake reminder sent to patient {PatientId} for appointment {AppointmentId}", appointment.PatientId, appointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending intake reminder for appointment {AppointmentId}", appointmentId);

        }
    }

    public async Task<ManualReminderResultDto> SendTreatmentQuestionnaireReminderIfIncompleteAsync(long patientTreatmentId, CancellationToken ct = default)
    {
        try
        {
            var treatment = await _db.PT_PatientTreatments
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.PatientTreatmentId == patientTreatmentId, ct);

            if (treatment == null)
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Treatment not found." };
            }

            if (!treatment.PatientId.HasValue)
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Treatment has no patient." };
            }

            var intakeFormFilled = await _db.PT_PatientTreatmentInTakeForms
                .AsNoTracking()
                .AnyAsync(f => f.PatientTreatmentId == patientTreatmentId && !string.IsNullOrWhiteSpace(f.Answer), ct);

            if (intakeFormFilled)
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Questionnaire is already completed for this treatment." };
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == treatment.PatientId.Value, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Patient not found or has no email on file." };
            }

            string? treatmentName = null;
            if (treatment.ProductId.HasValue)
            {
                var bundle = await _db.PD_Bundles
                    .AsNoTracking()
                    .Where(b => b.BundleId == treatment.ProductId.Value)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync(ct);

                if (bundle != null)
                {
                    treatmentName = bundle;
                }
                else
                {
                    var drug = await _db.PD_Drugs
                        .AsNoTracking()
                        .Where(d => d.ProductId == treatment.ProductId.Value)
                        .Select(d => d.CategoryId)
                        .FirstOrDefaultAsync(ct);

                    if (drug.HasValue)
                    {
                        treatmentName = await _db.PD_Categories
                            .AsNoTracking()
                            .Where(c => c.CategoryId == drug.Value)
                            .Select(c => c.CategoryName)
                            .FirstOrDefaultAsync(ct);
                    }
                }
            }

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var paragraphs = new List<string?>
            {
                "This is a reminder to complete your health questionnaire (intake form) for your treatment.",
                !string.IsNullOrWhiteSpace(treatmentName) ? $"Treatment: {treatmentName}" : null,
                "",
                "Completing the questionnaire helps your care team provide safe, appropriate care.",
                "Please complete it as soon as you can."
            };

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email?.Trim(),
                ToName = patientName,
                Subject = "Questionnaire Reminder - TelehealthUS",
                PreviewText = "Please complete your treatment questionnaire.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = paragraphs.Where(p => p != null).Cast<string>().ToList(),
                ButtonText = "Complete Questionnaire",
                ButtonUrl = $"{FrontendBaseUrl}/treatment/detail/{patientTreatmentId}",
                FooterNote = "If you have any questions or need assistance, please contact our support team."
            };

            await _mailSender.SendAsync(emailModel, ct);

            _logger.LogInformation("Manual treatment questionnaire reminder sent for PatientTreatmentId {PatientTreatmentId}", patientTreatmentId);
            return new ManualReminderResultDto { EmailSent = true, Message = "Reminder email sent to the patient." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending treatment questionnaire reminder for PatientTreatmentId {PatientTreatmentId}", patientTreatmentId);
            return new ManualReminderResultDto { EmailSent = false, Message = "An error occurred while sending the reminder." };
        }
    }

    public async Task<ManualReminderResultDto> SendInvoicePendingReminderToPatientAsync(int invoiceId, CancellationToken ct = default)
    {
        try
        {
            var invoice = await _db.Sys_Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

            if (invoice == null)
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Invoice not found." };
            }

            if (invoice.IsActive == false)
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Invoice is not active." };
            }

            if (!IsInvoicePaymentPending(invoice.Status))
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Invoice is not pending payment." };
            }

            if (!invoice.PatientId.HasValue)
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Invoice has no patient on file." };
            }

            var patient = await _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == invoice.PatientId.Value, ct);

            if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
            {
                return new ManualReminderResultDto { EmailSent = false, Message = "Patient not found or has no email on file." };
            }

            var formattedAmount = FormatCurrency(invoice.Amount ?? 0);
            var invoiceNumber = invoice.InvoiceNumber ?? invoice.InvoiceId.ToString();
            var recipientName = $"{patient.FirstName} {patient.LastName}".Trim();

            var emailModel = new MailTemplateModel
            {
                ToEmail = patient.Email.Trim(),
                ToName = recipientName,
                Subject = "Billing Reminder - Payment Due - TelehealthUS",
                PreviewText = $"Reminder: Payment of {formattedAmount} is due for invoice {invoiceNumber}.",
                Greeting = $"Hi {patient.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"This is a friendly reminder that a payment of {formattedAmount} is due.",
                    $"Invoice Number: {invoiceId}",
                    "Please make the payment at your earliest convenience to avoid any service interruptions.",
                    "If you've already made this payment, please disregard this reminder."
                },
                ButtonText = "Make Payment",
                ButtonUrl = $"{FrontendBaseUrl}/billing/clinicInvoices/detail/{invoice.InvoiceId}",
                FooterNote = "If you have any questions about this invoice, please contact our billing department."
            };

            await _mailSender.SendAsync(emailModel, ct);

            if (!string.IsNullOrWhiteSpace(patient.Phone))
            {
                var smsMessage = $"TelehealthUS: Payment reminder - {formattedAmount} due for invoice {invoiceNumber}. Pay at {FrontendBaseUrl}";
                await _smsSender.SendAsync(patient.Phone, smsMessage, ct);
            }

            _logger.LogInformation("Manual invoice pending reminder sent to patient {PatientId} for invoice {InvoiceId}", patient.PatientId, invoiceId);
            return new ManualReminderResultDto { EmailSent = true, Message = "Invoice payment reminder sent to the patient." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invoice pending reminder for invoice {InvoiceId}", invoiceId);
            return new ManualReminderResultDto { EmailSent = false, Message = "An error occurred while sending the reminder." };
        }
    }

    public async Task<bool> CancelAppointmentIfIntakeNotFilledAsync(long appointmentId, CancellationToken ct = default)
    {
        try
        {
            var appointment = await _db.PT_PatientAppointmentSlots
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId &&
                                         (a.IsActive == true || a.IsActive == null), ct);

            if (appointment == null || !appointment.PatientTreatmentId.HasValue)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found or missing treatment ID for intake cancellation check", appointmentId);
                return false;
            }

            var intakeFormFilled = await _db.PT_PatientTreatmentInTakeForms
                .AsNoTracking()
                .AnyAsync(f => f.PatientTreatmentId == appointment.PatientTreatmentId.Value &&
                             !string.IsNullOrWhiteSpace(f.Answer), ct);

            if (intakeFormFilled)
            {
                _logger.LogInformation("Intake form is filled for appointment {AppointmentId}, no cancellation needed", appointmentId);
                return false;
            }

            var previousStatus = appointment.Status;
            appointment.Status = "Missed";
            appointment.ModifiedBy = 1;
            appointment.ModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await _auditService.LogEntityChangeAsync(
                action: "Update",
                entityType: "PT_PatientAppointmentSlot",
                entityId: appointmentId,
                oldValues: new { Status = previousStatus },
                userId: 1,
                patientId: appointment.PatientId,
                description: $"Appointment automatically marked as missed due to missing intake form - Patient ID {appointment.PatientId}",
                module: "Appointment"
            );

            try
            {
                await SendAppointmentCancellationAsync(appointmentId, isPatient: true, ct);
                await SendAppointmentCancellationAsync(appointmentId, isPatient: false, ct);

                if (appointment.FacilityId.HasValue)
                {
                    await SendAppointmentCancellationToClinicAdminAsync(appointmentId, null, ct);
                }
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send cancellation notifications for appointment {AppointmentId}", appointmentId);
            }

            _logger.LogInformation("Appointment {AppointmentId} marked missed due to missing intake form", appointmentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling appointment {AppointmentId} due to missing intake form", appointmentId);
            return false;
        }
    }

    public async Task SendClinicAdminCredentialEmailAsync(
           SYS_UserDetail clinicAdmin,
           SYS_Facility facility,
           string temporaryPassword,
           CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clinicAdmin.Email))
                return;

            var adminName = $"{clinicAdmin.FirstName} {clinicAdmin.LastName}".Trim();
            var facilityName = !string.IsNullOrWhiteSpace(facility.TitleLong)
                ? facility.TitleLong
                : facility.TitleShort;

            var emailModel = new MailTemplateModel
            {
                ToEmail = clinicAdmin.Email.Trim(),
                ToName = adminName,
                Subject = "Your TelehealthUS Clinic Admin Account",
                PreviewText = "Your clinic admin account has been created. Use the temporary password below to sign in.",
                Greeting = $"Hi {clinicAdmin.FirstName},",
                BodyParagraphs = new List<string>
                    {
                        !string.IsNullOrWhiteSpace(facilityName)
                            ? $"Your clinic admin account has been created for {facilityName.Trim()}."
                            : "Your clinic admin account has been created.",
                        $"Email: {clinicAdmin.Email.Trim()}",
                        $"Temporary password: {temporaryPassword}",
                        "Please sign in and change your password after your first login.",
                        "Keep this password secure and do not share it."
                    },
                ButtonText = "Sign In",
                ButtonUrl = $"{FrontendBaseUrl}/login",
                FooterNote = "If you did not expect this email, please contact support."
            };

            await _mailSender.SendAsync(emailModel, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send clinic admin credentials email for facility {FacilityId}", facility.FacilityId);
        }
    }

    public async Task SendExternalClinicSignupNotificationToGlobalAdminsAsync(
            SYS_Facility facility,
            SYS_UserDetail clinicAdmin,
            CancellationToken ct)
    {
        try
        {
            var globalAdmins = await (
                from u in _db.SYS_UserDetails.AsNoTracking()
                join l in _db.SYS_Logins.AsNoTracking() on u.LoginId equals l.LoginId
                where u.IsActive == true
                      && !string.IsNullOrWhiteSpace(u.Email)
                      && (l.RoleId == (int)UserRole.SuperAdmin || l.RoleId == (int)UserRole.GlobalAdmin)
                select new { Email = u.Email!, Name = (u.FirstName + " " + u.LastName).Trim() }
            ).ToListAsync(ct);

            if (globalAdmins.Count == 0)
                return;

            var clinicName = string.IsNullOrWhiteSpace(facility.TitleLong) ? facility.TitleShort : facility.TitleLong;
            var adminName = $"{clinicAdmin.FirstName} {clinicAdmin.LastName}".Trim();

            foreach (var admin in globalAdmins)
            {
                var model = new MailTemplateModel
                {
                    ToEmail = admin.Email.Trim(),
                    ToName = admin.Name,
                    Subject = "New clinic sign-up pending approval",
                    PreviewText = "A new clinic has signed up from the external link and is waiting for approval.",
                    Greeting = string.IsNullOrWhiteSpace(admin.Name) ? "Hello," : $"Hello {admin.Name},",
                    BodyParagraphs = new List<string>
                        {
                            "A new clinic registration was submitted through the external sign-up link.",
                            $"Clinic name: {clinicName}",
                            $"Clinic admin: {adminName}",
                            $"Clinic email: {facility.Email}",
                            "Please review and approve this clinic from the admin panel."
                        },

                    ButtonText = "View Clinic",
                    ButtonUrl = $"{FrontendBaseUrl}/clinic/view",
                    FooterNote = "This is an automated notification from TelehealthUS."
                };
                await _mailSender.SendAsync(model, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to notify global admins for facility {FacilityId}", facility.FacilityId);
        }
    }

    public async Task SendClinicPendingApprovalEmailAsync(
        SYS_Facility facility,
        SYS_UserDetail clinicAdmin,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clinicAdmin.Email))
                return;

            var clinicName = string.IsNullOrWhiteSpace(facility.TitleLong) ? facility.TitleShort : facility.TitleLong;
            var adminName = $"{clinicAdmin.FirstName} {clinicAdmin.LastName}".Trim();
            var model = new MailTemplateModel
            {
                ToEmail = clinicAdmin.Email.Trim(),
                ToName = adminName,
                Subject = "Your clinic sign-up is under review",
                PreviewText = "Your account has been created and is currently pending approval.",
                Greeting = string.IsNullOrWhiteSpace(clinicAdmin.FirstName) ? "Hello," : $"Hello {clinicAdmin.FirstName},",
                BodyParagraphs = new List<string>
                    {
                        $"Thank you for signing up your clinic{(string.IsNullOrWhiteSpace(clinicName) ? "." : $" ({clinicName}).")}",
                        "Your clinic admin account has been created and is currently pending approval by our team.",
                        "You will receive another email as soon as your account is approved and activated."
                    },

                FooterNote = "If you have any questions, please contact our support team."
            };
            await _mailSender.SendAsync(model, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send pending approval email for facility {FacilityId}", facility.FacilityId);
        }
    }

    public async Task SendClinicApprovedEmailAsync(
        SYS_Facility facility,
        SYS_UserDetail clinicAdmin,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clinicAdmin.Email))
                return;

            var clinicName = string.IsNullOrWhiteSpace(facility.TitleLong) ? facility.TitleShort : facility.TitleLong;
            var adminName = $"{clinicAdmin.FirstName} {clinicAdmin.LastName}".Trim();
            var model = new MailTemplateModel
            {
                ToEmail = clinicAdmin.Email.Trim(),
                ToName = adminName,
                Subject = "Your clinic account is approved",
                PreviewText = "Great news! Your clinic admin account has been approved and you can now log in.",
                Greeting = string.IsNullOrWhiteSpace(clinicAdmin.FirstName) ? "Hello," : $"Hello {clinicAdmin.FirstName},",
                BodyParagraphs = new List<string>
                {
                    $"Your clinic admin account{(string.IsNullOrWhiteSpace(clinicName) ? " has" : $" for {clinicName} has")} been approved.",
                    "You can now log in and access all the features and services available to your clinic:",
                    "• Manage your staff and providers",
                    "• View patient records and appointments",
                    "• Process orders and prescriptions",
                    "• Access analytics and reports",
                    "• Manage billing and invoices",
                    "If you have any trouble signing in, please contact support."
                },
                ButtonText = "Login to TelehealthUS",
                ButtonUrl = $"{FrontendBaseUrl}/login",
                FooterNote = "Welcome to TelehealthUS!"
            };
            await _mailSender.SendAsync(model, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send clinic approved email for facility {FacilityId}", facility.FacilityId);
        }
    }
}
