using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Roles;

namespace Vitality.Models.Repos.Interfaces
{
    /// <summary>
    /// Role and permission management. Covers the same surface as the reference
    /// project's <c>RoleController</c> - list, read, create/update, delete, permission
    /// tree - plus user-role assignment, which the reference had no endpoint for at all
    /// (there, a user's role could only ever be set at creation time).
    /// </summary>
    public interface IRolesRepo
    {
        /// <summary>Paged, searchable, sortable role grid.</summary>
        Task<PagedRolesResult> GetRoleListAsync(GetRolesRequestDTO request, CancellationToken cancellationToken = default);

        /// <summary>
        /// The permission catalog as a checkbox tree with <paramref name="roleId"/>'s
        /// grants marked. Pass null or 0 for the blank catalog used when creating a role -
        /// the same dual behaviour the reference project's single endpoint had.
        /// Returns null when a non-zero roleId does not exist.
        /// </summary>
        Task<RolePermissionTreeResponseDTO?> GetRolePermissionTreeAsync(int? roleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a role when <c>RoleId</c> is null or 0, otherwise updates it.
        /// Replaces the role's entire grant set with <c>PermissionIds</c>, then
        /// invalidates the permission cache so the change takes effect immediately.
        /// </summary>
        Task<RoleOperationResultDTO> SaveRoleAsync(SaveRoleRequestDTO request, long actingUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes a custom role. Refuses system roles and roles still assigned to a
        /// user - unlike the reference project, where both checks lived only inside a
        /// stored procedure and the UI-side check was cosmetic.
        /// </summary>
        Task<RoleOperationResultDTO> DeleteRoleAsync(int roleId, long actingUserId, CancellationToken cancellationToken = default);

        /// <summary>Changes which role a user holds.</summary>
        Task<RoleOperationResultDTO> AssignUserRoleAsync(long userId, int roleId, long actingUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The caller's own effective permissions, for the Angular client to consume.
        /// Super Admin's implicit grant is expanded to the full catalog here, so the
        /// client needs no special case for it.
        /// </summary>
        Task<MyPermissionsResponseDTO> GetMyPermissionsAsync(long userId, CancellationToken cancellationToken = default);
    }

    /// <summary>One page of the role grid, plus the total for the pager.</summary>
    public class PagedRolesResult
    {
        public List<RoleListItemResponseDTO> Items { get; set; } = new();

        public int TotalCount { get; set; }
    }
}
