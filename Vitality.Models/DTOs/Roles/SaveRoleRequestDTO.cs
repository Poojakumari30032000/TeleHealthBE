using System.Collections.Generic;

namespace Vitality.Models.DTOs.Roles
{
    /// <summary>
    /// Creates or updates a role. Like the reference project's single
    /// <c>AddRole</c> endpoint, RoleId decides which: null or 0 creates, a value updates.
    /// </summary>
    public class SaveRoleRequestDTO
    {
        /// <summary>Null or 0 to create a new role.</summary>
        public int? RoleId { get; set; }

        public string? RoleName { get; set; }

        public string? RoleDescription { get; set; }

        public bool? IsActive { get; set; }

        /// <summary>
        /// The COMPLETE set of permission ids the role should hold. The existing grants
        /// are replaced by this list, so omitting an id revokes it - the same
        /// replace-the-whole-set semantic as the reference project's table-valued
        /// parameter. An empty list is valid and means "grants nothing".
        /// </summary>
        public List<int>? PermissionIds { get; set; }
    }
}
