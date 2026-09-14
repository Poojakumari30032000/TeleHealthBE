namespace Vitality.Models.Repos.Services;

public sealed class TokenSubject
{
    public long UserId { get; init; }

    public long LoginId { get; init; }

    public long? OrganizationId { get; init; }

    public long? RoleId { get; init; }

    public string RoleName { get; init; } = string.Empty;

    public string? RoleTitle { get; init; }

    public long? PatientId { get; init; }
}
