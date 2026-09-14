using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Notifications
{
    public class GetAllNotificationsResponseDTO
    {
        public long NotificationId { get; set; }
        public long? FacilityId { get; set; }
        public string? NotificationType { get; set; }
        public bool? IsRead { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Title { get; set; }
    }
}
