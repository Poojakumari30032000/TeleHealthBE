using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.DTOs.AuditLogs;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.CommonMethods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vitality.Models.Repos.Services
{
    public class AuditLogsRepo : BaseRepo, IAuditLogsRepo
    {
        public async Task<List<GetAuditLogsResponseDTO>> GetFacilityAuditLogsAsync(GetFacilityAuditLogsRequestDTO request)
        {
            var query = _db.SYS_AuditLogs
                .AsNoTracking()
                .Where(a => a.FacilityId == request.FacilityId);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.CreatedDate >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(a => a.CreatedDate <= request.EndDate.Value);

            if (!string.IsNullOrEmpty(request.Action))
                query = query.Where(a => a.Action == request.Action);

            if (!string.IsNullOrEmpty(request.EntityType))
                query = query.Where(a => a.EntityType == request.EntityType);

            if (!string.IsNullOrEmpty(request.Module))
                query = query.Where(a => a.Module == request.Module);

            if (request.UserId.HasValue)
                query = query.Where(a => a.UserId == request.UserId.Value);

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return await MapAuditLogsToDTO(logs);
        }

        public async Task<List<GetAuditLogsResponseDTO>> GetUserAuditLogsAsync(GetUserAuditLogsRequestDTO request)
        {
            var query = _db.SYS_AuditLogs
                .AsNoTracking()
                .Where(a => a.UserId == request.UserId);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.CreatedDate >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(a => a.CreatedDate <= request.EndDate.Value);

            if (!string.IsNullOrEmpty(request.Action))
                query = query.Where(a => a.Action == request.Action);

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return await MapAuditLogsToDTO(logs);
        }

        public async Task<List<GetAuditLogsResponseDTO>> GetEntityAuditLogsAsync(GetEntityAuditLogsRequestDTO request)
        {
            var query = _db.SYS_AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityType == request.EntityType && a.EntityId == request.EntityId);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.CreatedDate >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(a => a.CreatedDate <= request.EndDate.Value);

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return await MapAuditLogsToDTO(logs);
        }

        public async Task<(List<GetAuditLogsResponseDTO> logs, int totalCount)> GetAllAuditLogsAsync(GetAllAuditLogsRequestDTO request)
        {
            var query = _db.SYS_AuditLogs.AsNoTracking();

            if (request.FacilityId.HasValue)
                query = query.Where(a => a.FacilityId == request.FacilityId.Value);

            if (request.OrganizationId.HasValue)
                query = query.Where(a => a.OrganizationId == request.OrganizationId.Value);

            if (request.UserId.HasValue)
                query = query.Where(a => a.UserId == request.UserId.Value);

            if (!string.IsNullOrEmpty(request.Action))
                query = query.Where(a => a.Action == request.Action);

            if (!string.IsNullOrEmpty(request.EntityType))
                query = query.Where(a => a.EntityType == request.EntityType);

            if (!string.IsNullOrEmpty(request.Module))
                query = query.Where(a => a.Module == request.Module);

            if (!string.IsNullOrEmpty(request.Status))
                query = query.Where(a => a.Status == request.Status);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.CreatedDate >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(a => a.CreatedDate <= request.EndDate.Value);

            var totalCount = await query.CountAsync();

            var pageSize = (request?.PageSize ?? 0) > 0 ? request.PageSize : 50;
            var pageNumber = (request?.PageNumber ?? 0) > 0 ? request.PageNumber : 1;

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var mappedLogs = await MapAuditLogsToDTO(logs);
            return (mappedLogs, totalCount);
        }

        public async Task<GetAuditLogStatisticsResponseDTO> GetFacilityAuditStatisticsAsync(long facilityId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _db.SYS_AuditLogs
                .AsNoTracking()
                .Where(a => a.FacilityId == facilityId);

            if (startDate.HasValue)
                query = query.Where(a => a.CreatedDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.CreatedDate <= endDate.Value);

            var logs = await query.ToListAsync();

            var facility = await _db.SYS_Facilities
                .AsNoTracking()
                .Where(f => f.FacilityId == facilityId)
                .Select(f => f.TitleShort)
                .FirstOrDefaultAsync();

            var stats = new GetAuditLogStatisticsResponseDTO
            {
                FacilityId = facilityId,
                FacilityName = facility,
                TotalActions = logs.Count,
                CreateActions = logs.Count(a => a.Action == "Create"),
                UpdateActions = logs.Count(a => a.Action == "Update"),
                DeleteActions = logs.Count(a => a.Action == "Delete"),
                FailedActions = logs.Count(a => a.Status == "Failed" || a.Status == "Error"),
                UniqueUsers = logs.Where(a => a.UserId.HasValue).Select(a => a.UserId.Value).Distinct().Count(),
                ActionsByType = logs.GroupBy(a => a.Action)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ActionsByEntity = logs.GroupBy(a => a.EntityType)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return stats;
        }

        public async Task<List<GetAuditLogsResponseDTO>> GetModuleAuditLogsAsync(GetModuleAuditLogsRequestDTO request)
        {
            var query = _db.SYS_AuditLogs
                .AsNoTracking()
                .Where(a => a.Module == request.Module);

            if (request.FacilityId.HasValue)
                query = query.Where(a => a.FacilityId == request.FacilityId.Value);

            if (request.EntityId.HasValue)
                query = query.Where(a => a.EntityId == request.EntityId.Value);

            if (!string.IsNullOrEmpty(request.Action))
                query = query.Where(a => a.Action == request.Action);

            if (request.UserId.HasValue)
                query = query.Where(a => a.UserId == request.UserId.Value);

            if (request.PatientId.HasValue)
                query = query.Where(a => a.PatientId == request.PatientId.Value);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.CreatedDate >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(a => a.CreatedDate <= request.EndDate.Value);

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return await MapAuditLogsToDTO(logs);
        }

        public async Task<(List<GetAuditLogsResponseDTO> logs, int totalCount)> GetPatientAuditLogsAsync(GetPatientAuditLogsRequestDTO request)
        {
            var treatmentIds = await _db.PT_PatientTreatments
                .AsNoTracking()
                .Where(t => t.PatientId == request.PatientId)
                .Select(t => t.PatientTreatmentId)
                .ToListAsync();

            var prescriptionIds = await _db.PT_PatientPrescriptions
                .AsNoTracking()
                .Where(p => p.PatientId == request.PatientId)
                .Select(p => p.PatientPrescriptionId)
                .ToListAsync();

            var appointmentIds = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .Where(a => a.PatientId == request.PatientId)
                .Select(a => a.PatientAppointmentSlotId)
                .ToListAsync();

            var orderIds = await _db.PT_PatientOrders
                .AsNoTracking()
                .Where(o => o.PatientId == request.PatientId)
                .Select(o => o.PatientOrderId)
                .ToListAsync();

            var invoiceIds = await _db.Sys_Invoices
                .AsNoTracking()
                .Where(i => i.PatientId == request.PatientId)
                .Select(i => (long)i.InvoiceId)
                .ToListAsync();

            var profileNoteIds = await _db.PT_PatientProfileNotes
                .AsNoTracking()
                .Where(n => n.PatientId == request.PatientId)
                .Select(n => n.PatientProfileNoteId)
                .ToListAsync();

            var patientDocumentIds = await _db.PT_PatientDocuments
                .AsNoTracking()
                .Where(d => d.PatientId == request.PatientId)
                .Select(d => d.PatientDocumentId)
                .ToListAsync();

            var treatmentDocumentIds = treatmentIds.Count == 0
                ? new List<long>()
                : await _db.PT_PatientTreatmentDocuments
                    .AsNoTracking()
                    .Where(d => treatmentIds.Contains(d.PatientTreatmentId))
                    .Select(d => d.PatientTreatmentDocumentId)
                    .ToListAsync();

            var treatmentSoapNoteIds = treatmentIds.Count == 0
                ? new List<long>()
                : await _db.PT_PatientTreatmentSoapNotes
                    .AsNoTracking()
                    .Where(s => treatmentIds.Contains(s.PatientTreatmentId))
                    .Select(s => s.SoapNoteId)
                    .ToListAsync();

            var prescriptionSoapNoteIds = prescriptionIds.Count == 0
                ? new List<long>()
                : await _db.PT_PatientPrescriptionSoapNotes
                    .AsNoTracking()
                    .Where(s => prescriptionIds.Contains(s.PatientPrescriptionId))
                    .Select(s => s.SoapNoteId)
                    .ToListAsync();

            var query = _db.SYS_AuditLogs
                .AsNoTracking()
                .Where(a =>
                    a.PatientId == request.PatientId
                    || (a.EntityType == "Patient" && a.EntityId == request.PatientId)
                    || (a.EntityType == "PT_Patient" && a.EntityId == request.PatientId)
                    || (a.EntityType == "PT_PatientTreatment" && treatmentIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientPrescription" && prescriptionIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientAppointmentSlot" && appointmentIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientOrder" && orderIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "Sys_Invoice" && invoiceIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientProfileNote" && profileNoteIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientDocument" && patientDocumentIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientTreatmentDocument" && treatmentDocumentIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientTreatmentSoapNote" && treatmentSoapNoteIds.Contains(a.EntityId ?? 0))
                    || (a.EntityType == "PT_PatientPrescriptionSoapNote" && prescriptionSoapNoteIds.Contains(a.EntityId ?? 0)));

            if (request.FacilityId.HasValue)
                query = query.Where(a => a.FacilityId == request.FacilityId.Value);

            if (!string.IsNullOrEmpty(request.Action))
                query = query.Where(a => a.Action == request.Action);

            if (!string.IsNullOrEmpty(request.Module))
                query = query.Where(a => a.Module == request.Module);

            if (!string.IsNullOrEmpty(request.EntityType))
                query = query.Where(a => a.EntityType == request.EntityType);

            if (request.UserId.HasValue)
                query = query.Where(a => a.UserId == request.UserId.Value);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.CreatedDate >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(a => a.CreatedDate <= request.EndDate.Value);

            var totalCount = await query.CountAsync();

            var pageSize = (request?.PageSize ?? 0) > 0 ? request.PageSize : 50;
            var pageNumber = (request?.PageNumber ?? 0) > 0 ? request.PageNumber : 1;

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var mappedLogs = await MapAuditLogsToDTO(logs);
            return (mappedLogs, totalCount);
        }

        private async Task<List<GetAuditLogsResponseDTO>> MapAuditLogsToDTO(List<SYS_AuditLog> logs)
        {
            var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
            var patientIds = logs.Where(l => l.PatientId.HasValue).Select(l => l.PatientId!.Value).Distinct().ToList();
            var facilityIds = logs.Where(l => l.FacilityId.HasValue).Select(l => l.FacilityId!.Value).Distinct().ToList();

            var users = await (from u in _db.SYS_UserDetails.AsNoTracking()
                               join login in _db.SYS_Logins.AsNoTracking()
                                   on u.LoginId equals login.LoginId into lj
                               from login in lj.DefaultIfEmpty()
                               join rt in _db.LK_RoleTitles.AsNoTracking()
                                   on u.RoleTitleId equals rt.RoleTitleId into rtj
                               from rt in rtj.DefaultIfEmpty()
                               where userIds.Contains(u.UserId)
                               select new
                               {
                                   u.UserId,
                                   u.FirstName,
                                   u.LastName,
                                   u.LoginId,
                                   RoleId = login != null ? (int?)login.RoleId : null,
                                   RoleTitleName = rt != null && rt.IsActive == true ? rt.RoleTitleName : null
                               })
                .ToListAsync();

            var patientUserLoginIds = users
                .Where(u => u.RoleId == 6 && u.LoginId.HasValue)
                .Select(u => u.LoginId!.Value)
                .Distinct()
                .ToList();

            var patientNamesByLoginId = patientUserLoginIds.Count == 0
                ? new Dictionary<long, string>()
                : await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.LoginId.HasValue && patientUserLoginIds.Contains(p.LoginId.Value))
                    .Select(p => new
                    {
                        LoginId = p.LoginId!.Value,
                        Name = ((p.FirstName ?? "") + " " + (p.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(x => x.LoginId, x => x.Name);

            var userDict = users.ToDictionary(
                u => u.UserId,
                u =>
                {
                    if (u.RoleId == 6 &&
                        u.LoginId.HasValue &&
                        patientNamesByLoginId.TryGetValue(u.LoginId.Value, out var patientName) &&
                        !string.IsNullOrWhiteSpace(patientName))
                    {
                        return patientName;
                    }

                    return FormatDisplayName(u.FirstName, u.LastName, u.RoleTitleName);
                });

            var patients = await _db.PT_Patients
                .AsNoTracking()
                .Where(p => patientIds.Contains(p.PatientId))
                .Select(p => new { p.PatientId, Name = p.FirstName + " " + p.LastName })
                .ToListAsync();

            var patientDict = patients.ToDictionary(p => p.PatientId, p => p.Name);

            var facilities = await _db.SYS_Facilities
                .AsNoTracking()
                .Where(f => facilityIds.Contains(f.FacilityId))
                .Select(f => new { f.FacilityId, f.TitleShort })
                .ToListAsync();

            var facilityDict = facilities.ToDictionary(f => f.FacilityId, f => f.TitleShort);

            return logs.Select(log =>
            {
                object? oldValuesObj = null;
                object? newValuesObj = null;
                object? additionalDataObj = null;

                try
                {

                    if (!string.IsNullOrWhiteSpace(log.OldValues))
                    {

                        try
                        {
                            oldValuesObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(log.OldValues);
                        }
                        catch
                        {

                            var jObj = JObject.Parse(log.OldValues);
                            oldValuesObj = jObj.ToObject<Dictionary<string, object>>();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializing OldValues for AuditLog {log.AuditLogId}: {ex.Message}");

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(log.OldValues))
                        {
                            oldValuesObj = JsonConvert.DeserializeObject<List<object>>(log.OldValues);
                        }
                    }
                    catch
                    {

                        oldValuesObj = JsonConvert.DeserializeObject(log.OldValues);
                    }
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(log.NewValues))
                    {

                        try
                        {
                            newValuesObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(log.NewValues);
                        }
                        catch
                        {

                            var jObj = JObject.Parse(log.NewValues);
                            newValuesObj = jObj.ToObject<Dictionary<string, object>>();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializing NewValues for AuditLog {log.AuditLogId}: {ex.Message}");

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(log.NewValues))
                        {
                            newValuesObj = JsonConvert.DeserializeObject<List<object>>(log.NewValues);
                        }
                    }
                    catch
                    {

                        newValuesObj = JsonConvert.DeserializeObject(log.NewValues);
                    }
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(log.AdditionalData))
                    {

                        try
                        {
                            additionalDataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(log.AdditionalData);
                        }
                        catch
                        {

                            var jObj = JObject.Parse(log.AdditionalData);
                            additionalDataObj = jObj.ToObject<Dictionary<string, object>>();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializing AdditionalData for AuditLog {log.AuditLogId}: {ex.Message}");

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(log.AdditionalData))
                        {
                            additionalDataObj = JsonConvert.DeserializeObject<List<object>>(log.AdditionalData);
                        }
                    }
                    catch
                    {

                        additionalDataObj = JsonConvert.DeserializeObject(log.AdditionalData);
                    }
                }

                return new GetAuditLogsResponseDTO
                {
                    AuditLogId = log.AuditLogId,
                    Action = log.Action,
                    EntityType = log.EntityType,
                    Module = log.Module,
                    EntityId = log.EntityId,
                    UserId = log.UserId,
                    UserName = log.UserId.HasValue && userDict.ContainsKey(log.UserId.Value)
                        ? userDict[log.UserId.Value]
                        : (log.PatientId.HasValue && patientDict.ContainsKey(log.PatientId.Value)
                            ? patientDict[log.PatientId.Value]
                            : (log.UserId.HasValue ? $"User {log.UserId.Value}" : "System")),
                    PatientId = log.PatientId,
                    PatientName = log.PatientId.HasValue && patientDict.ContainsKey(log.PatientId.Value)
                        ? patientDict[log.PatientId.Value] : null,
                    FacilityId = log.FacilityId,
                    FacilityName = log.FacilityId.HasValue && facilityDict.ContainsKey(log.FacilityId.Value)
                        ? facilityDict[log.FacilityId.Value] : null,
                    OrganizationId = log.OrganizationId,
                    Description = log.Description,
                    Status = log.Status,
                    ErrorMessage = log.ErrorMessage,
                    RequestPath = log.RequestPath,
                    RequestMethod = log.RequestMethod,
                    IpAddress = log.IpAddress,
                    CreatedDate = CommonMethods.CommonMethods.ToLocalTime(log.CreatedDate),
                    OldValues = oldValuesObj,
                    NewValues = newValuesObj,
                    AdditionalData = additionalDataObj
                };
            }).ToList();
        }

        private static string FormatDisplayName(string? firstName, string? lastName, string? roleTitleName)
        {
            var baseName = $"{firstName ?? ""} {lastName ?? ""}".Trim();
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "Unknown";

            if (!string.IsNullOrWhiteSpace(roleTitleName))
                return $"{baseName} ({roleTitleName.Trim()})";

            return baseName;
        }
    }
}
