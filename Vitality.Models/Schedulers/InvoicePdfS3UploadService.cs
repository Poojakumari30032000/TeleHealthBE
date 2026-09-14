using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services.S3;

namespace Vitality.Models.Schedulers
{

    public class InvoicePdfS3UploadService : BackgroundService
    {
        private const string LockName = "SCHEDULER:InvoicePdfS3Upload";

        private readonly ILogger<InvoicePdfS3UploadService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SchedulerSettings _settings;

        public InvoicePdfS3UploadService(
            ILogger<InvoicePdfS3UploadService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<SchedulerSettings> settings)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _settings = settings?.Value ?? new SchedulerSettings();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = Math.Max(1, _settings.InvoicePdfS3UploadIntervalHours);
            var maxRetries = Math.Max(0, _settings.S3UploadMaxRetries);
            var retryBaseSeconds = Math.Max(1, _settings.S3UploadRetryBaseSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                        var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();
                        var pdfService = scope.ServiceProvider.GetRequiredService<IInvoicePdfService>();
                        var webHostEnvironment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                        var s3Settings = scope.ServiceProvider.GetRequiredService<IOptions<S3Settings>>().Value;

                        var connectionString = db.Database.GetDbConnection().ConnectionString;
                        await using var distributedLock = await SchedulerDistributedLock.TryAcquireAsync(connectionString, LockName, stoppingToken).ConfigureAwait(false);
                        if (distributedLock is null)
                        {
                            _logger.LogInformation("InvoicePdfS3UploadService: another instance holds the {LockName} lock — skipping this iteration.", LockName);
                            await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
                            continue;
                        }

                        var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);

                        var invoicesToUpload = await db.Sys_Invoices
                            .Where(x => x.CreatedDate.HasValue &&
                                       x.CreatedDate.Value < oneMonthAgo &&
                                       !string.IsNullOrWhiteSpace(x.InvoiceNumber) &&
                                       (string.IsNullOrWhiteSpace(x.PdfS3Url)) &&
                                       (x.IsActive == true || x.IsActive == null))
                            .ToListAsync(stoppingToken);

                        _logger.LogInformation($"Found {invoicesToUpload.Count} invoices older than 1 month to upload to S3");

                        var webRootPath = webHostEnvironment.WebRootPath;
                        if (string.IsNullOrEmpty(webRootPath))
                        {
                            var currentDir = Directory.GetCurrentDirectory();
                            var wwwrootPath1 = Path.Combine(currentDir, "wwwroot");
                            var wwwrootPath2 = Path.Combine(Directory.GetParent(currentDir)?.FullName ?? currentDir, "Vitality", "wwwroot");

                            if (Directory.Exists(wwwrootPath1))
                                webRootPath = wwwrootPath1;
                            else if (Directory.Exists(wwwrootPath2))
                                webRootPath = wwwrootPath2;
                            else
                                webRootPath = wwwrootPath1;
                        }

                        int successCount = 0;
                        int failureCount = 0;

