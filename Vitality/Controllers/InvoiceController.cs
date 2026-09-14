using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Square.Models;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.Appointments;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Models.EntityClasses;

namespace Vitality.Controllers
{

    [Route("api/[controller]")]
    [ApiController]

    public class InvoicesController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IInvoiceRepo _IInvoicesRepo;
        private readonly INotificationService _notificationService;
        private readonly MainContext _db;

        public InvoicesController(
            IConfiguration config,
            IMapper IMapper,
            IInvoiceRepo IInvoicesRepo,
            INotificationService notificationService,
            MainContext db)
        {
            _configuration = config;
            _mapper = IMapper;
            _IInvoicesRepo = IInvoicesRepo;
            _notificationService = notificationService;
            _db = db;
        }

        [HttpPost]
        [Route("UpdateAppointmentStatus")]
        public async Task<ApiResponse<bool>> UpdateAppointmentStatus([FromBody] UpdateAppointmentStatusRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                if (request == null || request.AppointmentId <= 0 || string.IsNullOrWhiteSpace(request.Status))
                {
                    response.Message = "Invalid request data.";
                    response.Status = 0;
                    response.Data = false;
                    return response;
                }

                var result = await _IInvoicesRepo.UpdateAppointmentStatus(request.AppointmentId, request.Status);
                response.Data = result;
                response.Status = result ? 1 : 0;
                response.Message = result ? "Appointment status updated successfully." : "Failed to update appointment status.";

                if (result && request.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {

                        long? adminUserId = await _db.PT_PatientAppointmentSlots
                            .AsNoTracking()
                            .Where(a => a.PatientAppointmentSlotId == request.AppointmentId)
                            .Select(a => a.CreatedBy)
                            .FirstOrDefaultAsync();

                        await _notificationService.SendAppointmentCancellationAsync(
                            appointmentId: request.AppointmentId,
                            isPatient: true,
                            ct: HttpContext.RequestAborted
                        );

                        await _notificationService.SendAppointmentCancellationAsync(
                            appointmentId: request.AppointmentId,
                            isPatient: false,
                            ct: HttpContext.RequestAborted
                        );

                        await _notificationService.SendAppointmentCancellationToClinicAdminAsync(
                            appointmentId: request.AppointmentId,
                            adminUserId: adminUserId,
                            ct: HttpContext.RequestAborted
                        );
                    }
                    catch (Exception emailEx)
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
                response.Data = false;
            }

            return response;
        }

        [HttpGet]
        [Route("GetPatientInvoiceDetailById")]
        public ApiResponse<GetPatientBillResponseDTO> GetPatientInvoiceDetailById([FromQuery] long invoiceId)
        {
            ApiResponse<GetPatientBillResponseDTO> response = new ApiResponse<GetPatientBillResponseDTO>();
            try
            {
                var result = _IInvoicesRepo.GetPatientInvoiceDetail(invoiceId);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpGet]
        [Route("GetInvoiceById")]
        public ApiResponse<GetInvoiceByIdResponseDTO> GetInvoiceById([FromQuery] string? InvoiceId)
        {
            ApiResponse<GetInvoiceByIdResponseDTO> response = new ApiResponse<GetInvoiceByIdResponseDTO>();
            try
            {
                GetInvoiceByIdResponseDTO result = new GetInvoiceByIdResponseDTO();
                result = _IInvoicesRepo.GetInvoiceById(long.Parse(InvoiceId));
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveInvoice")]
        public ApiResponse<bool> SaveInvoice([FromBody] SaveInvoiceRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                bool res = _IInvoicesRepo.SaveInvoice(request, UserId);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("GetAllInvoices")]

        public IActionResult GetAllInvoices()
        {
            var invoices = _IInvoicesRepo.GetAllInvoices();
            return Ok(invoices);
        }
        [AuthorizeRoles(UserRole.Provider, UserRole.ClinicAdmin , UserRole.GlobalAdmin)]
        [HttpPost]
        [Route("GetInvoicesByFacilityIds")]
        public IActionResult GetInvoicesByFacilityIds([FromBody] GetInvoicesByFacilityRequestDTO request)
        {
            request ??= new GetInvoicesByFacilityRequestDTO();
            var invoices = _IInvoicesRepo.GetInvoicesByFacilityId(request);
            return Ok(invoices);
        }

        [HttpPost]
        [Route("GetInvoicesByPatientId")]
        public IActionResult GetInvoicesByPatientId([FromBody] GetInvoicesByPatientRequestDTO request)
        {
            request ??= new GetInvoicesByPatientRequestDTO();
            var invoices = _IInvoicesRepo.GetInvoicesByPatientId(request);
            return Ok(invoices);
        }

        [HttpPost]
        [Route("PayInvoice")]
        public async Task<ApiResponse<bool>> PayInvoice(long UserId, long InvoiceId)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool res = await _IInvoicesRepo.PayInvoice(UserId, InvoiceId);
                if (res == false)
                {

                    response.Message = "Failed to pay the invoice. Please check your payment method and try again.";
                    response.Status = 400;
                    response.Data = false;
                }
                else
                {
                    response.Message = "Invoice paid successfully";
                    response.Status = 200;
                    response.Data = true;
                }
            }
            catch (Exception ex)
            {

                response.Message = ex.Message;
                response.Status = 400;
                response.Data = false;
            }
            return response;
        }

        [HttpPost]
        [Route("PayManualInvoice")]
        public async Task<ApiResponse<bool>> PayManualInvoice([FromQuery] long InvoiceId)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (!long.TryParse(userIdClaim, out var userId))
                {
                    response.Status = 401;
                    response.Message = "Unauthorized: User ID not found.";
                    return response;
                }

                bool res = await _IInvoicesRepo.PayManualInvoice(userId, InvoiceId);
                if (res == false)
                {
                    response.Message = "Failed to pay the manual invoice. Please check invoice type (must be ClinicToPatient), card details, or invoice status.";
                    response.Status = 400;
                    response.Data = false;
                }
                else
                {
                    response.Message = "Manual invoice paid successfully";
                    response.Status = 200;
                    response.Data = true;
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 400;
                response.Data = false;
            }
            return response;
        }

