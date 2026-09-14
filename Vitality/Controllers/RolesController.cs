using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.Roles;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Security;

namespace Vitality.Controllers
{
    /// <summary>
    /// Role and permission management.
    /// <para>
    /// Covers the reference project's <c>RoleController</c> surface - role grid, role
    /// data, permission tree, create/update, delete - plus user-role assignment, which
    /// the reference had no endpoint for.
    /// </para>
    /// <para>
    /// Authorization is applied PER ACTION rather than on the class, deliberately:
    /// <c>myPermissions</c> must be callable by every authenticated user (each one needs
    /// its own permission list to render the UI), while every management action is
    /// restricted to Super Admin and Global Admin. Putting the restriction on the class
    /// and then punching a hole in it for one action is exactly the pattern that leads
    /// to an accidental <c>[AllowAnonymous]</c>.
    /// </para>
    /// <para>
    /// Note the global <c>AuthorizeFilter</c> registered in Program.cs already requires
    /// an authenticated user for everything here.
    /// </para>
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly IRolesRepo _rolesRepo;
        private readonly ILogger<RolesController> _logger;

        public RolesController(IRolesRepo rolesRepo, ILogger<RolesController> logger)
        {
            _rolesRepo = rolesRepo;
            _logger = logger;
        }

        // =====================================================================
        //  Available to every authenticated user
        // =====================================================================

        /// <summary>
        /// The caller's own effective permissions. The Angular client calls this after
        /// login and feeds the result into <c>PermissionsService.setApiPermissions()</c>.
        /// <para>
        /// Intentionally reflects the database on every call rather than reading the
        /// token: access tokens here last 30 days with no refresh flow, so a
        /// token-embedded permission set would keep a demoted user's access alive for a
        /// month.
        /// </para>
        /// <para>
        /// No permission is required - a user must always be able to discover their own
        /// permissions - but it returns only the CALLER's, resolved from their token's
        /// UserId. There is no parameter to ask about somebody else.
        /// </para>
        /// </summary>
        [HttpGet]
        [Route("myPermissions")]
        public async Task<ApiResponse<MyPermissionsResponseDTO>> GetMyPermissions(CancellationToken cancellationToken)
        {
            var response = new ApiResponse<MyPermissionsResponseDTO>();

            try
            {
                var userId = GetActingUserId();
                if (userId <= 0)
                {
                    response.Status = 0;
                    response.Message = "Unable to identify the signed-in user.";
                    return response;
                }

                response.Data = await _rolesRepo.GetMyPermissionsAsync(userId, cancellationToken);
                response.Message = "Success";
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to resolve permissions for the current user.");
                response.Status = 0;
                response.Message = "Unable to load permissions.";
                return response;
            }
        }

        // =====================================================================
        //  Role management - Super Admin and Global Admin only
        // =====================================================================

