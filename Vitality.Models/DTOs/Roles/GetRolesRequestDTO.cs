namespace Vitality.Models.DTOs.Roles
{
    /// <summary>
    /// Filter for the paged role grid. Mirrors the reference project's
    /// <c>GetRoleListQuery</c> / <c>usp_GetRoleList</c> parameters, and matches this
    /// project's existing request-DTO convention of all-nullable properties.
    /// </summary>
    public class GetRolesRequestDTO
    {
        public int? PageNumber { get; set; } = 1;

        public int? PageSize { get; set; } = 25;

        /// <summary>Matched against role name and description, case-insensitively.</summary>
        public string? SearchText { get; set; }

        /// <summary>One of: RoleName, RoleDescription, PermissionCount, RoleId. Defaults to RoleId.</summary>
        public string? SortColumn { get; set; }

        /// <summary>asc or desc. Defaults to asc.</summary>
        public string? SortOrder { get; set; }

        /// <summary>When true, inactive roles are included. Defaults to false.</summary>
        public bool? IncludeInactive { get; set; }
    }
}
