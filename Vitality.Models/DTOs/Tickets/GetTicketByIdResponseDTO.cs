using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Tickets
{

    public class GetTicketByIdResponseDTO
    {
        public long Id { get; set; }
        public long TicketId { get; set; }
        public string? Title { get; set; }

        public int Priority { get; set; }

        public int Status { get; set; }
        public string? CreatedBy { get; set; }

        public long? CreatedByUserId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Description { get; set; }
        public long? FacilityId { get; set; }
        public long? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public string? Type { get; set; }
        public List<SaveTicketFilesResponseDTO>? Documents { get; set; }
        public List<SaveTicketCommentResponseDTO>? Comments { get; set; }

    }

    public class SaveTicketFilesResponseDTO
    {
        public long? TicketFileId { get; set; }
        public string? TicketFileURL { get; set; }
        public string? TicketFileName { get; set; }
        public string? Description { get; set; }
        public string? CreatedDate { get; set; }
    }

    public class SaveTicketCommentResponseDTO
    {
        public long? TicketCommentId { get; set; }
        public string? Comment { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
        public string? CreatedByImage { get; set; }
    }
}
