using System;
using System.Collections.Generic;
using System.Linq;

namespace Vitality.Models.Repos.Services.Security
{
    /// <summary>
    /// The resolved authorization state of one user for one request: which role they
    /// currently hold and which permission codes that role grants.
    /// </summary>
    public sealed class UserPermissions
    {
        /// <summary>An unauthenticated or unresolvable user. Holds nothing.</summary>
        public static readonly UserPermissions None =
            new UserPermissions(0, null, false, new HashSet<string>(StringComparer.Ordinal));

        private readonly HashSet<string> _codes;

        public UserPermissions(int roleId, string? roleName, bool isSuperAdmin, HashSet<string> codes)
        {
            RoleId = roleId;
            RoleName = roleName;
            IsSuperAdmin = isSuperAdmin;
            _codes = codes ?? new HashSet<string>(StringComparer.Ordinal);
        }

        public int RoleId { get; }

        public string? RoleName { get; }

        /// <summary>
        /// True for RoleId 1. Super Admin holds every permission implicitly rather than
        /// through seeded grant rows, so adding a new permission code never leaves the
        /// platform owner locked out of it. This mirrors how AuthorizeRolesAttribute
        /// already treats RoleId 1 as the platform owner.
        /// </summary>
        public bool IsSuperAdmin { get; }

        /// <summary>The explicit grants. Empty for Super Admin - see <see cref="Has"/>.</summary>
        public IReadOnlyCollection<string> Codes => _codes;

        /// <summary>
        /// Whether this user holds <paramref name="permissionCode"/>.
        /// Comparison is case-sensitive and ordinal, matching how the Angular client
        /// compares the same strings.
        /// </summary>
        public bool Has(string permissionCode)
        {
            if (IsSuperAdmin)
            {
                return true;
            }

            return !string.IsNullOrEmpty(permissionCode) && _codes.Contains(permissionCode);
        }

        /// <summary>
        /// Whether this user holds AT LEAST ONE of the given codes. This is the semantic
        /// the Angular <c>PermissionGuard</c> and <c>[appHasAnyPermission]</c> use for
        /// route and element gating, so the server enforces the same rule the client
        /// displays - any-of, not all-of.
        /// </summary>
        public bool HasAny(IEnumerable<string> permissionCodes)
        {
            if (permissionCodes is null)
            {
                return false;
            }

            if (IsSuperAdmin)
            {
                return true;
            }

            return permissionCodes.Any(code => !string.IsNullOrEmpty(code) && _codes.Contains(code));
        }
    }
}
