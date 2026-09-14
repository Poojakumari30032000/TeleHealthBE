using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Vitality.Helper;
using Vitality.Models.DTOs.AuditLogs;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Enums;
using Vitality.Filters;
using Vitality.Models.Security;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequiresPermission(Permissions.UserManagement.Access, Permissions.Patient.View)]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogsRepo _auditLogsRepo;

        public AuditLogsController(IAuditLogsRepo auditLogsRepo)
        {
            _auditLogsRepo = auditLogsRepo;
        }

        [HttpGet]
        [Route("getFacilityAuditLogs")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<List<GetAuditLogsResponseDTO>>> GetFacilityAuditLogs([FromQuery] GetFacilityAuditLogsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAuditLogsResponseDTO>>();
            try
            {
                var logs = await _auditLogsRepo.GetFacilityAuditLogsAsync(request);
                response.Data = logs;
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
        [Route("getUserAuditLogs")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<List<GetAuditLogsResponseDTO>>> GetUserAuditLogs([FromQuery] GetUserAuditLogsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAuditLogsResponseDTO>>();
            try
            {
                var logs = await _auditLogsRepo.GetUserAuditLogsAsync(request);
                response.Data = logs;
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
        [Route("getEntityAuditLogs")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<List<GetAuditLogsResponseDTO>>> GetEntityAuditLogs([FromQuery] GetEntityAuditLogsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAuditLogsResponseDTO>>();
            try
            {
                var logs = await _auditLogsRepo.GetEntityAuditLogsAsync(request);
                response.Data = logs;
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
        [Route("getAllAuditLogs")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<List<GetAuditLogsResponseDTO>>> GetAllAuditLogs([FromQuery] GetAllAuditLogsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAuditLogsResponseDTO>>();
            try
            {
                var (logs, totalCount) = await _auditLogsRepo.GetAllAuditLogsAsync(request);

                var safePageSize = (request?.PageSize ?? 0) > 0 ? request.PageSize : 50;
                var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);

                response.Data = logs;
                response.TotalEntityCount = totalCount;
                response.TotalPages = totalPages;
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
        [Route("getFacilityAuditStatistics")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<GetAuditLogStatisticsResponseDTO>> GetFacilityAuditStatistics(
            [FromQuery] long facilityId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var response = new ApiResponse<GetAuditLogStatisticsResponseDTO>();
            try
            {
                var stats = await _auditLogsRepo.GetFacilityAuditStatisticsAsync(facilityId, startDate, endDate);
                response.Data = stats;
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
        [Route("getModuleAuditLogs")]
        [AuthorizeRoles(UserRole.GlobalAdmin, UserRole.ClinicAdmin)]
        public async Task<ApiResponse<List<GetAuditLogsResponseDTO>>> GetModuleAuditLogs([FromQuery] GetModuleAuditLogsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAuditLogsResponseDTO>>();
            try
            {

                var roleIdClaim = User.FindFirst("RoleId");
                if (roleIdClaim != null && int.TryParse(roleIdClaim.Value, out var roleId) && roleId == (int)UserRole.ClinicAdmin)
                {
                    var facilityIdClaim = User.FindFirst("FacilityId");
                    if (facilityIdClaim != null && long.TryParse(facilityIdClaim.Value, out var facilityId))
                    {
                        request.FacilityId = facilityId;
                    }
                }

                var logs = await _auditLogsRepo.GetModuleAuditLogsAsync(request);
                response.Data = logs;
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
        [Route("getPatientAuditLogs")]
        [AuthorizeRoles(UserRole.GlobalAdmin, UserRole.ClinicAdmin)]
        public async Task<ApiResponse<List<GetAuditLogsResponseDTO>>> GetPatientAuditLogs([FromQuery] GetPatientAuditLogsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAuditLogsResponseDTO>>();
            try
            {

                var roleIdClaim = User.FindFirst("RoleId");
                if (roleIdClaim != null && int.TryParse(roleIdClaim.Value, out var roleId) && roleId == (int)UserRole.ClinicAdmin)
                {
                    var facilityIdClaim = User.FindFirst("FacilityId");
                    if (facilityIdClaim != null && long.TryParse(facilityIdClaim.Value, out var facilityId))
                    {
                        request.FacilityId = facilityId;
                    }
                }

                var (logs, totalCount) = await _auditLogsRepo.GetPatientAuditLogsAsync(request);
                var safePageSize = (request?.PageSize ?? 0) > 0 ? request.PageSize : 50;
                var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);

                response.Data = logs;
                response.TotalEntityCount = totalCount;
                response.TotalPages = totalPages;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
            }
            return response;
        }
    }
}
