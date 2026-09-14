using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Vitality.Models.Enums;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Services;

namespace Vitality.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class AuthorizeRolesAttribute : Attribute, IAuthorizationFilter
{
    private static readonly StringComparer RoleComparer = StringComparer.OrdinalIgnoreCase;

    private readonly HashSet<string> _allowedRoles;

    public AuthorizeRolesAttribute(params string[] roles)
    {
        _allowedRoles = roles?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToHashSet(RoleComparer) ?? new HashSet<string>(RoleComparer);
    }

    public AuthorizeRolesAttribute(params UserRole[] roles)
        : this(roles.Select(role => EnumHelper.GetDescription(role)).ToArray())
    {
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            return;
        }

        var actionDescriptor = context.ActionDescriptor;
        if (actionDescriptor != null)
        {
            var hasAllowAnonymous = actionDescriptor.FilterDescriptors
                .Any(f => f.Filter is AllowAnonymousAttribute || f.Filter is IAllowAnonymous);
            if (hasAllowAnonymous)
            {
                return;
            }
        }

        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userIdClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst("UserId")?.Value;
        var roleIdClaim = user.FindFirst("RoleId")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var userId))
            {

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<MainContext>();

                bool isUserActive = false;
                long? facilityId = null;
                if (!string.IsNullOrEmpty(roleIdClaim) && int.TryParse(roleIdClaim, out var roleId))
                {
                    if (roleId == 6)
                    {

                        var userDetail = dbContext.SYS_UserDetails
                            .AsNoTracking()
                            .FirstOrDefault(u => u.UserId == userId);

                        if (userDetail != null && userDetail.IsActive == true && userDetail.Status == "Active")
                        {

                            if (userDetail.LoginId.HasValue)
                            {
                                var patient = dbContext.PT_Patients
                                    .AsNoTracking()
                                    .FirstOrDefault(p => p.LoginId == userDetail.LoginId.Value);
                                isUserActive = patient != null && patient.IsActive == true && patient.Status == "Active";
                                facilityId = patient?.FacilityId;
                            }
                        }
                    }
                    else
                    {
                        var userDetail = dbContext.SYS_UserDetails
                            .AsNoTracking()
                            .FirstOrDefault(u => u.UserId == userId);
                        isUserActive = userDetail != null && userDetail.IsActive == true && userDetail.Status == "Active";

                        if (isUserActive)
                        {
                            var userFacility = dbContext.FC_UsersInFacilities
                                .AsNoTracking()
                                .FirstOrDefault(uf => uf.UserId == userId);
                            facilityId = userFacility?.FacilityId;
                        }
                    }
                }

                if (!isUserActive)
                {

                    context.Result = new UnauthorizedObjectResult(new
                    {
                        error = "Unauthorized",
                        detail = "User account has been disabled. Please contact your administrator."
                    });
                    return;
                }

                if (facilityId.HasValue && (!int.TryParse(roleIdClaim, out var roleIdForFacility) || roleIdForFacility != 1))
                {
                    var facilityStatusService = new Vitality.Models.Repos.Services.FacilityStatusService(dbContext);
                    if (!facilityStatusService.IsFacilityActive(facilityId.Value))
                    {

                        context.Result = new ObjectResult(new
                        {
                            Status = 0,
                            Message = "Your facility has been disabled. Please contact your administrator to reactivate your facility.",
                            ErrorCode = "FACILITY_DISABLED"
                        })
                        {
                            StatusCode = 403
                        };
                        return;
                    }
                }
            }

        if (_allowedRoles.Count == 0)
        {
            return;
        }

        var roles = user.Claims
            .Where(c => c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                        || c.Type.Equals("RoleName", StringComparison.OrdinalIgnoreCase)
                        || c.Type.Contains("role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(RoleComparer);

        if (!roles.Any(role => _allowedRoles.Contains(role)))
        {
            context.Result = new ForbidResult();
        }
    }
}
