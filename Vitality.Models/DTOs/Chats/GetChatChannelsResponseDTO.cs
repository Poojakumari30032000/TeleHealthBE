using System;

namespace Vitality.Models.DTOs.Chats
{
    public class GetChatChannelsResponseDTO
    {
        public long? ChannelId { get; set; }
        public string? ChannelName { get; set; }
        public string? ChannelType { get; set; }

        public long? PatientId { get; set; }
        public long? TreatmentId { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageDate { get; set; }
        public string? LastMessageTime { get; set; }
        public int? UnreadCount { get; set; }
        public bool? IsIndividualMessage { get; set; }
        public long? IndividualReceiverId { get; set; }
        public string? IndividualReceiverName { get; set; }
        public DateTime? ChannelCreatedDate { get; set; }
        public string? PatientProfilePic { get; set; }
    }
}
