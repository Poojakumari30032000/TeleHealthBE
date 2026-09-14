using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Models.Schedulers;
using Vitality.Models.CommonMethods;

public class IntakeReminderService : BackgroundService
{
    private const string LockName = "SCHEDULER:IntakeReminder";
    private const string UnreadMessageReminderNotificationType = "UnreadMessageReminderEmail";

    private static readonly TimeSpan UnreadMessageThreshold = TimeSpan.FromHours(24);
    private static readonly TimeSpan UnreadMessageReminderInterval = TimeSpan.FromHours(24);

    private readonly ILogger<IntakeReminderService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SchedulerSettings _settings;

    public IntakeReminderService(
        ILogger<IntakeReminderService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<SchedulerSettings> settings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _settings = settings?.Value ?? new SchedulerSettings();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        var intervalSeconds = Math.Max(15, _settings.IntakeReminderIntervalSeconds);
        var lookbackDays = Math.Max(1, _settings.IntakeReminderLookbackDays);

        _logger.LogInformation("IntakeReminderService started at {Time}", DateTime.Now);

        while (!stoppingToken.IsCancellationRequested)
        {

            string? lockConnString = null;
            try
            {
                using var lockScope = _scopeFactory.CreateScope();
                var lockDb = lockScope.ServiceProvider.GetRequiredService<MainContext>();
                lockConnString = lockDb.Database.GetDbConnection().ConnectionString;
            }
            catch (Exception lockCfgEx)
            {
                _logger.LogError(lockCfgEx, "IntakeReminderService: could not resolve DB connection string for distributed lock — skipping iteration.");
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                continue;
            }

            await using var distributedLock = await SchedulerDistributedLock.TryAcquireAsync(lockConnString, LockName, stoppingToken).ConfigureAwait(false);
            if (distributedLock is null)
            {
                _logger.LogInformation("IntakeReminderService: another instance holds the {LockName} lock — skipping this iteration.", LockName);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                continue;
            }

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    var nowUtcForFilter = DateTime.UtcNow;
                    var minStartDateUtc = nowUtcForFilter.Date.AddDays(-lookbackDays);
                    var maxStartDateUtc = nowUtcForFilter.Date.AddDays(lookbackDays);

                    var nowLocal = DateTime.Now;
                    var nowUtc = DateTime.UtcNow;

                    _logger.LogInformation("IntakeReminderService running at Local: {LocalTime}, UTC: {UtcTime}", nowLocal, nowUtc);

                    var reminderWindowStartLocal = nowLocal.AddMinutes(29.5);
                    var reminderWindowEndLocal = nowLocal.AddMinutes(30.5);

                    var appointmentsForReminder = await db.PT_PatientAppointmentSlots
                        .AsNoTracking()
                        .Where(a => (a.IsActive == true || a.IsActive == null) &&
                                   a.StartDate.HasValue &&
                                   a.StartDate >= minStartDateUtc &&
                                   a.StartDate <= maxStartDateUtc &&
                                   a.PatientTreatmentId.HasValue &&
                                   a.PatientId.HasValue)
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation("Found {Count} active appointments to check for reminders. Window: {WindowStart} to {WindowEnd}",
                        appointmentsForReminder.Count, reminderWindowStartLocal, reminderWindowEndLocal);

                    var appointmentsToRemind = new List<PT_PatientAppointmentSlot>();

                    foreach (var appointment in appointmentsForReminder)
                    {
                        try
                        {

                            var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);

                            var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);

                            var minutesUntilAppointment = (appointmentDateTimeLocal - nowLocal).TotalMinutes;

                            _logger.LogInformation(
                                "Checking Appointment {AppointmentId}: StartDate UTC={StartDateUtc}, StartTime={StartTime}, " +
                                "Appointment UTC={ApptUtc}, Appointment Local={ApptLocal}, " +
                                "Minutes Until={MinutesUntil}, In Window={InWindow}",
                                appointment.PatientAppointmentSlotId,
                                appointment.StartDate.Value,
                                appointment.StartTime,
                                appointmentDateTimeUtc,
                                appointmentDateTimeLocal,
                                minutesUntilAppointment,
                                minutesUntilAppointment >= 29.5 && minutesUntilAppointment <= 30.5);

                            if (minutesUntilAppointment >= 29.5 && minutesUntilAppointment <= 30.5)
                            {
                                appointmentsToRemind.Add(appointment);
                                _logger.LogInformation(
                                    "✓ Appointment {AppointmentId} is in reminder window! Appointment Local Time: {ApptLocalTime}, " +
                                    "Window: {WindowStart} - {WindowEnd}, Minutes Until: {MinutesUntil}",
                                    appointment.PatientAppointmentSlotId,
                                    appointmentDateTimeLocal,
                                    reminderWindowStartLocal,
                                    reminderWindowEndLocal,
                                    minutesUntilAppointment);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error calculating appointment datetime for appointment {AppointmentId}: {Error}",
                                appointment.PatientAppointmentSlotId, ex.Message);
                        }
                    }

