using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.PatientAppointments;
using DudeMeds.Models.DTOs.ProviderSchedules;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Filters;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProviderSchedulesController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IProviderSchedulesRepo _IProviderSchedulesRepo;

        public ProviderSchedulesController(
           IConfiguration config,
           IMapper IMapper,
           IProviderSchedulesRepo IProviderSchedulesRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IProviderSchedulesRepo = IProviderSchedulesRepo;
        }

        [HttpGet]
        [Route("getAllProviderSchedules")]
        [RequiresPermission(Permissions.Availability.View)]
        public ApiResponse<List<GetAllProviderSchedulesResponseDTO>> GetAllProviderSchedules([FromQuery] GetAllProviderSchedulesRequestDTO request)
        {
            ApiResponse<List<GetAllProviderSchedulesResponseDTO>> response = new ApiResponse<List<GetAllProviderSchedulesResponseDTO>>();
            try
            {
                List<GetAllProviderSchedulesResponseDTO> result = new List<GetAllProviderSchedulesResponseDTO>();
                result = _IProviderSchedulesRepo.GetAllProviderSchedules(request, out int totalProviderScheduleCount);
                int totalPages = (int)Math.Ceiling((double)totalProviderScheduleCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalProviderScheduleCount;
                response.TotalPages = totalPages;
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllProviderScheduledSlotsByMonth")]
        public ApiResponse<List<GetAllProviderScheduledSlotsByMonthResponseDTO>> GetAllProviderScheduledSlotsByMonth([FromQuery] GetAllProviderScheduledSlotsByMonthRequestDTO request)
        {
            ApiResponse<List<GetAllProviderScheduledSlotsByMonthResponseDTO>> response = new ApiResponse<List<GetAllProviderScheduledSlotsByMonthResponseDTO>>();
            List<GetAllProviderScheduledSlotsByMonthResponseDTO> res = _IProviderSchedulesRepo.GetAllProviderScheduledSlotsByMonth(request);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getAllProviderScheduledSlotsByDays")]
        public ApiResponse<List<GetAllProviderScheduledSlotsByDaysResponseDTO>> GetAllProviderScheduledSlotsByDays([FromQuery] GetAllProviderScheduledSlotsByDaysRequestDTO request)
        {
            ApiResponse<List<GetAllProviderScheduledSlotsByDaysResponseDTO>> response = new ApiResponse<List<GetAllProviderScheduledSlotsByDaysResponseDTO>>();
            List<GetAllProviderScheduledSlotsByDaysResponseDTO> res = _IProviderSchedulesRepo.GetAllProviderScheduledSlotsByDays(request);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getAllProviderScheduledSlots")]
        public ApiResponse<List<GetAllProviderScheduledSlotsResponseDTO>> GetAllProviderScheduledSlots([FromQuery] GetAllProviderScheduledSlotsRequestDTO request)
        {
            ApiResponse<List<GetAllProviderScheduledSlotsResponseDTO>> response = new ApiResponse<List<GetAllProviderScheduledSlotsResponseDTO>>();
            try
            {
                List<GetAllProviderScheduledSlotsResponseDTO> result = new List<GetAllProviderScheduledSlotsResponseDTO>();
                result = _IProviderSchedulesRepo.GetAllProviderScheduledSlots(request, out int totalProviderScheduledSlotCount);
                int totalPages = (int)Math.Ceiling((double)totalProviderScheduledSlotCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalProviderScheduledSlotCount;
                response.TotalPages = totalPages;
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getProviderSsheduleById")]
        [RequiresPermission(Permissions.Availability.View)]
        public ApiResponse<GetProviderScheduleByIdResponseDTO> GetProviderSsheduleById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetProviderScheduleByIdResponseDTO> response = new ApiResponse<GetProviderScheduleByIdResponseDTO>();
            try
            {
                GetProviderScheduleByIdResponseDTO result = new GetProviderScheduleByIdResponseDTO();
                result = _IProviderSchedulesRepo.GetProviderSsheduleById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveProviderSlot")]
        [RequiresPermission(Permissions.Availability.Add, Permissions.Availability.Edit)]
        public ApiResponse<bool> SaveProviderSlot([FromBody] SaveProviderSlotRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                _IProviderSchedulesRepo.SaveProviderSlot(request, UserId, OrganizationId);
                response.Data = true;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("rescheduleProviderSchedule")]
        [RequiresPermission(Permissions.Availability.Edit)]
        public ApiResponse<bool> RescheduleProviderSchedule([FromBody] SaveProviderSlotRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                if (request.ProviderScheduleId != 0)
                {
                    var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                    var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                    _IProviderSchedulesRepo.DeleteProviderScheduleById(request.ProviderScheduleId,UserId);
                    _IProviderSchedulesRepo.SaveProviderSlot(request,UserId,OrganizationId);
                    response.Data = true;
                }
                else
                {
                    response.Data = false;
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("rescheduleProviderScheduledSlot")]
        [RequiresPermission(Permissions.AvailabilitySlot.Edit)]
        public ApiResponse<bool> RescheduleProviderScheduledSlot([FromBody] SaveRescheduleProviderScheduledSlotRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                _IProviderSchedulesRepo.RescheduleProviderScheduledSlot(request, UserId);
                response.Data = true;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getProviderSchedulesStartDateById")]
        [RequiresPermission(Permissions.Availability.View)]
        public ApiResponse<GetProviderSchedulesStartDateByIdResponseDTO> GetProviderSchedulesStartDateById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetProviderSchedulesStartDateByIdResponseDTO> response = new ApiResponse<GetProviderSchedulesStartDateByIdResponseDTO>();
            try
            {
                GetProviderSchedulesStartDateByIdResponseDTO result = new GetProviderSchedulesStartDateByIdResponseDTO();
                result = _IProviderSchedulesRepo.GetProviderSchedulesStartDateById(request.Id, request.ClientTimezoneOffsetMinutes);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getProviderScheduledSlotStartDateById")]
        [RequiresPermission(Permissions.AvailabilitySlot.View)]
        public ApiResponse<GetProviderScheduledSlotStartDateByIdResponseDTO> GetProviderScheduledSlotStartDateById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetProviderScheduledSlotStartDateByIdResponseDTO> response = new ApiResponse<GetProviderScheduledSlotStartDateByIdResponseDTO>();
            try
            {
                GetProviderScheduledSlotStartDateByIdResponseDTO result = new GetProviderScheduledSlotStartDateByIdResponseDTO();
                result = _IProviderSchedulesRepo.GetProviderScheduledSlotStartDateById(request.Id, request.ClientTimezoneOffsetMinutes);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getProviderScheduledSlotTimes")]
        public ApiResponse<List<GetProviderScheduledSlotTimesResponseDTO>> GetProviderScheduledSlotTimes([FromQuery] GetProviderScheduledSlotTimesRequestDTO request)
        {
            ApiResponse<List<GetProviderScheduledSlotTimesResponseDTO>> response = new ApiResponse<List<GetProviderScheduledSlotTimesResponseDTO>>();
            try
            {
                List<GetProviderScheduledSlotTimesResponseDTO> result = new List<GetProviderScheduledSlotTimesResponseDTO>();
                result = _IProviderSchedulesRepo.GetProviderScheduledSlotTimes(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteProviderScheduleById")]
        [RequiresPermission(Permissions.Availability.Delete)]
        public ApiResponse<bool> DeleteProviderScheduleById([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
            var result = _IProviderSchedulesRepo.DeleteProviderScheduleById(request.Id,UserId);
            response.Data = result;
            return response;
        }

        [HttpPost]
        [Route("deleteProviderScheduledSlotById")]
        [RequiresPermission(Permissions.AvailabilitySlot.Delete)]
        public ApiResponse<bool> DeleteProviderScheduledSlotById([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
            _IProviderSchedulesRepo.DeleteProviderScheduledSlotById(request.Id,UserId);
            response.Data = true;
            return response;
        }

    }
}
