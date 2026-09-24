using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Models.Repos.Services.S3;

namespace DudeMeds.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    // Was anonymous (AllowAnonymous). trigger-monthly-invoice generates billable invoices
    // for every active facility, so anonymous access let anyone create real
    // financial records. Restricted to Super Admin. See TEL-36.
    [AuthorizeRoles(UserRole.SuperAdmin)]
    public class TestMonthlyInvoiceController : ControllerBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TestMonthlyInvoiceController> _logger;

        public TestMonthlyInvoiceController(
            IServiceScopeFactory scopeFactory,
            ILogger<TestMonthlyInvoiceController> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpPost("trigger-monthly-invoice")]
        public async Task<ApiResponse<object>> TriggerMonthlyInvoice(
            [FromQuery] long? facilityId = null,
            [FromQuery] bool forceRegenerate = false,
            [FromQuery] int periodMonthsAgo = 0,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] bool skipPdf = false,
            [FromQuery] bool skipNotification = false)
        {
            var response = new ApiResponse<object>();
            var results = new List<string>();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepo>();
                var pdfService = scope.ServiceProvider.GetRequiredService<IInvoicePdfService>();
                var webHostEnvironment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var now = DateTime.UtcNow;
                var firstDayOfCurrentMonth = new DateTime(now.Year, now.Month, 1);
                var firstDayOfNextMonth = firstDayOfCurrentMonth.AddMonths(1);

                DateTime periodStart;
                DateTime periodEnd;
                string rangeSource;
                if (startDate.HasValue && endDate.HasValue)
                {
                    periodStart = startDate.Value;
                    periodEnd = endDate.Value;
                    if (periodEnd.TimeOfDay == TimeSpan.Zero)
                    {
                        periodEnd = periodEnd.AddDays(1).AddTicks(-1);
                    }
                    rangeSource = "explicit startDate/endDate";
                }
                else
                {
                    periodStart = firstDayOfCurrentMonth.AddMonths(-periodMonthsAgo);
                    periodEnd = periodStart.AddMonths(1).AddTicks(-1);
                    rangeSource = $"periodMonthsAgo={periodMonthsAgo}";
                }

                results.Add($"Billing window ({rangeSource}): {periodStart:yyyy-MM-dd HH:mm:ss} → {periodEnd:yyyy-MM-dd HH:mm:ss} (UTC)");

                var globalSubscription = db.SYS_Subscriptions
                    .Where(x => x.IsGlobal == true)
                    .OrderByDescending(x => x.SubscriptionId)
                    .FirstOrDefault();
                decimal globalMonthlyFee = globalSubscription?.MonthlyPrice ?? 0m;
                long? globalSubscriptionId = globalSubscription?.SubscriptionId;
                results.Add($"Global subscription: SubscriptionId={globalSubscriptionId?.ToString() ?? "none"}, MonthlyPrice={globalMonthlyFee}");

                var clinicsQuery = db.SYS_Facilities.Where(x => x.IsActive == true);
                if (facilityId.HasValue && facilityId.Value > 0)
                {
                    clinicsQuery = clinicsQuery.Where(x => x.FacilityId == facilityId.Value);
                }

                var clinics = await clinicsQuery.ToListAsync();
                results.Add($"Active clinics to process: {clinics.Count}");

                int generated = 0;
                int skippedDuplicate = 0;
                int skippedZero = 0;
                int pdfGeneratedCount = 0;
                int s3UploadedCount = 0;
                int notificationsSent = 0;

                foreach (var clinic in clinics)
                {
                    try
                    {
                        if (!forceRegenerate)
                        {
                            bool alreadyExists = db.Sys_Invoices.Any(x =>
                                x.FacilityId == clinic.FacilityId &&
                                x.CreatedDate.HasValue &&
                                x.CreatedDate.Value >= firstDayOfCurrentMonth &&
                                x.CreatedDate.Value < firstDayOfNextMonth &&
                                (x.IsMonthlyInvoiceGenerated ?? false));

                            if (alreadyExists)
                            {
                                skippedDuplicate++;
                                results.Add($"• Facility {clinic.FacilityId} '{clinic.TitleShort ?? clinic.TitleLong}' — skipped (already billed this month)");
                                continue;
                            }
                        }

                        var detailedInvoice = await invoiceRepo.GenerateMonthlyFacilityInvoice(
                            clinic.FacilityId,
                            periodStart,
                            periodEnd);

                        bool clinicIsBillable = clinic.IsBillable == true;
                        decimal billableFee = clinicIsBillable ? globalMonthlyFee : 0m;
                        decimal providerPharmacyTotal = detailedInvoice?.TotalAmount ?? 0m;
                        decimal computedTotal = providerPharmacyTotal + billableFee;

                        if (detailedInvoice == null || computedTotal == 0m)
                        {
                            skippedZero++;
                            results.Add($"• Facility {clinic.FacilityId} '{clinic.TitleShort ?? clinic.TitleLong}' — skipped (total=0, IsBillable={clinicIsBillable}, GlobalFee={billableFee}, Provider={detailedInvoice?.ProviderBillTotal ?? 0}, Pharmacy={detailedInvoice?.PharmacyBillTotal ?? 0})");
                            continue;
                        }

                        detailedInvoice.TotalAmount = computedTotal;
                        detailedInvoice.PlatformFee = billableFee;

                        var invoice = new Sys_Invoice
                        {
                            InvoiceNumber = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}-{clinic.FacilityId}-{Guid.NewGuid().ToString().Substring(0, 6)}",
                            CustomerName = detailedInvoice.FacilityName,
                            Amount = computedTotal,
                            InvoiceType = InvoiceType.ClinicToGlobal.ToString(),
                            Status = InvoiceStatus.Pending.ToString(),
                            FacilityId = clinic.FacilityId,
                            SubscriptionId = clinicIsBillable ? globalSubscriptionId : null,
                            PharmacyBillToltal = (long?)detailedInvoice.PharmacyBillTotal,
                            ProviderBillTotal = (long?)detailedInvoice.ProviderBillTotal,
                            CreatedBy = (long)UserRole.GlobalAdmin,
                            CreatedDate = DateTime.UtcNow,
                            IsActive = true,
                            IsMonthlyInvoiceGenerated = true
                        };

                        db.Sys_Invoices.Add(invoice);
                        await db.SaveChangesAsync();
                        generated++;

                        var headline =
                            $"✓ Facility {clinic.FacilityId} '{clinic.TitleShort ?? clinic.TitleLong}' — Invoice {invoice.InvoiceId} ({invoice.InvoiceNumber}). " +
                            $"Provider={detailedInvoice.ProviderBillTotal}, Pharmacy={detailedInvoice.PharmacyBillTotal}, " +
                            $"GlobalFee={billableFee} (IsBillable={clinicIsBillable}), Total={computedTotal}";

                        string? pdfStatus = null;
                        if (!skipPdf)
                        {
                            try
                            {
                                detailedInvoice.InvoiceId = invoice.InvoiceId;
                                detailedInvoice.InvoiceNumber = invoice.InvoiceNumber;
                                detailedInvoice.Status = invoice.Status;
                                detailedInvoice.InvoiceDate = invoice.CreatedDate;
                                detailedInvoice.DueDate = invoice.CreatedDate?.AddDays(29);

                                var pdfRelativePath = pdfService.GetInvoicePdfRelativePath(invoice.InvoiceNumber ?? string.Empty, invoice.InvoiceId);

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

                                var pdfFullPath = Path.Combine(webRootPath, pdfRelativePath);
                                var pdfGenerated2 = await pdfService.GenerateFacilityInvoicePdfAsync(detailedInvoice, pdfFullPath);

                                if (pdfGenerated2)
                                {
                                    pdfGeneratedCount++;
                                    var fileExists = System.IO.File.Exists(pdfFullPath);
                                    var fileSize = fileExists ? new FileInfo(pdfFullPath).Length : 0;
                                    pdfStatus = $"PDF OK ({fileSize} bytes, {pdfFullPath})";

                                    try
                                    {
                                        using var fileStream = new FileStream(pdfFullPath, FileMode.Open, FileAccess.Read);
                                        var fileName = Path.GetFileName(pdfRelativePath);
                                        var uploadResult = await s3Service.UploadFileAsync(
                                            fileStream,
                                            fileName,
                                            "application/pdf",
                                            "Invoices");

                                        invoice.PdfS3Url = uploadResult.Url;
                                        await db.SaveChangesAsync();
                                        s3UploadedCount++;
                                        pdfStatus += $"; S3 OK ({uploadResult.Url})";
                                    }
                                    catch (Exception s3Ex)
                                    {
                                        pdfStatus += $"; S3 FAILED: {s3Ex.Message}";
                                        _logger.LogError(s3Ex, "TestMonthlyInvoice: S3 upload failed for invoice {InvoiceId}", invoice.InvoiceId);
                                    }
                                }
                                else
                                {
                                    pdfStatus = $"PDF generation returned false (path: {pdfFullPath})";
                                }
                            }
                            catch (Exception pdfEx)
                            {
                                pdfStatus = $"PDF FAILED: {pdfEx.Message}";
                                _logger.LogError(pdfEx, "TestMonthlyInvoice: PDF generation failed for invoice {InvoiceId}", invoice.InvoiceId);
                            }
                        }
                        else
                        {
                            pdfStatus = "PDF skipped";
                        }

                        string? notifStatus = null;
                        if (!skipNotification)
                        {
                            try
                            {
                                await notificationService.SendMonthlyInvoiceGeneratedAsync(
                                    facilityId: clinic.FacilityId,
                                    amount: invoice.Amount ?? 0,
                                    invoiceNumber: invoice.InvoiceId.ToString(),
                                    ct: HttpContext.RequestAborted);
                                notificationsSent++;
                                notifStatus = "notification OK";
                            }
                            catch (Exception notifEx)
                            {
                                notifStatus = $"notification FAILED: {notifEx.Message}";
                                _logger.LogError(notifEx, "TestMonthlyInvoice: notification failed for facility {FacilityId}", clinic.FacilityId);
                            }
                        }
                        else
                        {
                            notifStatus = "notification skipped";
                        }

                        results.Add($"{headline} | {pdfStatus} | {notifStatus}");
                    }
                    catch (Exception clinicEx)
                    {
                        results.Add($"✗ Facility {clinic.FacilityId} — ERROR: {clinicEx.Message}");
                        _logger.LogError(clinicEx, "TestMonthlyInvoice: facility {FacilityId} failed", clinic.FacilityId);
                    }
                }

                response.Data = new
                {
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    GlobalSubscriptionId = globalSubscriptionId,
                    GlobalMonthlyFee = globalMonthlyFee,
                    Clinics = clinics.Count,
                    Generated = generated,
                    SkippedDuplicate = skippedDuplicate,
                    SkippedZeroAmount = skippedZero,
                    PdfGenerated = pdfGeneratedCount,
                    S3Uploaded = s3UploadedCount,
                    NotificationsSent = notificationsSent,
                    Log = results
                };
                response.Message = $"Done. Generated={generated}, PDF={pdfGeneratedCount}, S3={s3UploadedCount}, Notifications={notificationsSent}, SkippedDup={skippedDuplicate}, SkippedZero={skippedZero}.";
            }
            catch (Exception ex)
            {
                response.Message = $"Error: {ex.Message}";
                _logger.LogError(ex, "TestMonthlyInvoice trigger failed");
            }

            return response;
        }
    }
}
