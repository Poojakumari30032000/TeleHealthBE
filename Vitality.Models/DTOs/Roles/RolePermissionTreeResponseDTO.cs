using System.Collections.Generic;

namespace Vitality.Models.DTOs.Roles
{
    /// <summary>
    /// The permission catalog as a two-level checkbox tree, with the given role's grants
    /// already marked.
    /// <para>
    /// This is the adaptation of the reference project's
    /// <c>usp_GetModulesSubModulesScreensPermissionsbyId</c> result. That returned a flat
    /// 12-column rowset which the client reshaped into a 4-level tree
    /// (Module/SubModule/Screen/ScreenAction); this returns the tree already built, two
    /// levels deep, because the permission vocabulary here is Module + Action.
    /// </para>
    /// <para>
    /// Called with RoleId null or 0 it returns the blank catalog for a new role - the
    /// same dual behaviour the reference endpoint had.
    /// </para>
    /// </summary>
    public class RolePermissionTreeResponseDTO
    {
        public int? RoleId { get; set; }

        public string? RoleName { get; set; }

        public bool IsSystemRole { get; set; }

        /// <summary>True for Super Admin: every node is checked and the editor is read-only.</summary>
        public bool HasAllPermissions { get; set; }

        public List<PermissionModuleNodeDTO> Modules { get; set; } = new();
    }

    /// <summary>A module grouping in the permission tree, e.g. "Patients".</summary>
    public class PermissionModuleNodeDTO
    {
        public string ModuleKey { get; set; } = string.Empty;

        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// True when every permission below is granted. A ROLLUP computed at read time,
        /// never stored - only leaf grants exist in SYS_RolePermission, so the tree and
        /// the grants cannot drift apart.
        /// </summary>
        public bool Checked { get; set; }

        /// <summary>True when some but not all children are granted. Drives the tri-state box.</summary>
        public bool Indeterminate { get; set; }

        public List<PermissionNodeDTO> Permissions { get; set; } = new();
    }

    /// <summary>A single permission leaf - the only level that is actually granted.</summary>
    public class PermissionNodeDTO
    {
        public int PermissionId { get; set; }

        public string PermissionCode { get; set; } = string.Empty;

        public string ActionName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool Checked { get; set; }
    }
}
