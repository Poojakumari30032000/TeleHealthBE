using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_BroadcastMessage
    {
        public long BroadcastMessageId { get; set; }
        public DateTime? BroadcastDate { get; set; }
        public string? Description { get; set; }
    }
}
