using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using Vitality.Models.DTOs.Common;

namespace Vitality.Models.Repos.Services.S3;

public class S3Service : IS3Service, IDisposable
{
    private readonly S3Settings _settings;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3Service> _logger;

    public S3Service(
        IOptions<S3Settings> settings,
        ILogger<S3Service> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_settings.BucketName))
            throw new ArgumentException("S3 BucketName is required", nameof(settings));

        if (string.IsNullOrWhiteSpace(_settings.Region))
            throw new ArgumentException("S3 Region is required", nameof(settings));

        var region = RegionEndpoint.GetBySystemName(_settings.Region);

        if (!string.IsNullOrWhiteSpace(_settings.AccessKeyId) && !string.IsNullOrWhiteSpace(_settings.SecretAccessKey))
        {

            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(
                _settings.AccessKeyId,
                _settings.SecretAccessKey);
            _s3Client = new AmazonS3Client(awsCredentials, region);
        }
        else
        {

            _s3Client = new AmazonS3Client(region);
        }
    }

    public async Task<UploadFileResponseDTO> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? folder = null)
    {
        try
        {

            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid():N}{fileExtension}";

            var key = string.IsNullOrWhiteSpace(folder)
                ? uniqueFileName
                : $"{folder.TrimEnd('/')}/{uniqueFileName}";

            if (!string.IsNullOrWhiteSpace(_settings.DefaultFolder))
            {
                key = $"{_settings.DefaultFolder.TrimEnd('/')}/{key}";
            }

            long fileSize = 0;
            if (fileStream.CanSeek)
            {
                var currentPosition = fileStream.Position;
                fileStream.Position = 0;
                fileSize = fileStream.Length;
                fileStream.Position = currentPosition;
            }
            else
            {

                fileSize = 0;
            }

            var transferUtility = new TransferUtility(_s3Client);
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                BucketName = _settings.BucketName,
                Key = key,
                ContentType = contentType,

                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            await transferUtility.UploadAsync(uploadRequest);

            var fileUrl = string.IsNullOrWhiteSpace(_settings.BaseUrl)
                ? $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{key}"
                : $"{_settings.BaseUrl.TrimEnd('/')}/{key}";

            _logger?.LogInformation("File uploaded successfully to S3: {Key}, Size: {Size} bytes, URL: {Url}", key, fileSize, fileUrl);

            return new UploadFileResponseDTO
            {
                FileName = uniqueFileName,
                RelativePath = key,
                Url = fileUrl,
                Size = fileSize,
                ContentType = contentType
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error uploading file to S3: {FileName}", fileName);
            throw new Exception($"Failed to upload file to S3: {ex.Message}", ex);
        }
    }

    public async Task<UploadFileResponseDTO> UploadFileAsync(
        Microsoft.AspNetCore.Http.IFormFile file,
        string? folder = null)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null", nameof(file));

        await using var stream = file.OpenReadStream();
        return await UploadFileAsync(stream, file.FileName, file.ContentType, folder);
    }

    public async Task<bool> DeleteFileAsync(string key)
    {
        try
        {

            var fullKey = !string.IsNullOrWhiteSpace(_settings.DefaultFolder)
                ? $"{_settings.DefaultFolder.TrimEnd('/')}/{key}"
                : key;

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fullKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest);
            _logger?.LogInformation("File deleted successfully from S3: {Key}", fullKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting file from S3: {Key}", key);
            return false;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string key, int expirationMinutes = 60)
    {
        try
        {

            var fullKey = !string.IsNullOrWhiteSpace(_settings.DefaultFolder)
                ? $"{_settings.DefaultFolder.TrimEnd('/')}/{key}"
                : key;

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = fullKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            var url = await _s3Client.GetPreSignedURLAsync(request);
            return url;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating presigned URL for S3: {Key}", key);
            throw new Exception($"Failed to generate presigned URL: {ex.Message}", ex);
        }
    }

    public async Task<bool> FileExistsAsync(string key)
    {
        try
        {

            var fullKey = !string.IsNullOrWhiteSpace(_settings.DefaultFolder)
                ? $"{_settings.DefaultFolder.TrimEnd('/')}/{key}"
                : key;

            var request = new GetObjectMetadataRequest
            {
                BucketName = _settings.BucketName,
                Key = fullKey
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking file existence in S3: {Key}", key);
            return false;
        }
    }

    public async Task<Stream> DownloadFileAsync(string key)
    {
        try
        {

            var fullKey = !string.IsNullOrWhiteSpace(_settings.DefaultFolder)
                ? $"{_settings.DefaultFolder.TrimEnd('/')}/{key}"
                : key;

            var request = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fullKey
            };

            var response = await _s3Client.GetObjectAsync(request);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error downloading file from S3: {Key}", key);
            throw new Exception($"Failed to download file from S3: {ex.Message}", ex);
        }
    }

    public async Task<byte[]?> TryGetBytesFromConfiguredObjectUrlAsync(string? absoluteUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(absoluteUrl) || string.IsNullOrWhiteSpace(_settings.BucketName))
            return null;

        if (!Uri.TryCreate(absoluteUrl.Trim(), UriKind.Absolute, out var uri))
            return null;

        var key = TryExtractObjectKeyFromUrl(absoluteUrl.Trim(), uri);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request, ct);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not fetch S3 object for PDF image. Key hint: {Key}", key);
            return null;
        }
    }

    private string? TryExtractObjectKeyFromUrl(string absoluteUrl, Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/');
        path = Uri.UnescapeDataString(path);
        var bucket = (_settings.BucketName ?? string.Empty).Trim();

        var baseUrl = (_settings.BaseUrl ?? string.Empty).TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl) &&
            absoluteUrl.StartsWith(baseUrl + "/", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UnescapeDataString(absoluteUrl[(baseUrl.Length + 1)..].TrimStart('/'));
        }

        if (!string.IsNullOrEmpty(bucket) &&
            uri.Host.StartsWith($"{bucket}.s3", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrEmpty(path) ? null : path;

        if (uri.Host.StartsWith("s3.", StringComparison.OrdinalIgnoreCase) &&
            uri.Host.Contains("amazonaws.com", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(bucket) &&
            path.StartsWith($"{bucket}/", StringComparison.OrdinalIgnoreCase))
        {
            return path.Length > bucket.Length + 1
                ? Uri.UnescapeDataString(path[(bucket.Length + 1)..])
                : null;
        }

        return null;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
