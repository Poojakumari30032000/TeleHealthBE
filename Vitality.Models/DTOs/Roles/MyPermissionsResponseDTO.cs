using System.Collections.Generic;

namespace Vitality.Models.DTOs.Roles
{
    /// <summary>
    /// What the signed-in caller is allowed to do. Returned by
    /// <c>GET api/Roles/myPermissions</c> and fed straight into the Angular
    /// <c>PermissionsService.setApiPermissions()</c> hook.
    /// <para>
    /// The reference project did the same thing with two calls after login
    /// (<c>getRolePermissionListByRoleId</c> and <c>GetModulesSubModulesScreensById</c>).
    /// One call is enough here because this project's sidebar is static and only the
    /// permission codes are needed.
    /// </para>
    /// <para>
    /// Permissions are NOT put in the JWT: tokens last 30 days with no refresh flow, so
    /// a token-embedded permission set would keep a demoted user's access alive for a
    /// month. This endpoint reflects the database on every call.
    /// </para>
    /// </summary>
    public class MyPermissionsResponseDTO
    {
        public long UserId { get; set; }

        public int RoleId { get; set; }

        public string? RoleName { get; set; }

        /// <summary>True for Super Admin. The client should treat every check as granted.</summary>
        public bool HasAllPermissions { get; set; }

        /// <summary>
        /// The granted permission codes, e.g. patient_view. For Super Admin this is the
        /// full catalog, expanded server-side so the client needs no special case.
        /// </summary>
        public List<string> Permissions { get; set; } = new();
    }
}
