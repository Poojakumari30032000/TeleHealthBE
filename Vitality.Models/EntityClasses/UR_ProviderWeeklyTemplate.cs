using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class UR_ProviderWeeklyTemplate
    {
        public UR_ProviderWeeklyTemplate()
        {
            UR_ProviderDayHours = new HashSet<UR_ProviderDayHours>();
        }

        public long ProviderWeeklyTemplateId { get; set; }
        public long ProviderId { get; set; }

        public string Timezone { get; set; } = "Etc/UTC";
        public int DefaultSlotDurationMinutes { get; set; } = 10;
        public DateTime? LastMaterializedDate { get; set; }
        public int MaterializationHorizonDays { get; set; } = 30;

        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public long? OrganizationId { get; set; }

        public virtual ICollection<UR_ProviderDayHours> UR_ProviderDayHours { get; set; }
    }
}
