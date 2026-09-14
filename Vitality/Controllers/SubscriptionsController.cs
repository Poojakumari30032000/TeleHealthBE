using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.Subscriptions;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Security;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubscriptionsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ISubscriptionsRepo _ISubscriptionsRepo;

        public SubscriptionsController(
            IConfiguration config,
            IMapper IMapper,
            ISubscriptionsRepo ISubscriptionsRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _ISubscriptionsRepo = ISubscriptionsRepo;
        }

        [HttpGet]
        [Route("getAllSubscriptions")]
        [RequiresPermission(Permissions.SubscriptionPlan.View)]
        public ApiResponse<List<GetAllSubscriptionsResponseDTO>> GetAllSubscriptions()
        {
            ApiResponse<List<GetAllSubscriptionsResponseDTO>> response = new ApiResponse<List<GetAllSubscriptionsResponseDTO>>();
            try
            {
                List<GetAllSubscriptionsResponseDTO> result = new List<GetAllSubscriptionsResponseDTO>();
                result = _ISubscriptionsRepo.GetAllSubscriptions();
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getSubscriptionById")]
        [RequiresPermission(Permissions.SubscriptionPlan.View)]
        public ApiResponse<GetSubscriptionByIdResponseDTO> GetSubscriptionById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetSubscriptionByIdResponseDTO> response = new ApiResponse<GetSubscriptionByIdResponseDTO>();
            try
            {
                GetSubscriptionByIdResponseDTO result = new GetSubscriptionByIdResponseDTO();
                result = _ISubscriptionsRepo.GetSubscriptionById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveSubscription")]
        [RequiresPermission(Permissions.SubscriptionPlan.Add, Permissions.SubscriptionPlan.Edit)]
        public ApiResponse<bool> SaveSubscription([FromBody] SaveSubscriptionRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                bool res = _ISubscriptionsRepo.SaveSubscription(request, UserId);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteSubscription")]
        [RequiresPermission(Permissions.SubscriptionPlan.Delete)]
        public ApiResponse<bool> DeleteSubscription([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool res = _ISubscriptionsRepo.DeleteSubscription(request.Id);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("updateSubscriptionStatus")]
        [RequiresPermission(Permissions.SubscriptionPlan.Edit)]
        public ApiResponse<bool> UpdateSubscriptionStatus([FromBody] UpdateSubscriptionStatusRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool res = _ISubscriptionsRepo.UpdateSubscriptionStatus(request);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [HttpPost]
        [Route("updateGlobalSubscription")]
        [RequiresPermission(Permissions.SubscriptionPlan.Edit)]
        public ApiResponse<bool> UpdateGlobalSubscription([FromBody] UpdateGlobalSubscriptionRequestDTO request)
        {
            var response = new ApiResponse<bool> { Data = false };
            try
            {
                if (request == null || request.MonthlyPrice < 0)
                {
                    response.Message = "A valid non-negative MonthlyPrice is required.";
                    return response;
                }

                var userId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                bool res = _ISubscriptionsRepo.UpdateGlobalSubscription(request, userId);
                response.Data = res;
                response.Message = res ? "Global subscription fee updated." : "Unable to update global subscription fee.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getGlobalSubscription")]
        public ApiResponse<GetSubscriptionByIdResponseDTO?> GetGlobalSubscription()
        {
            var response = new ApiResponse<GetSubscriptionByIdResponseDTO?>();
            try
            {
                response.Data = _ISubscriptionsRepo.GetGlobalSubscription();
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getSubscriptionByFacilityId")]
        public ApiResponse<GetSubscriptionByFacilityIdResponseDTO> GetSubscriptionByFacilityId(string? FacilityGuid)
        {
            ApiResponse<GetSubscriptionByFacilityIdResponseDTO> response = new ApiResponse<GetSubscriptionByFacilityIdResponseDTO>();
            try
            {
                GetSubscriptionByFacilityIdResponseDTO result = new GetSubscriptionByFacilityIdResponseDTO();
                result = _ISubscriptionsRepo.GetSubscriptionByFacilityId(FacilityGuid);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
    }
}
