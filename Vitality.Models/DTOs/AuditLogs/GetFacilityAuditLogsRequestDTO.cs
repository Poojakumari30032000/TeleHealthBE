using System;

namespace Vitality.Models.DTOs.AuditLogs
{
    public class GetFacilityAuditLogsRequestDTO
    {
        public long FacilityId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Action { get; set; }
        public string? EntityType { get; set; }
        public string? Module { get; set; }
        public long? UserId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class GetUserAuditLogsRequestDTO
    {
        public long UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Action { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class GetEntityAuditLogsRequestDTO
    {
        public string EntityType { get; set; } = string.Empty;
        public long EntityId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class GetAllAuditLogsRequestDTO
    {
        public long? FacilityId { get; set; }
        public long? OrganizationId { get; set; }
        public long? UserId { get; set; }
        public string? Action { get; set; }
        public string? EntityType { get; set; }
        public string? Module { get; set; }
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class GetPatientAuditLogsRequestDTO
    {
        public long PatientId { get; set; }
        public long? FacilityId { get; set; }
        public string? Action { get; set; }
        public string? Module { get; set; }
        public string? EntityType { get; set; }
        public long? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class GetAuditLogsResponseDTO
    {
        public long AuditLogId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? Module { get; set; }
        public long? EntityId { get; set; }
        public long? UserId { get; set; }
        public string? UserName { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public long? OrganizationId { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestMethod { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedDate { get; set; }
        public object? OldValues { get; set; }
        public object? NewValues { get; set; }
        public object? AdditionalData { get; set; }
    }

    public class GetAuditLogStatisticsResponseDTO
    {
        public long FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public int TotalActions { get; set; }
        public int CreateActions { get; set; }
        public int UpdateActions { get; set; }
        public int DeleteActions { get; set; }
        public int FailedActions { get; set; }
        public int UniqueUsers { get; set; }
        public Dictionary<string, int> ActionsByType { get; set; } = new();
        public Dictionary<string, int> ActionsByEntity { get; set; } = new();
    }
}
