namespace Vitality.Models.DTOs.Chats
{
    public class GetChannelMessagesRequestDTO
    {
        public long? ChannelId { get; set; }
        public long? IndividualReceiverId { get; set; }
        public int? PageNumber { get; set; }
        public int? PageSize { get; set; }
    }
}
