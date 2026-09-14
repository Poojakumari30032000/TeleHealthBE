using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_CouponUsage
    {
        public long CouponUsageId { get; set; }
        public long? PatientId { get; set; }
        public long? BundleId { get; set; }
        public long? CouponCodeId { get; set; }
        public string? CouponCode { get; set; }
        public long? PatientPaymentId { get; set; }
        public long? PatientOrderId { get; set; }
        public DateTime? UsedDate { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }

        public virtual PD_Bundle? Bundle { get; set; }
        public virtual SYS_CouponCode? CouponCodeNavigation { get; set; }
        public virtual PT_Patient? Patient { get; set; }
        public virtual PT_PatientOrder? PatientOrder { get; set; }
        public virtual PT_PatientPaymentDetail? PatientPayment { get; set; }
    }
}
