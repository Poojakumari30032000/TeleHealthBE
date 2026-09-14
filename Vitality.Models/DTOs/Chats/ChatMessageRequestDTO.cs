using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Chats
{
    public class ChatMessageRequestDTO
    {
        public string? SenderId { get; set; }
        public string? ReceiverId { get; set; }
        public long? ChannelId { get; set; }
        public long? IndividualReceiverId { get; set; }
        public string? Content { get; set; }
    }
}
