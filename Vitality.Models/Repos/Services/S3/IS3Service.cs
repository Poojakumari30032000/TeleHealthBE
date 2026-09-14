using Vitality.Models.DTOs.Common;

namespace Vitality.Models.Repos.Services.S3;

public interface IS3Service
{

    Task<UploadFileResponseDTO> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? folder = null);

    Task<UploadFileResponseDTO> UploadFileAsync(
        Microsoft.AspNetCore.Http.IFormFile file,
        string? folder = null);

    Task<bool> DeleteFileAsync(string key);

    Task<string> GetPresignedUrlAsync(string key, int expirationMinutes = 60);

    Task<bool> FileExistsAsync(string key);

    Task<Stream> DownloadFileAsync(string key);

    Task<byte[]?> TryGetBytesFromConfiguredObjectUrlAsync(string? absoluteUrl, CancellationToken ct = default);
}
