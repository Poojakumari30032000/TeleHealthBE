using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_FacilityCategory
    {
        public long FacilityId { get; set; }
        public long CategoryId { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual PD_Category Category { get; set; } = null!;
        public virtual SYS_Facility Facility { get; set; } = null!;
    }
}
