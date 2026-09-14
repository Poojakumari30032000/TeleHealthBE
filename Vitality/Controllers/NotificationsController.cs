using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Users;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Models.DTOs.Notifications;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly INotificationsRepo _INotificationsRepo;

        public NotificationsController(
            IConfiguration config,
            IMapper IMapper,
            INotificationsRepo INotificationsRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _INotificationsRepo = INotificationsRepo;
        }

        [HttpGet]
        [Route("getAllNotifications")]
        public async Task<ApiResponse<List<GetAllNotificationsResponseDTO>>> GetAllNotificationsAsync([FromQuery] GetAllNotificationsRequestDTO request)
        {
            ApiResponse<List<GetAllNotificationsResponseDTO>> response = new ApiResponse<List<GetAllNotificationsResponseDTO>>();
            try
            {
                List<GetAllNotificationsResponseDTO> result = new List<GetAllNotificationsResponseDTO>();
                result = await _INotificationsRepo.GetAllNotificationsAsync(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("updateNotifications")]
        public async Task<ApiResponse<bool>> UpdateNotificationsAsync([FromBody] List<GetByIdRequestDTO> requestList)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var userId = User.FindFirst("UserId")?.Value != null ? Convert.ToInt64(User.FindFirst("UserId")!.Value) : (long?)null;
            var roleId = User.FindFirst("RoleId")?.Value != null ? Convert.ToInt64(User.FindFirst("RoleId")!.Value) : (long?)null;
            var claimFacilityId = User.FindFirst("FacilityId")?.Value != null ? Convert.ToInt64(User.FindFirst("FacilityId")!.Value) : (long?)null;
            var bodyFacilityId = requestList?.FirstOrDefault()?.FacilityId;
            var facilityId = claimFacilityId ?? bodyFacilityId;

            bool res = await _INotificationsRepo.UpdateNotificationsAsync(requestList, userId, roleId, facilityId);
            response.Data = res;
            return response;
        }
    }
}
