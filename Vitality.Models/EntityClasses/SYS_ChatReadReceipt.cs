using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_ChatReadReceipt
    {
        public long Id { get; set; }
        public long ChatId { get; set; }
        public long UserId { get; set; }
        public DateTime ReadDate { get; set; }
        public bool? IsActive { get; set; }

        public virtual SYS_Chat Chat { get; set; } = null!;
    }
}
