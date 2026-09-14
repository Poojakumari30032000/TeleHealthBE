using AutoMapper;
using Dapper;
using DudeMeds.Models.DTOs.PatientAppointments;
using DudeMeds.Models.DTOs.PatientTreatments;
using DudeMeds.Models.DTOs.ProviderSchedules;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.PatientAppointments;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services.Audit;
using Vitality.Models.CommonMethods;

namespace DudeMeds.Models.Repos.Services
{
    public class PatientAppointmentsRepo : BaseRepo, IPatientAppointmentsRepo
    {
        private readonly IMapper _mapper;
        private readonly IAppointmentZoomService _appointmentZoom;
        private readonly IAuditService _auditService;
        public PatientAppointmentsRepo(IMapper mapper, IAppointmentZoomService appointmentZoom, IAuditService auditService)
        {
            _mapper = mapper;
            _appointmentZoom = appointmentZoom;
            _auditService = auditService;
        }

        public List<GetAllPatientAppointmentsByMonthResponseDTO> GetAllPatientAppointmentsByMonth(GetAllPatientAppointmentsByMonthRequestDTO request)
        {
            DynamicParameters param = new DynamicParameters();

            param.Add("@ScheduleMonth", request.ScheduledMonth);
            param.Add("@ScheduleYear", request.ScheduledYear);
            param.Add("@FacilityId", request.FacilityId);
            param.Add("@PatientId", request.PatientId);
            param.Add("@ProviderId", request.ProviderId);

            var data = ReturnJson<GetAllPatientAppointmentsByMonthResponseDTO>("[dbo].[sprocGetAllPatientAppointmentsByMonth]", param).ToList();
            return data;
        }

