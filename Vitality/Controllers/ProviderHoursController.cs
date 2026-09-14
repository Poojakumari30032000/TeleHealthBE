using System;
using System.Threading;
using System.Threading.Tasks;
using DudeMeds.Models.DTOs.ProviderHours;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Models.Repos.Services.Validators;
using Vitality.Filters;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{

    [Authorize]
    [Route("api/providerHours")]
    [ApiController]
    public class ProviderHoursController : ControllerBase
    {
        private readonly IProviderHoursRepo _repo;

        public ProviderHoursController(IProviderHoursRepo repo)
        {
            _repo = repo;
        }

        [HttpGet("week")]
        [RequiresPermission(Permissions.Availability.View, Permissions.AvailabilitySlot.View)]
        public async Task<ApiResponse<WeekViewDto>> GetWeek(
            [FromQuery] long providerId,
            [FromQuery] DateTime weekStartDate,
            [FromQuery] string? clientTimezone,
            CancellationToken ct)
        {
            var response = new ApiResponse<WeekViewDto>();
            try
            {
                var organizationId = Convert.ToInt64(User.FindFirst("OrganizationId")?.Value ?? "0");
                response.Data = await _repo.GetWeekAsync(providerId, weekStartDate, organizationId, clientTimezone, ct);
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPut("day/{dayOfWeek:int}")]
        [RequiresPermission(Permissions.Availability.Add, Permissions.Availability.Edit)]
        public async Task<ApiResponse<bool>> SaveDay(
            [FromRoute] int dayOfWeek,
            [FromBody] SaveDayRequest request,
            CancellationToken ct)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                var organizationId = Convert.ToInt64(User.FindFirst("OrganizationId")?.Value ?? "0");
                if (dayOfWeek < 0 || dayOfWeek > 6)
                {
                    response.Status = 0;
                    response.Message = "dayOfWeek must be 0 (Sunday) through 6 (Saturday).";
                    response.Data = false;
                    return response;
                }
                await _repo.SaveDayAsync(request.ProviderId, (byte)dayOfWeek, request, userId, organizationId, ct);
                response.Data = true;
            }
            catch (ProviderHoursValidationException vex)
            {
                response.Status = 0;
                response.Message = vex.Message;
                response.Data = false;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }

        [HttpPut("dateOverride")]
        [RequiresPermission(Permissions.Availability.Add, Permissions.Availability.Edit)]
        public async Task<ApiResponse<bool>> SaveDateOverride(
            [FromBody] DateOverrideRequest request,
            CancellationToken ct)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                var organizationId = Convert.ToInt64(User.FindFirst("OrganizationId")?.Value ?? "0");
                await _repo.SaveDateOverrideAsync(request.ProviderId, request, userId, organizationId, ct);
                response.Data = true;
            }
            catch (ProviderHoursValidationException vex)
            {
                response.Status = 0;
                response.Message = vex.Message;
                response.Data = false;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }

        [HttpDelete("dateOverride")]
        [RequiresPermission(Permissions.Availability.Delete)]
        public async Task<ApiResponse<bool>> DeleteDateOverride(
            [FromQuery] long providerId,
            [FromQuery] DateTime date,
            CancellationToken ct)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                var organizationId = Convert.ToInt64(User.FindFirst("OrganizationId")?.Value ?? "0");
                await _repo.DeleteDateOverrideAsync(providerId, date, userId, organizationId, ct);
                response.Data = true;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }
    }
}
