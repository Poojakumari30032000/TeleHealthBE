namespace Vitality.Models.DTOs.Chats
{
    public class SendChannelMessageRequestDTO
    {
        public long? ChannelId { get; set; }
        public string? Content { get; set; }
        public long? IndividualReceiverId { get; set; }
    }
}
