using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vitality.Models.DTOs.Roles;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services.Audit;
using Vitality.Models.Repos.Services.Security;

namespace Vitality.Models.Repos.Services
{
    /// <summary>
    /// Role and permission management against <see cref="MainContext"/>.
    /// <para>
    /// Note this takes <c>MainContext</c> through dependency injection rather than
    /// deriving from <c>BaseRepo</c>, which constructs its own context. That follows the
    /// newer services in this project (<c>SlotMaterializer</c>, the schedulers) and it
    /// matters here specifically: role writes and the permission cache invalidation that
    /// follows them must observe the same unit of work, and the acting user must come
    /// from the request scope.
    /// </para>
    /// </summary>
    public sealed class RolesRepo : IRolesRepo
    {
        /// <summary>RoleId of Super Admin, whose permissions are implicit.</summary>
        private const int SuperAdminRoleId = 1;

        private const int MaxPageSize = 200;

        private readonly MainContext _db;
        private readonly IPermissionResolver _permissionResolver;
        private readonly IAuditService _auditService;
        private readonly ILogger<RolesRepo> _logger;

        public RolesRepo(
            MainContext db,
            IPermissionResolver permissionResolver,
            IAuditService auditService,
            ILogger<RolesRepo> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
            _auditService = auditService;
            _logger = logger;
        }

        // -------------------------------------------------------------------------
        //  Reads
        // -------------------------------------------------------------------------

        public async Task<PagedRolesResult> GetRoleListAsync(
            GetRolesRequestDTO request, CancellationToken cancellationToken = default)
        {
            request ??= new GetRolesRequestDTO();

            var pageNumber = request.PageNumber.GetValueOrDefault(1);
            if (pageNumber < 1) pageNumber = 1;

            var pageSize = request.PageSize.GetValueOrDefault(25);
            if (pageSize < 1) pageSize = 25;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var query = _db.LK_Roles.AsNoTracking().Where(r => !r.IsDeleted);

            if (request.IncludeInactive != true)
            {
                query = query.Where(r => r.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                var search = request.SearchText.Trim();
                query = query.Where(r =>
                    (r.RoleName ?? string.Empty).Contains(search) ||
                    (r.RoleDescription ?? string.Empty).Contains(search));
            }

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            // Project counts as correlated subqueries so paging happens in SQL rather
            // than by materialising every role and its grants.
            var projected = query.Select(r => new RoleListItemResponseDTO
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                RoleDescription = r.RoleDescription,
                IsSystemRole = r.IsSystemRole,
                IsDefault = r.IsDefault,
                IsActive = r.IsActive,
                HasAllPermissions = r.RoleId == SuperAdminRoleId,
                PermissionCount = _db.SYS_RolePermissions.Count(rp => rp.RoleId == r.RoleId),
                AssignedUserCount = _db.SYS_Logins.Count(l => l.RoleId == r.RoleId)
            });

            projected = ApplySort(projected, request.SortColumn, request.SortOrder);

            var items = await projected
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new PagedRolesResult { Items = items, TotalCount = totalCount };
        }

