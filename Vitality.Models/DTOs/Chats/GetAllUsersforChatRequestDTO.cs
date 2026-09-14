using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Chats
{
    public class GetAllUsersforChatRequestDTO
    {
        public long? UserId { get; set; }
        public int? RoleId { get; set; }
        public long? FacilityId { get; set; }
    }
}