        /// <summary>Paged role grid.</summary>
        [HttpPost]
        [Route("getRoleList")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [RequiresPermission(Permissions.UserManagement.Access)]
        public async Task<ApiResponse<List<RoleListItemResponseDTO>>> GetRoleList(
            [FromBody] GetRolesRequestDTO request, CancellationToken cancellationToken)
        {
            var response = new ApiResponse<List<RoleListItemResponseDTO>>();

            try
            {
                var result = await _rolesRepo.GetRoleListAsync(request, cancellationToken);

                response.Data = result.Items;
                response.TotalEntityCount = result.TotalCount;

                var pageSize = request?.PageSize.GetValueOrDefault(25) ?? 25;
                if (pageSize > 0)
                {
                    response.TotalPages = (int)Math.Ceiling((decimal)result.TotalCount / pageSize);
                }

                response.Message = "Success";
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load the role list.");
                response.Status = 0;
                response.Message = "Unable to load roles.";
                return response;
            }
        }

        /// <summary>
        /// The permission catalog as a checkbox tree, with this role's grants marked.
        /// Call with <paramref name="roleId"/> 0 (or omit it) to get the blank catalog
        /// for a new role - the same dual behaviour the reference endpoint had.
        /// </summary>
        [HttpGet]
        [Route("getRolePermissionTree")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [RequiresPermission(Permissions.UserManagement.Access)]
        public async Task<ApiResponse<RolePermissionTreeResponseDTO>> GetRolePermissionTree(
            [FromQuery] int? roleId, CancellationToken cancellationToken)
        {
            var response = new ApiResponse<RolePermissionTreeResponseDTO>();

            try
            {
                var tree = await _rolesRepo.GetRolePermissionTreeAsync(roleId, cancellationToken);

                if (tree is null)
                {
                    response.Status = 0;
                    response.Message = "Role not found.";
                    return response;
                }

                response.Data = tree;
                response.Message = "Success";
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load the permission tree for RoleId {RoleId}.", roleId);
                response.Status = 0;
                response.Message = "Unable to load permissions.";
                return response;
            }
        }

        /// <summary>
        /// Creates a role when <c>RoleId</c> is null or 0, otherwise updates it - one
        /// endpoint for both, matching the reference project's <c>AddRole</c>.
        /// <c>PermissionIds</c> replaces the role's entire grant set.
        /// </summary>
        [HttpPost]
        [Route("saveRole")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [RequiresPermission(Permissions.UserManagement.Access)]
        public async Task<ApiResponse<RoleOperationResultDTO>> SaveRole(
            [FromBody] SaveRoleRequestDTO request, CancellationToken cancellationToken)
        {
            var response = new ApiResponse<RoleOperationResultDTO>();

            try
            {
                var result = await _rolesRepo.SaveRoleAsync(request, GetActingUserId(), cancellationToken);

                response.Data = result;

                if (!result.Success)
                {
                    response.Status = 0;
                    response.Message = DescribeError(result);
                    return response;
                }

                response.Message = request?.RoleId.GetValueOrDefault() > 0
                    ? "Role updated successfully."
                    : "Role created successfully.";
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save RoleId {RoleId}.", request?.RoleId);
                response.Status = 0;
                response.Message = "Unable to save the role.";
                return response;
            }
        }

        /// <summary>
        /// Soft-deletes a custom role. System roles and roles still assigned to a user
        /// are refused server-side, not merely hidden in the UI.
        /// </summary>
        [HttpPost]
        [Route("deleteRole")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [RequiresPermission(Permissions.UserManagement.Access)]
        public async Task<ApiResponse<RoleOperationResultDTO>> DeleteRole(
            [FromBody] DeleteRoleRequestDTO request, CancellationToken cancellationToken)
        {
            var response = new ApiResponse<RoleOperationResultDTO>();

            try
            {
                var roleId = request?.RoleId.GetValueOrDefault() ?? 0;
                if (roleId <= 0)
                {
                    response.Status = 0;
                    response.Message = "A role must be specified.";
                    return response;
                }

                var result = await _rolesRepo.DeleteRoleAsync(roleId, GetActingUserId(), cancellationToken);

                response.Data = result;

                if (!result.Success)
                {
                    response.Status = 0;
                    response.Message = DescribeError(result);
                    return response;
                }

                response.Message = "Role deleted successfully.";
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete RoleId {RoleId}.", request?.RoleId);
                response.Status = 0;
                response.Message = "Unable to delete the role.";
                return response;
            }
        }

        /// <summary>
        /// Changes which role a user holds. Takes effect immediately, because
        /// permission resolution reads the user's role from the database on every request
        /// rather than from their token.
        /// </summary>
        [HttpPost]
        [Route("assignUserRole")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [RequiresPermission(Permissions.UserManagement.Access)]
        public async Task<ApiResponse<RoleOperationResultDTO>> AssignUserRole(
            [FromBody] AssignUserRoleRequestDTO request, CancellationToken cancellationToken)
        {
            var response = new ApiResponse<RoleOperationResultDTO>();

            try
            {
                var userId = request?.UserId.GetValueOrDefault() ?? 0;
                var roleId = request?.RoleId.GetValueOrDefault() ?? 0;

                if (userId <= 0 || roleId <= 0)
                {
                    response.Status = 0;
                    response.Message = "A user and a role must both be specified.";
                    return response;
                }

                var result = await _rolesRepo.AssignUserRoleAsync(
                    userId, roleId, GetActingUserId(), cancellationToken);

                response.Data = result;

                if (!result.Success)
                {
                    response.Status = 0;
                    response.Message = DescribeError(result);
                    return response;
                }

                response.Message = "Role assigned successfully.";
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to assign RoleId {RoleId} to UserId {UserId}.",
                    request?.RoleId, request?.UserId);
                response.Status = 0;
                response.Message = "Unable to assign the role.";
                return response;
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// Maps a repository error code to a user-facing message, the same way
        /// <c>AccountsController.Login</c> maps its login error codes. Detail beyond the
        /// message is logged, never returned.
        /// </summary>
        private string DescribeError(RoleOperationResultDTO result)
        {
            if (!string.IsNullOrWhiteSpace(result.ErrorDetail))
            {
                _logger?.LogWarning("Role operation refused ({ErrorCode}): {ErrorDetail}",
                    result.ErrorCode, result.ErrorDetail);
            }

            return result.ErrorCode switch
            {
                "ROLE_NAME_REQUIRED" => "A role name is required.",
                "ROLE_NAME_DUPLICATE" => "A role with that name already exists.",
                "ROLE_NOT_FOUND" => "That role no longer exists.",
                "ROLE_IS_SYSTEM" => "Built-in roles cannot be deleted. You can still change their permissions.",
                "ROLE_RENAME_NOT_ALLOWED" => "Built-in roles cannot be renamed. You can still change their permissions and description.",
                "ROLE_IN_USE" => "This role is still assigned to one or more users. Reassign them first.",
                "ROLE_INACTIVE" => "That role is inactive and cannot be assigned.",
                "PERMISSION_UNKNOWN" => "The permission list is out of date. Reload the page and try again.",
                "USER_NOT_FOUND" => "That user account could not be found.",
                _ => "The role could not be saved."
            };
        }

        /// <summary>
        /// The caller's UserId, taken from the signed token only.
        /// <para>
        /// Never from the request body. The reference project passed <c>CreatedByGUID</c>
        /// and <c>SubDomain</c> up from <c>localStorage</c> in every role payload, which
        /// made both audit attribution and tenant selection client-controlled.
        /// </para>
        /// </summary>
        private long GetActingUserId()
        {
            var raw = User.FindFirst("UserId")?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return long.TryParse(raw, out var userId) ? userId : 0L;
        }
    }
}
