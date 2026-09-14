using DudeMeds.Models.DTOs.DropDown;
using DudeMeds.Models.DTOs.Tickets;
using DudeMeds.Models.DTOs.Users;
using Microsoft.AspNetCore.Mvc;
using Square.Models;
using Vitality.Helper;
using Vitality.Models.DTOs.Chats;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.DTOs.Square;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly ISquarePaymentRepo _paymentService;
        private readonly IInvoiceRepo _invoiceRepo;
        private readonly INotificationService _notificationService;
        private readonly FacilityStatusService _facilityStatusService;
        private readonly MainContext _db;

        public PaymentController(ISquarePaymentRepo paymentService, IInvoiceRepo invoiceRepo, INotificationService notificationService, MainContext db)
        {
            _paymentService = paymentService;
            _invoiceRepo = invoiceRepo;
            _notificationService = notificationService;
            _db = db;
            _facilityStatusService = new FacilityStatusService(_db);
        }

        [HttpPost]
        [Route("CreatePayment")]
        public async Task<ApiResponse<bool>> CreatePayment([FromBody] PaymentRequest request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            if (string.IsNullOrWhiteSpace(request.SourceId) || request.Amount <= 0)
                response.Message = "Invalid payment data.";

            try
            {
                var userIdClaim = User.FindFirst("UserId");
                if (userIdClaim == null || string.IsNullOrWhiteSpace(userIdClaim.Value))
                {
                    response.Message = "UserId claim is missing or invalid.";
                }

                var UserId = Convert.ToInt64(userIdClaim.Value);
                request.UserId = UserId;

                if (request.FacilityId.HasValue && !_facilityStatusService.IsFacilityActive(request.FacilityId.Value))
                {
                    response.Status = 403;
                    response.Message = "Your facility has been disabled. Payment processing is not available. Please contact your administrator to reactivate your facility.";
                    response.Data = false;
                    return response;
                }
                response.Data = true;
                CreatePaymentResponse obj = await _paymentService.CreatePaymentAsync(request);
                if (obj.Payment != null && obj.Payment.Status == "COMPLETED")
                {
                    response.Status = 200;
                    response.Message = $"Payment successful! ID: {obj.Payment.Id}";

                    SaveInvoiceRequestDTO invoiceRequest = new SaveInvoiceRequestDTO
                    {
                        Amount = request.Amount,
                        InvoiceType = request.InvoiceType,
                        SubscriptionId = request.SubscriptionId,
                        UserId = request.UserId,
                        InvoiceNumber = obj.Payment.Id
                    };
                    var savedInvoice = _invoiceRepo.SaveInvoiceReturnInoice(invoiceRequest, (long)request.UserId);

                    if (savedInvoice != null && savedInvoice.InvoiceId > 0)
                    {
                        var paymentDetail = await _invoiceRepo.RecordPayment(
                            invoiceId: savedInvoice.InvoiceId,
                            amount: request.Amount,
                            transactionId: obj.Payment.Id ?? Guid.NewGuid().ToString(),
                            squarePaymentId: obj.Payment.Id,
                            userId: request.UserId,
                            patientId: null,
                            facilityId: request.FacilityId,
                            cardId: null,
                            paymentMethod: "Card",
                            notes: $"Payment via Square - {obj.Payment.Id}"
                        );

                        if (paymentDetail?.PaidByPatientId.HasValue == true && paymentDetail.PaidByPatientId.Value > 0)
                        {
                            await _notificationService.SendPaymentConfirmationAsync(
                                patientId: paymentDetail.PaidByPatientId.Value,
                                amount: request.Amount,
                                transactionId: obj.Payment.Id ?? Guid.NewGuid().ToString(),
                                ct: HttpContext.RequestAborted
                            );
                        }
                    }
                }
                else
                {
                    response.Status = 500;
                    response.Message = $"Payment not completed. Status: {obj.Payment?.Status}";
                }
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("SaveCardPayment")]
        public async Task<ApiResponse<bool>> SaveCardPayment([FromBody] SaveCardRequest request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();

            try
            {
                if (string.IsNullOrWhiteSpace(request.SourceId))
                {
                    response.Status = 400;
                    response.Message = "Invalid payment data.";
                    return response;
                }

                var userIdClaim = User.FindFirst("UserId");
                if (userIdClaim == null || string.IsNullOrWhiteSpace(userIdClaim.Value))
                {
                    response.Status = 400;
                    response.Message = "UserId claim is missing or invalid.";
                    return response;
                }
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);

                request.UserId = UserId;

                bool isCardSaved = await _paymentService.SavePaymentCards(request);

                if (isCardSaved)
                {
                    response.Status = 200;
                    response.Message = "Card Created Successfully";
                    response.Data = true;
                }
                else
                {
                    response.Status = 500;
                    response.Message = "Error occurred while creating the card.";
                }
            }
            catch (Exception ex)
            {
                response.Status = 500;
                response.Message = ex.Message;
            }

            return response;
        }

        [HttpGet]
        [Route("GetCardByUserId")]
        public ApiResponse<List<SquareCardDTO>> GetCardByUserId(long UserId)
        {
            ApiResponse<List<SquareCardDTO>> response = new ApiResponse<List<SquareCardDTO>>();
            try
            {
                List<SquareCardDTO> result = new List<SquareCardDTO>();
                result = _paymentService.GetCardsByUserId(UserId);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("GetCardsByPatientId")]
        public ApiResponse<List<SquareCardDTO>> GetCardsByPatientId(long PatientId)
        {
            ApiResponse<List<SquareCardDTO>> response = new ApiResponse<List<SquareCardDTO>>();
            try
            {
                List<SquareCardDTO> result = new List<SquareCardDTO>();
                result = _paymentService.GetCardsByPatientId(PatientId);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("SetDefaultCard")]
        public ApiResponse<bool> SetDefaultCard(long userId, long cardId)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool result = _paymentService.SetDefaultCard(userId, cardId);

                response.Data = result;
                if (!result)
                {
                    response.Message = "Failed to set the default card.";
                }
            }
            catch (Exception ex)
            {

                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }
        [HttpPost]
        [Route("DeleteCard")]
        public ApiResponse<bool> DeleteCard(long cardId)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool result = _paymentService.DeleteCard(cardId);

                response.Data = result;
                if (!result)
                {
                    response.Message = "Failed to set the default card.";
                    response.Status = 500;
                }
            }
            catch (Exception ex)
            {

                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }

        [HttpGet("GetSquareAppIdbyFacilityID")]
        public async Task<IActionResult> GetSquareAppIdbyFacilityID([FromQuery] long facilityId)
        {
            if (facilityId <= 0)
                return BadRequest("facilityId must be a positive number.");

            var result = await _paymentService.GetSquareAppIdByFacilityIdAsync(facilityId);
            if (result == null)
                return NotFound("No Square credentials found for this facility.");
            return Ok(result);
        }

    }
}
