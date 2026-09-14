using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Ticket
    {
        public long TicketId { get; set; }
        public long? OrganizationId { get; set; }
        public long? FacilityId { get; set; }

        public long? AssignedToUserId { get; set; }
        public string? Subject { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }

        public string? Priority { get; set; }

        public string? ContactEmail { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
