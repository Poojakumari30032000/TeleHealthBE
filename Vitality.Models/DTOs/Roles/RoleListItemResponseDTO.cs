namespace Vitality.Models.DTOs.Roles
{
    /// <summary>One row of the role grid.</summary>
    public class RoleListItemResponseDTO
    {
        public int RoleId { get; set; }

        public string? RoleName { get; set; }

        public string? RoleDescription { get; set; }

        /// <summary>
        /// True for the seven built-in roles. The client hides delete and disables the
        /// name field for these; the server refuses the operation regardless.
        /// </summary>
        public bool IsSystemRole { get; set; }

        public bool IsDefault { get; set; }

        public bool IsActive { get; set; }

        /// <summary>How many permissions this role grants. Null count is shown as "All" for Super Admin.</summary>
        public int PermissionCount { get; set; }

        /// <summary>True for Super Admin, whose permissions are implicit rather than stored.</summary>
        public bool HasAllPermissions { get; set; }

        /// <summary>Users currently assigned this role. A role in use cannot be deleted.</summary>
        public int AssignedUserCount { get; set; }
    }
}
