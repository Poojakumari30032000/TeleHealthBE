using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services.Audit;

namespace Vitality.Models.Schedulers
{

    public class ReconciliationService : BackgroundService
    {
        private const string LockName = "SCHEDULER:Reconciliation";

        private readonly ILogger<ReconciliationService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SchedulerSettings _settings;

        public ReconciliationService(
            ILogger<ReconciliationService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<SchedulerSettings> settings)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _settings = settings?.Value ?? new SchedulerSettings();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (TaskCanceledException) { return; }

            var intervalHours = Math.Max(1, _settings.ReconciliationIntervalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                    var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

                    var connectionString = db.Database.GetDbConnection().ConnectionString;
                    await using var distributedLock = await SchedulerDistributedLock.TryAcquireAsync(connectionString, LockName, stoppingToken).ConfigureAwait(false);
                    if (distributedLock is null)
                    {
                        _logger.LogInformation("ReconciliationService: another instance holds the {LockName} lock — skipping this iteration.", LockName);
                    }
                    else
                    {
                        await ScanAsync(db, audit, stoppingToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReconciliationService iteration failed.");
                }

                try { await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task ScanAsync(MainContext db, IAuditService audit, CancellationToken ct)
        {
            var nowUtc = DateTime.UtcNow;
            var cutoff = nowUtc.AddHours(-24);
            int findings = 0;

            try
            {
                var stalePending = await db.Sys_Invoices
                    .AsNoTracking()
                    .Where(i =>
                        i.Status == InvoiceStatus.Pending.ToString() &&
                        (i.IsActive == true || i.IsActive == null) &&
                        i.CreatedDate.HasValue && i.CreatedDate.Value < cutoff &&
                        !db.Sys_InvoicePayments.Any(p => p.InvoiceId == i.InvoiceId && p.IsActive == true && p.PaymentStatus == "Completed"))
                    .Select(i => new { i.InvoiceId, i.InvoiceNumber, i.Amount, i.FacilityId, i.PatientId, i.CreatedDate })
                    .Take(1000)
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var row in stalePending)
                {
                    findings++;
                    _logger.LogWarning(
                        "Reconciliation finding [StalePendingInvoice]: InvoiceId={InvoiceId} InvoiceNumber={Number} Amount={Amount} CreatedDate={Created} FacilityId={Facility} PatientId={Patient}",
                        row.InvoiceId, row.InvoiceNumber, row.Amount, row.CreatedDate, row.FacilityId, row.PatientId);
                    try
                    {
                        await audit.LogEntityChangeAsync(
                            action: "ReconciliationFinding",
                            entityType: "Sys_Invoice",
                            entityId: row.InvoiceId,
                            oldValues: null,
                            newValues: new { Finding = "StalePendingInvoice", row.Amount, row.CreatedDate },
                            userId: 1,
                            patientId: row.PatientId,
                            facilityId: row.FacilityId.HasValue ? (long?)row.FacilityId.Value : null,
                            description: $"Stale Pending invoice {row.InvoiceNumber} (>24h, no payment).",
                            module: "Reconciliation");
                    }
                    catch (Exception auditEx)
                    {
                        _logger.LogWarning(auditEx, "Failed to write reconciliation audit row for invoice {InvoiceId}.", row.InvoiceId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconciliation: stale-Pending-invoice scan failed.");
            }

            try
            {
                var orphans = await db.PT_PatientTreatments
                    .AsNoTracking()
                    .Where(t =>
                        t.IsRecurring == true &&
                        t.NextRecurringPaymentDate == null &&
                        (t.Status != "Completed" && t.Status != "Cancelled") &&
                        (t.IsActive == true || t.IsActive == null))
                    .Select(t => new { t.PatientTreatmentId, t.PatientId, t.FacilityId, t.RecurringDurationMonths })
                    .Take(1000)
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var row in orphans)
                {
                    findings++;
                    _logger.LogWarning(
                        "Reconciliation finding [RecurringWithoutNextDate]: PatientTreatmentId={Tid} PatientId={Pid} FacilityId={Fid} DurationMonths={Months}",
                        row.PatientTreatmentId, row.PatientId, row.FacilityId, row.RecurringDurationMonths);
                    try
                    {
                        await audit.LogEntityChangeAsync(
                            action: "ReconciliationFinding",
                            entityType: "PT_PatientTreatment",
                            entityId: row.PatientTreatmentId,
                            oldValues: null,
                            newValues: new { Finding = "RecurringWithoutNextDate", row.RecurringDurationMonths },
                            userId: 1,
                            patientId: row.PatientId,
                            facilityId: row.FacilityId.HasValue ? (long?)row.FacilityId.Value : null,
                            description: $"Treatment {row.PatientTreatmentId} has IsRecurring=true but no NextRecurringPaymentDate — scheduler cannot process.",
                            module: "Reconciliation");
                    }
                    catch (Exception auditEx)
                    {
                        _logger.LogWarning(auditEx, "Failed to write reconciliation audit row for treatment {Tid}.", row.PatientTreatmentId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconciliation: orphan-recurring scan failed.");
            }

            try
            {
                var maxRetries = Math.Max(1, _settings.RecurringMaxRetries);
                var overdue = await db.PT_PatientTreatments
                    .AsNoTracking()
                    .Where(t =>
                        t.IsRecurring == true &&
                        t.RetryCount.HasValue && t.RetryCount.Value >= maxRetries &&
                        (t.Status != "Completed" && t.Status != "Cancelled") &&
                        (t.IsActive == true || t.IsActive == null))
                    .Select(t => new { t.PatientTreatmentId, t.PatientId, t.FacilityId, t.RetryCount, t.LastFailureAt, t.LastFailureMessage })
                    .Take(1000)
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var row in overdue)
                {
                    findings++;
                    _logger.LogWarning(
                        "Reconciliation finding [StuckOverMaxRetries]: PatientTreatmentId={Tid} RetryCount={Retries} LastFailureAt={At} LastFailure={Msg}",
                        row.PatientTreatmentId, row.RetryCount, row.LastFailureAt, row.LastFailureMessage);
                    try
                    {
                        await audit.LogEntityChangeAsync(
                            action: "ReconciliationFinding",
                            entityType: "PT_PatientTreatment",
                            entityId: row.PatientTreatmentId,
                            oldValues: null,
                            newValues: new { Finding = "StuckOverMaxRetries", row.RetryCount, row.LastFailureAt },
                            userId: 1,
                            patientId: row.PatientId,
                            facilityId: row.FacilityId.HasValue ? (long?)row.FacilityId.Value : null,
                            description: $"Treatment {row.PatientTreatmentId} has RetryCount={row.RetryCount} (>= max {maxRetries}) but IsRecurring is still true.",
                            module: "Reconciliation");
                    }
                    catch (Exception auditEx)
                    {
                        _logger.LogWarning(auditEx, "Failed to write reconciliation audit row for treatment {Tid}.", row.PatientTreatmentId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconciliation: stuck-over-max-retries scan failed.");
            }

            if (findings == 0)
                _logger.LogInformation("ReconciliationService: scan complete, no inconsistencies found.");
            else
                _logger.LogWarning("ReconciliationService: scan complete, {Findings} inconsistencies surfaced (see audit log + warnings above).", findings);
        }
    }
}
