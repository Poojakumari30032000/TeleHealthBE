using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class TK_TicketComment
    {
        public long TicketCommentId { get; set; }
        public long? TicketId { get; set; }
        public string? Comment { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