                        foreach (var invoice in invoicesToUpload)
                        {
                            try
                            {

                                var pdfRelativePath = pdfService.GetInvoicePdfRelativePath(
                                    invoice.InvoiceNumber ?? string.Empty,
                                    invoice.InvoiceId);

                                var pdfFullPath = Path.Combine(webRootPath, pdfRelativePath);

                                if (!File.Exists(pdfFullPath))
                                {
                                    _logger.LogWarning($"PDF file not found for invoice {invoice.InvoiceId}: {pdfFullPath}");
                                    failureCount++;
                                    continue;
                                }

                                var s3Key = pdfRelativePath.Replace("\\", "/");
                                var fileExistsInS3 = await s3Service.FileExistsAsync(s3Key);

                                if (fileExistsInS3)
                                {
                                    _logger.LogInformation($"PDF already exists in S3 for invoice {invoice.InvoiceId}, updating database with S3 URL");

                                    var s3Url = string.IsNullOrWhiteSpace(s3Settings.BaseUrl)
                                        ? $"https://{s3Settings.BucketName}.s3.{s3Settings.Region}.amazonaws.com/{s3Key}"
                                        : $"{s3Settings.BaseUrl.TrimEnd('/')}/{s3Key}";

                                    invoice.PdfS3Url = s3Url;
                                    await db.SaveChangesAsync(stoppingToken);
                                    successCount++;
                                    continue;
                                }

                                _logger.LogInformation($"Starting S3 upload for invoice {invoice.InvoiceId} from background service...");
                                using (var fileStream = new FileStream(pdfFullPath, FileMode.Open, FileAccess.Read))
                                {
                                    var fileName = Path.GetFileName(pdfRelativePath);
                                    _logger.LogInformation($"Uploading PDF to S3: InvoiceId={invoice.InvoiceId}, FileName={fileName}, Path={pdfFullPath}");

                                    Vitality.Models.DTOs.Common.UploadFileResponseDTO? uploadResult = null;
                                    Exception? lastUploadError = null;
                                    for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
                                    {
                                        try
                                        {

                                            if (fileStream.CanSeek && fileStream.Position != 0)
                                                fileStream.Position = 0;

                                            uploadResult = await s3Service.UploadFileAsync(
                                                fileStream,
                                                fileName,
                                                "application/pdf",
                                                "Invoices");
                                            lastUploadError = null;
                                            break;
                                        }
                                        catch (Exception uploadEx)
                                        {
                                            lastUploadError = uploadEx;
                                            if (attempt > maxRetries)
                                            {
                                                _logger.LogError(uploadEx, $"S3 upload failed for invoice {invoice.InvoiceId} after {attempt} attempt(s); will retry on next scheduler run.");
                                                break;
                                            }
                                            var delaySeconds = retryBaseSeconds * (int)Math.Pow(2, attempt - 1);
                                            _logger.LogWarning(uploadEx, $"S3 upload attempt {attempt} failed for invoice {invoice.InvoiceId}: {uploadEx.Message}. Retrying in {delaySeconds}s.");
                                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                                        }
                                    }

                                    if (uploadResult is null)
                                    {

                                        if (lastUploadError != null) throw lastUploadError;
                                        throw new InvalidOperationException("S3 upload returned null with no exception.");
                                    }

                                    _logger.LogInformation($"S3 upload completed for invoice {invoice.InvoiceId}. S3 URL: {uploadResult.Url}, Size: {uploadResult.Size} bytes");

                                    invoice.PdfS3Url = uploadResult.Url;
                                    var rowsAffected = await db.SaveChangesAsync(stoppingToken);

                                    await db.Entry(invoice).ReloadAsync(stoppingToken);

                                    if (!string.IsNullOrWhiteSpace(invoice.PdfS3Url) && invoice.PdfS3Url == uploadResult.Url)
                                    {
                                        _logger.LogInformation($"✓ SUCCESS: PDF invoice {invoice.InvoiceId} uploaded to S3 and URL saved to database. InvoiceId: {invoice.InvoiceId}, InvoiceNumber: {invoice.InvoiceNumber}, S3 URL: {invoice.PdfS3Url}, RowsAffected: {rowsAffected}");
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"⚠ WARNING: PDF uploaded to S3 but URL verification failed. InvoiceId: {invoice.InvoiceId}, Expected URL: {uploadResult.Url}, Saved URL: {invoice.PdfS3Url}");
                                    }

                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Failed to upload PDF for invoice {invoice.InvoiceId} to S3: {ex.Message}");
                                failureCount++;
                            }
                        }

                        if (invoicesToUpload.Count > 0)
                        {
                            _logger.LogInformation($"Invoice PDF S3 upload completed. Success: {successCount}, Failed: {failureCount}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error in InvoicePdfS3UploadService: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
            }
        }
    }
}
