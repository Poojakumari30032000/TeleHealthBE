using Microsoft.AspNetCore.Mvc;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.Reminders;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AuthorizeRoles(UserRole.GlobalAdmin, UserRole.SuperAdmin, UserRole.ClinicAdmin)]
    public class ReminderEmailsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<ReminderEmailsController> _logger;

        public ReminderEmailsController(
            INotificationService notificationService,
            ILogger<ReminderEmailsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPost("treatment-questionnaire")]
        public async Task<ApiResponse<ManualReminderResultDto>> SendTreatmentQuestionnaireReminder(
            [FromQuery] long patientTreatmentId,
            CancellationToken cancellationToken)
        {
            var response = new ApiResponse<ManualReminderResultDto>();
            try
            {
                response.Data = await _notificationService.SendTreatmentQuestionnaireReminderIfIncompleteAsync(
                    patientTreatmentId,
                    cancellationToken);
                response.Success = response.Data.EmailSent;
                response.Message = response.Data.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendTreatmentQuestionnaireReminder failed for treatment {PatientTreatmentId}", patientTreatmentId);
                response.Success = false;
                response.Message = ex.Message;
                response.Data = new ManualReminderResultDto { EmailSent = false, Message = ex.Message };
            }

            return response;
        }

        [HttpPost("invoice-pending")]
        public async Task<ApiResponse<ManualReminderResultDto>> SendInvoicePendingReminder(
            [FromQuery] int invoiceId,
            CancellationToken cancellationToken)
        {
            var response = new ApiResponse<ManualReminderResultDto>();
            try
            {
                if (invoiceId <= 0)
                {
                    response.Success = false;
                    response.Message = "InvoiceId is required.";
                    response.Data = new ManualReminderResultDto { EmailSent = false, Message = response.Message };
                    return response;
                }

                response.Data = await _notificationService.SendInvoicePendingReminderToPatientAsync(
                    invoiceId,
                    cancellationToken);
                response.Success = response.Data.EmailSent;
                response.Message = response.Data.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendInvoicePendingReminder failed for invoice {InvoiceId}", invoiceId);
                response.Success = false;
                response.Message = ex.Message;
                response.Data = new ManualReminderResultDto { EmailSent = false, Message = ex.Message };
            }

            return response;
        }
    }
}
