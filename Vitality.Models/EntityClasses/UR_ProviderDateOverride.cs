using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class UR_ProviderDateOverride
    {
        public UR_ProviderDateOverride()
        {
            UR_ProviderDateOverrideTimeRanges = new HashSet<UR_ProviderDateOverrideTimeRange>();
        }

        public long ProviderDateOverrideId { get; set; }
        public long ProviderId { get; set; }
        public DateTime OverrideDate { get; set; }
        public bool IsClosed { get; set; }
        public string? Note { get; set; }
        public int? SlotDurationMinutes { get; set; }

        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public long? OrganizationId { get; set; }

        public virtual ICollection<UR_ProviderDateOverrideTimeRange> UR_ProviderDateOverrideTimeRanges { get; set; }
    }
}
