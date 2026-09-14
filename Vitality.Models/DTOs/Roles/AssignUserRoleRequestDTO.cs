namespace Vitality.Models.DTOs.Roles
{
    /// <summary>
    /// Changes which role a user holds. One role per user, matching both this project's
    /// existing SYS_Login.RoleId column and the reference project's [User].UserRole.
    /// </summary>
    public class AssignUserRoleRequestDTO
    {
        public long? UserId { get; set; }

        public int? RoleId { get; set; }
    }
}
