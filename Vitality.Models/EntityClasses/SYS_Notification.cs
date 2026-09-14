using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Notification
    {
        public long NotificationId { get; set; }
        public long? UserId { get; set; }
        public string? NotificationType { get; set; }
        public bool? IsRead { get; set; }
        public long? PatientId { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? FacilityId { get; set; }
    }
}
