using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Helper;
using Vitality.Models.DTOs.Coupons;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CouponsController : ControllerBase
    {
        private readonly ICouponRepo _couponRepo;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CouponsController> _logger;

        public CouponsController(
            ICouponRepo couponRepo,
            IServiceScopeFactory scopeFactory,
            ILogger<CouponsController> logger)
        {
            _couponRepo = couponRepo;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpPost]
        [Route("create")]
        public async Task<ApiResponse<CouponCodeResponseDTO>> CreateCouponCode(CreateCouponCodeRequestDTO request)
        {
            var response = new ApiResponse<CouponCodeResponseDTO>();
            try
            {
                response.Data = await _couponRepo.CreateCouponCode(request);
                response.Message = "Coupon code created successfully.";

                if (response.Data != null && response.Data.CoupanCodeId > 0 && request.FacilityId > 0)
                {
                    var facilityId = request.FacilityId;
                    var couponCode = response.Data.CouponCode ?? string.Empty;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                            await notificationService.SendCouponCreatedAsync(facilityId, couponCode, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Background coupon-created notification failed for facility {FacilityId}", facilityId);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Success = false;
            }
            return response;
        }

        [HttpPut]
        [Route("update")]
        public async Task<ApiResponse<CouponCodeResponseDTO>> UpdateCouponCode(UpdateCouponCodeRequestDTO request)
        {
            var response = new ApiResponse<CouponCodeResponseDTO>();
            try
            {
                response.Data = await _couponRepo.UpdateCouponCode(request);
                response.Message = "Coupon code updated successfully.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Success = false;
            }
            return response;
        }

        [HttpDelete]
        [Route("delete/{couponCodeId}")]
        public async Task<ApiResponse<bool>> DeleteCouponCode(long couponCodeId)
        {
            var response = new ApiResponse<bool>();
            try
            {
                response.Data = await _couponRepo.DeleteCouponCode(couponCodeId);
                response.Message = "Coupon code deleted successfully.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Success = false;
            }
            return response;
        }

        [HttpGet]
        [Route("getById/{id}")]
        public async Task<ApiResponse<CouponCodeResponseDTO>> GetCouponCodeById(long id)
        {
            var response = new ApiResponse<CouponCodeResponseDTO>();
            try
            {
                response.Data = await _couponRepo.GetCouponCodeById(id);
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Success = false;
            }
            return response;
        }

        [HttpPost]
        [Route("getByFacility")]
        public async Task<ApiResponse<List<GetCouponsByFacilityItemDTO>>> GetCouponsByFacility([FromBody] GetCouponsByFacilityRequestDTO request)
        {
            var response = new ApiResponse<List<GetCouponsByFacilityItemDTO>>();
            int pageSize = request.PageSize > 0 ? request.PageSize : 10;
            int pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
            try
            {
                var (coupons, totalCount) = await _couponRepo.GetCouponsByFacility(request);
                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                response.Data = coupons;
                response.TotalEntityCount = totalCount;
                response.TotalPages = totalPages;

            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Success = false;
            }
            return response;
        }

        [HttpPatch("activate/{couponCodeId:long}")]
        public async Task<ApiResponse<CouponCodeResponseDTO>> Activate(long couponCodeId)
        {
            var response = new ApiResponse<CouponCodeResponseDTO>();
            try
            {
                response.Data = await _couponRepo.SetCouponActiveStatus(couponCodeId, true);
                response.Message = "Coupon activated.";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPatch("deactivate/{couponCodeId:long}")]
        public async Task<ApiResponse<CouponCodeResponseDTO>> Deactivate(long couponCodeId)
        {
            var response = new ApiResponse<CouponCodeResponseDTO>();
            try
            {
                response.Data = await _couponRepo.SetCouponActiveStatus(couponCodeId, false);
                response.Message = "Coupon deactivated.";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPatch("toggle/{couponCodeId:long}")]
        public async Task<ApiResponse<CouponCodeResponseDTO>> Toggle(long couponCodeId)
        {
            var response = new ApiResponse<CouponCodeResponseDTO>();
            try
            {
                response.Data = await _couponRepo.ToggleCouponActiveStatus(couponCodeId);
                response.Message = $"Coupon {(response.Data.IsActive == true ? "activated" : "deactivated")}.";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost("validateCoupon")]
        [AllowAnonymous]
        public async Task<ApiResponse<ValidateCouponResponseDTO>> ValidateCoupon(
           [FromBody] ValidateCouponRequestDTO request,
           CancellationToken ct)
        {
            var response = new ApiResponse<ValidateCouponResponseDTO>();
            try
            {
                var result = await _couponRepo.ValidateCouponAsync(request, ct);
                response.Data = result;

                if (!result.IsValid)
                {
                    response.Message = result.Message;
                    response.Success = false;
                }
                else
                {
                    response.Message = "Coupon validated.";
                    response.Success = true;
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Success = false;
            }
            return response;
        }

    }
}
