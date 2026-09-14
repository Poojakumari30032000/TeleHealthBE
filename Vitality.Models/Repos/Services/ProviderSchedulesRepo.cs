using AutoMapper;
using Dapper;
using DudeMeds.Models.DTOs.PatientAppointments;
using DudeMeds.Models.DTOs.ProviderSchedules;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Vitality.Models.CommonMethods;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;

namespace DudeMeds.Models.Repos.Services
{

    public class ProviderSchedulesRepo : BaseRepo, IProviderSchedulesRepo
    {
        private readonly IMapper _mapper;
        public ProviderSchedulesRepo(IMapper mapper)
        {
            _mapper = mapper;
        }

        public List<GetAllProviderSchedulesResponseDTO> GetAllProviderSchedules(
            GetAllProviderSchedulesRequestDTO request, out int totalProviderScheduleCount)
        {

            var pageSize = request.PageSize > 0 ? request.PageSize : 25;
            var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;

            IQueryable<UR_ProviderWeeklyTemplate> baseQuery = _db.UR_ProviderWeeklyTemplates
                .AsNoTracking()
                .Where(t => t.IsActive == true);

            if (request.ProviderId.HasValue)
                baseQuery = baseQuery.Where(t => t.ProviderId == request.ProviderId.Value);

            totalProviderScheduleCount = baseQuery.Count();

            var raw = baseQuery
                .OrderBy(t => t.ProviderId)
                .Skip(pageSize * (pageNumber - 1))
                .Take(pageSize)
                .Select(t => new
                {
                    t.ProviderWeeklyTemplateId,
                    t.ProviderId,
                    ProviderName = _db.SYS_UserDetails
                        .Where(u => u.UserId == t.ProviderId)
                        .Select(u => (u.FirstName ?? "") + " " + (u.LastName ?? ""))
                        .FirstOrDefault(),
                    t.DefaultSlotDurationMinutes,
                    SlotCount = _db.UR_ProviderScheduledSlots
                        .Count(s => s.ProviderId == t.ProviderId
                                 && s.IsActive == true
                                 && s.StartTimeUtc.HasValue),
                    FirstSlotUtc = _db.UR_ProviderScheduledSlots
                        .Where(s => s.ProviderId == t.ProviderId
                                 && s.IsActive == true
                                 && s.StartTimeUtc.HasValue)
                        .OrderBy(s => s.StartTimeUtc)
                        .Select(s => s.StartTimeUtc)
                        .FirstOrDefault()
                })
                .ToList();

            var offset = request.ClientTimezoneOffsetMinutes;
            return raw.Select(x => new GetAllProviderSchedulesResponseDTO
            {
                ProviderScheduleId = x.ProviderWeeklyTemplateId,
                Title = $"Weekly hours",
                ProviderId = x.ProviderId,
                ProviderName = x.ProviderName,
                StartDate = x.FirstSlotUtc.HasValue
                    ? CommonMethods.UtcToClientLocal(x.FirstSlotUtc.Value, offset)
                    : (DateTime?)null,
                IsRecurrence = true,
                Duration = x.DefaultSlotDurationMinutes,
                SlotCount = x.SlotCount
            }).ToList();
        }

        public List<GetAllProviderScheduledSlotsByMonthResponseDTO> GetAllProviderScheduledSlotsByMonth(GetAllProviderScheduledSlotsByMonthRequestDTO request)
        {
            DynamicParameters param = new DynamicParameters();
            param.Add("@ScheduleMonth", request.ScheduledMonth);
            param.Add("@ScheduleYear", request.ScheduledYear);
            param.Add("@FacilityId", request.FacilityId);
            param.Add("@ProviderId", request.ProviderId);

            var data = ReturnJson<GetAllProviderScheduledSlotsByMonthResponseDTO>("[dbo].[sprocGetAllProviderScheduledSlotsByMonth]", param).ToList();
            return data;
        }

        public List<GetAllProviderScheduledSlotsByDaysResponseDTO> GetAllProviderScheduledSlotsByDays(GetAllProviderScheduledSlotsByDaysRequestDTO request)
        {
            DynamicParameters param = new DynamicParameters();
            param.Add("@SDate", request.StartDate);
            param.Add("@EDate", request.EndDate);
            param.Add("@FacilityId", request.FacilityId);
            param.Add("@ProviderId", request.ProviderId);

            var data = ReturnJson<GetAllProviderScheduledSlotsByDaysResponseDTO>("[dbo].[sprocGetAllProviderScheduledSlotsByDays]", param).ToList();
            return data;
        }

