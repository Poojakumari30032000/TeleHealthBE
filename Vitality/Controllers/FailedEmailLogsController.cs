using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.FailedEmailLogs;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Security;

namespace Vitality.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[RequiresPermission(Permissions.UserManagement.Access)]
public class FailedEmailLogsController : ControllerBase
{
    private readonly IFailedEmailLogsRepo _repo;

    public FailedEmailLogsController(IFailedEmailLogsRepo repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    [HttpGet]
    [Route("getPaged")]
    [AuthorizeRoles(UserRole.GlobalAdmin)]
    public async Task<ApiResponse<PagedResult<FailedEmailLogItemDTO>>> GetPaged([FromQuery] GetFailedEmailLogsRequestDTO request)
    {
        var response = new ApiResponse<PagedResult<FailedEmailLogItemDTO>>();
        try
        {
            var (items, totalCount) = await _repo.GetPagedAsync(request).ConfigureAwait(false);
            response.Data = new PagedResult<FailedEmailLogItemDTO>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
            response.TotalEntityCount = totalCount;
            response.TotalPages = request.PageSize > 0 ? Math.Ceiling((decimal)totalCount / request.PageSize) : 0;
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
    [Route("getById/{id}")]
    [AuthorizeRoles(UserRole.GlobalAdmin)]
    public async Task<ApiResponse<FailedEmailLogItemDTO>> GetById(long id)
    {
        var response = new ApiResponse<FailedEmailLogItemDTO>();
        try
        {
            var item = await _repo.GetByIdAsync(id).ConfigureAwait(false);
            if (item == null)
            {
                response.Message = "Failed email log not found.";
                response.Status = 0;
                return response;
            }
            response.Data = item;
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