                    _logger.LogInformation("Found {Count} appointments in reminder window (30 minutes before)", appointmentsToRemind.Count);

                    foreach (var appointment in appointmentsToRemind)
                    {
                        try
                        {

                            var intakeFormFilled = await db.PT_PatientTreatmentInTakeForms
                                .AsNoTracking()
                                .AnyAsync(f => f.PatientTreatmentId == appointment.PatientTreatmentId.Value &&
                                             !string.IsNullOrWhiteSpace(f.Answer), stoppingToken);

                            if (!intakeFormFilled)
                            {
                                await notificationService.SendIntakeReminderAsync(appointment.PatientAppointmentSlotId, stoppingToken);
                                _logger.LogInformation("Intake reminder sent for appointment {AppointmentId}", appointment.PatientAppointmentSlotId);
                            }
                            else
                            {
                                _logger.LogInformation("Intake form already filled for appointment {AppointmentId}, skipping reminder", appointment.PatientAppointmentSlotId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing intake reminder for appointment {AppointmentId}", appointment.PatientAppointmentSlotId);
                        }
                    }

                    var cancellationWindowStartLocal = nowLocal.AddMinutes(4.5);
                    var cancellationWindowEndLocal = nowLocal.AddMinutes(5.5);

                    var appointmentsForCancellation = await db.PT_PatientAppointmentSlots
                        .AsNoTracking()
                        .Where(a => (a.IsActive == true || a.IsActive == null) &&
                                   a.StartDate.HasValue &&
                                   a.StartDate >= minStartDateUtc &&
                                   a.StartDate <= maxStartDateUtc &&
                                   a.PatientTreatmentId.HasValue &&
                                   a.PatientId.HasValue)
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation("Found {Count} active appointments to check for cancellation. Window: {WindowStart} to {WindowEnd}",
                        appointmentsForCancellation.Count, cancellationWindowStartLocal, cancellationWindowEndLocal);

                    var appointmentsToCancel = new List<PT_PatientAppointmentSlot>();

                    foreach (var appointment in appointmentsForCancellation)
                    {
                        try
                        {

                            var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);

                            var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);

                            if (appointmentDateTimeLocal >= cancellationWindowStartLocal &&
                                appointmentDateTimeLocal <= cancellationWindowEndLocal)
                            {
                                appointmentsToCancel.Add(appointment);
                                _logger.LogInformation(
                                    "Appointment {AppointmentId} is in cancellation window. Appointment Local Time: {ApptLocalTime}, Window: {WindowStart} - {WindowEnd}",
                                    appointment.PatientAppointmentSlotId,
                                    appointmentDateTimeLocal,
                                    cancellationWindowStartLocal,
                                    cancellationWindowEndLocal);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error calculating appointment datetime for cancellation check for appointment {AppointmentId}", appointment.PatientAppointmentSlotId);
                        }
                    }

                    _logger.LogInformation("Found {Count} appointments in cancellation window (5 minutes before)", appointmentsToCancel.Count);

                    foreach (var appointment in appointmentsToCancel)
                    {
                        try
                        {

                            var intakeFormFilled = await db.PT_PatientTreatmentInTakeForms
                                .AsNoTracking()
                                .AnyAsync(f => f.PatientTreatmentId == appointment.PatientTreatmentId.Value &&
                                             !string.IsNullOrWhiteSpace(f.Answer), stoppingToken);

                            if (!intakeFormFilled)
                            {

                                var cancelled = await notificationService.CancelAppointmentIfIntakeNotFilledAsync(
                                    appointment.PatientAppointmentSlotId,
                                    stoppingToken);

                                if (cancelled)
                                {
                                    _logger.LogInformation("Appointment {AppointmentId} cancelled due to missing intake form", appointment.PatientAppointmentSlotId);
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Intake form is filled for appointment {AppointmentId}, no cancellation needed", appointment.PatientAppointmentSlotId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing appointment cancellation check for appointment {AppointmentId}", appointment.PatientAppointmentSlotId);
                        }
                    }

                    await ProcessUnreadMessageRemindersAsync(db, notificationService, nowUtc, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in intake reminder service: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessUnreadMessageRemindersAsync(
        MainContext db,
        INotificationService notificationService,
        DateTime nowUtc,
        CancellationToken ct)
    {
        try
        {
            var cutoffUtc = nowUtc.Subtract(UnreadMessageThreshold);

            var patientUsers = await (
                from ud in db.SYS_UserDetails.AsNoTracking()
                join login in db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                where login.RoleId == (int)UserRole.Patient
                      && ud.IsActive == true
                      && ud.Status == "Active"
                      && !string.IsNullOrWhiteSpace(ud.Email)
                select new { ud.UserId, ud.Email }
            ).ToListAsync(ct);

            if (!patientUsers.Any())
            {
                return;
            }

            var patientUserIds = patientUsers.Select(x => x.UserId).Distinct().ToList();

            foreach (var patientUserId in patientUserIds)
            {
                var patient = patientUsers.FirstOrDefault(x => x.UserId == patientUserId);
                if (patient == null || string.IsNullOrWhiteSpace(patient.Email))
                {
                    continue;
                }

                var lastReminderSentUtc = await db.SYS_Notifications
                    .AsNoTracking()
                    .Where(n => n.NotificationType == UnreadMessageReminderNotificationType
                                && n.UserId == patientUserId
                                && n.CreatedDate.HasValue)
                    .OrderByDescending(n => n.CreatedDate)
                    .Select(n => n.CreatedDate)
                    .FirstOrDefaultAsync(ct);

                if (lastReminderSentUtc.HasValue &&
                    nowUtc.Subtract(lastReminderSentUtc.Value) < UnreadMessageReminderInterval)
                {
                    continue;
                }

                var isFirstReminder = !lastReminderSentUtc.HasValue;

                var patientChannelIds = await (
                    from p in db.SYS_ChatChannelParticipants.AsNoTracking()
                    join c in db.SYS_ChatChannels.AsNoTracking() on p.ChannelId equals c.ChannelId
                    where p.UserId == patientUserId
                          && p.IsActive == true
                          && p.ChannelId.HasValue
                          && c.IsActive == true
                          && c.ChannelType == "Treatment"
                    select p.ChannelId!.Value
                )
                .Distinct()
                .ToListAsync(ct);

                var channelUnread = new List<(string? Content, DateTime CreatedDate)>();
                if (patientChannelIds.Any())
                {
                    var regularChannelUnread = await db.SYS_Chats
                        .AsNoTracking()
                        .Where(m => m.IsActive == true
                                    && m.ChannelId.HasValue
                                    && patientChannelIds.Contains(m.ChannelId.Value)
                                    && m.SenderId != patientUserId
                                    && (isFirstReminder ? m.CreatedDate <= cutoffUtc : true)
                                    && (m.MessageType == "Channel" || m.MessageType == null)
                                    && !db.SYS_ChatReadReceipts.Any(r =>
                                        r.ChatId == m.Id
                                        && r.UserId == patientUserId
                                        && (r.IsActive == true || r.IsActive == null)))
                        .Select(m => new { m.Content, m.CreatedDate })
                        .ToListAsync(ct);

                    channelUnread.AddRange(regularChannelUnread.Select(x => (x.Content, x.CreatedDate)));

                    var individualChannelUnread = await db.SYS_Chats
                        .AsNoTracking()
                        .Where(m => m.IsActive == true
                                    && m.ChannelId.HasValue
                                    && patientChannelIds.Contains(m.ChannelId.Value)
                                    && m.MessageType == "IndividualInChannel"
                                    && m.IndividualReceiverId == patientUserId
                                    && m.SenderId != patientUserId
                                    && m.IsRead != true
                                    && (isFirstReminder ? m.CreatedDate <= cutoffUtc : true))
                        .Select(m => new { m.Content, m.CreatedDate })
                        .ToListAsync(ct);

                    channelUnread.AddRange(individualChannelUnread.Select(x => (x.Content, x.CreatedDate)));
                }

                var totalUnreadCount = channelUnread.Count;
                if (totalUnreadCount == 0)
                {
                    continue;
                }

                var latestUnreadMessage = channelUnread
                    .OrderByDescending(x => x.CreatedDate)
                    .Select(x => x.Content)
                    .FirstOrDefault();

                var preview = $"{totalUnreadCount} unread message(s) are waiting in your chat inbox.";
                if (!string.IsNullOrWhiteSpace(latestUnreadMessage))
                {
                    var latestPreview = latestUnreadMessage.Length > 100
                        ? latestUnreadMessage.Substring(0, 100) + "..."
                        : latestUnreadMessage;
                    preview = $"You still have unread messages. Latest: {latestPreview}";
                }

                var emailSent = await notificationService.SendUnreadMessageReminderEmailAsync(
                    recipientEmail: patient.Email!,
                    messagePreview: preview,
                    ct: ct);

                if (!emailSent)
                {
                    _logger.LogWarning("Unread reminder email failed for patient user {UserId}; will retry next run.", patientUserId);
                    continue;
                }

                db.SYS_Notifications.Add(new SYS_Notification
                {
                    UserId = patientUserId,
                    NotificationType = UnreadMessageReminderNotificationType,
                    IsRead = true,
                    Description = $"Unread message reminder email sent. Count={totalUnreadCount}",
                    CreatedDate = nowUtc
                });
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing unread message reminders in IntakeReminderService.");
        }
    }
}
