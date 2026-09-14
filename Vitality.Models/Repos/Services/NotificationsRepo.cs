using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Tickets;
using DudeMeds.Models.DTOs.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Notifications;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.CommonMethods;

namespace Vitality.Models.Repos.Services
{
    public class NotificationsRepo : BaseRepo , INotificationsRepo
    {
        private readonly IMapper _mapper;
        public NotificationsRepo(IMapper mapper)
        {
            _mapper = mapper;
        }

        private static bool IsRefillInAppTypeForClinicAdmin(string? t) =>
            t == "RefillRequest" || t == "RefillSubmittedByAdmin";

        public async Task<List<GetAllNotificationsResponseDTO>> GetAllNotificationsAsync(GetAllNotificationsRequestDTO request)
        {
            List<GetAllNotificationsResponseDTO> finalresponse = new List<GetAllNotificationsResponseDTO>();

            var query = _db.SYS_Notifications
                .AsNoTracking()
                .Where(x => x.IsRead == false);

            if (request.RoleId == 1 || request.RoleId == 2)
            {

                if (request.UserId.HasValue)
                {
                    query = query.Where(x => x.UserId == request.UserId.Value || x.UserId == null);
                }
            }
            else if (request.RoleId == 7)
            {
                if (request.UserId.HasValue)
                {
                    query = query.Where(x => x.UserId == request.UserId.Value);
                }
            }
            else if (request.RoleId == 3)
            {
                IQueryable<SYS_Notification> clinicQuery = query;
                if (request.UserId.HasValue)
                {
                    var clinicUserId = request.UserId.Value;
                    List<long> facilityIdsForBroadcast;
                    if (request.FacilityId.HasValue)
                    {
                        facilityIdsForBroadcast = new List<long> { request.FacilityId.Value };
                    }
                    else
                    {
                        facilityIdsForBroadcast = await _db.FC_UsersInFacilities
                            .AsNoTracking()
                            .Where(uf => uf.UserId == clinicUserId && uf.IsAssign == true && uf.FacilityId != null)
                            .Select(uf => uf.FacilityId!.Value)
                            .Distinct()
                            .ToListAsync();
                    }

                    if (facilityIdsForBroadcast.Count > 0)
                    {
                        clinicQuery = query.Where(x =>
                            (x.UserId == clinicUserId
                             || (x.UserId == null && x.FacilityId != null && facilityIdsForBroadcast.Contains(x.FacilityId.Value)))
                            && (x.NotificationType == null || !IsRefillInAppTypeForClinicAdmin(x.NotificationType)));
                    }
                    else
                    {
                        clinicQuery = query.Where(x =>
                            x.UserId == clinicUserId
                            && (x.NotificationType == null || !IsRefillInAppTypeForClinicAdmin(x.NotificationType)));
                    }
                }
                else if (request.FacilityId.HasValue)
                {
                    clinicQuery = query.Where(x =>
                        x.FacilityId == request.FacilityId && x.UserId == null
                        && (x.NotificationType == null || !IsRefillInAppTypeForClinicAdmin(x.NotificationType)));
                }

                query = clinicQuery;
            }
            else if (request.RoleId == 4)
            {
                if (request.UserId.HasValue)
                {
                    var providerUserId = request.UserId.Value;
                    List<long> facilityIdsForBroadcast;
                    if (request.FacilityId.HasValue)
                    {
                        facilityIdsForBroadcast = new List<long> { request.FacilityId.Value };
                    }
                    else
                    {
                        facilityIdsForBroadcast = await _db.FC_UsersInFacilities
                            .AsNoTracking()
                            .Where(uf => uf.UserId == providerUserId && uf.IsAssign == true && uf.FacilityId != null)
                            .Select(uf => uf.FacilityId!.Value)
                            .Distinct()
                            .ToListAsync();
                    }

                    if (facilityIdsForBroadcast.Count > 0)
                    {
                        query = query.Where(x =>
                            x.UserId == providerUserId
                            || (x.UserId == null && x.FacilityId != null && facilityIdsForBroadcast.Contains(x.FacilityId.Value)));
                    }
                    else
                    {
                        query = query.Where(x => x.UserId == providerUserId);
                    }
                }
                else if (request.FacilityId.HasValue)
                {
                    query = query.Where(x => x.FacilityId == request.FacilityId.Value && x.UserId == null);
                }
            }
            else if (request.RoleId == 6)
            {
                if (request.UserId.HasValue)
                {

                    var loginId = await _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u => u.UserId == request.UserId.Value)
                        .Select(u => u.LoginId)
                        .FirstOrDefaultAsync();

                    if (loginId.HasValue)
                    {
                        var patient = await _db.PT_Patients
                            .AsNoTracking()
                            .FirstOrDefaultAsync(p => p.LoginId == loginId.Value);

                        if (patient != null)
                        {
                            var patientId = patient.PatientId;

                            query = query.Where(x => x.UserId == request.UserId.Value || x.PatientId == patientId);
                        }
                        else
                        {
                            query = query.Where(x => x.UserId == request.UserId.Value);
                        }
                    }
                    else
                    {
                        query = query.Where(x => x.UserId == request.UserId.Value);
                    }
                }
            }
            else
            {

                if (request.FacilityId.HasValue)
                {
                    query = query.Where(x => x.FacilityId == request.FacilityId.Value);
                }
            }

