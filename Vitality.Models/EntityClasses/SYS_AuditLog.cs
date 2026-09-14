using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_AuditLog
    {
        public long AuditLogId { get; set; }
        public string Action { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public long? EntityId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public long? OrganizationId { get; set; }
        public long? FacilityId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestMethod { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? AdditionalData { get; set; }
        public string? Module { get; set; }
    }
}
