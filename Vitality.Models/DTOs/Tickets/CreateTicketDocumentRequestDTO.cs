namespace DudeMeds.Models.DTOs.Tickets
{
    public class CreateTicketDocumentRequestDTO
    {
        public long TicketId { get; set; }
        public string? Description { get; set; }
        public string? DocumentName { get; set; }
        public string? DocumentUrl { get; set; }
    }
}
