using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitality.Helper;
using Vitality.Models.CommonMethods;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class IntakeReminderDiagnosticController : ControllerBase
    {
        private readonly MainContext _db;
        private readonly ILogger<IntakeReminderDiagnosticController> _logger;

        public IntakeReminderDiagnosticController(
            MainContext db,
            ILogger<IntakeReminderDiagnosticController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        [Route("CheckAppointment/{appointmentId}")]
        public async Task<IActionResult> CheckAppointment(long appointmentId)
        {
            try
            {
                var appointment = await _db.PT_PatientAppointmentSlots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId);

                if (appointment == null)
                {
                    return NotFound(new { Success = false, Message = $"Appointment {appointmentId} not found" });
                }

                var nowLocal = DateTime.Now;
                var nowUtc = DateTime.UtcNow;

                var appointmentDateTimeUtc = appointment.StartDate.HasValue
                    ? appointment.StartDate.Value.Date.Add(appointment.StartTime)
                    : (DateTime?)null;

                var appointmentDateTimeLocal = appointmentDateTimeUtc.HasValue
                    ? CommonMethods.ToLocalTime(appointmentDateTimeUtc.Value)
                    : (DateTime?)null;

                var intakeFormFilled = appointment.PatientTreatmentId.HasValue
                    ? await _db.PT_PatientTreatmentInTakeForms
                        .AsNoTracking()
                        .AnyAsync(f => f.PatientTreatmentId == appointment.PatientTreatmentId.Value &&
                                     !string.IsNullOrWhiteSpace(f.Answer))
                    : false;

                var reminderWindowStart = nowLocal.AddMinutes(29.5);
                var reminderWindowEnd = nowLocal.AddMinutes(30.5);
                var cancellationWindowStart = nowLocal.AddMinutes(4.5);
                var cancellationWindowEnd = nowLocal.AddMinutes(5.5);

                var minutesUntilAppointment = appointmentDateTimeLocal.HasValue
                    ? (appointmentDateTimeLocal.Value - nowLocal).TotalMinutes
                    : (double?)null;

                var isInReminderWindow = appointmentDateTimeLocal.HasValue && minutesUntilAppointment.HasValue
                    ? minutesUntilAppointment.Value >= 29.5 && minutesUntilAppointment.Value <= 30.5
                    : false;

                var isInCancellationWindow = appointmentDateTimeLocal.HasValue && minutesUntilAppointment.HasValue
                    ? minutesUntilAppointment.Value >= 4.5 && minutesUntilAppointment.Value <= 5.5
                    : false;

                var patient = appointment.PatientId.HasValue
                    ? await _db.PT_Patients
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId.Value)
                    : null;

                return Ok(new
                {
                    Success = true,
                    Appointment = new
                    {
                        AppointmentId = appointment.PatientAppointmentSlotId,
                        PatientId = appointment.PatientId,
                        PatientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown",
                        PatientEmail = patient?.Email,
                        TreatmentId = appointment.PatientTreatmentId,
                        IsActive = appointment.IsActive,
                        Status = appointment.Status,
                        StartDateUtc = appointment.StartDate,
                        StartTime = appointment.StartTime.ToString(),
                        EndTime = appointment.EndTime.ToString(),
                    },
                    DateTimeCalculations = new
                    {
                        CurrentLocalTime = nowLocal,
                        CurrentUtcTime = nowUtc,
                        AppointmentDateTimeUtc = appointmentDateTimeUtc,
                        AppointmentDateTimeLocal = appointmentDateTimeLocal,
                        MinutesUntilAppointment = minutesUntilAppointment,
                    },
                    Windows = new
                    {
                        ReminderWindow = new
                        {
                            Start = reminderWindowStart,
                            End = reminderWindowEnd,
                            IsInWindow = isInReminderWindow
                        },
                        CancellationWindow = new
                        {
                            Start = cancellationWindowStart,
                            End = cancellationWindowEnd,
                            IsInWindow = isInCancellationWindow
                        }
                    },
                    IntakeForm = new
                    {
                        Filled = intakeFormFilled,
                        TreatmentId = appointment.PatientTreatmentId
                    },
                    Analysis = new
                    {
                        WillTriggerReminder = isInReminderWindow && !intakeFormFilled && (appointment.IsActive == true || appointment.IsActive == null),
                        WillTriggerCancellation = isInCancellationWindow && !intakeFormFilled && (appointment.IsActive == true || appointment.IsActive == null),
                        Issues = new List<string>
                        {
                            appointment.IsActive == false ? "Appointment is not active" : null,
                            !appointment.StartDate.HasValue ? "StartDate is missing" : null,
                            !appointment.PatientTreatmentId.HasValue ? "PatientTreatmentId is missing" : null,
                            !appointment.PatientId.HasValue ? "PatientId is missing" : null,
                            intakeFormFilled ? "Intake form is already filled" : null,
                            minutesUntilAppointment.HasValue && minutesUntilAppointment.Value < 0 ? "Appointment is in the past" : null,
                            minutesUntilAppointment.HasValue && minutesUntilAppointment.Value > 30.5 ? $"Appointment is more than 30.5 minutes away ({minutesUntilAppointment.Value:F2} minutes)" : null,
                            minutesUntilAppointment.HasValue && minutesUntilAppointment.Value < 29.5 && minutesUntilAppointment.Value > 5.5 ? $"Appointment is between 5.5 and 29.5 minutes away ({minutesUntilAppointment.Value:F2} minutes) - outside both windows" : null,
                        }.Where(i => i != null).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking appointment {AppointmentId}", appointmentId);
                return StatusCode(500, new { Success = false, Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        [Route("GetAllAppointments")]
        public async Task<IActionResult> GetAllAppointments()
        {
            try
            {
                var nowLocal = DateTime.Now;
                var appointments = await _db.PT_PatientAppointmentSlots
                    .AsNoTracking()
                    .Where(a => a.StartDate.HasValue)
                    .OrderBy(a => a.StartDate)
                    .ThenBy(a => a.StartTime)
                    .Take(50)
                    .ToListAsync();

                var results = new List<object>();

                foreach (var appointment in appointments)
                {
                    var appointmentDateTimeUtc = appointment.StartDate.Value.Date.Add(appointment.StartTime);
                    var appointmentDateTimeLocal = CommonMethods.ToLocalTime(appointmentDateTimeUtc);
                    var minutesUntil = (appointmentDateTimeLocal - nowLocal).TotalMinutes;

                    results.Add(new
                    {
                        AppointmentId = appointment.PatientAppointmentSlotId,
                        StartDateUtc = appointment.StartDate,
                        StartTime = appointment.StartTime.ToString(),
                        AppointmentLocal = appointmentDateTimeLocal,
                        MinutesUntil = minutesUntil,
                        IsActive = appointment.IsActive,
                        HasTreatmentId = appointment.PatientTreatmentId.HasValue,
                        HasPatientId = appointment.PatientId.HasValue
                    });
                }

                return Ok(new
                {
                    Success = true,
                    CurrentLocalTime = nowLocal,
                    TotalAppointments = appointments.Count,
                    Appointments = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
    }
}
