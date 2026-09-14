using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Tickets
{
    public class GetAllTicketsResponseDTO
    {

        public long Id { get; set; }
        public long TicketId { get; set; }

        public string? Title { get; set; }

        public int Priority { get; set; }

        public int Status { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? FacilityId { get; set; }
        public long? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public int? CommentCount { get; set; }
        public bool? IsActive { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
