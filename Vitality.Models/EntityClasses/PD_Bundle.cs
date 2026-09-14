using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_Bundle
    {
        public PD_Bundle()
        {
            PD_CouponCodeBundles = new HashSet<PD_CouponCodeBundle>();
            PD_FacilityBundlePrices = new HashSet<PD_FacilityBundlePrice>();
            PT_CouponUsages = new HashSet<PT_CouponUsage>();
        }

        public long BundleId { get; set; }
        public long? ProductId { get; set; }
        public long? ActiveDrugId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? RegularImageURL { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Status { get; set; }
        public long? CategoryId { get; set; }

        public long? FacilityId { get; set; }
        public int? visits { get; set; }

        public virtual ICollection<PD_CouponCodeBundle> PD_CouponCodeBundles { get; set; }
        public virtual ICollection<PD_FacilityBundlePrice> PD_FacilityBundlePrices { get; set; }
        public virtual ICollection<PT_CouponUsage> PT_CouponUsages { get; set; }
    }
}