        public List<GetAllProviderScheduledSlotsResponseDTO> GetAllProviderScheduledSlots(GetAllProviderScheduledSlotsRequestDTO request, out int totalProviderScheduledSlotCount)
        {
            var pageSize = request.PageSize > 0 ? request.PageSize : 100;
            var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;

            IQueryable<UR_ProviderScheduledSlot> query = _db.UR_ProviderScheduledSlots
                .AsNoTracking()
                .Where(x => x.IsActive == true && x.StartTimeUtc.HasValue);

            if (request.ProviderId.HasValue)
                query = query.Where(x => x.ProviderId == request.ProviderId.Value);

            if (request.Duration.HasValue)
                query = query.Where(x => x.DurationMinutes == request.Duration.Value);

            if (request.Date.HasValue)
            {
                var requestDateOnly = request.Date.Value.Date;
                var offset = request.ClientTimezoneOffsetMinutes ?? 0;

                var utcDayStart = requestDateOnly.AddMinutes(-offset);
                var utcDayEnd = utcDayStart.AddDays(1);

                query = query.Where(x => x.StartTimeUtc!.Value >= utcDayStart
                                      && x.StartTimeUtc.Value < utcDayEnd);
            }

            if (!string.IsNullOrWhiteSpace(request.AppointmentStatus))
            {
                var status = request.AppointmentStatus.Trim();
                if (status.Equals("Available", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(x => x.Status == "Available");
                else if (status.Equals("Booked", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(x => x.Status == "Booked");
            }

            totalProviderScheduledSlotCount = query.Count();

            var rawItems = (from s in query
                            join u in _db.SYS_UserDetails.AsNoTracking()
                                on s.ProviderId equals u.UserId into users
                            from u in users.DefaultIfEmpty()
                            orderby s.StartTimeUtc, s.ProviderScheduledSlotId
                            select new
                            {
                                Slot = s,
                                ProviderName = u != null ? ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim() : null
                            })
                           .Skip(pageSize * (pageNumber - 1))
                           .Take(pageSize)
                           .ToList();

            var viewerOffsetMinutes = request.ClientTimezoneOffsetMinutes ?? 0;
            return rawItems.Select(x =>
            {
                var startUtc = x.Slot.StartTimeUtc!.Value;
                var endUtc = x.Slot.EndTimeUtc ?? startUtc;
                var localStart = startUtc.AddMinutes(viewerOffsetMinutes);
                var localEnd = endUtc.AddMinutes(viewerOffsetMinutes);

                return new GetAllProviderScheduledSlotsResponseDTO
                {
                    ProviderScheduledSlotId = x.Slot.ProviderScheduledSlotId,
                    ProviderId = x.Slot.ProviderId,
                    ProviderName = x.ProviderName,
                    Date = localStart.Date,
                    StartTime = localStart.TimeOfDay.ToString(@"hh\:mm\:ss"),
                    EndTime = localEnd.TimeOfDay.ToString(@"hh\:mm\:ss"),
                    Duration = x.Slot.DurationMinutes,
                    IsAppointment = x.Slot.Status == "Booked"
                };
            }).ToList();
        }

        public GetProviderScheduleByIdResponseDTO GetProviderSsheduleById(long ProviderScheduleId)
        {

            var template = _db.UR_ProviderWeeklyTemplates
                .AsNoTracking()
                .FirstOrDefault(t => t.ProviderWeeklyTemplateId == ProviderScheduleId);

            if (template == null) return new GetProviderScheduleByIdResponseDTO();

            return new GetProviderScheduleByIdResponseDTO
            {
                ProviderScheduleId = template.ProviderWeeklyTemplateId,
                ProviderId = template.ProviderId,
                Duration = template.DefaultSlotDurationMinutes,
                IsRecurrence = true,
                RecurrenceDays = new List<string>()
            };
        }

        [Obsolete("Use IProviderHoursRepo.SaveDayAsync / SaveDateOverrideAsync instead. Bundled-schedule writes are no longer supported.")]
        public bool SaveProviderSlot(SaveProviderSlotRequestDTO request, long UserId, long OrganizationId)
        {

            return true;
        }

        public bool RescheduleProviderScheduledSlot(SaveRescheduleProviderScheduledSlotRequestDTO request, long UserId)
        {
            try
            {
                var slot = _db.UR_ProviderScheduledSlots
                    .FirstOrDefault(x => x.ProviderScheduledSlotId == request.ProviderScheduledSlotId);
                if (slot == null) return false;
                if (slot.Status == "Booked")
                {

                    return false;
                }

                var newDate = request.StartDate?.Date ?? DateTime.UtcNow.Date;
                var startTod = Convert.ToDateTime(DateTime.UtcNow.ToShortDateString() + " " + request.StartTime).TimeOfDay;
                var endTod = Convert.ToDateTime(DateTime.UtcNow.ToShortDateString() + " " + request.EndTime).TimeOfDay;

                slot.StartTimeUtc = newDate.Add(startTod);
                slot.EndTimeUtc = newDate.Add(endTod);
                slot.DurationMinutes = request.Duration;

                slot.SlotDate = newDate;
                slot.StartTime = startTod;
                slot.EndTime = endTod;
                slot.Duration = request.Duration;

                slot.ModifiedBy = UserId;
                slot.ModifiedDate = DateTime.UtcNow;
                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public GetProviderSchedulesStartDateByIdResponseDTO GetProviderSchedulesStartDateById(long ProviderId, int? clientTimezoneOffsetMinutes = null)
        {
            var response = new GetProviderSchedulesStartDateByIdResponseDTO();

            var firstUtc = _db.UR_ProviderScheduledSlots
                .AsNoTracking()
                .Where(x => x.ProviderId == ProviderId && x.IsActive == true && x.StartTimeUtc.HasValue)
                .OrderBy(x => x.StartTimeUtc)
                .Select(x => x.StartTimeUtc)
                .FirstOrDefault();

            var lastUtc = _db.UR_ProviderScheduledSlots
                .AsNoTracking()
                .Where(x => x.ProviderId == ProviderId && x.IsActive == true && x.StartTimeUtc.HasValue)
                .OrderByDescending(x => x.StartTimeUtc)
                .Select(x => x.StartTimeUtc)
                .FirstOrDefault();

            if (firstUtc.HasValue)
            {
                var localFirst = clientTimezoneOffsetMinutes.HasValue
                    ? CommonMethods.UtcToClientLocal(firstUtc.Value, clientTimezoneOffsetMinutes)
                    : CommonMethods.ToLocalTime(firstUtc.Value);
                response.StartDate = localFirst.Date;
            }

            if (lastUtc.HasValue)
            {
                var localLast = clientTimezoneOffsetMinutes.HasValue
                    ? CommonMethods.UtcToClientLocal(lastUtc.Value, clientTimezoneOffsetMinutes)
                    : CommonMethods.ToLocalTime(lastUtc.Value);
                response.EndDate = localLast.Date.AddDays(1);
            }
            else
            {
                response.EndDate = DateTime.UtcNow;
            }
            return response;
        }

        public GetProviderScheduledSlotStartDateByIdResponseDTO GetProviderScheduledSlotStartDateById(long ProviderScheduleId, int? clientTimezoneOffsetMinutes = null)
        {

            var inner = GetProviderSchedulesStartDateById(ProviderScheduleId, clientTimezoneOffsetMinutes);
            return new GetProviderScheduledSlotStartDateByIdResponseDTO
            {
                StartDate = inner.StartDate,
                EndDate = inner.EndDate
            };
        }

        public List<GetProviderScheduledSlotTimesResponseDTO> GetProviderScheduledSlotTimes(GetProviderScheduledSlotTimesRequestDTO request)
        {
            var response = new List<GetProviderScheduledSlotTimesResponseDTO>();
            var slots = _db.UR_ProviderScheduledSlots
                .AsNoTracking()
                .Where(x => x.ProviderId == request.ProviderId
                         && x.IsActive == true
                         && x.SlotDate == request.SlotDate)
                .ToList();

            foreach (var item in slots)
            {
                response.Add(new GetProviderScheduledSlotTimesResponseDTO
                {
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    Duration = item.DurationMinutes ?? item.Duration,
                });
            }
            return response;
        }

        [Obsolete("Bundled schedules no longer exist. This is a no-op for back-compat.")]
        public bool DeleteProviderScheduleById(long ProviderScheduleId, long UserId)
        {

            return true;
        }

        public bool DeleteProviderScheduledSlotById(long ProviderScheduledSlotId, long UserId)
        {
            try
            {
                var slot = _db.UR_ProviderScheduledSlots
                    .FirstOrDefault(x => x.ProviderScheduledSlotId == ProviderScheduledSlotId);
                if (slot == null) return false;
                if (slot.Status == "Booked")
                {

                    return false;
                }

                slot.IsActive = false;
                slot.ModifiedBy = UserId;
                slot.ModifiedDate = DateTime.UtcNow;
                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
