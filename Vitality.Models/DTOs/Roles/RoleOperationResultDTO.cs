namespace Vitality.Models.DTOs.Roles
{
    /// <summary>
    /// Outcome of a role write. Follows the existing convention set by
    /// <c>AccountsRepo.Login</c>: the repository returns a machine-readable
    /// <see cref="ErrorCode"/> and the controller maps it to a user-facing message, so
    /// wording lives in one place and the repository stays free of presentation concerns.
    /// </summary>
    public class RoleOperationResultDTO
    {
        public bool Success { get; set; }

        /// <summary>
        /// One of: ROLE_NAME_REQUIRED, ROLE_NAME_DUPLICATE, ROLE_NOT_FOUND,
        /// ROLE_IS_SYSTEM, ROLE_IN_USE, ROLE_RENAME_NOT_ALLOWED, PERMISSION_UNKNOWN,
        /// USER_NOT_FOUND, ROLE_INACTIVE. Null on success.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>Extra detail for the log, e.g. which permission id was unknown. Not shown to the user.</summary>
        public string? ErrorDetail { get; set; }

        /// <summary>The affected role. Set on a successful create so the client can navigate to it.</summary>
        public int? RoleId { get; set; }

        public static RoleOperationResultDTO Ok(int? roleId = null) =>
            new RoleOperationResultDTO { Success = true, RoleId = roleId };

        public static RoleOperationResultDTO Fail(string errorCode, string? detail = null) =>
            new RoleOperationResultDTO { Success = false, ErrorCode = errorCode, ErrorDetail = detail };
    }
}
