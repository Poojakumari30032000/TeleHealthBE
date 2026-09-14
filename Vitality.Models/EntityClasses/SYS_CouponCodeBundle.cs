using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.EntityClasses
{
    public class SYS_CouponCodeBundle
    {
        public long CoupanCodeId { get; set; }
        public long BundleId { get; set; }

        public SYS_CouponCode Coupon { get; set; } = null!;
        public PD_Bundle Bundle { get; set; } = null!;
    }
}
