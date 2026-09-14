namespace Vitality.Models.Repos.Services.S3;

public sealed class S3Settings
{
    public const string SectionName = "S3";

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string? DefaultFolder { get; set; }
}
