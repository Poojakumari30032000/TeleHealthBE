using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Services.Audit
{
    public class AuditService : IAuditService
    {
        private readonly MainContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public AuditService(MainContext db, IHttpContextAccessor httpContextAccessor, IServiceScopeFactory serviceScopeFactory)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task LogAsync(AuditLogEntry entry)
        {
            try
            {
                var auditLog = new SYS_AuditLog
                {
                    Action = entry.Action,
                    EntityType = entry.EntityType,
                    Module = entry.Module ?? DeriveModuleFromEntityType(entry.EntityType),
                    EntityId = entry.EntityId,
                    OldValues = entry.OldValues != null ? JsonConvert.SerializeObject(entry.OldValues) : null,
                    NewValues = entry.NewValues != null ? JsonConvert.SerializeObject(entry.NewValues) : null,
                    UserId = entry.UserId,
                    PatientId = entry.PatientId,
                    OrganizationId = entry.OrganizationId,
                    FacilityId = entry.FacilityId,
                    IpAddress = entry.IpAddress ?? GetIpAddress(),
                    UserAgent = entry.UserAgent ?? GetUserAgent(),
                    RequestPath = entry.RequestPath ?? GetRequestPath(),
                    RequestMethod = entry.RequestMethod ?? GetRequestMethod(),
                    Description = entry.Description,
                    Status = entry.Status ?? "Success",
                    ErrorMessage = entry.ErrorMessage,
                    AdditionalData = entry.AdditionalData != null ? JsonConvert.SerializeObject(entry.AdditionalData) : null,
                    CreatedDate = DateTime.UtcNow
                };

                _db.SYS_AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Audit logging failed: {ex.Message}");

            }
        }

        public void Log(AuditLogEntry entry)
        {

            try
            {
                var auditLog = new SYS_AuditLog
                {
                    Action = entry.Action,
                    EntityType = entry.EntityType,
                    Module = entry.Module ?? DeriveModuleFromEntityType(entry.EntityType),
                    EntityId = entry.EntityId,
                    OldValues = entry.OldValues != null ? JsonConvert.SerializeObject(entry.OldValues) : null,
                    NewValues = entry.NewValues != null ? JsonConvert.SerializeObject(entry.NewValues) : null,
                    UserId = entry.UserId,
                    PatientId = entry.PatientId,
                    OrganizationId = entry.OrganizationId,
                    FacilityId = entry.FacilityId,
                    IpAddress = entry.IpAddress ?? GetIpAddress(),
                    UserAgent = entry.UserAgent ?? GetUserAgent(),
                    RequestPath = entry.RequestPath ?? GetRequestPath(),
                    RequestMethod = entry.RequestMethod ?? GetRequestMethod(),
                    Description = entry.Description,
                    Status = entry.Status ?? "Success",
                    ErrorMessage = entry.ErrorMessage,
                    AdditionalData = entry.AdditionalData != null ? JsonConvert.SerializeObject(entry.AdditionalData) : null,
                    CreatedDate = DateTime.UtcNow
                };

                _db.SYS_AuditLogs.Add(auditLog);
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audit logging failed: {ex.Message}");
            }
        }

        public void LogEntityChange(string action, string entityType, long? entityId, object? oldValues = null, object? newValues = null, long? userId = null, long? patientId = null, long? facilityId = null, long? organizationId = null, string? description = null, string? module = null)
        {

            if (!facilityId.HasValue)
            {
                facilityId = ExtractFacilityIdFromEntity(newValues) ?? ExtractFacilityIdFromEntity(oldValues);
            }

            if (!facilityId.HasValue && patientId.HasValue)
            {
                facilityId = GetFacilityIdFromPatient(patientId.Value);
            }

            if (!facilityId.HasValue)
            {
                facilityId = GetFacilityIdFromContext();
            }

            if (!organizationId.HasValue)
            {
                organizationId = GetOrganizationIdFromContext();
            }

            var entry = new AuditLogEntry
            {
                Action = action,
                EntityType = entityType,
                Module = module ?? DeriveModuleFromEntityType(entityType),
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                UserId = userId ?? GetUserIdFromContext(),
                PatientId = patientId ?? GetPatientIdFromContext(),
                FacilityId = facilityId,
                OrganizationId = organizationId,
                Description = description ?? $"{action} {entityType} (ID: {entityId})"
            };

            Log(entry);
        }

        public async Task LogEntityChangeAsync(string action, string entityType, long? entityId, object? oldValues = null, object? newValues = null, long? userId = null, long? patientId = null, long? facilityId = null, long? organizationId = null, string? description = null, string? module = null)
        {

            if (!facilityId.HasValue)
            {
                facilityId = ExtractFacilityIdFromEntity(newValues) ?? ExtractFacilityIdFromEntity(oldValues);
            }

            if (!facilityId.HasValue && patientId.HasValue)
            {
                facilityId = GetFacilityIdFromPatient(patientId.Value);
            }

            if (!facilityId.HasValue)
            {
                facilityId = GetFacilityIdFromContext();
            }

            if (!organizationId.HasValue)
            {
                organizationId = GetOrganizationIdFromContext();
            }

            var entry = new AuditLogEntry
            {
                Action = action,
                EntityType = entityType,
                Module = module ?? DeriveModuleFromEntityType(entityType),
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                UserId = userId ?? GetUserIdFromContext(),
                PatientId = patientId ?? GetPatientIdFromContext(),
                FacilityId = facilityId,
                OrganizationId = organizationId,
                Description = description ?? $"{action} {entityType} (ID: {entityId})"
            };

            await LogAsync(entry);
        }

        public void LogEntityChangeBackground(string action, string entityType, long? entityId, object? oldValues = null, object? newValues = null, long? userId = null, long? patientId = null, long? facilityId = null, long? organizationId = null, string? description = null, string? module = null)
        {

            _ = Task.Run(async () =>
            {
                try
                {

                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

                        long? capturedFacilityId = facilityId;
                        if (!capturedFacilityId.HasValue)
                        {
                            capturedFacilityId = ExtractFacilityIdFromEntity(newValues) ?? ExtractFacilityIdFromEntity(oldValues);
                        }

                        if (!capturedFacilityId.HasValue && patientId.HasValue)
                        {
                            try
                            {
                                capturedFacilityId = await db.PT_Patients
                                    .AsNoTracking()
                                    .Where(p => p.PatientId == patientId.Value)
                                    .Select(p => p.FacilityId)
                                    .FirstOrDefaultAsync();
                            }
                            catch {  }
                        }

                        var capturedOrganizationId = organizationId ?? GetOrganizationIdFromContext();
                        var capturedUserId = userId ?? GetUserIdFromContext();
                        var capturedPatientId = patientId ?? GetPatientIdFromContext();

                        var entry = new AuditLogEntry
                        {
                            Action = action,
                            EntityType = entityType,
                            Module = module ?? DeriveModuleFromEntityType(entityType),
                            EntityId = entityId,
                            OldValues = oldValues,
                            NewValues = newValues,
                            UserId = capturedUserId,
                            PatientId = capturedPatientId,
                            FacilityId = capturedFacilityId,
                            OrganizationId = capturedOrganizationId,
                            Description = description ?? $"{action} {entityType} (ID: {entityId})"
                        };

                        var auditLog = new SYS_AuditLog
                        {
                            Action = entry.Action,
                            EntityType = entry.EntityType,
                            Module = entry.Module ?? DeriveModuleFromEntityType(entry.EntityType),
                            EntityId = entry.EntityId,
                            OldValues = entry.OldValues != null ? JsonConvert.SerializeObject(entry.OldValues) : null,
                            NewValues = entry.NewValues != null ? JsonConvert.SerializeObject(entry.NewValues) : null,
                            UserId = entry.UserId,
                            PatientId = entry.PatientId,
                            OrganizationId = entry.OrganizationId,
                            FacilityId = entry.FacilityId,
                            IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                            UserAgent = httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault(),
                            RequestPath = httpContextAccessor.HttpContext?.Request.Path.Value,
                            RequestMethod = httpContextAccessor.HttpContext?.Request.Method,
                            Description = entry.Description,
                            Status = entry.Status ?? "Success",
                            ErrorMessage = entry.ErrorMessage,
                            AdditionalData = entry.AdditionalData != null ? JsonConvert.SerializeObject(entry.AdditionalData) : null,
                            CreatedDate = DateTime.UtcNow
                        };

                        db.SYS_AuditLogs.Add(auditLog);
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {

                    Console.WriteLine($"Background audit logging failed: {ex.Message}");
                }
            });
        }

        public async Task LogUserActionAsync(string action, long? userId, long? patientId = null, string? description = null, string? status = "Success", string? errorMessage = null)
        {
            var entry = new AuditLogEntry
            {
                Action = action,
                EntityType = "User",
                EntityId = userId,
                UserId = userId,
                PatientId = patientId,
                Description = description ?? action,
                Status = status,
                ErrorMessage = errorMessage
            };

            await LogAsync(entry);
        }

        public async Task LogApiRequestAsync(string method, string path, long? userId, long? patientId = null, object? requestData = null, string? status = "Success", string? errorMessage = null)
        {
            var entry = new AuditLogEntry
            {
                Action = "API_Request",
                RequestMethod = method,
                RequestPath = path,
                UserId = userId,
                PatientId = patientId,
                AdditionalData = requestData,
                Status = status,
                ErrorMessage = errorMessage,
                Description = $"{method} {path}"
            };

            await LogAsync(entry);
        }

        private string? GetIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            return httpContext.Connection.RemoteIpAddress?.ToString();
        }

        private string? GetUserAgent()
        {
            return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
        }

        private string? GetRequestPath()
        {
            return _httpContextAccessor.HttpContext?.Request.Path.Value;
        }

        private string? GetRequestMethod()
        {
            return _httpContextAccessor.HttpContext?.Request.Method;
        }

        private long? GetUserIdFromContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var userIdClaim = user.FindFirst("UserId");
            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
            return null;
        }

        private long? GetPatientIdFromContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var patientIdClaim = user.FindFirst("PatientId");
            if (patientIdClaim != null && long.TryParse(patientIdClaim.Value, out var patientId))
            {
                return patientId;
            }
            return null;
        }

        private long? ExtractFacilityIdFromEntity(object? entity)
        {
            if (entity == null) return null;

            try
            {

                var type = entity.GetType();
                var facilityIdProp = type.GetProperty("FacilityId");
                if (facilityIdProp != null)
                {
                    var value = facilityIdProp.GetValue(entity);
                    if (value is long longValue)
                        return longValue;
                    if (value is long?)
                    {
                        var nullableValue = (long?)value;
                        if (nullableValue.HasValue)
                            return nullableValue.Value;
                    }
                }
            }
            catch
            {

            }

            return null;
        }

        private long? GetFacilityIdFromPatient(long patientId)
        {
            try
            {
                return _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.PatientId == patientId)
                    .Select(p => p.FacilityId)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private long? GetFacilityIdFromContext()
        {

            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var facilityIdClaim = user.FindFirst("FacilityId");
            if (facilityIdClaim != null && long.TryParse(facilityIdClaim.Value, out var facilityId))
            {
                return facilityId;
            }
            return null;
        }

        private long? GetOrganizationIdFromContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var orgIdClaim = user.FindFirst("OrganizationId");
            if (orgIdClaim != null && long.TryParse(orgIdClaim.Value, out var orgId))
            {
                return orgId;
            }
            return null;
        }

        private string? DeriveModuleFromEntityType(string entityType)
        {
            if (string.IsNullOrEmpty(entityType)) return null;

            var moduleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "PT_PatientPrescription", "Prescription" },
                { "PT_PrescriptionMedicine", "Prescription" },
                { "PT_PatientPrescriptionSoapNote", "Prescription" },
                { "PT_Patient", "Patient" },
                { "PT_PatientTreatment", "Treatment" },
                { "PT_PatientAppointmentSlot", "Appointment" },
                { "PT_PatientOrder", "Order" },
                { "PT_PatientPaymentDetail", "Payment" },
                { "Sys_Invoice", "Invoice" },
                { "Sys_InvoicePayment", "Payment" },
                { "SYS_Facility", "Facility" },
                { "SYS_UserDetail", "User" },
                { "SYS_UserCard", "Payment" },
                { "SYS_Login", "User" },
                { "PD_Bundle", "Package" },
                { "PD_Drug", "Product" },
                { "PD_Category", "Category" },
                { "PD_FacilityBundlePrice", "Pricing" },
                { "PC_CLINICTOPATIENT", "Pricing" },
                { "SYS_Pharmacy", "Pharmacy" },
                { "SYS_Ticket", "Ticket" },
                { "SYS_Questionnaire", "IntakeForm" },
                { "SYS_Notification", "Notification" }
            };

            return moduleMap.TryGetValue(entityType, out var module) ? module : null;
        }
    }
}