        /// <summary>
        /// Whitelisted sort. The column name arrives from the client, so it is matched
        /// against a fixed set rather than interpolated into a query - there is no
        /// dynamic-SQL path here to inject into.
        /// </summary>
        private static IQueryable<RoleListItemResponseDTO> ApplySort(
            IQueryable<RoleListItemResponseDTO> query, string? sortColumn, string? sortOrder)
        {
            var descending = string.Equals(sortOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

            return (sortColumn?.Trim().ToLowerInvariant()) switch
            {
                "rolename" => descending
                    ? query.OrderByDescending(r => r.RoleName)
                    : query.OrderBy(r => r.RoleName),
                "roledescription" => descending
                    ? query.OrderByDescending(r => r.RoleDescription)
                    : query.OrderBy(r => r.RoleDescription),
                "permissioncount" => descending
                    ? query.OrderByDescending(r => r.PermissionCount)
                    : query.OrderBy(r => r.PermissionCount),
                _ => descending
                    ? query.OrderByDescending(r => r.RoleId)
                    : query.OrderBy(r => r.RoleId)
            };
        }

        public async Task<RolePermissionTreeResponseDTO?> GetRolePermissionTreeAsync(
            int? roleId, CancellationToken cancellationToken = default)
        {
            var effectiveRoleId = roleId.GetValueOrDefault();

            LK_Role? role = null;

            if (effectiveRoleId > 0)
            {
                role = await _db.LK_Roles.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RoleId == effectiveRoleId && !r.IsDeleted, cancellationToken)
                    .ConfigureAwait(false);

                if (role is null)
                {
                    return null;
                }
            }

            var catalog = await _db.SYS_Permissions.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new
                {
                    p.PermissionId,
                    p.PermissionCode,
                    p.ModuleKey,
                    p.ModuleName,
                    p.ActionName,
                    p.Description
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var isSuperAdmin = effectiveRoleId == SuperAdminRoleId;

            HashSet<int> granted;
            if (isSuperAdmin)
            {
                // Super Admin holds everything implicitly, so present every node checked.
                granted = catalog.Select(p => p.PermissionId).ToHashSet();
            }
            else if (effectiveRoleId > 0)
            {
                granted = (await _db.SYS_RolePermissions.AsNoTracking()
                        .Where(rp => rp.RoleId == effectiveRoleId)
                        .Select(rp => rp.PermissionId)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false))
                    .ToHashSet();
            }
            else
            {
                // Creating a new role: blank catalog.
                granted = new HashSet<int>();
            }

            var modules = catalog
                .GroupBy(p => new { p.ModuleKey, p.ModuleName })
                .Select(group =>
                {
                    var permissions = group.Select(p => new PermissionNodeDTO
                    {
                        PermissionId = p.PermissionId,
                        PermissionCode = p.PermissionCode,
                        ActionName = p.ActionName,
                        Description = p.Description,
                        Checked = granted.Contains(p.PermissionId)
                    }).ToList();

                    var checkedCount = permissions.Count(p => p.Checked);

                    return new PermissionModuleNodeDTO
                    {
                        ModuleKey = group.Key.ModuleKey,
                        ModuleName = group.Key.ModuleName,
                        // Rollups, computed here and never stored - only leaf grants exist.
                        Checked = permissions.Count > 0 && checkedCount == permissions.Count,
                        Indeterminate = checkedCount > 0 && checkedCount < permissions.Count,
                        Permissions = permissions
                    };
                })
                .ToList();

            return new RolePermissionTreeResponseDTO
            {
                RoleId = effectiveRoleId > 0 ? effectiveRoleId : null,
                RoleName = role?.RoleName,
                IsSystemRole = role?.IsSystemRole ?? false,
                HasAllPermissions = isSuperAdmin,
                Modules = modules
            };
        }

        public async Task<MyPermissionsResponseDTO> GetMyPermissionsAsync(
            long userId, CancellationToken cancellationToken = default)
        {
            var resolved = await _permissionResolver
                .GetForUserAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            var response = new MyPermissionsResponseDTO
            {
                UserId = userId,
                RoleId = resolved.RoleId,
                RoleName = resolved.RoleName,
                HasAllPermissions = resolved.IsSuperAdmin
            };

            if (resolved.IsSuperAdmin)
            {
                // Expand the implicit grant so the client can use one uniform code path.
                response.Permissions = await _db.SYS_Permissions.AsNoTracking()
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.DisplayOrder)
                    .Select(p => p.PermissionCode)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                response.Permissions = resolved.Codes.OrderBy(c => c, StringComparer.Ordinal).ToList();
            }

            return response;
        }

        // -------------------------------------------------------------------------
        //  Writes
        // -------------------------------------------------------------------------

        public async Task<RoleOperationResultDTO> SaveRoleAsync(
            SaveRoleRequestDTO request, long actingUserId, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return RoleOperationResultDTO.Fail("ROLE_NAME_REQUIRED");
            }

            var roleName = request.RoleName?.Trim();
            var isUpdate = request.RoleId.GetValueOrDefault() > 0;

            if (string.IsNullOrWhiteSpace(roleName))
            {
                return RoleOperationResultDTO.Fail("ROLE_NAME_REQUIRED");
            }

            var permissionIds = (request.PermissionIds ?? new List<int>()).Distinct().ToList();

