using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// One row per permission code the application recognises - the permission catalog.
    /// <para>
    /// This is the adaptation of the reference project's four-table
    /// Module -> SubModule -> Screen -> ScreenAction tree. That tree existed to give
    /// the role editor something to render as a grouped checkbox list; the grant
    /// itself was always a single leaf id (<c>ScreenActionID</c>). Here the same
    /// grouping comes from <see cref="ModuleKey"/> plus <see cref="ActionName"/>, so
    /// the editor renders an identical two-level tree without three extra join tables
    /// to keep in step.
    /// </para>
    /// <para>
    /// <see cref="PermissionCode"/> is the value that actually travels: it is what
    /// <c>[RequiresPermission]</c> names on the server and what the Angular
    /// <c>PermissionsService</c> compares on the client. Rows are seeded by
    /// <c>Sql/Create_RBAC_Tables_And_Seed.sql</c> and must stay in step with
    /// <see cref="Vitality.Models.Security.Permissions"/>.
    /// </para>
    /// </summary>
    public partial class SYS_Permission
    {
        public SYS_Permission()
        {
            SYS_RolePermissions = new HashSet<SYS_RolePermission>();
        }

        public int PermissionId { get; set; }

        /// <summary>
        /// The permission string, e.g. <c>patient_view</c>. Unique, case-sensitive,
        /// and compared literally on both server and client.
        /// </summary>
        public string PermissionCode { get; set; } = null!;

        /// <summary>Grouping key derived from the code prefix, e.g. <c>patient</c>.</summary>
        public string ModuleKey { get; set; } = null!;

        /// <summary>Human-readable module heading for the role editor, e.g. "Patients".</summary>
        public string ModuleName { get; set; } = null!;

        /// <summary>Human-readable action label, e.g. "View", "Delete", "More Actions".</summary>
        public string ActionName { get; set; } = null!;

        public string? Description { get; set; }

        /// <summary>Sort order within the catalog. Drives the editor's row order.</summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Set to false to retire a permission without deleting grant history.
        /// Inactive permissions are hidden from the editor and never resolved.
        /// </summary>
        public bool IsActive { get; set; }

        public DateTime CreatedDateUtc { get; set; }

        public virtual ICollection<SYS_RolePermission> SYS_RolePermissions { get; set; }
    }
}
