using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;
using Vitality.Services.Notifications;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AuthorizeRoles(UserRole.GlobalAdmin, UserRole.SuperAdmin)]
    public class NotificationTestController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly MainContext _db;
        private readonly ILogger<NotificationTestController> _logger;

        public NotificationTestController(
            INotificationService notificationService,
            MainContext db,
            ILogger<NotificationTestController> logger)
        {
            _notificationService = notificationService;
            _db = db;
            _logger = logger;
        }

        [HttpGet("testAlls")]
        public async Task<ApiResponse<Dictionary<string, string>>> TestAllNotifications([FromQuery] string? testEmail = null)
        {
            var results = new Dictionary<string, string>();
            var email = testEmail ?? User.FindFirst("Email")?.Value ?? "test@example.com";

            try
            {

                var testPatient = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => !string.IsNullOrWhiteSpace(p.Email) && p.Email == "check@yopmail.com")
                    .FirstOrDefaultAsync();

                var testFacility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => !string.IsNullOrWhiteSpace(f.Email))
                    .FirstOrDefaultAsync();

                var testUser = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                    .FirstOrDefaultAsync();

                long? testUserFacilityId = null;
                if (testUser != null)
                {
                    var userFacility = await _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Where(uf => uf.UserId == testUser.UserId && uf.IsAssign == true)
                        .Select(uf => uf.FacilityId)
                        .FirstOrDefaultAsync();
                    testUserFacilityId = userFacility;
                }

                var testOrder = await _db.PT_PatientOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                var testAppointment = await _db.PT_PatientAppointmentSlots
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                var testPrescription = await _db.PT_PatientPrescriptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                var testTreatment = await _db.PT_PatientTreatments
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                var testTicket = await _db.SYS_Tickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (testPatient != null)
                {
                    try
                    {
                        await _notificationService.SendPaymentConfirmationAsync(
                            testPatient.PatientId, 100.50m, "TEST-TXN-001", HttpContext.RequestAborted);
                        results["1. Payment Confirmation"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["1. Payment Confirmation"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPatient != null && testOrder != null)
                {
                    try
                    {
                        await _notificationService.SendOrderToPharmacyAsync(
                            testPatient.PatientId, testOrder.PatientOrderId, HttpContext.RequestAborted);
                        results["2. Order Sent to Pharmacy"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["2. Order Sent to Pharmacy"] = $"❌ Error: {ex.Message}"; }
                }

                try
                {
                    await _notificationService.SendBillingReminderAsync(
                        testPatient?.PatientId, testFacility?.FacilityId, 250.00m, "INV-TEST-001", HttpContext.RequestAborted);
                    results["3. Billing Reminder"] = "✅ Sent";
                }
                catch (Exception ex) { results["3. Billing Reminder"] = $"❌ Error: {ex.Message}"; }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendOverdueClinicBillAsync(
                            testFacility.FacilityId, 5000.00m, "INV-OVERDUE-001", HttpContext.RequestAborted);
                        results["4. Overdue Clinic Bill"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["4. Overdue Clinic Bill"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPatient != null)
                {
                    try
                    {
                        await _notificationService.SendPatientSignUpAsync(
                            testPatient.PatientId, HttpContext.RequestAborted);
                        results["5. Patient Sign Up"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["5. Patient Sign Up"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPatient != null && !string.IsNullOrWhiteSpace(testPatient.Email))
                {
                    try
                    {
                        await _notificationService.SendPasswordResetSmsAsync(
                            testPatient.Email, "123456", HttpContext.RequestAborted);
                        results["6. Password Reset SMS"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["6. Password Reset SMS"] = $"❌ Error: {ex.Message}"; }
                }

                if (testAppointment != null)
                {
                    try
                    {
                        await _notificationService.SendAppointmentConfirmationAsync(
                            testAppointment.PatientAppointmentSlotId, true, HttpContext.RequestAborted);
                        results["7. Appointment Confirmation (Patient)"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["7. Appointment Confirmation (Patient)"] = $"❌ Error: {ex.Message}"; }

                    try
                    {
                        await _notificationService.SendAppointmentConfirmationAsync(
                            testAppointment.PatientAppointmentSlotId, false, HttpContext.RequestAborted);
                        results["7. Appointment Confirmation (Provider)"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["7. Appointment Confirmation (Provider)"] = $"❌ Error: {ex.Message}"; }
                }

                if (testAppointment != null)
                {
                    try
                    {
                        await _notificationService.SendAppointmentCancellationAsync(
                            testAppointment.PatientAppointmentSlotId, true, HttpContext.RequestAborted);
                        results["8. Appointment Cancellation (Patient)"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["8. Appointment Cancellation (Patient)"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPatient != null && testTreatment != null)
                {
                    try
                    {
                        await _notificationService.SendTreatmentSelectionAsync(
                            testPatient.PatientId, testTreatment.PatientTreatmentId, HttpContext.RequestAborted);
                        results["9. Treatment Selection"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["9. Treatment Selection"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPatient != null && testOrder != null)
                {
                    try
                    {
                        await _notificationService.SendOrderStatusUpdateAsync(
                            testPatient.PatientId, testOrder.PatientOrderId, "Start", HttpContext.RequestAborted);
                        results["10. Order Status Update"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["10. Order Status Update"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPatient != null && testTreatment != null)
                {
                    try
                    {
                        await _notificationService.SendRefillNotificationAsync(
                            testPatient.PatientId, testTreatment.PatientTreatmentId, HttpContext.RequestAborted);
                        results["11. Refill Notification"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["11. Refill Notification"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendClinicSignUpAsync(
                            testFacility.FacilityId, HttpContext.RequestAborted);
                        results["12. Clinic Sign Up"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["12. Clinic Sign Up"] = $"❌ Error: {ex.Message}"; }
                }

                if (testUser != null)
                {
                    try
                    {
                        await _notificationService.SendDoctorSignUpAsync(
                            testUser.UserId, HttpContext.RequestAborted);
                        results["13. Doctor Sign Up"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["13. Doctor Sign Up"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPrescription != null)
                {
                    try
                    {
                        await _notificationService.SendPrescriptionUploadedAsync(
                            testPrescription.PatientPrescriptionId, HttpContext.RequestAborted);
                        results["14. Prescription Uploaded"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["14. Prescription Uploaded"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPrescription != null)
                {
                    try
                    {
                        await _notificationService.SendPrescriptionEditedAsync(
                            testPrescription.PatientPrescriptionId, HttpContext.RequestAborted);
                        results["15. Prescription Edited"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["15. Prescription Edited"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendMonthlyInvoiceGeneratedAsync(
                            testFacility.FacilityId, 10000.00m, "INV-MONTHLY-001", HttpContext.RequestAborted);
                        results["16. Monthly Invoice Generated"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["16. Monthly Invoice Generated"] = $"❌ Error: {ex.Message}"; }
                }

                if (testPatient != null)
                {
                    try
                    {
                        await _notificationService.SendNewPatientAlertAsync(
                            testPatient.PatientId, testPatient.FacilityId, HttpContext.RequestAborted);
                        results["17. New Patient Alert"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["17. New Patient Alert"] = $"❌ Error: {ex.Message}"; }
                }

                try
                {
                    await _notificationService.SendMessageNotificationAsync(
                        email, "Test Sender", "This is a test message preview...", HttpContext.RequestAborted);
                    results["18. Message Notification"] = "✅ Sent";
                }
                catch (Exception ex) { results["18. Message Notification"] = $"❌ Error: {ex.Message}"; }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendPriceChangeNotificationAsync(
                            testFacility.FacilityId,
                            "Test Product",
                            100.00m,
                            120.00m,
                            1,
                            false,
                            HttpContext.RequestAborted);
                        results["19. Price Change Notification"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["19. Price Change Notification"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendMedicineCatalogUpdateAsync(
                            testFacility.FacilityId,
                            "Added",
                            "Test Medicine",
                            1,
                            false,
                            previousDrugName: null,
                            ct: HttpContext.RequestAborted);
                        results["20. Medicine Catalog Update"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["20. Medicine Catalog Update"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendMonthlyAnalyticsAsync(
                            testFacility.FacilityId, HttpContext.RequestAborted);
                        results["21. Monthly Analytics"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["21. Monthly Analytics"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendStaffChangeNotificationAsync(
                            testFacility.FacilityId, "Added", "Test Staff Member", HttpContext.RequestAborted);
                        results["22. Staff Change Notification"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["22. Staff Change Notification"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendCouponCreatedAsync(
                            testFacility.FacilityId, "TEST2024", HttpContext.RequestAborted);
                        results["23. Coupon Created"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["23. Coupon Created"] = $"❌ Error: {ex.Message}"; }
                }

                if (testUser != null)
                {
                    try
                    {
                        await _notificationService.SendGlobalAdminSignUpAsync(
                            testUser.UserId, HttpContext.RequestAborted);
                        results["24. Global Admin Sign Up"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["24. Global Admin Sign Up"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendClinicManagementChangeAsync(
                            "Add", testFacility.FacilityId, testFacility.TitleLong ?? "Test Clinic", HttpContext.RequestAborted);
                        results["25. Clinic Management Change"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["25. Clinic Management Change"] = $"❌ Error: {ex.Message}"; }
                }

                if (testFacility != null)
                {
                    try
                    {
                        await _notificationService.SendPharmacyPriceUpdateAsync(
                            testFacility.FacilityId, "Test Pharmacy Product", HttpContext.RequestAborted);
                        results["26. Pharmacy Price Update"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["26. Pharmacy Price Update"] = $"❌ Error: {ex.Message}"; }
                }

                if (testUser != null)
                {
                    try
                    {
                        var testUserRoleId = await _db.SYS_Logins
                            .AsNoTracking()
                            .Where(l => l.LoginId == testUser.LoginId)
                            .Select(l => l.RoleId)
                            .FirstOrDefaultAsync();

                        await _notificationService.SendUserManagementChangeAsync(
                            "Update", $"{testUser.FirstName} {testUser.LastName}", testUserFacilityId, testUserRoleId, HttpContext.RequestAborted);
                        results["27. User Management Change"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["27. User Management Change"] = $"❌ Error: {ex.Message}"; }
                }

                if (testTicket != null)
                {
                    try
                    {
                        await _notificationService.SendSupportTicketNotificationAsync(
                            testTicket.TicketId, email, HttpContext.RequestAborted);
                        results["28. Support Ticket Notification"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["28. Support Ticket Notification"] = $"❌ Error: {ex.Message}"; }
                }

                if (testUser != null)
                {
                    try
                    {
                        await _notificationService.SendTimeSlotConfirmationAsync(
                            testUser.UserId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), HttpContext.RequestAborted);
                        results["29. Time Slot Confirmation"] = "✅ Sent";
                    }
                    catch (Exception ex) { results["29. Time Slot Confirmation"] = $"❌ Error: {ex.Message}"; }
                }

                results["Summary"] = $"Total: {results.Count(r => r.Value.Contains("✅"))} successful, {results.Count(r => r.Value.Contains("❌"))} failed";
            }
            catch (Exception ex)
            {
                results["Error"] = $"Critical error: {ex.Message}";
            }

            var response = new ApiResponse<Dictionary<string, string>>
            {
                Data = results,
                Message = "Notification test completed. Check results for details.",
                Status = 1
            };

            return response;
        }

        [HttpGet("testSingle")]
        public async Task<ApiResponse<string>> TestSingleNotification([FromQuery] int id, [FromQuery] string? testEmail = null)
        {
            var email = testEmail ?? User.FindFirst("Email")?.Value ?? "test@example.com";
            string result = "";

            try
            {
                var testPatient = await _db.PT_Patients.AsNoTracking().Where(p => !string.IsNullOrWhiteSpace(p.Email)).FirstOrDefaultAsync();
                var testFacility = await _db.SYS_Facilities.AsNoTracking().Where(f => !string.IsNullOrWhiteSpace(f.Email)).FirstOrDefaultAsync();
                var testUser = await _db.SYS_UserDetails.AsNoTracking().Where(u => !string.IsNullOrWhiteSpace(u.Email)).FirstOrDefaultAsync();

                long? testUserFacilityId = null;
                if (testUser != null)
                {
                    var userFacility = await _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Where(uf => uf.UserId == testUser.UserId && uf.IsAssign == true)
                        .Select(uf => uf.FacilityId)
                        .FirstOrDefaultAsync();
                    testUserFacilityId = userFacility;
                }

                var testOrder = await _db.PT_PatientOrders.AsNoTracking().FirstOrDefaultAsync();
                var testAppointment = await _db.PT_PatientAppointmentSlots.AsNoTracking().FirstOrDefaultAsync();
                var testPrescription = await _db.PT_PatientPrescriptions.AsNoTracking().FirstOrDefaultAsync();
                var testTreatment = await _db.PT_PatientTreatments.AsNoTracking().FirstOrDefaultAsync();
                var testTicket = await _db.SYS_Tickets.AsNoTracking().FirstOrDefaultAsync();

                switch (id)
                {
                    case 1:
                        if (testPatient != null)
                            await _notificationService.SendPaymentConfirmationAsync(testPatient.PatientId, 100.50m, "TEST-TXN", HttpContext.RequestAborted);
                        result = "Payment Confirmation sent";
                        break;
                    case 2:
                        if (testPatient != null && testOrder != null)
                            await _notificationService.SendOrderToPharmacyAsync(testPatient.PatientId, testOrder.PatientOrderId, HttpContext.RequestAborted);
                        result = "Order to Pharmacy sent";
                        break;
                    case 3:
                        await _notificationService.SendBillingReminderAsync(testPatient?.PatientId, testFacility?.FacilityId, 250m, "INV-TEST", HttpContext.RequestAborted);
                        result = "Billing Reminder sent";
                        break;
                    case 4:
                        if (testFacility != null)
                            await _notificationService.SendOverdueClinicBillAsync(testFacility.FacilityId, 5000m, "INV-OVERDUE", HttpContext.RequestAborted);
                        result = "Overdue Clinic Bill sent";
                        break;
                    case 5:
                        if (testPatient != null)
                            await _notificationService.SendPatientSignUpAsync(testPatient.PatientId, HttpContext.RequestAborted);
                        result = "Patient Sign Up sent";
                        break;
                    case 6:
                        if (testPatient != null && !string.IsNullOrWhiteSpace(testPatient.Email))
                            await _notificationService.SendPasswordResetSmsAsync(testPatient.Email, "123456", HttpContext.RequestAborted);
                        result = "Password Reset SMS sent";
                        break;
                    case 7:
                        if (testAppointment != null)
                        {
                            await _notificationService.SendAppointmentConfirmationAsync(testAppointment.PatientAppointmentSlotId, true, HttpContext.RequestAborted);
                            await _notificationService.SendAppointmentConfirmationAsync(testAppointment.PatientAppointmentSlotId, false, HttpContext.RequestAborted);
                        }
                        result = "Appointment Confirmation sent";
                        break;
                    case 8:
                        if (testAppointment != null)
                            await _notificationService.SendAppointmentCancellationAsync(testAppointment.PatientAppointmentSlotId, true, HttpContext.RequestAborted);
                        result = "Appointment Cancellation sent";
                        break;
                    case 9:
                        if (testPatient != null && testTreatment != null)
                            await _notificationService.SendTreatmentSelectionAsync(testPatient.PatientId, testTreatment.PatientTreatmentId, HttpContext.RequestAborted);
                        result = "Treatment Selection sent";
                        break;
                    case 10:
                        if (testPatient != null && testOrder != null)
                            await _notificationService.SendOrderStatusUpdateAsync(testPatient.PatientId, testOrder.PatientOrderId, "Start", HttpContext.RequestAborted);
                        result = "Order Status Update sent";
                        break;
                    case 11:
                        if (testPatient != null && testTreatment != null)
                            await _notificationService.SendRefillNotificationAsync(testPatient.PatientId, testTreatment.PatientTreatmentId, HttpContext.RequestAborted);
                        result = "Refill Notification sent";
                        break;
                    case 12:
                        if (testFacility != null)
                            await _notificationService.SendClinicSignUpAsync(testFacility.FacilityId, HttpContext.RequestAborted);
                        result = "Clinic Sign Up sent";
                        break;
                    case 13:
                        if (testUser != null)
                            await _notificationService.SendDoctorSignUpAsync(testUser.UserId, HttpContext.RequestAborted);
                        result = "Doctor Sign Up sent";
                        break;
                    case 14:
                        if (testPrescription != null)
                            await _notificationService.SendPrescriptionUploadedAsync(testPrescription.PatientPrescriptionId, HttpContext.RequestAborted);
                        result = "Prescription Uploaded sent";
                        break;
                    case 15:
                        if (testPrescription != null)
                            await _notificationService.SendPrescriptionEditedAsync(testPrescription.PatientPrescriptionId, HttpContext.RequestAborted);
                        result = "Prescription Edited sent";
                        break;
                    case 16:
                        if (testFacility != null)
                            await _notificationService.SendMonthlyInvoiceGeneratedAsync(testFacility.FacilityId, 10000m, "INV-MONTHLY", HttpContext.RequestAborted);
                        result = "Monthly Invoice Generated sent";
                        break;
                    case 17:
                        if (testPatient != null)
                            await _notificationService.SendNewPatientAlertAsync(testPatient.PatientId, testPatient.FacilityId, HttpContext.RequestAborted);
                        result = "New Patient Alert sent";
                        break;
                    case 18:
                        await _notificationService.SendMessageNotificationAsync(email, "Test Sender", "Test message preview", HttpContext.RequestAborted);
                        result = "Message Notification sent";
                        break;
                    case 19:
                        if (testFacility != null)
                            await _notificationService.SendPriceChangeNotificationAsync(
                                testFacility.FacilityId, "Test Product", 100m, 120m, 1, false, HttpContext.RequestAborted);
                        result = "Price Change Notification sent";
                        break;
                    case 20:
                        if (testFacility != null)
                            await _notificationService.SendMedicineCatalogUpdateAsync(
                                testFacility.FacilityId, "Added", "Test Medicine", 1, false, previousDrugName: null, ct: HttpContext.RequestAborted);
                        result = "Medicine Catalog Update sent";
                        break;
                    case 21:
                        if (testFacility != null)
                            await _notificationService.SendMonthlyAnalyticsAsync(testFacility.FacilityId, HttpContext.RequestAborted);
                        result = "Monthly Analytics sent";
                        break;
                    case 22:
                        if (testFacility != null)
                            await _notificationService.SendStaffChangeNotificationAsync(testFacility.FacilityId, "Added", "Test Staff", HttpContext.RequestAborted);
                        result = "Staff Change Notification sent";
                        break;
                    case 23:
                        if (testFacility != null)
                            await _notificationService.SendCouponCreatedAsync(testFacility.FacilityId, "TEST2024", HttpContext.RequestAborted);
                        result = "Coupon Created sent";
                        break;
                    case 24:
                        if (testUser != null)
                            await _notificationService.SendGlobalAdminSignUpAsync(testUser.UserId, HttpContext.RequestAborted);
                        result = "Global Admin Sign Up sent";
                        break;
                    case 25:
                        if (testFacility != null)
                            await _notificationService.SendClinicManagementChangeAsync("Add", testFacility.FacilityId, testFacility.TitleLong ?? "Test", HttpContext.RequestAborted);
                        result = "Clinic Management Change sent";
                        break;
                    case 26:
                        if (testFacility != null)
                            await _notificationService.SendPharmacyPriceUpdateAsync(testFacility.FacilityId, "Test Product", HttpContext.RequestAborted);
                        result = "Pharmacy Price Update sent";
                        break;
                    case 27:
                        if (testUser != null)
                        {

                            var userFacilityId = await _db.FC_UsersInFacilities
                                .AsNoTracking()
                                .Where(uf => uf.UserId == testUser.UserId && uf.IsAssign == true)
                                .Select(uf => uf.FacilityId)
                                .FirstOrDefaultAsync();
                            var testUserRoleId = await _db.SYS_Logins
                                .AsNoTracking()
                                .Where(l => l.LoginId == testUser.LoginId)
                                .Select(l => l.RoleId)
                                .FirstOrDefaultAsync();

                            await _notificationService.SendUserManagementChangeAsync("Update", $"{testUser.FirstName} {testUser.LastName}", userFacilityId, testUserRoleId, HttpContext.RequestAborted);
                        }
                        result = "User Management Change sent";
                        break;
                    case 28:
                        if (testTicket != null)
                            await _notificationService.SendSupportTicketNotificationAsync(testTicket.TicketId, email, HttpContext.RequestAborted);
                        result = "Support Ticket Notification sent";
                        break;
                    case 29:
                        if (testUser != null)
                            await _notificationService.SendTimeSlotConfirmationAsync(testUser.UserId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), HttpContext.RequestAborted);
                        result = "Time Slot Confirmation sent";
                        break;
                    default:
                        result = $"Invalid notification ID. Use 1-29.";
                        break;
                }

                return new ApiResponse<string> { Data = result, Message = "Notification sent successfully", Status = 1 };
            }
            catch (Exception ex)
            {
                return new ApiResponse<string> { Data = $"Error: {ex.Message}", Message = "Failed to send notification", Status = 0 };
            }
        }

        [HttpGet("list")]
        public ApiResponse<List<string>> GetNotificationList()
        {
            var list = new List<string>
            {
                "1. Payment Confirmation",
                "2. Order Sent to Pharmacy",
                "3. Billing Reminder",
                "4. Overdue Clinic Bill",
                "5. Patient Sign Up",
                "6. Password Reset SMS",
                "7. Appointment Confirmation",
                "8. Appointment Cancellation",
                "9. Treatment Selection",
                "10. Order Status Update",
                "11. Refill Notification",
                "12. Clinic Sign Up",
                "13. Doctor Sign Up",
                "14. Prescription Uploaded",
                "15. Prescription Edited",
                "16. Monthly Invoice Generated",
                "17. New Patient Alert",
                "18. Message Notification",
                "19. Price Change Notification",
                "20. Medicine Catalog Update",
                "21. Monthly Analytics",
                "22. Staff Change Notification",
                "23. Coupon Created",
                "24. Global Admin Sign Up",
                "25. Clinic Management Change",
                "26. Pharmacy Price Update",
                "27. User Management Change",
                "28. Support Ticket Notification",
                "29. Time Slot Confirmation"
            };

            return new ApiResponse<List<string>>
            {
                Data = list,
                Message = "Available notification types",
                Status = 1
            };
        }
    }
}