            // Validate every submitted id against the catalog. The client sends ids, so an
            // unknown one means a stale or tampered payload; refuse the whole save rather
            // than silently granting a subset.
            if (permissionIds.Count > 0)
            {
                var knownIds = await _db.SYS_Permissions.AsNoTracking()
                    .Where(p => permissionIds.Contains(p.PermissionId) && p.IsActive)
                    .Select(p => p.PermissionId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var unknown = permissionIds.Except(knownIds).ToList();
                if (unknown.Count > 0)
                {
                    return RoleOperationResultDTO.Fail(
                        "PERMISSION_UNKNOWN",
                        $"Unknown or inactive permission id(s): {string.Join(", ", unknown)}");
                }
            }

            LK_Role role;

            if (isUpdate)
            {
                var roleId = request.RoleId!.Value;

                // Assigned via a local first so the null check narrows the type and
                // `role` is never nullable-assigned (CS8600).
                var existingRole = await _db.LK_Roles
                    .FirstOrDefaultAsync(r => r.RoleId == roleId && !r.IsDeleted, cancellationToken)
                    .ConfigureAwait(false);

                if (existingRole is null)
                {
                    return RoleOperationResultDTO.Fail("ROLE_NOT_FOUND");
                }

                role = existingRole;

                // A system role's NAME is load-bearing: [AuthorizeRoles(UserRole.X)]
                // matches on the name string, so renaming "Clinic Admin" would silently
                // lock every clinic admin out of the endpoints gated on it. Permissions
                // stay editable.
                if (role.IsSystemRole &&
                    !string.Equals(role.RoleName, roleName, StringComparison.Ordinal))
                {
                    return RoleOperationResultDTO.Fail("ROLE_RENAME_NOT_ALLOWED");
                }

                if (await NameIsTakenAsync(roleName, roleId, cancellationToken).ConfigureAwait(false))
                {
                    return RoleOperationResultDTO.Fail("ROLE_NAME_DUPLICATE");
                }

                role.RoleName = roleName;
                role.RoleDescription = request.RoleDescription?.Trim();
                role.UpdatedBy = actingUserId;
                role.UpdatedDateUtc = DateTime.UtcNow;

                // A system role must stay active - deactivating it would lock out every
                // user who holds it, with no way back through the UI.
                if (request.IsActive.HasValue && !role.IsSystemRole)
                {
                    role.IsActive = request.IsActive.Value;
                }
            }
            else
            {
                if (await NameIsTakenAsync(roleName, null, cancellationToken).ConfigureAwait(false))
                {
                    return RoleOperationResultDTO.Fail("ROLE_NAME_DUPLICATE");
                }

                // RoleId is left unset: dbo.LK_Roles.RoleId is an IDENTITY column, so the
                // database assigns it. The new role therefore gets the next available id
                // rather than a value chosen here.
                role = new LK_Role
                {
                    RoleName = roleName,
                    RoleDescription = request.RoleDescription?.Trim(),
                    IsSystemRole = false,
                    IsDefault = false,
                    IsActive = request.IsActive ?? true,
                    IsDeleted = false,
                    CreatedBy = actingUserId,
                    CreatedDateUtc = DateTime.UtcNow
                };

                // Grants are attached through the navigation collection, NOT by setting
                // RoleId. On an insert the id does not exist until SaveChanges returns,
                // so writing rows with role.RoleId here would persist RoleId = 0 and
                // violate FK_SYS_RolePermission_LK_Roles. Letting EF fix up the foreign
                // key from the navigation keeps it to a single round trip.
                foreach (var permissionId in permissionIds)
                {
                    role.SYS_RolePermissions.Add(new SYS_RolePermission
                    {
                        PermissionId = permissionId,
                        CreatedBy = actingUserId,
                        CreatedDateUtc = DateTime.UtcNow
                    });
                }

                _db.LK_Roles.Add(role);
            }

            // On update the id is known, so the grant set can be diffed in place.
            // Super Admin is skipped either way: its grants are implicit, and storing
            // rows for it would drift from the catalog as new permissions are added.
            if (isUpdate && role.RoleId != SuperAdminRoleId)
            {
                await ReplaceGrantsAsync(role.RoleId, permissionIds, actingUserId, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Take effect on the very next request rather than when the cache expires.
            _permissionResolver.InvalidateRole(role.RoleId);

            await SafeAuditAsync(
                isUpdate ? "RoleUpdated" : "RoleCreated",
                role.RoleId,
                actingUserId,
                $"Role '{role.RoleName}' saved with {permissionIds.Count} permission(s).")
                .ConfigureAwait(false);

            _logger?.LogInformation(
                "RoleId {RoleId} ({RoleName}) saved by UserId {ActingUserId} with {Count} permission(s).",
                role.RoleId, role.RoleName, actingUserId, permissionIds.Count);

            return RoleOperationResultDTO.Ok(role.RoleId);
        }

        /// <summary>
        /// Replaces a role's grants with exactly <paramref name="permissionIds"/>.
        /// Computes the delta rather than delete-all-then-reinsert, so unchanged rows
        /// keep their original CreatedBy/CreatedDateUtc audit values.
        /// </summary>
        private async Task ReplaceGrantsAsync(
            int roleId, List<int> permissionIds, long actingUserId, CancellationToken cancellationToken)
        {
            var existing = await _db.SYS_RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var existingIds = existing.Select(rp => rp.PermissionId).ToHashSet();
            var desiredIds = permissionIds.ToHashSet();

            var toRemove = existing.Where(rp => !desiredIds.Contains(rp.PermissionId)).ToList();
            if (toRemove.Count > 0)
            {
                _db.SYS_RolePermissions.RemoveRange(toRemove);
            }

            foreach (var permissionId in desiredIds.Where(id => !existingIds.Contains(id)))
            {
                _db.SYS_RolePermissions.Add(new SYS_RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId,
                    CreatedBy = actingUserId,
                    CreatedDateUtc = DateTime.UtcNow
                });
            }
        }

        private Task<bool> NameIsTakenAsync(string roleName, int? excludeRoleId, CancellationToken cancellationToken)
        {
            var query = _db.LK_Roles.AsNoTracking().Where(r => !r.IsDeleted && r.RoleName == roleName);

            if (excludeRoleId.HasValue)
            {
                var excluded = excludeRoleId.Value;
                query = query.Where(r => r.RoleId != excluded);
            }

            return query.AnyAsync(cancellationToken);
        }

        public async Task<RoleOperationResultDTO> DeleteRoleAsync(
            int roleId, long actingUserId, CancellationToken cancellationToken = default)
        {
            var role = await _db.LK_Roles
                .FirstOrDefaultAsync(r => r.RoleId == roleId && !r.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            if (role is null)
            {
                return RoleOperationResultDTO.Fail("ROLE_NOT_FOUND");
            }

            if (role.IsSystemRole)
            {
                return RoleOperationResultDTO.Fail("ROLE_IS_SYSTEM");
            }

            // Enforced here, not just hidden in the UI. In the reference project this
            // lived inside usp_DeleteRoleByID and the client-side check was an *ngIf.
            var assignedUsers = await _db.SYS_Logins.AsNoTracking()
                .CountAsync(l => l.RoleId == roleId, cancellationToken)
                .ConfigureAwait(false);

            if (assignedUsers > 0)
            {
                return RoleOperationResultDTO.Fail(
                    "ROLE_IN_USE", $"{assignedUsers} user(s) still hold this role.");
            }

            // Soft delete, and drop the grants so a re-created role with the same id
            // cannot inherit stale permissions.
            role.IsDeleted = true;
            role.IsActive = false;
            role.UpdatedBy = actingUserId;
            role.UpdatedDateUtc = DateTime.UtcNow;

            var grants = await _db.SYS_RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (grants.Count > 0)
            {
                _db.SYS_RolePermissions.RemoveRange(grants);
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _permissionResolver.InvalidateRole(roleId);

            await SafeAuditAsync("RoleDeleted", roleId, actingUserId,
                $"Role '{role.RoleName}' soft-deleted; {grants.Count} grant(s) removed.")
                .ConfigureAwait(false);

            return RoleOperationResultDTO.Ok(roleId);
        }

        public async Task<RoleOperationResultDTO> AssignUserRoleAsync(
            long userId, int roleId, long actingUserId, CancellationToken cancellationToken = default)
        {
            var role = await _db.LK_Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleId == roleId && !r.IsDeleted, cancellationToken)
                .ConfigureAwait(false);

            if (role is null)
            {
                return RoleOperationResultDTO.Fail("ROLE_NOT_FOUND");
            }

            if (!role.IsActive)
            {
                return RoleOperationResultDTO.Fail("ROLE_INACTIVE");
            }

            // The role lives on SYS_Login, which is what the login flow and
            // PermissionResolver both read.
            var userDetail = await _db.SYS_UserDetails.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (userDetail?.LoginId is null)
            {
                return RoleOperationResultDTO.Fail("USER_NOT_FOUND");
            }

            var login = await _db.SYS_Logins
                .FirstOrDefaultAsync(l => l.LoginId == userDetail.LoginId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (login is null)
            {
                return RoleOperationResultDTO.Fail("USER_NOT_FOUND");
            }

            var previousRoleId = login.RoleId;

            if (previousRoleId == roleId)
            {
                return RoleOperationResultDTO.Ok(roleId);
            }

            login.RoleId = roleId;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // No cache to clear: PermissionResolver reads the user -> role link from the
            // database on every request precisely so a change like this is immediate.

            await SafeAuditAsync("UserRoleAssigned", userId, actingUserId,
                $"UserId {userId} moved from RoleId {previousRoleId?.ToString() ?? "none"} to RoleId {roleId} ('{role.RoleName}').")
                .ConfigureAwait(false);

            _logger?.LogInformation(
                "UserId {UserId} role changed from {PreviousRoleId} to {RoleId} by UserId {ActingUserId}.",
                userId, previousRoleId, roleId, actingUserId);

            return RoleOperationResultDTO.Ok(roleId);
        }

        /// <summary>
        /// Writes through the project's existing audit service. Audit failure must never
        /// fail the operation that succeeded, so it is swallowed and logged.
        /// </summary>
        private async Task SafeAuditAsync(string action, long entityId, long actingUserId, string description)
        {
            if (_auditService is null)
            {
                return;
            }

            try
            {
                await _auditService.LogEntityChangeAsync(
                    action: action,
                    entityType: "LK_Role",
                    entityId: entityId,
                    oldValues: null,
                    newValues: null,
                    userId: actingUserId,
                    description: description,
                    module: "RBAC").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to write RBAC audit entry for {Action} on {EntityId}.", action, entityId);
            }
        }
    }
}
