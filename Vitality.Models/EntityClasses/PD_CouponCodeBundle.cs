using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_CouponCodeBundle
    {
        public long CouponCodeBundleId { get; set; }
        public long CoupanCodeId { get; set; }
        public long BundleId { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }

        public virtual PD_Bundle Bundle { get; set; } = null!;
        public virtual SYS_CouponCode CoupanCode { get; set; } = null!;
    }
}
