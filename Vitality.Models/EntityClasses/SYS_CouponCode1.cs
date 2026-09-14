using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_CouponCode1
    {
        public long CoupanCodeId { get; set; }
        public long? FacilityId { get; set; }
        public string? CoupanCode { get; set; }
        public int? DiscountPercentage { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
