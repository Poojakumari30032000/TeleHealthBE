using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Chats
{
    public class OnlineUserDTO
    {
        public string? UserId { get; set; }
        public string? ConnectionId { get; set; }
        public string? UserName { get; set; }
        public bool? IsOnline { get; set; }

        public long? PatientId { get; set; }
        public string? LastMessage { get; set; }
        public string? LastMsgTime { get; set; }
        public string? ProfilePic { get; set; }
        public bool? IsTyping { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int UnreadCount { get; set; }

        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
    }
}
