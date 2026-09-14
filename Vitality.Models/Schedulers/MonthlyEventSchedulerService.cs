using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Models.Repos.Services.S3;
using Vitality.Models.Repos.Services.Audit;
using Vitality.Models.Schedulers;

public class MonthlyEventSchedulerService : BackgroundService
{
    private const string LockName = "SCHEDULER:MonthlyEvent";

    private readonly ILogger<MonthlyEventSchedulerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMapper _mapper;
    private readonly SchedulerSettings _settings;

    public MonthlyEventSchedulerService(
        ILogger<MonthlyEventSchedulerService> logger,
        IServiceScopeFactory scopeFactory,
        IMapper mapper,
        IOptions<SchedulerSettings> settings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _mapper = mapper;
        _settings = settings?.Value ?? new SchedulerSettings();
    }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = Math.Max(1, _settings.MonthlyEventIntervalHours);

            while (!stoppingToken.IsCancellationRequested)
            {

                string? connectionString = null;
                try
                {
                    using var lockScope = _scopeFactory.CreateScope();
                    var lockDb = lockScope.ServiceProvider.GetRequiredService<MainContext>();
                    connectionString = lockDb.Database.GetDbConnection().ConnectionString;
                }
                catch (Exception lockCfgEx)
                {
                    _logger.LogError(lockCfgEx, "MonthlyEventSchedulerService: could not resolve DB connection string for distributed lock — skipping iteration.");
                    await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
                    continue;
                }

                await using var distributedLock = await SchedulerDistributedLock.TryAcquireAsync(connectionString, LockName, stoppingToken).ConfigureAwait(false);
                if (distributedLock is null)
                {
                    _logger.LogInformation("MonthlyEventSchedulerService: another instance holds the {LockName} lock — skipping this iteration.", LockName);
                    await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
                    continue;
                }

                bool shouldGenerateMonthlyInvoices = true;

                if (shouldGenerateMonthlyInvoices)
                {
                    try
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                            var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepo>();

                            var now = DateTime.UtcNow;
                            var firstDayOfCurrentMonth = new DateTime(now.Year, now.Month, 1);
                            var firstDayOfNextMonth = firstDayOfCurrentMonth.AddMonths(1);
                            var firstDayOfPreviousMonth = firstDayOfCurrentMonth.AddMonths(-1);
                            var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);

                            var startDate = firstDayOfPreviousMonth;

                            var endDate = lastDayOfPreviousMonth.Date.AddHours(23).AddMinutes(59).AddSeconds(59).AddMilliseconds(999);

                            var globalSubscription = db.SYS_Subscriptions
                                .Where(x => x.IsGlobal == true)
                                .OrderByDescending(x => x.SubscriptionId)
                                .FirstOrDefault();
                            decimal globalMonthlyFee = globalSubscription?.MonthlyPrice ?? 0m;
                            long? globalSubscriptionId = globalSubscription?.SubscriptionId;
                            _logger.LogInformation($"Monthly run: global subscription fee = {globalMonthlyFee} (SubscriptionId: {globalSubscriptionId?.ToString() ?? "none"}).");

                            var clinics = db.SYS_Facilities.Where(x => x.IsActive == true).ToList();
                            foreach (var clinic in clinics)
                            {

                                var existingInvoice = db.Sys_Invoices
                                    .Where(x => x.FacilityId == clinic.FacilityId &&
                                                x.CreatedDate.HasValue &&
                                                x.CreatedDate.Value >= firstDayOfCurrentMonth &&
                                                x.CreatedDate.Value < firstDayOfNextMonth &&
                                                (x.IsMonthlyInvoiceGenerated ?? false))
                                    .FirstOrDefault();

                                if (existingInvoice != null)
                                {
                                    _logger.LogInformation($"Monthly invoice already generated for facility {clinic.FacilityId} this month ({firstDayOfCurrentMonth:yyyy-MM}), skipping.");
                                    continue;
                                }

                            _logger.LogInformation($"Generating invoice for clinic {clinic.FacilityId} (Facility: {clinic.TitleShort ?? clinic.TitleLong}). Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                            var totalAppts = db.PT_PatientAppointmentSlots
                                .Where(apt => apt.FacilityId == clinic.FacilityId &&
                                              apt.StartDate >= startDate &&
                                              apt.StartDate <= endDate &&
                                              (apt.IsActive == true || apt.IsActive == null))
                                .Count();

                            var completedAppts = db.PT_PatientAppointmentSlots
                                .Where(apt => apt.FacilityId == clinic.FacilityId &&
                                              apt.StartDate >= startDate &&
                                              apt.StartDate <= endDate &&
                                              (apt.IsActive == true || apt.IsActive == null) &&
                                              (apt.Status == "Completed" || apt.Status == "completed" || apt.Status == "COMPLETED"))
                                .Count();

                            var allOrderStatuses = db.PT_PatientOrders
                                .Where(o => o.FacilityId == clinic.FacilityId &&
                                            o.CreatedDate >= startDate &&
                                            o.CreatedDate <= endDate &&
                                            (o.IsActive == true || o.IsActive == null))
                                .Select(o => o.OrderStatus)
                                .Distinct()
                                .ToList();

                            var totalOrders = db.PT_PatientOrders
                                .Where(o => o.FacilityId == clinic.FacilityId &&
                                            o.CreatedDate >= startDate &&
                                            o.CreatedDate <= endDate &&
                                            (o.IsActive == true || o.IsActive == null))
                                .Count();

                            var startOrders = db.PT_PatientOrders
                                .Where(o => o.FacilityId == clinic.FacilityId &&
                                            o.CreatedDate >= startDate &&
                                            o.CreatedDate <= endDate &&
                                            (o.IsActive == true || o.IsActive == null) &&
                                            (o.OrderStatus == "Start" || o.OrderStatus == "start" || o.OrderStatus == "START"))
                                .Count();

                            _logger.LogInformation($"Clinic {clinic.FacilityId} - Order statuses found: {string.Join(", ", allOrderStatuses.Where(s => !string.IsNullOrEmpty(s)))}");

                            _logger.LogInformation($"Clinic {clinic.FacilityId} diagnostic - Total Appointments: {totalAppts}, Completed: {completedAppts}, Total Orders: {totalOrders}, Start Orders: {startOrders}");

                            var detailedInvoice = await invoiceRepo.GenerateMonthlyFacilityInvoice(
                                clinic.FacilityId,
                                startDate,
                                endDate);

                            bool clinicIsBillable = clinic.IsBillable == true;
                            decimal billableFee = clinicIsBillable ? globalMonthlyFee : 0m;
                            decimal providerPharmacyTotal = detailedInvoice?.TotalAmount ?? 0m;
                            decimal computedTotal = providerPharmacyTotal + billableFee;

                            if (detailedInvoice == null || computedTotal == 0m)
                            {
                                _logger.LogInformation($"No billable activity for clinic {clinic.FacilityId} (Facility: {clinic.TitleShort ?? clinic.TitleLong}). " +
                                    $"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}. " +
                                    $"ProviderBill: {detailedInvoice?.ProviderBillTotal ?? 0}, PharmacyBill: {detailedInvoice?.PharmacyBillTotal ?? 0}, " +
                                    $"IsBillable: {clinicIsBillable}, GlobalFee: {billableFee}, " +
                                    $"Appointments: {detailedInvoice?.Summary?.TotalAppointments ?? 0}, Orders: {detailedInvoice?.Summary?.TotalOrders ?? 0}");
                                continue;
                            }

                            detailedInvoice.TotalAmount = computedTotal;
                            detailedInvoice.PlatformFee = billableFee;

                            _logger.LogInformation($"Invoice data for clinic {clinic.FacilityId}: " +
                                $"ProviderBill: {detailedInvoice.ProviderBillTotal}, PharmacyBill: {detailedInvoice.PharmacyBillTotal}, " +
                                $"GlobalFee: {billableFee} (IsBillable: {clinicIsBillable}), " +
                                $"Total: {computedTotal}, Appointments: {detailedInvoice.Summary?.TotalAppointments ?? 0}, " +
                                $"Orders: {detailedInvoice.Summary?.TotalOrders ?? 0}");

                            var invoice = new Sys_Invoice
                            {
                                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{clinic.FacilityId}-{Guid.NewGuid().ToString().Substring(0, 8)}",
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
                                await db.SaveChangesAsync(stoppingToken);

                            try
                            {
                                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                                await auditService.LogEntityChangeAsync(
                                    action: "Create",
                                    entityType: "Sys_Invoice",
                                    entityId: invoice.InvoiceId,
                                    newValues: invoice,
                                    userId: (long)UserRole.GlobalAdmin,
                                    description: $"Monthly invoice generated for Facility {clinic.FacilityId} ({clinic.TitleShort ?? clinic.TitleLong}), Amount: ${invoice.Amount}, Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                                    module: "Invoice"
                                );
                            }
                            catch (Exception auditEx)
                            {
                                _logger.LogWarning(auditEx, $"Failed to log audit for invoice {invoice.InvoiceId}");
                            }

                            _logger.LogInformation($"Invoice for clinic {clinic.FacilityId} created successfully. " +
                                $"Total: {invoice.Amount}, Provider: {invoice.ProviderBillTotal}, Pharmacy: {invoice.PharmacyBillToltal}");

                            try
                            {
                                _logger.LogInformation($"Starting PDF generation for invoice {invoice.InvoiceId}...");

                                var pdfService = scope.ServiceProvider.GetRequiredService<IInvoicePdfService>();
                                var webHostEnvironment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                                var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();

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

                                _logger.LogInformation($"Generating PDF for invoice {invoice.InvoiceId}. WebRoot: {webRootPath}, FullPath: {pdfFullPath}");

                                var pdfGenerated = await pdfService.GenerateFacilityInvoicePdfAsync(detailedInvoice, pdfFullPath);

                                if (pdfGenerated)
                                {
                                    var fileExists = System.IO.File.Exists(pdfFullPath);
                                    var fileSize = fileExists ? new FileInfo(pdfFullPath).Length : 0;
                                    _logger.LogInformation($"PDF invoice generated successfully for invoice {invoice.InvoiceId}. Path: {pdfFullPath}, Exists: {fileExists}, Size: {fileSize} bytes");

                                    try
                                    {
                                        _logger.LogInformation($"Starting S3 upload for invoice {invoice.InvoiceId}...");
                                        using (var fileStream = new FileStream(pdfFullPath, FileMode.Open, FileAccess.Read))
                                        {
                                            var fileName = Path.GetFileName(pdfRelativePath);
                                            _logger.LogInformation($"Uploading PDF to S3: InvoiceId={invoice.InvoiceId}, FileName={fileName}, Path={pdfFullPath}");

                                            var uploadResult = await s3Service.UploadFileAsync(
                                                fileStream,
                                                fileName,
                                                "application/pdf",
                                                "Invoices");

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
                                        }
                                    }
                                    catch (Exception s3Ex)
                                    {
                                        _logger.LogError(s3Ex, $"✗ FAILED: Upload PDF to S3 for invoice {invoice.InvoiceId}. Error: {s3Ex.Message}, StackTrace: {s3Ex.StackTrace}");

                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"Failed to generate PDF for invoice {invoice.InvoiceId}. Path attempted: {pdfFullPath}");
                                }
                            }
                            catch (Exception pdfEx)
                            {
                                _logger.LogError(pdfEx, $"Failed to generate PDF invoice for clinic {clinic.FacilityId}. Error: {pdfEx.Message}, StackTrace: {pdfEx.StackTrace}");

                            }

                            try
                            {
                                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                                await notificationService.SendMonthlyInvoiceGeneratedAsync(
                                    facilityId: clinic.FacilityId,
                                    amount: invoice.Amount ?? 0,
                                    invoiceNumber: invoice.InvoiceId.ToString() ?? string.Empty,
                                    ct: stoppingToken
                                );
                            }
                            catch (Exception notifEx)
                            {
                                _logger.LogError(notifEx, $"Failed to send monthly invoice notification for clinic {clinic.FacilityId}");
                            }
                        }

                        try
                        {
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                            var overdueInvoices = db.Sys_Invoices
                                .Where(x => x.FacilityId.HasValue &&
                                           x.InvoiceType == InvoiceType.ClinicToGlobal.ToString() &&
                                           x.Status == InvoiceStatus.Pending.ToString() &&
                                           x.IsActive == true &&
                                           x.CreatedDate.HasValue &&
                                           x.CreatedDate.Value.AddDays(29) < DateTime.UtcNow)
                                .ToList();

                            foreach (var overdueInvoice in overdueInvoices)
                            {
                                try
                                {
                                    await notificationService.SendOverdueClinicBillAsync(
                                        facilityId: overdueInvoice.FacilityId.Value,
                                        amount: overdueInvoice.Amount ?? 0,
                                        invoiceNumber: overdueInvoice.InvoiceId.ToString() ?? string.Empty,
                                        ct: stoppingToken
                                    );
                                }
                                catch (Exception overdueEx)
                                {
                                    _logger.LogError(overdueEx, $"Failed to send overdue bill notification for invoice {overdueInvoice.InvoiceId}");
                                }
                            }
                        }
                        catch (Exception overdueCheckEx)
                        {
                            _logger.LogError(overdueCheckEx, "Error checking for overdue bills");
                        }

                        try
                        {
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                            var pendingInvoices = db.Sys_Invoices
                                .Where(x => x.Status == InvoiceStatus.Pending.ToString() &&
                                           x.IsActive == true &&
                                           x.CreatedDate.HasValue &&
                                           x.CreatedDate.Value.AddDays(7) <= DateTime.UtcNow &&
                                           x.CreatedDate.Value.AddDays(8) > DateTime.UtcNow)
                                .ToList();

                            foreach (var pendingInvoice in pendingInvoices)
                            {
                                try
                                {
                                    await notificationService.SendBillingReminderAsync(
                                        patientId: pendingInvoice.PatientId,
                                        facilityId: pendingInvoice.FacilityId,
                                        amount: pendingInvoice.Amount ?? 0,
                                        invoiceNumber: pendingInvoice.InvoiceId.ToString() ?? string.Empty,
                                        ct: stoppingToken
                                    );
                                }
                                catch (Exception reminderEx)
                                {
                                    _logger.LogError(reminderEx, $"Failed to send billing reminder for invoice {pendingInvoice.InvoiceId}");
                                }
                            }
                        }
                        catch (Exception reminderCheckEx)
                        {
                            _logger.LogError(reminderCheckEx, "Error checking for billing reminders");
                        }

                        try
                        {
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                            var treatmentsNeedingRefills = db.PT_PatientTreatments
                                .Where(t => t.IsActive == true &&
                                           t.Status != "Completed" &&
                                           t.Status != "Cancelled")
                                .Join(db.PD_Drugs,
                                    t => t.ProductId,
                                    d => d.DrugId,
                                    (t, d) => new { Treatment = t, Drug = d })
                                .Where(x => x.Drug.Refills.HasValue && x.Drug.Refills.Value > 0)
                                .ToList();

                            foreach (var item in treatmentsNeedingRefills)
                            {
                                try
                                {

                                    var lastOrder = db.PT_PatientOrders
                                        .Where(o => o.PatientTreamentId == item.Treatment.PatientTreatmentId &&
                                                   o.IsActive == true)
                                        .OrderByDescending(o => o.CreatedDate)
                                        .FirstOrDefault();

                                    if (lastOrder != null && lastOrder.CreatedDate.HasValue)
                                    {
                                        var daysSinceLastOrder = (DateTime.UtcNow - lastOrder.CreatedDate.Value).TotalDays;

                                        int shippingFrequencyDays = 30;
                                        if (!string.IsNullOrWhiteSpace(item.Drug.ShippingFrequency))
                                        {

                                            if (int.TryParse(item.Drug.ShippingFrequency, out int parsedFrequency))
                                            {
                                                shippingFrequencyDays = parsedFrequency;
                                            }
                                        }

                                        if (daysSinceLastOrder >= shippingFrequencyDays * 0.8 && item.Treatment.PatientId.HasValue)
                                        {
                                            await notificationService.SendRefillNotificationAsync(
                                                patientId: item.Treatment.PatientId.Value,
                                                treatmentId: item.Treatment.PatientTreatmentId,
                                                ct: stoppingToken
                                            );
                                        }
                                    }
                                }
                                catch (Exception refillEx)
                                {
                                    _logger.LogError(refillEx, $"Failed to send refill notification for treatment {item.Treatment.PatientTreatmentId}");
                                }
                            }
                        }
                        catch (Exception refillCheckEx)
                        {
                            _logger.LogError(refillCheckEx, "Error checking for refill notifications");
                        }

                                if (DateTime.UtcNow.Day == 1)
                                {
                                    try
                                    {
                                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                                        var activeFacilities = db.SYS_Facilities
                                            .Where(f => f.IsActive == true)
                                            .ToList();

                                        foreach (var facility in activeFacilities)
                                        {
                                            try
                                            {
                                                await notificationService.SendMonthlyAnalyticsAsync(
                                                    facilityId: facility.FacilityId,
                                                    ct: stoppingToken
                                                );
                                            }
                                            catch (Exception analyticsEx)
                                            {
                                                _logger.LogError(analyticsEx, $"Failed to send monthly analytics for facility {facility.FacilityId}");
                                            }
                                        }
                                    }
                                    catch (Exception analyticsCheckEx)
                                    {
                                        _logger.LogError(analyticsCheckEx, "Error sending monthly analytics");
                                    }
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error in monthly invoice generation: {ex.Message}. StackTrace: {ex.StackTrace}");
                    }
                }

                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                        var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepo>();

                        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

                        var invoicesToAutoPay = db.Sys_Invoices
                            .Where(x => x.FacilityId.HasValue &&
                                       x.InvoiceType == InvoiceType.ClinicToGlobal.ToString() &&
                                       x.Status == InvoiceStatus.Pending.ToString() &&
                                       x.IsActive == true &&
                                       x.CreatedDate.HasValue &&
                                       x.CreatedDate.Value < sevenDaysAgo)
                            .ToList();

                        if (invoicesToAutoPay.Count > 0)
                        {
                            _logger.LogInformation($"Found {invoicesToAutoPay.Count} facility invoices older than 7 days that are eligible for auto-payment");

                            int autoPaySuccessCount = 0;
                            int autoPayFailureCount = 0;

                            foreach (var invoiceToPay in invoicesToAutoPay)
                            {
                                try
                                {

                                    var currentInvoice = await db.Sys_Invoices
                                        .FirstOrDefaultAsync(x => x.InvoiceId == invoiceToPay.InvoiceId, stoppingToken);

                                    if (currentInvoice == null || currentInvoice.Status != InvoiceStatus.Pending.ToString())
                                    {
                                        _logger.LogInformation($"Invoice {invoiceToPay.InvoiceId} is no longer pending (Status: {currentInvoice?.Status ?? "Deleted"}), skipping auto-payment");
                                        continue;
                                    }

                                    if (currentInvoice.FacilityId.HasValue)
                                    {
                                        var facility = await db.SYS_Facilities
                                            .FirstOrDefaultAsync(f => f.FacilityId == currentInvoice.FacilityId.Value, stoppingToken);

                                        if (facility == null || facility.IsActive != true)
                                        {
                                            _logger.LogWarning($"Facility {currentInvoice.FacilityId} for invoice {invoiceToPay.InvoiceId} is not active or doesn't exist, skipping auto-payment");
                                            continue;
                                        }
                                    }

                                    _logger.LogInformation($"Attempting auto-payment for invoice {invoiceToPay.InvoiceId} (InvoiceNumber: {invoiceToPay.InvoiceNumber}, FacilityId: {invoiceToPay.FacilityId}, Amount: {invoiceToPay.Amount})");

                                    var autoPayResult = await invoiceRepo.AutoPayFacilityInvoice(invoiceToPay.InvoiceId);

                                    if (autoPayResult)
                                    {
                                        _logger.LogInformation($"✓ SUCCESS: Auto-payment completed for invoice {invoiceToPay.InvoiceId} (InvoiceNumber: {invoiceToPay.InvoiceNumber}, FacilityId: {invoiceToPay.FacilityId})");
                                        autoPaySuccessCount++;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"✗ FAILED: Auto-payment failed for invoice {invoiceToPay.InvoiceId} (InvoiceNumber: {invoiceToPay.InvoiceNumber}, FacilityId: {invoiceToPay.FacilityId}). Facility may not have a payment method set up.");
                                        autoPayFailureCount++;
                                    }
                                }
                                catch (Exception autoPayEx)
                                {
                                    _logger.LogError(autoPayEx, $"Error during auto-payment for invoice {invoiceToPay.InvoiceId}: {autoPayEx.Message}. StackTrace: {autoPayEx.StackTrace}");
                                    autoPayFailureCount++;
                                }
                            }

                            _logger.LogInformation($"Auto-payment process completed. Success: {autoPaySuccessCount}, Failed: {autoPayFailureCount}");
                        }
                    }
                }
                catch (Exception autoPayCheckEx)
                {
                    _logger.LogError(autoPayCheckEx, $"Error checking for invoices to auto-pay: {autoPayCheckEx.Message}. StackTrace: {autoPayCheckEx.StackTrace}");
                }

            await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
        }
    }
}
