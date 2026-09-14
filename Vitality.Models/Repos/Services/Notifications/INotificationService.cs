using Vitality.Models.DTOs.Reminders;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Services;
public interface INotificationService
{
    Task SendPaymentConfirmationAsync(long patientId, decimal amount, string transactionId, CancellationToken ct = default);

    Task SendPaymentFailedAsync(long patientId, decimal amount, long? treatmentId, string? failureReason, int retryCount = 1, CancellationToken ct = default);

    Task SendSubscriptionEndedDueToFailureAsync(long patientId, long? treatmentId, int totalRetries, CancellationToken ct = default);

    Task SendSubscriptionPausedAsync(long patientId, long treatmentId, CancellationToken ct = default);

    Task SendSubscriptionCancelledAsync(long patientId, long treatmentId, CancellationToken ct = default);
    Task SendInvoicePaidNotificationAsync(long? patientId, long? facilityId, decimal amount, string invoiceNumber, long? adminUserId = null, CancellationToken ct = default);
    Task SendInvoiceCancelledNotificationAsync(long invoiceId, string? cancellationReason = null, CancellationToken ct = default);
    Task SendOrderToPharmacyAsync(long patientId, long orderId, CancellationToken ct = default);
    Task SendBillingReminderAsync(long? patientId, long? facilityId, decimal amount, string invoiceNumber, CancellationToken ct = default);
    Task SendOverdueClinicBillAsync(long facilityId, decimal amount, string invoiceNumber, CancellationToken ct = default);
    Task SendPatientSignUpAsync(long patientId, CancellationToken ct = default);
    Task SendPasswordResetSmsAsync(string email, string resetCode, CancellationToken ct = default);
    Task SendAppointmentConfirmationAsync(long appointmentId, bool isPatient, CancellationToken ct = default);
    Task SendAppointmentConfirmationToClinicAdminAsync(long appointmentId, long? adminUserId = null, CancellationToken ct = default);
    Task SendAppointmentCancellationAsync(long appointmentId, bool isPatient, CancellationToken ct = default);
    Task SendAppointmentCancellationToClinicAdminAsync(long appointmentId, long? adminUserId = null, CancellationToken ct = default);
    Task SendTreatmentSelectionAsync(long patientId, long treatmentId, CancellationToken ct = default);
    Task SendOrderStatusUpdateAsync(long patientId, long orderId, string status, CancellationToken ct = default);
    Task SendRefillNotificationAsync(long patientId, long treatmentId, CancellationToken ct = default);
    Task SendClinicSignUpAsync(long facilityId, CancellationToken ct = default);
    Task SendDoctorSignUpAsync(long userId, CancellationToken ct = default);
    Task SendClinicAdminSignUpAsync(long userId, long? facilityId, CancellationToken ct = default);
    Task SendPrescriptionUploadedAsync(long prescriptionId, CancellationToken ct = default);
    Task SendPrescriptionCreatedAsync(long prescriptionId, CancellationToken ct = default);
    Task SendPrescriptionEditedAsync(long prescriptionId, CancellationToken ct = default);
    Task SendMonthlyInvoiceGeneratedAsync(long facilityId, decimal amount, string invoiceNumber, CancellationToken ct = default);
    Task SendManualInvoiceCreatedNotificationAsync(long facilityId, long? patientId, decimal amount, string invoiceNumber, CancellationToken ct = default);
    Task SendNewPatientAlertAsync(long patientId, long? facilityId, CancellationToken ct = default);
    Task SendManualPatientWelcomeWithPasswordAsync(long patientId, string temporaryPassword, CancellationToken ct = default);
    Task SendMessageNotificationAsync(string recipientEmail, string senderName, string messagePreview, CancellationToken ct = default);
    Task<bool> SendUnreadMessageReminderEmailAsync(string recipientEmail, string messagePreview, CancellationToken ct = default);
    Task SendPriceChangeNotificationAsync(long facilityId, string productName, decimal oldPrice, decimal newPrice, long? drugId, bool isCustom, CancellationToken ct = default);

    Task SendBundleRecurringStatusChangeAsync(long patientId, long treatmentId, long bundleId, bool isNowEnabled, CancellationToken ct = default);

    Task SendMedicineCatalogUpdateAsync(long facilityId, string action, string productName, long? drugId, bool isCustom, string? previousDrugName = null, CancellationToken ct = default);
    Task SendMonthlyAnalyticsAsync(long facilityId, CancellationToken ct = default);
    Task SendStaffChangeNotificationAsync(long facilityId, string action, string staffName, CancellationToken ct = default);
    Task SendCouponCreatedAsync(long facilityId, string couponCode, CancellationToken ct = default);
    Task SendGlobalAdminSignUpAsync(long userId, CancellationToken ct = default);
    Task SendClinicManagementChangeAsync(string action, long? facilityId, string facilityName, CancellationToken ct = default);
    Task SendPharmacyPriceUpdateAsync(long facilityId, string productName, CancellationToken ct = default);
    Task SendUserManagementChangeAsync(string action, string userName, long? facilityId, int? changedUserRoleId = null, CancellationToken ct = default);
    Task SendSupportTicketNotificationAsync(long ticketId, string recipientEmail, CancellationToken ct = default);

    Task SendTicketAssignedToTechSupportAsync(long ticketId, string recipientEmail, string subject, string priority, CancellationToken ct = default);

    Task SendTicketUpdateToTechSupportAsync(long ticketId, string recipientEmail, string updateType, string updateDetail, string? ticketSubject, CancellationToken ct = default);

    Task SendTicketCommentByTechSupportToGlobalAdminAsync(long ticketId, string recipientEmail, string techSupportUserName, string ticketSubject, string commentPreview, CancellationToken ct = default);
    Task SendTimeSlotConfirmationAsync(long providerId, DateTime slotDate, TimeSpan slotTime, CancellationToken ct = default);

    Task SendRefillRequestNotificationAsync(long patientId, long treatmentId, long? facilityId, CancellationToken ct = default, bool includePatientRecipient = false, bool notifyGlobalAdminsInApp = false);

    Task SendRefillSubmittedByAdminToPatientAsync(long patientId, long treatmentId, long? facilityId, CancellationToken ct = default);
    Task SendIntakeReminderAsync(long appointmentId, CancellationToken ct = default);
    Task<bool> CancelAppointmentIfIntakeNotFilledAsync(long appointmentId, CancellationToken ct = default);

    Task<ManualReminderResultDto> SendTreatmentQuestionnaireReminderIfIncompleteAsync(long patientTreatmentId, CancellationToken ct = default);

    Task<ManualReminderResultDto> SendInvoicePendingReminderToPatientAsync(int invoiceId, CancellationToken ct = default);
    Task SendClinicAdminCredentialEmailAsync(SYS_UserDetail clinicAdmin, SYS_Facility facility, string temporaryPassword, CancellationToken ct = default);
    Task SendExternalClinicSignupNotificationToGlobalAdminsAsync(SYS_Facility facility, SYS_UserDetail clinicAdmin, CancellationToken ct = default);
    Task SendClinicPendingApprovalEmailAsync(SYS_Facility facility, SYS_UserDetail clinicAdmin, CancellationToken ct = default);
    Task SendClinicApprovedEmailAsync(SYS_Facility facility, SYS_UserDetail clinicAdmin, CancellationToken ct = default);
}