        [HttpDelete]
        [Route("CancelInvoice")]
        public async Task<ApiResponse<bool>> CancelInvoice([FromQuery] long invoiceId, [FromQuery] string? cancellationReason = null)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                if (invoiceId <= 0)
                {
                    response.Status = 0;
                    response.Data = false;
                    response.Message = "Invalid invoice ID.";
                    return response;
                }

                var cancelled = await _IInvoicesRepo.CancelInvoice(invoiceId, cancellationReason);
                response.Data = cancelled;
                response.Status = cancelled ? 1 : 0;
                response.Message = cancelled
                    ? "Invoice cancelled successfully."
                    : "Invoice not found or invoice type is not supported. Only PatientToClinic and ClinicToPatient invoices can be cancelled.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
                response.Data = false;
            }

            return response;
        }

        [HttpGet]
        [Route("GetDetailedFacilityInvoice")]
        public ApiResponse<GetDetailedFacilityInvoiceResponseDTO> GetDetailedFacilityInvoice([FromQuery] long invoiceId)
        {
            ApiResponse<GetDetailedFacilityInvoiceResponseDTO> response = new ApiResponse<GetDetailedFacilityInvoiceResponseDTO>();
            try
            {
                var result = _IInvoicesRepo.GetDetailedFacilityInvoice(invoiceId);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpGet]
        [Route("GetPatientBill")]
        public ApiResponse<GetPatientBillResponseDTO> GetPatientBill([FromQuery] long invoiceId)
        {
            ApiResponse<GetPatientBillResponseDTO> response = new ApiResponse<GetPatientBillResponseDTO>();
            try
            {
                var result = _IInvoicesRepo.GetPatientBill(invoiceId);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpPost]
        [Route("GetFacilityInvoicesForGlobalAdmin")]
        public ApiResponse<PagedFacilityInvoicesResponseDTO> GetFacilityInvoicesForGlobalAdmin([FromBody] GetFacilityInvoiceSummaryRequestDTO request)
        {
            ApiResponse<PagedFacilityInvoicesResponseDTO> response = new ApiResponse<PagedFacilityInvoicesResponseDTO>();
            try
            {
                var result = _IInvoicesRepo.GetFacilityInvoicesForGlobalAdmin(request);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpPost]
        [Route("GenerateMonthlyFacilityInvoice")]
        public async Task<ApiResponse<GetDetailedFacilityInvoiceResponseDTO>> GenerateMonthlyFacilityInvoice(
            [FromQuery] long facilityId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            ApiResponse<GetDetailedFacilityInvoiceResponseDTO> response = new ApiResponse<GetDetailedFacilityInvoiceResponseDTO>();
            try
            {
                var sDate = startDate ?? DateTime.UtcNow.AddDays(-30);
                var eDate = endDate ?? DateTime.UtcNow;

                var result = await _IInvoicesRepo.GenerateMonthlyFacilityInvoice(facilityId, sDate, eDate);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpGet]
        [Route("GetInvoicePaymentSummary")]
        public async Task<ApiResponse<GetInvoicePaymentSummaryResponseDTO>> GetInvoicePaymentSummary([FromQuery] long invoiceId)
        {
            ApiResponse<GetInvoicePaymentSummaryResponseDTO> response = new ApiResponse<GetInvoicePaymentSummaryResponseDTO>();
            try
            {
                var result = await _IInvoicesRepo.GetInvoicePaymentSummary(invoiceId);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpPost]
        [Route("GetPayments")]
        public async Task<ApiResponse<GetPaymentsResponseDTO>> GetPayments([FromBody] GetPaymentsRequestDTO request)
        {
            ApiResponse<GetPaymentsResponseDTO> response = new ApiResponse<GetPaymentsResponseDTO>();
            try
            {
                var result = await _IInvoicesRepo.GetPayments(request);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpGet]
        [Route("GetFacilityPaymentSummary")]
        public async Task<ApiResponse<GetFacilityPaymentSummaryResponseDTO>> GetFacilityPaymentSummary(
            [FromQuery] long facilityId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            ApiResponse<GetFacilityPaymentSummaryResponseDTO> response = new ApiResponse<GetFacilityPaymentSummaryResponseDTO>();
            try
            {
                var result = await _IInvoicesRepo.GetFacilityPaymentSummary(facilityId, startDate, endDate);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpGet]
        [Route("GetAdminPaymentDashboard")]
        public async Task<ApiResponse<GetAdminPaymentDashboardResponseDTO>> GetAdminPaymentDashboard(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            ApiResponse<GetAdminPaymentDashboardResponseDTO> response = new ApiResponse<GetAdminPaymentDashboardResponseDTO>();
            try
            {
                var result = await _IInvoicesRepo.GetAdminPaymentDashboard(startDate, endDate);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpPost]
        [Route("RefundPayment")]
        public async Task<ApiResponse<bool>> RefundPayment(
            [FromQuery] long paymentId,
            [FromQuery] decimal refundAmount,
            [FromQuery] string reason,
            [FromBody] RefundPaymentRequestDTO? request = null)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);

                var actualPaymentId = request?.PaymentId ?? paymentId;
                var actualRefundAmount = request?.RefundAmount ?? refundAmount;
                var actualReason = request?.Reason ?? reason;

                var result = await _IInvoicesRepo.RefundPayment(actualPaymentId, actualRefundAmount, actualReason, userId);
                response.Data = result;
                response.Status = result ? 1 : 0;
                response.Message = result ? "Payment refunded successfully" : "Failed to refund payment";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
                response.Data = false;
            }
            return response;
        }

        [HttpGet]
        [Route("GetPatientPaymentHistory")]
        public async Task<ApiResponse<List<GetInvoicePaymentSummaryResponseDTO>>> GetPatientPaymentHistory([FromQuery] long patientId)
        {
            ApiResponse<List<GetInvoicePaymentSummaryResponseDTO>> response = new ApiResponse<List<GetInvoicePaymentSummaryResponseDTO>>();
            try
            {
                var result = await _IInvoicesRepo.GetPatientPaymentHistory(patientId);
                response.Data = result;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpGet]
        [Route("DownloadInvoicePdf")]
        public IActionResult DownloadInvoicePdf([FromQuery] long invoiceId, [FromServices] IWebHostEnvironment webHostEnvironment, [FromServices] IInvoicePdfService pdfService)
        {
            try
            {

                var invoice = _IInvoicesRepo.GetInvoiceById(invoiceId);
                if (invoice == null || string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                {
                    return NotFound(new { Message = "Invoice not found" });
                }

                if (!string.IsNullOrWhiteSpace(invoice.PdfS3Url))
                {
                    return Redirect(invoice.PdfS3Url);
                }

                var pdfRelativePath = pdfService.GetInvoicePdfRelativePath(invoice.InvoiceNumber, invoiceId);
                var webRootPath = webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var pdfFullPath = Path.Combine(webRootPath, pdfRelativePath);

                if (!System.IO.File.Exists(pdfFullPath))
                {

                    var detailedInvoice = _IInvoicesRepo.GetDetailedFacilityInvoice(invoiceId);
                    if (detailedInvoice == null)
                    {
                        return NotFound(new { Message = "Invoice details not found" });
                    }

                    detailedInvoice.InvoiceId = invoiceId;
                    detailedInvoice.InvoiceNumber = invoice.InvoiceNumber;
                    detailedInvoice.Status = invoice.Status;
                    detailedInvoice.InvoiceDate = invoice.CreatedDate;

                    var pdfGenerated = pdfService.GenerateFacilityInvoicePdfAsync(detailedInvoice, pdfFullPath).Result;
                    if (!pdfGenerated)
                    {
                        return StatusCode(500, new { Message = "Failed to generate PDF invoice" });
                    }
                }

                var fileBytes = System.IO.File.ReadAllBytes(pdfFullPath);
                var fileName = Path.GetFileName(pdfRelativePath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error downloading PDF: {ex.Message}" });
            }
        }

        [HttpGet]
        [Route("GetInvoicePdfUrl")]
        public ApiResponse<string> GetInvoicePdfUrl([FromQuery] long invoiceId, [FromServices] IInvoicePdfService pdfService)
        {
            ApiResponse<string> response = new ApiResponse<string>();
            try
            {
                var invoice = _IInvoicesRepo.GetInvoiceById(invoiceId);
                if (invoice == null || string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                {
                    response.Message = "Invoice not found";
                    response.Status = 0;
                    return response;
                }

                if (!string.IsNullOrWhiteSpace(invoice.PdfS3Url))
                {
                    response.Data = invoice.PdfS3Url;
                    response.Status = 1;
                    return response;
                }

                var pdfRelativePath = pdfService.GetInvoicePdfRelativePath(invoice.InvoiceNumber, invoiceId);
                var pdfUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/{pdfRelativePath.Replace("\\", "/")}";

                response.Data = pdfUrl;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpPost]
        [Route("createManualClinicToPatientInvoice")]
        public async Task<ApiResponse<CreateManualClinicToPatientInvoiceResponseDTO>> CreateManualClinicToPatientInvoice(
            [FromBody] CreateManualClinicToPatientInvoiceRequestDTO request)
        {
            var response = new ApiResponse<CreateManualClinicToPatientInvoiceResponseDTO>();
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (!long.TryParse(userIdClaim, out var userId))
                {
                    response.Status = 401;
                    response.Message = "Unauthorized: User ID not found.";
                    return response;
                }

                var result = await _IInvoicesRepo.CreateManualClinicToPatientInvoice(request, userId);

                if (result.Success)
                {
                    response.Data = result;
                    response.Status = 200;
                    response.Message = result.Message;

                    try
                    {
                        await _notificationService.SendManualInvoiceCreatedNotificationAsync(
                            facilityId: request.FacilityId,
                            patientId: request.PatientId,
                            amount: request.AmountDue,
                            invoiceNumber: result.InvoiceId.ToString() ?? string.Empty,
                            ct: HttpContext.RequestAborted);
                    }
                    catch
                    {

                    }
                }
                else
                {
                    response.Status = 500;
                    response.Message = result.Message;
                    response.Data = result;
                }
            }
            catch (Exception ex)
            {
                response.Status = 500;
                response.Message = $"Error creating manual invoice: {ex.Message}";
            }
            return response;
        }

        [AuthorizeRoles(UserRole.GlobalAdmin)]
        [HttpPost]
        [Route("createManualGAToClinicInvoice")]
        public async Task<ApiResponse<CreateManualClinicToPatientInvoiceResponseDTO>> CreateManualGAToClinicInvoice(
            [FromBody] CreateManualGAToClinicInvoiceRequestDTO request)
        {
            var response = new ApiResponse<CreateManualClinicToPatientInvoiceResponseDTO>();
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (!long.TryParse(userIdClaim, out var userId))
                {
                    response.Status = 401;
                    response.Message = "Unauthorized: User ID not found.";
                    return response;
                }

                var result = await _IInvoicesRepo.CreateManualGAToClinicInvoice(request, userId);

                if (result.Success)
                {
                    response.Data = result;
                    response.Status = 200;
                    response.Message = result.Message;

                    try
                    {
                        await _notificationService.SendManualInvoiceCreatedNotificationAsync(
                            facilityId: request.FacilityId,
                            patientId: null,
                            amount: request.AmountDue,
                            invoiceNumber: result.InvoiceNumber ?? string.Empty,
                            ct: HttpContext.RequestAborted);
                    }
                    catch
                    {

                    }
                }
                else
                {
                    response.Status = 500;
                    response.Message = result.Message;
                    response.Data = result;
                }
            }
            catch (Exception ex)
            {
                response.Status = 500;
                response.Message = $"Error creating GA-to-Clinic manual invoice: {ex.Message}";
            }
            return response;
        }

    }
}