            var list = await query.OrderByDescending(x => x.CreatedDate).ToListAsync();

            var patientIds = list.Where(x => x.PatientId.HasValue).Select(x => x.PatientId.Value).Distinct().ToList();
            var patients = new Dictionary<long, string>();

            if (patientIds.Any())
            {
                var patientList = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => patientIds.Contains(p.PatientId))
                    .Select(p => new { p.PatientId, Name = (p.FirstName ?? "") + " " + (p.LastName ?? "") })
                    .ToListAsync();

                foreach (var p in patientList)
                {
                    patients[p.PatientId] = p.Name.Trim();
                }
            }

            foreach (var item in list)
            {

                var localCreatedDate = CommonMethods.CommonMethods.ToLocalTime(item.CreatedDate);

                GetAllNotificationsResponseDTO response = new GetAllNotificationsResponseDTO
                {
                    NotificationId = item.NotificationId,
                    FacilityId = item.FacilityId,
                    NotificationType = item.NotificationType,
                    IsRead = item.IsRead,
                    Description = item.Description,
                    CreatedDate = localCreatedDate,
                    Title = item.PatientId.HasValue && patients.ContainsKey(item.PatientId.Value)
                        ? patients[item.PatientId.Value]
                        : null
                };
                finalresponse.Add(response);
            }

            return finalresponse;
        }

        public async Task<bool> UpdateNotificationsAsync(List<GetByIdRequestDTO> requestList, long? userId, long? roleId, long? facilityId)
        {
            try
            {
                if (requestList == null || requestList.Count == 0)
                    return true;

                var notificationIds = requestList.Select(x => x.Id).ToList();
                var scopedQuery = _db.SYS_Notifications
                    .Where(x => notificationIds.Contains(x.NotificationId));

                if (roleId == 1 || roleId == 2)
                {
                    if (userId.HasValue)
                        scopedQuery = scopedQuery.Where(x => x.UserId == userId.Value || x.UserId == null);
                }
                else if (roleId == 3)
                {
                    if (userId.HasValue)
                    {
                        List<long> facilityIds;
                        if (facilityId.HasValue)
                            facilityIds = new List<long> { facilityId.Value };
                        else
                        {
                            facilityIds = await _db.FC_UsersInFacilities
                                .AsNoTracking()
                                .Where(uf => uf.UserId == userId && uf.IsAssign == true && uf.FacilityId != null)
                                .Select(uf => uf.FacilityId!.Value)
                                .Distinct()
                                .ToListAsync();
                        }

                        if (facilityIds.Count > 0)
                        {
                            var uid = userId.Value;
                            scopedQuery = scopedQuery.Where(x =>
                                (x.UserId == uid
                                 || (x.UserId == null && x.FacilityId != null && facilityIds.Contains(x.FacilityId.Value)))
                                && (x.NotificationType == null || !IsRefillInAppTypeForClinicAdmin(x.NotificationType)));
                        }
                        else
                        {
                            var uidOnly = userId.Value;
                            scopedQuery = scopedQuery.Where(x =>
                                x.UserId == uidOnly
                                && (x.NotificationType == null || !IsRefillInAppTypeForClinicAdmin(x.NotificationType)));
                        }
                    }
                    else if (facilityId.HasValue)
                    {
                        scopedQuery = scopedQuery.Where(x =>
                            x.FacilityId == facilityId && x.UserId == null
                            && (x.NotificationType == null || !IsRefillInAppTypeForClinicAdmin(x.NotificationType)));
                    }
                }
                else if (roleId == 4)
                {
                    if (userId.HasValue)
                    {
                        var providerUserId = userId.Value;
                        List<long> facilityIds;
                        if (facilityId.HasValue)
                            facilityIds = new List<long> { facilityId.Value };
                        else
                        {
                            facilityIds = await _db.FC_UsersInFacilities
                                .AsNoTracking()
                                .Where(uf => uf.UserId == providerUserId && uf.IsAssign == true && uf.FacilityId != null)
                                .Select(uf => uf.FacilityId!.Value)
                                .Distinct()
                                .ToListAsync();
                        }

                        if (facilityIds.Count > 0)
                        {
                            scopedQuery = scopedQuery.Where(x =>
                                x.UserId == providerUserId
                                || (x.UserId == null && x.FacilityId != null && facilityIds.Contains(x.FacilityId.Value)));
                        }
                        else
                        {
                            scopedQuery = scopedQuery.Where(x => x.UserId == providerUserId);
                        }
                    }
                    else if (facilityId.HasValue)
                    {
                        scopedQuery = scopedQuery.Where(x => x.FacilityId == facilityId.Value && x.UserId == null);
                    }
                }
                else if (roleId == 7)
                {
                    if (userId.HasValue)
                        scopedQuery = scopedQuery.Where(x => x.UserId == userId.Value);
                }
                else if (roleId == 6)
                {
                    if (userId.HasValue)
                        scopedQuery = scopedQuery.Where(x => x.UserId == userId.Value);
                    else
                        return false;
                }
                else if (userId.HasValue)
                {
                    scopedQuery = scopedQuery.Where(x => x.UserId == userId.Value);
                }

                var notifications = await scopedQuery.ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
