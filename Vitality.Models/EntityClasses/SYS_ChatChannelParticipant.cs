using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_ChatChannelParticipant
    {
        public long ParticipantId { get; set; }
        public long? ChannelId { get; set; }
        public long? UserId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? JoinedDate { get; set; }
        public DateTime? LeftDate { get; set; }

        public virtual SYS_ChatChannel? Channel { get; set; }
    }
}
