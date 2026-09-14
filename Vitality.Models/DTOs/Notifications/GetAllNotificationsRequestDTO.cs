using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Notifications
{
    public class GetAllNotificationsRequestDTO
    {
        public long? FacilityId { get; set; }
        public long? UserId { get; set; }
        public long? RoleId { get; set; }
    }
}
