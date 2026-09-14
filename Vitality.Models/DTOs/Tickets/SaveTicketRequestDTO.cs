using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Tickets
{
    public class SaveTicketRequestDTO
    {
        public long TicketId { get; set; }
        public long? AssignedToUserId { get; set; }

        public string? Subject { get; set; }
        public string? Description { get; set; }

        public int? Status { get; set; }

        public int? Priority { get; set; }

        public string? ContactEmail { get; set; }
        public string? Name { get; set; }

    }
    public class SaveTicketFilesRequestDTO
    {
        public string? TicketFileURL { get; set; }
        public string? TicketFileName { get; set; }
    }

    public class SaveTicketCommentRequestDTO
    {
        public string? Comment { get; set; }
    }
}
