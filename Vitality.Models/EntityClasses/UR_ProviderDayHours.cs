using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class UR_ProviderDayHours
    {
        public UR_ProviderDayHours()
        {
            UR_ProviderTimeRanges = new HashSet<UR_ProviderTimeRange>();
        }

        public long ProviderDayHoursId { get; set; }
        public long ProviderWeeklyTemplateId { get; set; }
        public byte DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public int? SlotDurationMinutes { get; set; }

        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual UR_ProviderWeeklyTemplate? ProviderWeeklyTemplate { get; set; }
        public virtual ICollection<UR_ProviderTimeRange> UR_ProviderTimeRanges { get; set; }
    }
}
