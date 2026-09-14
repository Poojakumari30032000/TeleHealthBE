using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Chat
    {
        public SYS_Chat()
        {
            SYS_ChatReadReceipts = new HashSet<SYS_ChatReadReceipt>();
        }

        public long Id { get; set; }
        public long? SenderId { get; set; }
        public long? ReceiverId { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsRead { get; set; }
        public long? ChannelId { get; set; }
        public long? IndividualReceiverId { get; set; }
        public string? MessageType { get; set; }

        public virtual SYS_ChatChannel? Channel { get; set; }
        public virtual ICollection<SYS_ChatReadReceipt> SYS_ChatReadReceipts { get; set; }
    }
}
