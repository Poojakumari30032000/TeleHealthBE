namespace DudeMeds.Models.DTOs.Tickets
{
    public class AddTicketCommentRequestDTO
    {
        public long TicketId { get; set; }
        public string? Comment { get; set; }
    }
}