        public List<GetAllPatientAppointmentsByDaysResponseDTO> GetAllPatientAppointmentsByDays(GetAllPatientAppointmentsByDaysRequestDTO request)
        {
            var startOfDay = request.StartDate.Date;
            var endExclusive = request.EndDate.Date.AddDays(1);

            var statusFilter = request.Status?.Trim();
            var q =
                from a in _db.PT_PatientAppointmentSlots.AsNoTracking()
                where a.IsActive == true
                   && a.StartDate >= startOfDay
                   && a.StartDate < endExclusive
                   && (!request.FacilityId.HasValue || a.FacilityId == request.FacilityId.Value)
                   && (!request.ProviderId.HasValue || a.ProviderId == request.ProviderId.Value)
                   && (!request.PatientId.HasValue || a.PatientId == request.PatientId.Value)
                   && (string.IsNullOrEmpty(statusFilter)
                       || ((statusFilter.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                            || statusFilter.Equals("Scheduled", StringComparison.OrdinalIgnoreCase))
                           ? (a.Status == "Scheduled" || a.Status == "Pending" || a.Status == "Confirmed")
                           : (statusFilter.Equals("Missed", StringComparison.OrdinalIgnoreCase)
                               ? (a.Status == "Missed" || a.Status == "Cancelled" || a.Status == "Canceled" || a.Status == "Declined")
                               : (statusFilter.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                                   ? (a.Status == "Completed" || a.Status == "Done")
                                   : (a.Status != null && a.Status.Trim().ToLower() == statusFilter.ToLower())))))

                join u0 in _db.SYS_UserDetails.AsNoTracking()
                    on a.ProviderId equals u0.UserId into uj
                from u in uj.DefaultIfEmpty()

                join p0 in _db.PT_Patients.AsNoTracking()
                    on a.PatientId equals p0.PatientId into pj
                from p in pj.DefaultIfEmpty()

                join f0 in _db.SYS_Facilities.AsNoTracking()
                    on a.FacilityId equals f0.FacilityId into fj
                from f in fj.DefaultIfEmpty()

                join d0 in _db.PD_Drugs.AsNoTracking()
                    on a.ProductId equals d0.ProductId into dj
                from d in dj.DefaultIfEmpty()

                join c0 in _db.PD_Categories.AsNoTracking()
                    on d.CategoryId equals c0.CategoryId into cj
                from c in cj.DefaultIfEmpty()

                select new GetAllPatientAppointmentsByDaysResponseDTO
                {
                    PatientAppointmentSlotId = a.PatientAppointmentSlotId,
                    FacilityId = a.FacilityId,
                    ClinicName = f != null ? f.TitleLong : null,
                    ProviderScheduledSlotId = a.ProviderScheduledSlotId,
                    Title = a.Title,
                    ProviderId = a.ProviderId,
                    ProviderName = (((u.FirstName ?? string.Empty).Trim() + " " + (u.LastName ?? string.Empty).Trim()).Trim()),
                    PatientId = a.PatientId,
                    PatientName = (((p.FirstName ?? string.Empty).Trim() + " " + (p.LastName ?? string.Empty).Trim()).Trim()),
                    StartDate = a.StartDate,
                    StartTime = a.StartTime.ToString(),
                    EndTime = a.EndTime.ToString(),
                    Duration = a.Duration,
                    Status = a.Status,
                    CategoryId = d.CategoryId,
                    CategoryName = c.CategoryName,

                };

            var results = q.OrderBy(x => x.StartDate)
                           .ThenBy(x => x.PatientAppointmentSlotId)
                           .ToList();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                results = results.Where(x => x.Status != null && string.Equals(x.Status.Trim(), statusFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            int? offset = request.ClientTimezoneOffsetMinutes;
            foreach (var item in results)
            {
                item.Status = NormalizeAppointmentStatus(item.Status);
                if (!item.StartDate.HasValue) continue;
                if (offset.HasValue && !string.IsNullOrEmpty(item.StartTime) && !string.IsNullOrEmpty(item.EndTime)
                    && TimeSpan.TryParse(item.StartTime, out var startTs) && TimeSpan.TryParse(item.EndTime, out var endTs))
                {
                    var utcStart = item.StartDate.Value.Date.Add(startTs);
                    var utcEnd = item.StartDate.Value.Date.Add(endTs);
                    var localStart = utcStart.AddMinutes(offset.Value);
                    var localEnd = utcEnd.AddMinutes(offset.Value);
                    item.StartDate = localStart;
                    item.StartTime = localStart.ToString("HH:mm:ss");
                    item.EndTime = localEnd.ToString("HH:mm:ss");
                }
                else
                    item.StartDate = CommonMethods.UtcToClientLocal(item.StartDate, offset);
            }

            return results;
        }

        public List<GetAllPatientAppointmentsResponseDTO> GetAllPatientAppointments(GetAllPatientAppointmentsRequestDTO request, out int totalPatientAppointmentCount)
        {
            DynamicParameters param = new DynamicParameters();

            param.Add("@FacilityId", request.FacilityId);
            param.Add("@ProviderId", request.ProviderId);
            param.Add("@Title", request.Title);
            param.Add("@Status", request.Status);
            param.Add("@StartDate", request.StartDate);
            param.Add("@EndDate", request.EndDate);
            param.Add("@PageSize", request.PageSize);
            param.Add("@PageNumber", request.PageNumber);
            param.Add("@TotalPatientAppointmentCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var data = ReturnJson<GetAllPatientAppointmentsResponseDTO>("[dbo].[sprocGetAllPatientAppointments]", param).ToList();
            totalPatientAppointmentCount = param.Get<int>("@TotalPatientAppointmentCount");
            return data;
        }

        public GetPatientAppointmentByIdResponseDTO GetPatientAppointmentById(long PatientAppointmentSlotId)
        {
            var response = new GetPatientAppointmentByIdResponseDTO();
            var appointmentSlot = _db.PT_PatientAppointmentSlots
                                     .Where(x => x.IsActive == true && x.PatientAppointmentSlotId == PatientAppointmentSlotId)
                                     .FirstOrDefault();
            response = _mapper.Map<GetPatientAppointmentByIdResponseDTO>(appointmentSlot);
            return response;
        }

        public async Task<GetPatientAppointmentZoomUrlResponseDTO> GetPatientAppointmentZoomUrlAsync(long PatientAppointmentSlotId, CancellationToken ct = default)
        {
            var response = new GetPatientAppointmentZoomUrlResponseDTO
            {
                PatientAppointmentSlotId = PatientAppointmentSlotId,
                HasZoomMeeting = false
            };

            var appointmentSlot = await _db.PT_PatientAppointmentSlots
                                     .AsNoTracking()
                                     .Where(x => x.IsActive == true && x.PatientAppointmentSlotId == PatientAppointmentSlotId)
                                     .FirstOrDefaultAsync(ct);

            if (appointmentSlot == null)
            {
                response.Message = "Appointment not found or is inactive.";
                return response;
            }

            if (!string.IsNullOrWhiteSpace(appointmentSlot.ZoomJoinUrl))
            {
                response.HasZoomMeeting = true;
                response.ZoomJoinUrl = appointmentSlot.ZoomJoinUrl;
                response.ZoomPassword = appointmentSlot.ZoomPassword;
                response.ZoomMeetingId = appointmentSlot.ZoomMeetingId;
                response.ZoomUUID = appointmentSlot.ZoomUUID;
                response.ZoomStatus = appointmentSlot.ZoomStatus;
                response.ZoomHostEmail = appointmentSlot.ZoomHostEmail;
                response.ZoomCreatedAt = appointmentSlot.ZoomCreatedAt;
                response.Message = "Zoom meeting details retrieved successfully.";
            }
            else
            {
                response.Message = "No Zoom meeting found for this appointment. The meeting may not have been created yet or there was an error during creation.";
                response.ZoomStatus = appointmentSlot.ZoomStatus;
            }

            return response;
        }

        public async Task<long> SavePatientAppointmentAsync(
            SavePatientAppointmentRequestDTO request,
            long userId,
            long? providerScheduledSlotId,
            long? patientId,
            long? productId,
            long? patientTreatmentId,
            CancellationToken ct = default)
        {
            try
            {
                var guid = Guid.NewGuid();
                var appointmentSlot = new PT_PatientAppointmentSlot();

                if (providerScheduledSlotId != 0)
                {

                    using var bookingTx = await _db.Database.BeginTransactionAsync(ct);

                    var slot = await _db.UR_ProviderScheduledSlots
                        .FirstOrDefaultAsync(x => x.ProviderScheduledSlotId == providerScheduledSlotId, ct);

                    if (slot != null)
                    {

                        var statusTaken = !string.IsNullOrEmpty(slot.Status) && slot.Status != "Available";
                        if (statusTaken || await IsSlotAlreadyBookedAsync(slot.ProviderScheduledSlotId, ct))
                            throw new InvalidOperationException("Slot is no longer available.");

                        var patientFacilityId = await _db.PT_Patients
                            .Where(x => x.PatientId == patientId)
                            .Select(x => x.FacilityId)
                            .FirstOrDefaultAsync(ct);

                        appointmentSlot.PatientAppointmentSlotId = 0;
                        appointmentSlot.FacilityId = PreferFacility(request.FacilityId, patientFacilityId, slot.FacilityId);
                        appointmentSlot.PatientTreatmentId = patientTreatmentId;
                        appointmentSlot.ProductId = productId;
                        appointmentSlot.Title = slot.Title;
                        appointmentSlot.PatientId = patientId;
                        appointmentSlot.ProviderId = slot.ProviderId;
                        appointmentSlot.StartDate = slot.SlotDate;
                        appointmentSlot.StartTime = slot.StartTime;
                        appointmentSlot.EndTime = slot.EndTime;
                        appointmentSlot.Duration = slot.Duration;
                        appointmentSlot.ProviderScheduledSlotId = slot.ProviderScheduledSlotId;
                        appointmentSlot.IsActive = true;
                        appointmentSlot.Status = "Scheduled";
                        appointmentSlot.CreatedDate = DateTime.UtcNow;
                        appointmentSlot.CreatedBy = userId;
                        appointmentSlot.Guid = guid.ToString();

                        await _db.PT_PatientAppointmentSlots.AddAsync(appointmentSlot, ct);

                        slot.Status = "Booked";
                        slot.ModifiedBy = userId;
                        slot.ModifiedDate = DateTime.UtcNow;

                        await _db.SaveChangesAsync(ct);
                        await bookingTx.CommitAsync(ct);

                        try
                        {
                            await _appointmentZoom.EnsureZoomMeetingForAppointmentAsync(appointmentSlot.PatientAppointmentSlotId, ct);
                        }
                        catch
                        {
                            appointmentSlot.ZoomStatus = "Error";
                            await _db.SaveChangesAsync(ct);
                        }

                        var notification = new SYS_Notification
                        {
                            FacilityId = patientFacilityId,
                            NotificationType = "Appointment",
                            IsRead = false,
                            Description = "A New Appointment Has Been Generated",
                            CreatedDate = DateTime.UtcNow,
                            PatientId = appointmentSlot.PatientId
                        };
                        await _db.SYS_Notifications.AddAsync(notification, ct);
                        await _db.SaveChangesAsync(ct);

                        return appointmentSlot.PatientAppointmentSlotId;
                    }
                }
                else
                {

                    if (request.PatientAppointmentId == 0)
                    {
                        if (DateTime.TryParseExact(request.StartTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStartTime) &&
                            DateTime.TryParseExact(request.EndTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEndTime))
                        {

                            using var manualTx = await _db.Database.BeginTransactionAsync(ct);

                            UR_ProviderScheduledSlot? targetSlot = null;
                            var manualSlotId = request.ProviderScheduledSlotId;
                            if (manualSlotId.HasValue && manualSlotId.Value > 0)
                            {
                                targetSlot = await _db.UR_ProviderScheduledSlots
                                    .FirstOrDefaultAsync(x => x.ProviderScheduledSlotId == manualSlotId.Value, ct);
                                if (targetSlot != null)
                                {
                                    var statusTaken = !string.IsNullOrEmpty(targetSlot.Status) && targetSlot.Status != "Available";
                                    if (statusTaken || await IsSlotAlreadyBookedAsync(targetSlot.ProviderScheduledSlotId, ct))
                                        throw new InvalidOperationException("Slot is no longer available.");
                                }
                            }

                            appointmentSlot.PatientAppointmentSlotId = 0;
                            appointmentSlot.FacilityId = request.FacilityId;
                            appointmentSlot.PatientTreatmentId = patientTreatmentId;
                            appointmentSlot.Title = request.Title;
                            appointmentSlot.ProductId = productId;
                            appointmentSlot.PatientId = request.PatientId;
                            appointmentSlot.ProviderId = request.ProviderId;
                            appointmentSlot.StartDate = request.StartDate;
                            appointmentSlot.StartTime = parsedStartTime.TimeOfDay;
                            appointmentSlot.EndTime = parsedEndTime.TimeOfDay;
                            appointmentSlot.Duration = request.Duration;
                            appointmentSlot.ProviderScheduledSlotId = request.ProviderScheduledSlotId;
                            appointmentSlot.IsActive = true;
                            appointmentSlot.Status = "Scheduled";
                            appointmentSlot.CreatedDate = DateTime.UtcNow;
                            appointmentSlot.CreatedBy = userId;
                            appointmentSlot.Guid = guid.ToString();

                            await _db.PT_PatientAppointmentSlots.AddAsync(appointmentSlot, ct);

                            if (targetSlot != null)
                            {
                                targetSlot.Status = "Booked";
                                targetSlot.ModifiedBy = userId;
                                targetSlot.ModifiedDate = DateTime.UtcNow;
                            }

                            await _db.SaveChangesAsync(ct);
                            await manualTx.CommitAsync(ct);

                            try
                            {
                                await _appointmentZoom.EnsureZoomMeetingForAppointmentAsync(appointmentSlot.PatientAppointmentSlotId, ct);
                            }
                            catch
                            {
                                appointmentSlot.ZoomStatus = "Error";
                                await _db.SaveChangesAsync(ct);
                            }
                        }
                    }
                    else
                    {

                        appointmentSlot = await _db.PT_PatientAppointmentSlots
                            .FirstOrDefaultAsync(x => x.PatientAppointmentSlotId == request.PatientAppointmentId, ct);

                        if (appointmentSlot != null)
                        {
                            var oldProviderId = appointmentSlot.ProviderId;

                            if (DateTime.TryParseExact(request.StartTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStartTime) &&
                                DateTime.TryParseExact(request.EndTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEndTime))
                            {
                                appointmentSlot.Title = request.Title;
                                appointmentSlot.PatientId = request.PatientId;
                                appointmentSlot.ProviderId = request.ProviderId;
                                appointmentSlot.StartDate = request.StartDate;
                                appointmentSlot.StartTime = parsedStartTime.TimeOfDay;
                                appointmentSlot.EndTime = parsedEndTime.TimeOfDay;
                                appointmentSlot.Duration = request.Duration;
                                appointmentSlot.ProviderScheduledSlotId = request.ProviderScheduledSlotId;
                                appointmentSlot.IsActive = true;
                                appointmentSlot.ModifiedDate = DateTime.UtcNow;
                                appointmentSlot.ModifiedBy = userId;

                                await _db.SaveChangesAsync(ct);

                                try
                                {
                                    if (oldProviderId != appointmentSlot.ProviderId && appointmentSlot.ProviderId.HasValue)
                                    {
                                        await _appointmentZoom.RehostZoomMeetingForAppointmentAsync(
                                            appointmentSlot.PatientAppointmentSlotId,
                                            appointmentSlot.ProviderId.Value,
                                            ct);
                                    }
                                    else
                                    {
                                        await _appointmentZoom.UpdateZoomMeetingForAppointmentAsync(
                                            appointmentSlot.PatientAppointmentSlotId,
                                            ct);
                                    }
                                }
                                catch
                                {
                                    appointmentSlot.ZoomStatus = "Error";
                                    await _db.SaveChangesAsync(ct);
                                }
                            }
                        }
                    }
                }

                return appointmentSlot.PatientAppointmentSlotId;
            }
            catch (DbUpdateException ex) when (CommonMethods.IsUniqueConstraintViolation(ex))
            {

                throw new InvalidOperationException("This slot has just been booked. Please choose another time.");
            }
            catch (InvalidOperationException)
            {

                throw;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<bool> IsSlotAlreadyBookedAsync(long providerScheduledSlotId, CancellationToken ct)
        {
            return await _db.PT_PatientAppointmentSlots.AnyAsync(a =>
                a.ProviderScheduledSlotId == providerScheduledSlotId
                && a.IsActive == true
                && (a.Status == null
                    || (a.Status != "Cancelled" && a.Status != "Canceled" && a.Status != "Declined")), ct);
        }

        public async Task<bool> DeletePatientAppointmentAsync(long patientAppointmentSlotId, long userId, CancellationToken ct = default)
        {
            try
            {

                var oldAppointment = await _db.PT_PatientAppointmentSlots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PatientAppointmentSlotId == patientAppointmentSlotId, ct);

                var appointmentSlot = await _db.PT_PatientAppointmentSlots
                    .FirstOrDefaultAsync(x => x.PatientAppointmentSlotId == patientAppointmentSlotId, ct);

                if (appointmentSlot == null) return false;

                appointmentSlot.IsActive = false;
                appointmentSlot.ModifiedBy = userId;
                appointmentSlot.ModifiedDate = DateTime.UtcNow;

                if (appointmentSlot.ProviderScheduledSlotId.HasValue)
                {
                    var slot = await _db.UR_ProviderScheduledSlots
                        .FirstOrDefaultAsync(x => x.ProviderScheduledSlotId == appointmentSlot.ProviderScheduledSlotId.Value, ct);
                    if (slot != null && slot.IsActive == true && slot.Status == "Booked")
                    {
                        slot.Status = "Available";
                        slot.ModifiedBy = userId;
                        slot.ModifiedDate = DateTime.UtcNow;
                    }
                }

                await _db.SaveChangesAsync(ct);

                await _auditService.LogEntityChangeAsync(
                    action: "Delete",
                    entityType: "PT_PatientAppointmentSlot",
                    entityId: patientAppointmentSlotId,
                    oldValues: oldAppointment,
                    userId: userId,
                    patientId: appointmentSlot.PatientId,
                    description: $"Appointment cancelled for Patient ID {appointmentSlot.PatientId}",
                    module: "Appointment"
                );

                try
                {
                    await _appointmentZoom.CancelZoomMeetingForAppointmentAsync(patientAppointmentSlotId, ct);
                }
                catch
                {
                    appointmentSlot.ZoomStatus = "Error";
                    await _db.SaveChangesAsync(ct);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public GetPatientAppointmentInfoResponseDTO GetPatientAppointmentInfo(long patientAppointmentSlotId, int? clientTimezoneOffsetMinutes = null)
        {
            static string? FormatTimeSafe(object? t)
            {
                if (t == null) return null;
                if (t is TimeSpan ts) return DateTime.Today.Add(ts).ToString("h:mm tt");
                if (t is DateTime dt) return dt.ToString("h:mm tt");
                var s = t.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (TimeSpan.TryParse(s, out var ts2)) return DateTime.Today.Add(ts2).ToString("h:mm tt");
                    if (DateTime.TryParse(s, out var dt2)) return dt2.ToString("h:mm tt");
                }
                return s;
            }

            var resp = new GetPatientAppointmentInfoResponseDTO();
            int? offset = clientTimezoneOffsetMinutes;

            var appt = _db.PT_PatientAppointmentSlots.AsNoTracking()
                          .FirstOrDefault(x => x.IsActive == true && x.PatientAppointmentSlotId == patientAppointmentSlotId);

            if (appt == null) return resp;

            var patient = _db.PT_Patients.AsNoTracking()
                            .FirstOrDefault(x => x.PatientId == appt.PatientId);

            if (patient != null)
            {
                resp.PatientId = patient.PatientId;
                resp.FirstName = patient.FirstName;
                resp.LastName = patient.LastName;
                resp.Email = patient.Email;
                resp.Address = patient.Address;
                resp.DOB = patient.DOB;
                resp.Gender = patient.Gender;
                resp.PhoneNumber = patient.Phone;
            }

            resp.PatientAppointmentId = appt.PatientAppointmentSlotId;
            if (appt.StartDate.HasValue)
            {
                var startUtc = appt.StartDate.Value.Date.Add(appt.StartTime);
                var endUtc = appt.StartDate.Value.Date.Add(appt.EndTime);
                var localStart = CommonMethods.UtcToClientLocal(startUtc, offset);
                var localEnd = CommonMethods.UtcToClientLocal(endUtc, offset);
                resp.StartDate = localStart;
                resp.StartTime = localStart.ToString("h:mm tt");
                resp.EndTime = localEnd.ToString("h:mm tt");
            }
            else
            {
                resp.StartDate = null;
                resp.StartTime = FormatTimeSafe(appt.StartTime);
                resp.EndTime = FormatTimeSafe(appt.EndTime);
            }
            resp.Duration = appt.Duration;
            resp.Status = NormalizeAppointmentStatus(appt.Status);

            resp.ProviderId = appt.ProviderId;
            resp.ProviderName = _db.SYS_UserDetails.AsNoTracking()
                                  .Where(x => x.UserId == appt.ProviderId)
                                  .Select(x => ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim())
                                  .FirstOrDefault();

            resp.ProductId = appt.ProductId;
            resp.ProductName = _db.PD_Bundles.AsNoTracking()
                               .Where(b => b.BundleId == appt.ProductId)
                               .Select(b => b.Name)
                               .FirstOrDefault();

            if (appt.PatientTreatmentId.HasValue)
            {
                var tId = appt.PatientTreatmentId.Value;

                var tmt = _db.PT_PatientTreatments.AsNoTracking()
                             .FirstOrDefault(t => t.PatientTreatmentId == tId);

                if (tmt != null)
                {
                    string? bundleName = null;
                    string? categoryName = null;
                    string? frequency = null;
                    decimal? price = null;

                    if (tmt.ProductId.HasValue)
                    {
                        var b = (from pb in _db.PD_Bundles.AsNoTracking()
                                 where pb.BundleId == tmt.ProductId.Value
                                 join c0 in _db.PD_Categories.AsNoTracking()
                                      on pb.CategoryId equals c0.CategoryId into cj
                                 from c in cj.DefaultIfEmpty()
                                 select new
                                 {
                                     BundleName = pb.Name,
                                     CategoryName = c != null ? c.CategoryName : null,
                                     Visits = pb.visits,
                                     Price = pb.Price
                                 })
                                .FirstOrDefault();

                        if (b != null)
                        {
                            bundleName = b.BundleName;
                            categoryName = b.CategoryName;
                            frequency = b.Visits.HasValue ? b.Visits.Value.ToString() : null;

                            if (tmt.FacilityId.HasValue)
                            {
                                var clinicPrice = _db.PD_FacilityBundlePrices
                                    .AsNoTracking()
                                    .Where(x => x.BundleId == tmt.ProductId.Value && x.FacilityId == tmt.FacilityId.Value)
                                    .Select(x => (decimal?)x.ClinicPrice)
                                    .FirstOrDefault();
                                price = clinicPrice ?? b.Price;
                            }
                            else
                            {
                                price = b.Price;
                            }
                        }
                    }

                    string? questionnaireName =
                        (from qp in _db.SYS_QuestionnairesInProducts.AsNoTracking()
                         join q in _db.SYS_Questionnaires.AsNoTracking()
                            on qp.QuestionnaireId equals q.QuestionnaireId
                         where qp.ProductId == tmt.ProductId
                         select q.QuestionnaireName)
                        .FirstOrDefault();

                    int prescriptions =
                        _db.PT_PatientPrescriptions.AsNoTracking()
                           .Count(x => x.PatientTreatmentId == tId && x.IsActive == true);

                    int orderCount =
                        _db.PT_PatientOrders.AsNoTracking()
                           .Count(o => o.PatientTreamentId == tId && o.IsActive == true);

                    resp.Treatment = new TreatmentInAppointmentDTO
                    {
                        PatientTreatmentId = tmt.PatientTreatmentId,
                        TreatmentGuid = tmt.Guid,
                        TreatmentStatus = tmt.TreatmentStatus,
                        ProductId = tmt.ProductId,
                        BundleName = bundleName,
                        ProviderScheduledSlotId = tmt.ProviderScheduledSlotId,
                        CreatedDate = CommonMethods.UtcToClientLocal(tmt.CreatedDate, offset),
                        ModifiedDate = CommonMethods.UtcToClientLocal(tmt.ModifiedDate, offset),
                        ExpiryDate = CommonMethods.UtcToClientLocal(tmt.ExpiryDate, offset),

                        Name = categoryName,
                        QuestionnaireName = questionnaireName,
                        Frequency = frequency,
                        Price = price,
                        NumberOfProducts = 1,
                        Prescriptions = prescriptions,
                        PrescriptionsCount = prescriptions,
                        OrderCount = orderCount,
                        StartDate = CommonMethods.UtcToClientLocal(tmt.CreatedDate, offset),
                        NextShippingDate = null,
                        NextPaymentDate = null,
                        Refills = null
                    };
                }
            }

            resp.ZoomJoinUrl = appt.ZoomJoinUrl;
            resp.ZoomPassword = appt.ZoomPassword;
            resp.ZoomMeetingId = appt.ZoomMeetingId;
            resp.ZoomStatus = appt.ZoomStatus;

            return resp;
        }

        public long SavePatientAppointment(SavePatientAppointmentRequestDTO request)
        {
            try
            {

                static long? PreferFacility(long? req, long? patient, long? slot) => req ?? patient ?? slot;

                Guid guid = Guid.NewGuid();

                var slot = _db.UR_ProviderScheduledSlots
                              .FirstOrDefault(x => x.ProviderScheduledSlotId == request.ProviderScheduledSlotId);

                if (slot == null) return 0;

                bool alreadyBooked = _db.PT_PatientAppointmentSlots.Any(a =>
                    a.ProviderScheduledSlotId == slot.ProviderScheduledSlotId &&
                    a.IsActive == true &&
                    (a.Status == null || !new[] { "Missed", "Completed", "Declined", "Cancelled" }.Contains(a.Status)));

                if (alreadyBooked) return 0;

                int visitsCount = 0;
                if (request.ProductId != null)
                {
                    var bundle = _db.PD_Bundles.FirstOrDefault(b => b.BundleId == request.ProductId);
                    visitsCount = (int)bundle.visits;
                }
                if (visitsCount <= 0) return 0;

                var patientFacilityId = _db.PT_Patients
                    .Where(x => x.PatientId == request.PatientId)
                    .Select(x => x.FacilityId)
                    .FirstOrDefault();

                var baseCreated = DateTime.UtcNow;
                long firstAppointmentId = 0;

                for (int i = 0; i < visitsCount; i++)
                {
                    var appointmentSlot = new PT_PatientAppointmentSlot
                    {
                        PatientAppointmentSlotId = 0,

                        FacilityId = PreferFacility(request.FacilityId, patientFacilityId, slot.FacilityId),

                        ProductId = request.ProductId,
                        PatientId = request.PatientId,
                        PatientTreatmentId = request.PatientTreatmentId,
                        ProviderId = slot.ProviderId,

                        StartDate = (i == 0 ? slot.SlotDate : null),
                        EndTime = slot.EndTime,
                        Duration = slot.Duration,
                        ProviderScheduledSlotId = (i == 0 ? slot.ProviderScheduledSlotId : (long?)null),

                        IsActive = true,
                        Status = "Scheduled",
                        CreatedDate = baseCreated.AddMonths(i),
                        CreatedBy = request.UserId ?? 2,
                        Guid = Guid.NewGuid().ToString()
                    };

                    if (i == 0)
                    {
                        appointmentSlot.StartTime = slot.StartTime;
                    }

                    _db.PT_PatientAppointmentSlots.Add(appointmentSlot);
                    _db.SaveChanges();

                    if (i == 0 && appointmentSlot.StartDate.HasValue)
                    {
                        try
                        {
                            _appointmentZoom
                                .EnsureZoomMeetingForAppointmentAsync(appointmentSlot.PatientAppointmentSlotId)
                                .GetAwaiter().GetResult();
                        }
                        catch
                        {
                            appointmentSlot.ZoomStatus = "Error";
                            _db.SaveChanges();
                        }
                    }

                    if (i == 0)
                    {
                        firstAppointmentId = appointmentSlot.PatientAppointmentSlotId;

                        var notification = new SYS_Notification
                        {
                            FacilityId = patientFacilityId,
                            NotificationType = "Appointment",
                            IsRead = false,
                            Description = "A New Appointment Has Been Generated",
                            CreatedDate = DateTime.UtcNow,
                            PatientId = appointmentSlot.PatientId
                        };
                        _db.SYS_Notifications.Add(notification);
                        _db.SaveChanges();
                    }
                }

                return firstAppointmentId;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> SaveFollowUpAppointmentAsync(SaveFollowUpAppointmentRequestDTO request)
        {
            try
            {
                if (request.ProviderScheduledSlotId == null || request.ProviderScheduledSlotId <= 0) return false;
                if (request.PatientId == null || request.PatientId <= 0) return false;
                if (request.PatientTreatmentId == null || request.PatientTreatmentId <= 0) return false;
                if (request.ProviderId == null || request.ProviderId <= 0) return false;

                var slot = await _db.UR_ProviderScheduledSlots
                    .FirstOrDefaultAsync(x => x.ProviderScheduledSlotId == request.ProviderScheduledSlotId);

                if (slot == null || slot.IsActive != true) return false;

                bool alreadyBooked = await _db.PT_PatientAppointmentSlots.AnyAsync(a =>
                    a.ProviderScheduledSlotId == slot.ProviderScheduledSlotId &&
                    a.IsActive == true &&
                    (a.Status == null || !new[] { "Missed", "Completed", "Declined", "Cancelled" }.Contains(a.Status)));

                if (alreadyBooked) return false;

                var patientFacilityId = await _db.PT_Patients
                    .Where(p => p.PatientId == request.PatientId)
                    .Select(p => p.FacilityId)
                    .FirstOrDefaultAsync();

                var appt = new PT_PatientAppointmentSlot
                {
                    PatientAppointmentSlotId = 0,

                    FacilityId = patientFacilityId ?? slot.FacilityId,

                    ProductId = request.ProductId,
                    PatientId = request.PatientId,
                    PatientTreatmentId = request.PatientTreatmentId,
                    ProviderId = request.ProviderId,
                    StartDate = slot.SlotDate,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Duration = slot.Duration,
                    ProviderScheduledSlotId = slot.ProviderScheduledSlotId,
                    IsActive = true,
                    Status = "Scheduled",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = request.UserId ?? 2,
                    Guid = Guid.NewGuid().ToString()
                };

                await _db.PT_PatientAppointmentSlots.AddAsync(appt);
                await _db.SaveChangesAsync();

                try
                {
                    await _appointmentZoom.EnsureZoomMeetingForAppointmentAsync(appt.PatientAppointmentSlotId);
                }
                catch
                {
                    appt.ZoomStatus = "Error";
                    await _db.SaveChangesAsync();
                }

                var notification = new SYS_Notification
                {
                    FacilityId = patientFacilityId,
                    NotificationType = "Appointment",
                    IsRead = false,
                    Description = "A Follow-Up Appointment Has Been Generated",
                    CreatedDate = DateTime.UtcNow,
                    PatientId = appt.PatientId
                };

                await _db.SYS_Notifications.AddAsync(notification);
                await _db.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateAppointmentForFollowUpAsync(UpdateAppointmentForFollowUpRequestDTO request)
        {
            try
            {

                if (request.PatientAppointmentSlotId <= 0) return false;

                if (request.ProviderScheduledSlotId <= 0) return false;

                var appointment = await _db.PT_PatientAppointmentSlots
                    .FirstOrDefaultAsync(x => x.PatientAppointmentSlotId == request.PatientAppointmentSlotId
                                           && (x.IsActive == true || x.IsActive == null));

                if (appointment == null) return false;

                var newSlot = await _db.UR_ProviderScheduledSlots
                    .FirstOrDefaultAsync(x => x.ProviderScheduledSlotId == request.ProviderScheduledSlotId);

                if (newSlot == null || newSlot.IsActive != true) return false;

                bool alreadyBooked = await _db.PT_PatientAppointmentSlots.AnyAsync(a =>
                    a.ProviderScheduledSlotId == newSlot.ProviderScheduledSlotId &&
                    a.IsActive == true &&
                    a.PatientAppointmentSlotId != request.PatientAppointmentSlotId &&
                    (a.Status == null || !new[] { "Missed", "Completed", "Declined", "Cancelled" }.Contains(a.Status)));

                if (alreadyBooked) return false;

                var oldStartDate = appointment.StartDate;
                var oldStartTime = appointment.StartTime;
                var oldEndTime = appointment.EndTime;
                var oldProviderScheduledSlotId = appointment.ProviderScheduledSlotId;

                appointment.ProviderScheduledSlotId = newSlot.ProviderScheduledSlotId;
                appointment.StartDate = newSlot.SlotDate;
                appointment.StartTime = newSlot.StartTime;
                appointment.EndTime = newSlot.EndTime;
                appointment.Duration = newSlot.Duration;

                var currentStatus = appointment.Status ?? "Scheduled";
                if (!currentStatus.Contains("Followup") && !currentStatus.Contains("Rescheduled"))
                {
                    appointment.Status = currentStatus + " - Rescheduled for Followup";
                }

                appointment.ModifiedDate = DateTime.UtcNow;
                appointment.ModifiedBy = request.UserId ?? 2;

                await _db.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(appointment.ZoomMeetingId))
                {
                    try
                    {
                        await _appointmentZoom.EnsureZoomMeetingForAppointmentAsync(appointment.PatientAppointmentSlotId);
                    }
                    catch
                    {

                        appointment.ZoomStatus = "Error";
                        await _db.SaveChangesAsync();
                    }
                }

                var notification = new SYS_Notification
                {
                    FacilityId = appointment.FacilityId,
                    NotificationType = "Appointment",
                    IsRead = false,
                    Description = $"Appointment #{request.PatientAppointmentSlotId} has been rescheduled for followup",
                    CreatedDate = DateTime.UtcNow,
                    PatientId = appointment.PatientId
                };

                await _db.SYS_Notifications.AddAsync(notification);
                await _db.SaveChangesAsync();

                try
                {
                    await _auditService.LogEntityChangeAsync(
                        action: "Update",
                        entityType: "PT_PatientAppointmentSlot",
                        entityId: appointment.PatientAppointmentSlotId,
                        oldValues: new {
                            StartDate = oldStartDate,
                            StartTime = oldStartTime,
                            EndTime = oldEndTime,
                            ProviderScheduledSlotId = oldProviderScheduledSlotId,
                            Status = currentStatus
                        },
                        newValues: new {
                            StartDate = appointment.StartDate,
                            StartTime = appointment.StartTime,
                            EndTime = appointment.EndTime,
                            ProviderScheduledSlotId = appointment.ProviderScheduledSlotId,
                            Status = appointment.Status
                        },
                        userId: request.UserId ?? 2,
                        patientId: appointment.PatientId,
                        facilityId: appointment.FacilityId,
                        description: $"Appointment #{request.PatientAppointmentSlotId} rescheduled for followup to new date/time",
                        module: "Appointment"
                    );
                }
                catch
                {

                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<GetAllPatientAppointmentPrescriptionsResponseDTO> GetAllPatientAppointmentPrescriptions(long PatientAppointmentSlotId)
        {
            var response = new List<GetAllPatientAppointmentPrescriptionsResponseDTO>();
            var appointmentSlot = _db.PT_PatientAppointmentSlots
                                     .Where(x => x.IsActive == true && x.PatientAppointmentSlotId == PatientAppointmentSlotId)
                                     .ToList();
            if (appointmentSlot.Count > 0)
            {
                foreach (var item in appointmentSlot)
                {
                    var drug = _db.PD_Drugs.FirstOrDefault(x => x.ProductId == item.ProductId);
                    if (drug != null)
                    {
                        var emptyResponse = new GetAllPatientAppointmentPrescriptionsResponseDTO
                        {
                            DrugId = drug.DrugId,
                            DrugName = drug.Name,
                            Brand = drug.BrandName,
                            PharmacyName = _db.SYS_Pharmacies.Where(x => x.PharmacyId == drug.PharmacyId).Select(x => x.PharmacyName).FirstOrDefault(),
                            Dosage = drug.Dosage,
                            Quantity = drug.Quantity,
                            RefillQuantity = drug.Refills,
                            DirectionQuantity = null
                        };
                        response.Add(emptyResponse);
                    }
                }
            }
            return response;
        }

        public List<GetAllPatientAppointmentInTakeFormResponseDTO> GetAllPatientAppointmentInTakeForm(long PatientAppointmentSlotId)
        {
            var response = new List<GetAllPatientAppointmentInTakeFormResponseDTO>();

            var appointmentSlot = _db.PT_PatientAppointmentSlots
                                     .FirstOrDefault(x => x.IsActive == true && x.PatientAppointmentSlotId == PatientAppointmentSlotId);
            if (appointmentSlot != null)
            {
                var patientTreatment = _db.PT_PatientTreatments
                                          .FirstOrDefault(x => x.PatientTreatmentId == appointmentSlot.PatientTreatmentId);
                if (patientTreatment != null)
                {
                    var list = _db.PT_PatientTreatmentInTakeForms
                                  .Where(x => x.PatientTreatmentId == patientTreatment.PatientTreatmentId)
                                  .ToList();
                    if (list.Count > 0)
                    {
                        var responseItem = new GetAllPatientAppointmentInTakeFormResponseDTO
                        {
                            QuestionaireName = (from PQ in _db.SYS_QuestionnairesInProducts
                                                join Q in _db.SYS_Questionnaires on PQ.QuestionnaireId equals Q.QuestionnaireId
                                                where PQ.ProductId == patientTreatment.ProductId
                                                select Q.QuestionnaireName).FirstOrDefault(),

                            InTakeForm = _db.PT_PatientTreatmentInTakeForms
                                .Where(x => x.PatientTreatmentId == patientTreatment.PatientTreatmentId)
                                .Select(x => new SavePatientTreatmentInTakeRequestDTO
                                {
                                    Type = x.Type,
                                    Question = x.Question,
                                    Answer = x.Answer,
                                    OtherText = x.OtherText
                                }).ToList()
                        };

                        response.Add(responseItem);
                    }
                }
            }
            return response;
        }

        public List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO> GetAllPatientAppointmentInTakeFormAttahments(long PatientAppointmentSlotId)
        {
            var response = new List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO>();

            var appointmentSlot = _db.PT_PatientAppointmentSlots
                                     .FirstOrDefault(x => x.IsActive == true && x.PatientAppointmentSlotId == PatientAppointmentSlotId);
            if (appointmentSlot != null)
            {
                var list = _db.PT_PatientTreatmentInTakeForms
                              .Where(x => x.PatientTreatmentId == appointmentSlot.PatientTreatmentId && x.Type == "File")
                              .ToList();
                if (list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        var emptyResponse = new GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO
                        {
                            AttachmentURL = item.Answer,
                            UploadedDate = item.CreatedDate,
                            UploadedBy = _db.SYS_UserDetails.Where(x => x.UserId == item.CreatedBy)
                                                            .Select(x => x.FirstName + " " + x.LastName).FirstOrDefault()
                        };
                        response.Add(emptyResponse);
                    }
                }
            }
            return response;
        }

        public GetPatientAppointmentVideoCallIdRespnseDTO GetPatientAppointmentVideoCallId(GetPatientAppointmentVideoCallIdRequestDTO request)
        {
            var response = new GetPatientAppointmentVideoCallIdRespnseDTO();
            PT_PatientAppointmentSlot appointmentSlot;
            if (request.RoleId == 4)
            {

                appointmentSlot = _db.PT_PatientAppointmentSlots
                    .FirstOrDefault(x => x.IsActive == true && x.PatientAppointmentSlotId == request.Id && x.ProviderId == request.UserId);
            }
            else
            {

                long? patientId = null;
                if (request.UserId.HasValue)
                {

                    var user = _db.SYS_UserDetails
                        .Where(u => u.UserId == request.UserId.Value)
                        .Select(u => u.LoginId)
                        .FirstOrDefault();

                    if (user.HasValue)
                    {
                        patientId = _db.PT_Patients
                            .Where(p => p.LoginId == user.Value)
                            .Select(p => p.PatientId)
                            .FirstOrDefault();
                    }
                }

                appointmentSlot = _db.PT_PatientAppointmentSlots
                    .FirstOrDefault(x => x.IsActive == true && x.PatientAppointmentSlotId == request.Id && x.PatientId == patientId);
            }

            if (appointmentSlot != null)
            {
                response.SessionId = appointmentSlot.VonageSessionId;
                response.Token = appointmentSlot.VonageTokenId;
            }
            return response;
        }

        public GetPatientAppointmentAlertResponseDTO GetPatientAppointmentAlert(long ProviderId)
        {
            GetPatientAppointmentAlertResponseDTO response = null;
            DateTime currentTime = DateTime.UtcNow;
            DateTime timeThreshold = currentTime.AddMinutes(5);
            DateTime currentDate = currentTime.Date;

            var appointmentSlots = _db.PT_PatientAppointmentSlots
                .Where(x => x.IsActive == true && x.ProviderId == ProviderId && x.StartDate == currentDate)
                .AsEnumerable()
                .Where(x => (x.StartDate + x.StartTime) >= currentTime
                         && (x.StartDate + x.StartTime) <= timeThreshold)
                .FirstOrDefault();

            if (appointmentSlots != null)
            {
                response = _mapper.Map<GetPatientAppointmentAlertResponseDTO>(appointmentSlots);
                response.PatientName = _db.PT_Patients
                    .Where(x => x.PatientId == appointmentSlots.PatientId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefault();
            }

            return response;
        }

        private static long? PreferFacility(long? requestFac, long? patientFac, long? slotFac)
        {
            if (requestFac.HasValue && requestFac.Value > 0) return requestFac;
            if (patientFac.HasValue && patientFac.Value > 0) return patientFac;
            return slotFac;
        }

        List<GetAllPatientAppointmentInTakeFormResponseDTO> IPatientAppointmentsRepo.GetAllPatientAppointmentInTakeForm(long patientAppointmentSlotId)
            => GetAllPatientAppointmentInTakeForm(patientAppointmentSlotId);

        List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO> IPatientAppointmentsRepo.GetAllPatientAppointmentInTakeFormAttahments(long patientAppointmentSlotId)
            => GetAllPatientAppointmentInTakeFormAttahments(patientAppointmentSlotId);

        List<GetAllPatientAppointmentPrescriptionsResponseDTO> IPatientAppointmentsRepo.GetAllPatientAppointmentPrescriptions(long patientAppointmentSlotId)
            => GetAllPatientAppointmentPrescriptions(patientAppointmentSlotId);

        GetPatientAppointmentAlertResponseDTO IPatientAppointmentsRepo.GetPatientAppointmentAlert(long providerId)
            => GetPatientAppointmentAlert(providerId);

        GetPatientAppointmentVideoCallIdRespnseDTO IPatientAppointmentsRepo.GetPatientAppointmentVideoCallId(GetPatientAppointmentVideoCallIdRequestDTO request)
            => GetPatientAppointmentVideoCallId(request);

        private static string NormalizeAppointmentStatus(string? status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Done", StringComparison.OrdinalIgnoreCase))
                return "Completed";

            if (s.Equals("Missed", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Canceled", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Declined", StringComparison.OrdinalIgnoreCase))
                return "Missed";

            return "Scheduled";
        }
}
}
