using System;

namespace Vitality.Models.DTOs.Chats
{
    public class GetChatChannelsRequestDTO
    {
        public long? UserId { get; set; }
        public int? RoleId { get; set; }
        public long? FacilityId { get; set; }
    }
}
