using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_FacilityBundlePrice
    {
        public long FacilityBundlePriceId { get; set; }
        public long FacilityId { get; set; }
        public long BundleId { get; set; }
        public decimal ClinicPrice { get; set; }
        public bool? IsRecurring { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDateUtc { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDateUtc { get; set; }

        public virtual PD_Bundle Bundle { get; set; } = null!;
        public virtual SYS_Facility Facility { get; set; } = null!;
    }
}
