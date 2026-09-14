using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Tickets
{
    public class GetAllTicketsRequestDTO
    {

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 25;

        public long? TicketId { get; set; }

        public string? Subject { get; set; }

        public int? Status { get; set; }

        public int? Priority { get; set; }
        public long? AssignedToUserId { get; set; }
    }
}
