using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Chats
{
    public class ChatMessageResponseDTO
    {
        public long? Id { get; set; }
        public string? SenderId { get; set; }
        public string? SenderName { get; set; }
        public string? ReceiverId { get; set; }
        public string? ReceiverName { get; set; }

        public long? PatientId { get; set; }
        public long? ChannelId { get; set; }
        public string? ChannelName { get; set; }
        public long? IndividualReceiverId { get; set; }
        public string? IndividualReceiverName { get; set; }
        public string? Content { get; set; }
        public string? Time { get; set; }
        public DateTime? CreatedDate { get; set; }

        public DateTime? SentAt { get; set; }
        public bool? IsRead { get; set; }
        public string? Type { get; set; }
        public string? MessageType { get; set; }
        public string? FileType { get; set; }
        public string? FileName { get; set; }
        public string? FileSize { get; set; }

    }

    public class ChatDeliveredMessageDTO
    {
        public long? Id { get; set; }
        public string SenderId { get; set; }
        public string? SenderName { get; set; }
        public string ReceiverId { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }

        public long? PatientId { get; set; }

        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public long? ChannelId { get; set; }
        public string? MessageType { get; set; }
        public long? IndividualReceiverId { get; set; }
    }
}
