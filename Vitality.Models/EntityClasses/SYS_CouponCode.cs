using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_CouponCode
    {
        public SYS_CouponCode()
        {
            PD_CouponCodeBundles = new HashSet<PD_CouponCodeBundle>();
            PT_CouponUsages = new HashSet<PT_CouponUsage>();
        }

        public long CoupanCodeId { get; set; }
        public long? FacilityId { get; set; }
        public string CoupanCode { get; set; } = null!;
        public decimal Discount { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public byte DiscountType { get; set; }
        public bool? AppliesToRecurring { get; set; }

        public virtual ICollection<PD_CouponCodeBundle> PD_CouponCodeBundles { get; set; }
        public virtual ICollection<PT_CouponUsage> PT_CouponUsages { get; set; }
    }
}
