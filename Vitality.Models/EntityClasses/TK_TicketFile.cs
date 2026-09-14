using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class TK_TicketFile
    {
        public long TicketFileId { get; set; }
        public long? TicketId { get; set; }
        public string? TicketFileURL { get; set; }
        public string? TicketFileName { get; set; }

        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
