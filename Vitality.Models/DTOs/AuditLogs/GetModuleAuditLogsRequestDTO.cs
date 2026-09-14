using System;

namespace Vitality.Models.DTOs.AuditLogs
{

    public class GetModuleAuditLogsRequestDTO
    {
        public string Module { get; set; } = string.Empty;
        public long? FacilityId { get; set; }
        public long? EntityId { get; set; }
        public string? Action { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
