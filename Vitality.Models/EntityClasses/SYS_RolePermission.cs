using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// A single grant: role X holds permission Y.
    /// <para>
    /// Leaf-level only, exactly like the reference project's <c>RoleScreenPermission</c>
    /// table. A role either holds a permission code or it does not - there is no
    /// module-level or "partial" grant stored. The module-level checkbox state the
    /// role editor shows is a rollup computed from these rows at read time, never
    /// persisted, so the two can never disagree.
    /// </para>
    /// <para>
    /// Saving a role replaces its whole grant set (delete-all then reinsert), which
    /// is also what the reference project's <c>usp_UpdateRole</c> did with its
    /// table-valued parameter. The client always submits the complete set.
    /// </para>
    /// </summary>
    public partial class SYS_RolePermission
    {
        public long RolePermissionId { get; set; }

        public int RoleId { get; set; }

        public int PermissionId { get; set; }

        /// <summary>UserId of whoever granted this. Taken from the caller's token, never from the request body.</summary>
        public long? CreatedBy { get; set; }

        public DateTime CreatedDateUtc { get; set; }

        public virtual LK_Role Role { get; set; } = null!;

        public virtual SYS_Permission Permission { get; set; } = null!;
    }
}
