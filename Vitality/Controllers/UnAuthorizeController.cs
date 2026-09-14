using System;
using System.Linq;
using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.DropDown;
using DudeMeds.Models.DTOs.PatientAppointments;
using DudeMeds.Models.DTOs.PatientOrders;
using DudeMeds.Models.DTOs.PatientPayments;
using DudeMeds.Models.DTOs.PatientPrescriptions;
using DudeMeds.Models.DTOs.Patients;
using DudeMeds.Models.DTOs.PatientTreatments;
using DudeMeds.Models.DTOs.Products;
using DudeMeds.Models.DTOs.Questionnaires;
using DudeMeds.Models.DTOs.Users;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.FacilitySquareCred;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.DTOs.PatientProduct;
using Vitality.Models.DTOs.Patients;
using Vitality.Models.DTOs.ProductCategories;
using Vitality.Models.DTOs.Square;
using Vitality.Models.DTOs.Stripe;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Models.DTOs.Coupons;
using Microsoft.EntityFrameworkCore;
using Vitality.Models.DTOs.Square;
using Vitality.Models.DTOs.Facilities;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class UnAuthorizeController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IDropDownsRepo _IDropDownsRepo;
        private readonly IUsersRepo _IUserRepo;
        private readonly IProductsRepo _IProductsRepo;
        private readonly IQuestionnairesRepo _IQuestionnairesRepo;
        private readonly IPatientPaymentsRepo _IPatientPaymentsRepo;
        private readonly IPatientAppointmentsRepo _IPatientAppointmentsRepo;
        private readonly IProductCategoriesRepo _ICategoriesRepo;
        private readonly IInvoiceRepo _invoiceRepo;
        private readonly ISquarePaymentRepo _paymentService;
        private readonly INotificationService _notificationService;
        private readonly ICouponRepo _couponRepo;
        private readonly MainContext _db;
        private readonly IFacilitiesRepo _facilitiesRepo;

        public UnAuthorizeController(
         IConfiguration config,
         IMapper IMapper,
         IDropDownsRepo IDropDownsRepo,
         IUsersRepo IUsersRepo,
         IProductsRepo IProductsRepo,
          IQuestionnairesRepo IQuestionnairesRepo,
          IPatientPaymentsRepo IPatientPaymentsRepo,
          IPatientAppointmentsRepo IPatientAppointmentsRepo,
          IProductCategoriesRepo ICategoriesRepo,
          IInvoiceRepo invoiceRepo,
          ISquarePaymentRepo paymentService,
          INotificationService notificationService,
          ICouponRepo couponRepo,
          MainContext db,
          IFacilitiesRepo facilitiesRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IDropDownsRepo = IDropDownsRepo;
            _IUserRepo = IUsersRepo;
            _IProductsRepo = IProductsRepo;
            _IQuestionnairesRepo = IQuestionnairesRepo;
            _IPatientPaymentsRepo = IPatientPaymentsRepo;
            _IPatientAppointmentsRepo = IPatientAppointmentsRepo;
            _ICategoriesRepo = ICategoriesRepo;
            _invoiceRepo = invoiceRepo;
            _paymentService = paymentService;
            _notificationService = notificationService;
            _couponRepo = couponRepo;
            _db = db;
            _facilitiesRepo = facilitiesRepo;
        }

        [HttpPost]
        [Route("ClinicSignup")]
        public async Task<ApiResponse<bool>> ClinicSignup([FromBody] ClinicSignupRequestDTO request)
        {
            var response = new ApiResponse<bool> { Data = false };
            var result = await _facilitiesRepo.ExternalClinicSignupAsync(request, organizationId: 1, ct: HttpContext.RequestAborted);
            response.Message = result.Message;
            response.Data = result.FacilityId.HasValue && result.FacilityId.Value > 0;
            return response;
        }

        [HttpGet]
        [Route("getAllFacilities")]
        public ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> GetAllFacilities([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> response = new ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>>();
            List<GetAllFacilitiesDropDownResponseDTO> result = _IDropDownsRepo.GetAllFacilities(request.Id);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllCategories")]
        public ApiResponse<List<GetAllCategoriesDropDownResponseDTO>> GetAllCategories()
        {
            ApiResponse<List<GetAllCategoriesDropDownResponseDTO>> response = new ApiResponse<List<GetAllCategoriesDropDownResponseDTO>>();
            List<GetAllCategoriesDropDownResponseDTO> result = _IDropDownsRepo.GetAllCategories();
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllConditions")]
        public ApiResponse<List<GetAllConditionsDropDownResponseDTO>> GetAllConditions([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllConditionsDropDownResponseDTO>> response = new ApiResponse<List<GetAllConditionsDropDownResponseDTO>>();
            List<GetAllConditionsDropDownResponseDTO> result = _IDropDownsRepo.GetAllConditions(request.Id);
            response.Data = result;
            return response;
        }
        [HttpGet]
        [Route("getAllInTakeFormProducts")]
        public ApiResponse<List<GetAllInTakeFormProductsDropDownResponseDTO>> GetAllInTakeFormProducts([FromQuery] GetAllInTakeFormProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllInTakeFormProductsDropDownResponseDTO>> response = new ApiResponse<List<GetAllInTakeFormProductsDropDownResponseDTO>>();
            List<GetAllInTakeFormProductsDropDownResponseDTO> result = _IDropDownsRepo.GetAllInTakeFormProducts(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getProductInfoByName")]
        public ApiResponse<GetProductInfoByNameResponseDTO> GetProductInfoByName([FromQuery] GetProductInfoByNameRequestDTO request)
        {
            ApiResponse<GetProductInfoByNameResponseDTO> response = new ApiResponse<GetProductInfoByNameResponseDTO>();
            try
            {
                GetProductInfoByNameResponseDTO result = new GetProductInfoByNameResponseDTO();
                result = _IProductsRepo.GetProductInfoByName(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getUserById")]
        public ApiResponse<GetUserByIdResponseDTO> GetUserById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetUserByIdResponseDTO> response = new ApiResponse<GetUserByIdResponseDTO>();
            GetUserByIdResponseDTO result = new GetUserByIdResponseDTO();
            result = _IUserRepo.GetUserById(request.Id);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getQuestionnaireJsonById")]
        public ApiResponse<GetQuestionnaireJsonByIdResponseDTO> GetQuestionnaireJsonById([FromQuery] GetQuestionnaireJsonByIdRequestDTO request)
        {
            ApiResponse<GetQuestionnaireJsonByIdResponseDTO> response = new ApiResponse<GetQuestionnaireJsonByIdResponseDTO>();
            try
            {
                GetQuestionnaireJsonByIdResponseDTO result = new GetQuestionnaireJsonByIdResponseDTO();
                result = _IQuestionnairesRepo.GetQuestionnaireJsonById(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getCategoriesWithBundlesByFacilityId")]
        public async Task<ApiResponse<List<GetAllCategoriesWithBundlesResponseDTO>>> GetCategoriesWithBundles(
             [FromQuery] long facilityId,
             CancellationToken ct)
        {
            var response = new ApiResponse<List<GetAllCategoriesWithBundlesResponseDTO>>();
            try
            {
                if (facilityId <= 0) throw new ArgumentException("facilityId must be > 0.");

                var result = await _ICategoriesRepo.GetCategoriesWithBundlesAsync(facilityId, ct);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
        [HttpGet]
        [Route("getBundlesByCategoryId")]
        public async Task<ApiResponse<List<BundleDTO>>> GetBundlesByCategoryId(
    [FromQuery] long categoryId,
    [FromQuery] long facilityId,
    CancellationToken ct)
        {
            var response = new ApiResponse<List<BundleDTO>>();
            try
            {
                if (categoryId <= 0) throw new ArgumentException("categoryId must be > 0.");
                if (facilityId <= 0) throw new ArgumentException("facilityId must be > 0.");

                var bundles = await _ICategoriesRepo.GetBundlesByCategoryAndFacilityAsync(categoryId, facilityId, ct);
                response.Data = bundles;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getFacilityBundleDetail")]
        public async Task<ApiResponse<BundleDTO>> GetFacilityBundleDetail(
            [FromQuery] long facilityId,
            [FromQuery] long bundleId,
            CancellationToken ct)
        {
            var response = new ApiResponse<BundleDTO>();
            try
            {
                if (facilityId <= 0) throw new ArgumentException("facilityId must be > 0.");
                if (bundleId <= 0) throw new ArgumentException("bundleId must be > 0.");

                var result = await _ICategoriesRepo.GetFacilityBundleDetailAsync(facilityId, bundleId, ct);
                if (result == null)
                {
                    response.Message = "Bundle not found or not available for this facility.";
                    response.Status = 0;
                    response.Data = null;
                    return response;
                }
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }

        [HttpPost]
        [Route("CreateCardAndPayInvoice")]
        public async Task<ApiResponse<bool>> CreateCardAndPayInvoice([FromBody] PatientPaymentRequestRequest request)
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

                SaveCardRequest cardRequest = new SaveCardRequest
                {
                    SourceId = request.SourceId,
                    CardholderName = request.CardholderName,
                    UserId = request.UserId,
                    FacilityId = request.FacilityId,
                };
                cardRequest.UserId = request.UserId;
                cardRequest.CardholderName = request.CardholderName;
                cardRequest.SourceId = request.SourceId;
                cardRequest.Currency = "USD";
                bool isCardSaved = await _paymentService.SavePaymentCards(cardRequest);
                if (!isCardSaved)
                {
                    response.Status = 500;
                    response.Message = "Error occurred while saving the card.";
                    return response;
                }

                decimal bundlePrice = request.Price ?? 0m;
                if (request.ProductId.HasValue && request.FacilityId.HasValue)
                {
                    try
                    {

                        var bundle = _IProductsRepo.GetBundleByBundleId(request.ProductId.Value);
                        decimal? globalPrice = bundle?.Price;

                        var facilityPrice = await _db.PD_FacilityBundlePrices
                            .AsNoTracking()
                            .Where(fbp => fbp.FacilityId == request.FacilityId.Value &&
                                         fbp.BundleId == request.ProductId.Value)
                            .FirstOrDefaultAsync(HttpContext.RequestAborted);

                        if (facilityPrice != null)
                        {
                            bundlePrice = facilityPrice.ClinicPrice;
                        }
                        else if (globalPrice.HasValue)
                        {
                            bundlePrice = globalPrice.Value;
                        }
                        else if (request.Price.HasValue)
                        {
                            bundlePrice = request.Price.Value;
                        }
                    }
                    catch (Exception priceEx)
                    {

                        System.Diagnostics.Debug.WriteLine($"Error getting bundle price: {priceEx.Message}");
                        if (request.Price.HasValue)
                        {
                            bundlePrice = request.Price.Value;
                        }
                    }
                }

                decimal invoiceAmount = Math.Max(0m, Math.Round(bundlePrice, 2, MidpointRounding.AwayFromZero));
                ValidateCouponResponseDTO? couponValidateResult = null;

                if (!string.IsNullOrWhiteSpace(request.CouponCode) && request.ProductId.HasValue && request.FacilityId.HasValue)
                {
                    try
                    {

                            var validateRequest = new ValidateCouponRequestDTO
                            {
                                FacilityId = request.FacilityId.Value,
                                BundleId = request.ProductId.Value,
                                BundlePrice = bundlePrice,
                                CouponCode = request.CouponCode.Trim(),
                                PatientId = request.PatientId
                            };

                            couponValidateResult = await _couponRepo.ValidateCouponAsync(validateRequest, HttpContext.RequestAborted);

                            if (couponValidateResult.IsValid && couponValidateResult.DiscountedPrice.HasValue)
                            {
                                invoiceAmount = Math.Max(0m, Math.Round(couponValidateResult.DiscountedPrice.Value, 2, MidpointRounding.AwayFromZero));
                            }
                            else
                            {

                                response.Status = 400;
                                response.Message = couponValidateResult.Message ?? "Invalid or already used coupon code. Each coupon can only be used once per patient.";
                                response.Data = false;
                                return response;
                            }

                    }
                    catch (Exception couponEx)
                    {

                        System.Diagnostics.Debug.WriteLine($"Coupon validation error: {couponEx.Message}");
                        response.Status = 400;
                        response.Message = $"Coupon validation failed: {couponEx.Message}";
                        response.Data = false;
                        return response;
                    }
                }

                var invoiceRequest = new SaveInvoiceRequestDTO
                {
                    Amount = invoiceAmount,
                    InvoiceType = InvoiceType.PatientToClinic.ToString(),
                    Status = InvoiceStatus.Pending.ToString(),
                    FacilityId = request.FacilityId,
                    PatientId = request.PatientId,
                    InvoiceNumber = Guid.NewGuid().ToString(),
                    IsActive = true
                };

                Sys_Invoice chkIfInvoiceCreated = _invoiceRepo.SaveInvoiceReturnInoice(invoiceRequest, (long)request.UserId);
                if (chkIfInvoiceCreated == null)
                {
                    response.Status = 500;
                    response.Message = "Error while creating invoice.";
                    return response;
                }

                bool isPaymentSuccessful;
                if (invoiceAmount <= 0m)
                {
                    var payUser = await _db.SYS_UserDetails
                        .Include(x => x.SYS_UserCards)
                        .FirstOrDefaultAsync(x => x.UserId == request.UserId, HttpContext.RequestAborted);
                    var payCard = payUser?.SYS_UserCards?.Where(x => x.IsDefault).FirstOrDefault()
                        ?? payUser?.SYS_UserCards?.FirstOrDefault();
                    if (payUser == null || payCard == null)
                    {
                        response.Status = 500;
                        response.Message = "User or payment card not found.";
                        response.Data = false;
                        return response;
                    }

                    try
                    {
                        await _invoiceRepo.RecordPayment(
                            chkIfInvoiceCreated.InvoiceId,
                            0m,
                            $"COMPL-{Guid.NewGuid():N}",
                            string.Empty,
                            (long)request.UserId,
                            request.PatientId,
                            request.FacilityId,
                            payCard.CardId,
                            "Complimentary",
                            "No Square charge — fully discounted or zero balance.");
                        isPaymentSuccessful = true;
                    }
                    catch (Exception complEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Complimentary invoice record failed: {complEx.Message}");
                        response.Status = 500;
                        response.Message = "Error while recording zero-amount payment.";
                        response.Data = false;
                        return response;
                    }
                }
                else
                {
                    isPaymentSuccessful = await _invoiceRepo.PayInvoice((long)request.UserId, chkIfInvoiceCreated.InvoiceId);
                }
                if (isPaymentSuccessful)
                {
                    response.Status = 200;
                    response.Message = "Patient payment completed successfully for the selected bundle";
                    response.Data = true;

                    if (request.ProductId.HasValue && chkIfInvoiceCreated != null)
                    {
                        try
                        {

                            var originalPrice = bundlePrice;
                            var discountedPrice = invoiceAmount;

                            await _invoiceRepo.CreateInvoiceLineItemForBundle(
                                invoiceId: chkIfInvoiceCreated.InvoiceId,
                                bundleId: request.ProductId.Value,
                                couponCode: request.CouponCode,
                                originalPrice: originalPrice,
                                discountedPrice: discountedPrice,
                                userId: (long)request.UserId
                            );
                        }
                        catch (Exception lineItemEx)
                        {

                            System.Diagnostics.Debug.WriteLine($"Error creating invoice line item: {lineItemEx.Message}");
                        }
                    }

                    try
                    {

                        if (request.PatientId.HasValue && request.PatientId.Value > 0)
                        {
                            await _notificationService.SendPaymentConfirmationAsync(
                                patientId: request.PatientId.Value,
                                amount: invoiceAmount,
                                transactionId: chkIfInvoiceCreated.InvoiceId.ToString(),
                                ct: HttpContext.RequestAborted
                            );
                        }

                        if (request.FacilityId.HasValue && request.FacilityId.Value > 0)
                        {
                            await _notificationService.SendInvoicePaidNotificationAsync(
                                patientId: request.PatientId,
                                facilityId: request.FacilityId,
                                amount: invoiceAmount,
                                invoiceNumber: chkIfInvoiceCreated.InvoiceNumber ?? chkIfInvoiceCreated.InvoiceId.ToString(),
                                adminUserId: chkIfInvoiceCreated.CreatedBy,
                                ct: HttpContext.RequestAborted
                            );
                        }

                        if (request.PatientId.HasValue && request.ProductId.HasValue)
                        {

                            decimal? discountPrice = null;
                            if (!string.IsNullOrWhiteSpace(request.CouponCode) && request.Price.HasValue)
                            {
                                var bundle = _IProductsRepo.GetBundleByBundleId(request.ProductId.Value);
                                if (bundle != null && bundle.Price.HasValue)
                                {
                                    discountPrice = bundle.Price.Value - invoiceAmount;
                                }
                            }

                            string? facilityGuid = null;
                            if (request.FacilityId.HasValue)
                            {
                                var facility = _db.SYS_Facilities
                                    .Where(f => f.FacilityId == request.FacilityId.Value)
                                    .Select(f => f.Guid)
                                    .FirstOrDefault();
                                facilityGuid = facility;
                            }

                            PT_PatientPaymentDetail patientPayment = new PT_PatientPaymentDetail
                            {
                                PatientPaymentId = 0,
                                PatientId = request.PatientId,
                                ProductId = request.ProductId,
                                TotalPrice = invoiceAmount,
                                DiscountPrice = discountPrice,
                                CouponCode = request.CouponCode,
                                FacilityGuid = facilityGuid,
                                PaymentStatus = "Paid",
                                IsActive = true,
                                CreatedBy = (long)request.UserId,
                                CreatedDate = DateTime.UtcNow,
                                Guid = Guid.NewGuid().ToString()
                            };
                            _db.PT_PatientPaymentDetails.Add(patientPayment);
                            _db.SaveChanges();

                            SavePatientPaymentResponseDTO payment = new SavePatientPaymentResponseDTO
                            {
                                PatientPaymentId = patientPayment.PatientPaymentId,
                                PatientId = patientPayment.PatientId,
                                ProductId = patientPayment.ProductId,
                                TotalPrice = patientPayment.TotalPrice,
                                DiscountPrice = patientPayment.DiscountPrice,
                                CouponCode = patientPayment.CouponCode,
                                FacilityId = request.FacilityId,
                                FacilityGuid = patientPayment.FacilityGuid,
                                PatientTreatmentId = patientPayment.PatientTreatmentId
                            };

                            if (payment != null && payment.PatientPaymentId != 0)
                            {

                                if (!string.IsNullOrWhiteSpace(request.CouponCode) && request.PatientId.HasValue && request.ProductId.HasValue)
                                {
                                    try
                                    {
                                        var coupon = await _db.SYS_CouponCodes
                                            .Where(c => c.CoupanCode.ToLower() == request.CouponCode.Trim().ToLower() && c.IsActive == true)
                                            .FirstOrDefaultAsync(HttpContext.RequestAborted);

                                        if (coupon != null)
                                        {
                                            var couponUsage = new PT_CouponUsage
                                            {
                                                PatientId = request.PatientId,
                                                BundleId = request.ProductId,
                                                CouponCodeId = coupon.CoupanCodeId,
                                                CouponCode = request.CouponCode.Trim(),
                                                PatientPaymentId = payment.PatientPaymentId,
                                                UsedDate = DateTime.UtcNow,
                                                IsActive = true,
                                                CreatedBy = (long)request.UserId,
                                                CreatedDate = DateTime.UtcNow
                                            };
                                            _db.PT_CouponUsages.Add(couponUsage);
                                            await _db.SaveChangesAsync(HttpContext.RequestAborted);
                                        }
                                    }
                                    catch (Exception couponUsageEx)
                                    {

                                        System.Diagnostics.Debug.WriteLine($"Error recording coupon usage: {couponUsageEx.Message}");
                                    }
                                }

                                if (request.IsRecurring == true && request.PatientId.HasValue && request.ProductId.HasValue)
                                {
                                    try
                                    {
                                        var bundle = _IProductsRepo.GetBundleByBundleId(request.ProductId.Value);
                                        if (bundle != null && bundle.visits.HasValue && bundle.visits.Value > 0)
                                        {

                                            PT_PatientTreatment? treatment = await _db.PT_PatientTreatments
                                                .Where(t => t.PatientId == request.PatientId.Value &&
                                                           t.ProductId == request.ProductId.Value &&
                                                           (t.IsActive == true || t.IsActive == null))
                                                .OrderByDescending(t => t.CreatedDate)
                                                .FirstOrDefaultAsync(HttpContext.RequestAborted);

                                            if (treatment != null)
                                            {

                                                decimal? clinicPrice = null;
                                                if (request.FacilityId.HasValue)
                                                {
                                                    var facilityPrice = await _db.PD_FacilityBundlePrices
                                                        .Where(fbp => fbp.FacilityId == request.FacilityId.Value &&
                                                                     fbp.BundleId == request.ProductId.Value)
                                                        .FirstOrDefaultAsync(HttpContext.RequestAborted);
                                                    clinicPrice = facilityPrice?.ClinicPrice;
                                                }

                                                treatment.IsRecurring = true;
                                                treatment.OriginalPaymentAmount = invoiceAmount;
                                                treatment.RecurringAmountAfterCoupon = invoiceAmount;
                                                if (couponValidateResult?.CouponCodeId != null && couponValidateResult.AppliesToRecurring)
                                                    treatment.RecurringCouponCodeId = couponValidateResult.CouponCodeId;
                                                else
                                                    treatment.RecurringCouponCodeId = null;
                                                treatment.RecurringDurationMonths = bundle.visits.Value;
                                                treatment.RecurringStartDate = DateTime.UtcNow;
                                                treatment.NextRecurringPaymentDate = DateTime.UtcNow.AddMonths(bundle.visits.Value);
                                                treatment.ModifiedBy = (long)request.UserId;
                                                treatment.ModifiedDate = DateTime.UtcNow;

                                                await _db.SaveChangesAsync(HttpContext.RequestAborted);

                                                if (treatment.PatientTreatmentId > 0 && request.PatientId.HasValue)
                                                {
                                                    await _notificationService.SendTreatmentSelectionAsync(
                                                        patientId: request.PatientId.Value,
                                                        treatmentId: treatment.PatientTreatmentId,
                                                        ct: HttpContext.RequestAborted
                                                    );
                                                }
                                            }
                                            else
                                            {

                                                System.Diagnostics.Debug.WriteLine($"Warning: Treatment not found for PatientId={request.PatientId}, ProductId={request.ProductId}. Recurring fields not set.");
                                            }
                                        }
                                    }
                                    catch (Exception recurringEx)
                                    {

                                        System.Diagnostics.Debug.WriteLine($"Error setting up recurring payment: {recurringEx.Message}");
                                    }
                                }
                                else if (request.PatientId.HasValue && request.ProductId.HasValue)
                                {

                                    try
                                    {
                                        var treatment = await _db.PT_PatientTreatments
                                            .Where(t => t.PatientId == request.PatientId.Value &&
                                                       t.ProductId == request.ProductId.Value &&
                                                       (t.IsActive == true || t.IsActive == null))
                                            .OrderByDescending(t => t.CreatedDate)
                                            .FirstOrDefaultAsync(HttpContext.RequestAborted);

                                        if (treatment != null && treatment.PatientTreatmentId > 0)
                                        {
                                            await _notificationService.SendTreatmentSelectionAsync(
                                                patientId: request.PatientId.Value,
                                                treatmentId: treatment.PatientTreatmentId,
                                                ct: HttpContext.RequestAborted
                                            );
                                        }
                                    }
                                    catch (Exception notifEx)
                                    {

                                        System.Diagnostics.Debug.WriteLine($"Error sending treatment selection notification: {notifEx.Message}");
                                    }
                                }
                            }

                        }
                    }
                    catch (Exception notificationEx)
                    {

                        System.Diagnostics.Debug.WriteLine($"Notification error after payment: {notificationEx.Message}");

                    }
                }
                else
                {
                    response.Status = 500;
                    response.Message = "Error occurred while processing the payment.";
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
        [Route("getAllProviders")]
        public ApiResponse<List<GetAllProviderResponseDTO>> GetAllProviders([FromQuery] GetAllProviderRequestDTO request)
        {
            ApiResponse<List<GetAllProviderResponseDTO>> response = new ApiResponse<List<GetAllProviderResponseDTO>>();
            List<GetAllProviderResponseDTO> result = _IDropDownsRepo.GetAllDoctors(request);
            response.Data = result;
            return response;
        }

        [HttpPost]
        [Route("SaveFacilitySquareCredentials")]
        public ApiResponse<bool> SaveFacilitySquareCredentials([FromBody] SaveSquareCredRequestDto request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool res = _paymentService.SaveFacilitySquareCredentials(request);

                if (!res)
                {
                    response.Status = 500;
                    response.Message = "There was an error while trying to save credentials.";
                }
                else
                {
                    response.Status = 200;
                    response.Message = "Credentials saved successfully.";
                }

                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Status = 500;
                response.Message = $"An error occurred: {ex.Message}";
            }
            return response;
        }
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        [HttpGet]
        [Route("GetAllFacilitySquareCredentials")]

        public IActionResult GetAllFacilitySquareCredentials(long facilityId)
        {
            var Credentials = _paymentService.GetFacilitySquareCredentialsByFacilityId(facilityId);
            return Ok(Credentials);
        }
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        [HttpGet]
        [Route("GetAllFacilitySquareCredentialsWithoutToken")]

        public IActionResult GetAllFacilitySquareCredentialsWithoutToken(long facilityId)
        {
            var Credentials = _paymentService.GetAllFacilitySquareCredentialsByIdWithoutToken(facilityId);
            return Ok(Credentials);
        }

        [HttpGet]
        [Route("getProviderScheduledSlots")]
        public ApiResponse<List<GetProviderScheduledSlotsResponseDTO>> GetProviderScheduledSlots([FromQuery] GetProviderScheduledSlotsRequestDTO request)
        {
            ApiResponse<List<GetProviderScheduledSlotsResponseDTO>> response = new ApiResponse<List<GetProviderScheduledSlotsResponseDTO>>();
            try
            {
                List<GetProviderScheduledSlotsResponseDTO> result = new List<GetProviderScheduledSlotsResponseDTO>();
                result = _IDropDownsRepo.GetProviderScheduledSlots(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("QA/SetRecurringDueSoon")]
        public async Task<ApiResponse<object>> QA_SetRecurringDueSoon(
            [FromQuery] long treatmentId,
            [FromQuery] int minutesFromNow = 5)
        {
            var response = new ApiResponse<object>();
            try
            {
                if (treatmentId <= 0)
                {
                    response.Status = 400;
                    response.Success = false;
                    response.Message = "treatmentId is required (positive long).";
                    response.Data = null!;
                    return response;
                }

                if (minutesFromNow < 0) minutesFromNow = 0;

                var treatment = await _db.PT_PatientTreatments
                    .FirstOrDefaultAsync(t => t.PatientTreatmentId == treatmentId);

                if (treatment == null)
                {
                    response.Status = 404;
                    response.Success = false;
                    response.Message = $"PT_PatientTreatment {treatmentId} not found.";
                    response.Data = null!;
                    return response;
                }

                if (string.Equals(treatment.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(treatment.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    response.Status = 409;
                    response.Success = false;
                    response.Message =
                        $"Treatment is {treatment.Status} and is frozen (Task 4 contract). " +
                        "Have the patient purchase the package again to test on a fresh treatment row.";
                    response.Data = null!;
                    return response;
                }

                if (!treatment.RecurringDurationMonths.HasValue || treatment.RecurringDurationMonths.Value <= 0)
                {
                    if (treatment.ProductId.HasValue)
                    {
                        var bundleVisits = await _db.PD_Bundles
                            .AsNoTracking()
                            .Where(b => b.BundleId == treatment.ProductId.Value)
                            .Select(b => b.visits)
                            .FirstOrDefaultAsync();
                        if (bundleVisits.HasValue && bundleVisits.Value > 0)
                        {
                            treatment.RecurringDurationMonths = bundleVisits.Value;
                        }
                    }
                    if (!treatment.RecurringDurationMonths.HasValue || treatment.RecurringDurationMonths.Value <= 0)
                    {
                        response.Status = 422;
                        response.Success = false;
                        response.Message =
                            "Cannot set treatment as recurring-due: bundle has no visits > 0, so RecurringDurationMonths cannot be derived.";
                        response.Data = null!;
                        return response;
                    }
                }

                var nowUtc = DateTime.UtcNow;
                var dueAtUtc = nowUtc.AddMinutes(minutesFromNow);

                var oldDueDate = treatment.NextRecurringPaymentDate;

                treatment.NextRecurringPaymentDate = dueAtUtc;

                treatment.RecurringPausedDate = null;
                if (!treatment.RecurringStartDate.HasValue)
                    treatment.RecurringStartDate = nowUtc;
                treatment.ModifiedDate = nowUtc;

                await _db.SaveChangesAsync();

                response.Status = 1;
                response.Success = true;
                response.Message =
                    $"Treatment {treatmentId} set due in {minutesFromNow} minute(s). " +
                    "The next RecurringPaymentService tick that lands after that time will process it.";
                response.Data = new
                {
                    PatientTreatmentId = treatment.PatientTreatmentId,
                    PatientId = treatment.PatientId,
                    ProductId = treatment.ProductId,
                    FacilityId = treatment.FacilityId,
                    Before = new
                    {

                        NextRecurringPaymentDate = oldDueDate,

                    },
                    After = new
                    {
                        IsRecurring = treatment.IsRecurring,
                        NextRecurringPaymentDate = treatment.NextRecurringPaymentDate,
                        TreatmentStatus = treatment.TreatmentStatus,
                        RecurringDurationMonths = treatment.RecurringDurationMonths
                    },
                    ServerUtcNow = nowUtc,
                    DueAtUtc = dueAtUtc,
                    SchedulerIntervalNote =
                        "RecurringPaymentService tick is controlled by SchedulerSettings:RecurringPaymentIntervalMinutes (default override = 5)."
                };
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 500;
                response.Success = false;
                response.Message = $"An error occurred: {ex.Message}";
                response.Data = null!;
                return response;
            }
        }
    }
}
