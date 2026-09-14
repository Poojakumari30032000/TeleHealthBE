using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Services.Security
{
    /// <summary>
    /// Resolves what a user is allowed to do. The single authority for permission
    /// decisions; the <c>[RequiresPermission]</c> filter, the
    /// <c>GET api/Roles/myPermissions</c> endpoint and any service-level check all go
    /// through it, so they can never disagree.
    /// </summary>
    public interface IPermissionResolver
    {
        /// <summary>
        /// Resolves the permission set for a user.
        /// <para>
        /// The user's role is read from the DATABASE, never from the <c>RoleId</c> JWT
        /// claim. Tokens here last 30 days with no refresh flow, so trusting the claim
        /// would mean a demoted user kept their old permissions for up to a month.
        /// Role -> permission grants are cached; the user -> role lookup is not.
        /// </para>
        /// </summary>
        /// <param name="userId">The <c>UserId</c> claim value, already validated by JWT signature check.</param>
        Task<UserPermissions> GetForUserAsync(long userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Drops the cached grant set for one role. Called whenever a role's permissions
        /// change, so an admin's edit takes effect on the next request rather than after
        /// the cache expires.
        /// </summary>
        void InvalidateRole(int roleId);

        /// <summary>Drops every cached grant set. Used after a bulk change.</summary>
        void InvalidateAll();
    }
}
