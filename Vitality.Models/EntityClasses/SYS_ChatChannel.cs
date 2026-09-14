using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_ChatChannel
    {
        public SYS_ChatChannel()
        {
            SYS_ChatChannelParticipants = new HashSet<SYS_ChatChannelParticipant>();
            SYS_Chats = new HashSet<SYS_Chat>();
        }

        public long ChannelId { get; set; }
        public string? ChannelName { get; set; }
        public string? ChannelType { get; set; }
        public long? TreatmentId { get; set; }
        public long? PatientId { get; set; }
        public long? ProviderId { get; set; }
        public long? FacilityId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual ICollection<SYS_ChatChannelParticipant> SYS_ChatChannelParticipants { get; set; }
        public virtual ICollection<SYS_Chat> SYS_Chats { get; set; }
    }
}
