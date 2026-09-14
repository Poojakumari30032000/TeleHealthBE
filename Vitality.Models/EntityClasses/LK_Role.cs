using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// A role. RoleIds 1-7 mirror <see cref="Vitality.Models.Enums.UserRole"/> and are
    /// referenced directly by <c>[AuthorizeRoles(UserRole.X)]</c> across the API, so they
    /// are flagged <see cref="IsSystemRole"/> and cannot be renamed or deleted. Custom
    /// roles are created from RoleId 1000 upward.
    /// </summary>
    public partial class LK_Role
    {
        public LK_Role()
        {
            SYS_RolePermissions = new HashSet<SYS_RolePermission>();
        }

        public int RoleId { get; set; }
        public string? RoleName { get; set; }

        // ---------------------------------------------------------------------
        //  RBAC metadata. Added by Sql/Create_RBAC_Tables_And_Seed.sql as
        //  nullable-or-defaulted columns, so existing rows stayed valid.
        // ---------------------------------------------------------------------

        public string? RoleDescription { get; set; }

        /// <summary>
        /// True for the seven built-in roles. A system role's name cannot be changed
        /// and it cannot be deleted, because compiled <c>[AuthorizeRoles]</c> attributes
        /// match on the role NAME - renaming "Clinic Admin" would silently lock every
        /// clinic admin out of the endpoints gated on it. Its permission set is still
        /// editable.
        /// </summary>
        public bool IsSystemRole { get; set; }

        /// <summary>Marks the role offered by default when creating a user. Informational.</summary>
        public bool IsDefault { get; set; }

        public bool IsActive { get; set; }

        /// <summary>Soft delete. Deleted roles are excluded from every query and cannot be assigned.</summary>
        public bool IsDeleted { get; set; }

        public long? CreatedBy { get; set; }
        public DateTime? CreatedDateUtc { get; set; }
        public long? UpdatedBy { get; set; }
        public DateTime? UpdatedDateUtc { get; set; }

        public virtual ICollection<SYS_RolePermission> SYS_RolePermissions { get; set; }
    }
}
