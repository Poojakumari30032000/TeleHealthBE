using System.Threading.Tasks;

namespace Vitality.Models.Repos.Services.Audit
{
    public interface IAuditService
    {

        Task LogAsync(AuditLogEntry entry);

        void Log(AuditLogEntry entry);

        void LogEntityChange(string action, string entityType, long? entityId, object? oldValues = null, object? newValues = null, long? userId = null, long? patientId = null, long? facilityId = null, long? organizationId = null, string? description = null, string? module = null);

        Task LogEntityChangeAsync(string action, string entityType, long? entityId, object? oldValues = null, object? newValues = null, long? userId = null, long? patientId = null, long? facilityId = null, long? organizationId = null, string? description = null, string? module = null);

        void LogEntityChangeBackground(string action, string entityType, long? entityId, object? oldValues = null, object? newValues = null, long? userId = null, long? patientId = null, long? facilityId = null, long? organizationId = null, string? description = null, string? module = null);

        Task LogUserActionAsync(string action, long? userId, long? patientId = null, string? description = null, string? status = "Success", string? errorMessage = null);

        Task LogApiRequestAsync(string method, string path, long? userId, long? patientId = null, object? requestData = null, string? status = "Success", string? errorMessage = null);
    }

    public class AuditLogEntry
    {
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? Module { get; set; }
        public long? EntityId { get; set; }
        public object? OldValues { get; set; }
        public object? NewValues { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public long? OrganizationId { get; set; }
        public long? FacilityId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestMethod { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; } = "Success";
        public string? ErrorMessage { get; set; }
        public object? AdditionalData { get; set; }
    }
}
