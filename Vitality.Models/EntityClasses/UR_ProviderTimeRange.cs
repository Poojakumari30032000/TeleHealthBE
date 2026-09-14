using System;

namespace Vitality.Models.EntityClasses
{
    public partial class UR_ProviderTimeRange
    {
        public long ProviderTimeRangeId { get; set; }
        public long ProviderDayHoursId { get; set; }
        public TimeSpan StartTimeLocal { get; set; }
        public TimeSpan EndTimeLocal { get; set; }
        public int SortOrder { get; set; }

        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual UR_ProviderDayHours? ProviderDayHours { get; set; }
    }
}
