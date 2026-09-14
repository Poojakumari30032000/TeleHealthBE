using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_Category
    {
        public PD_Category()
        {
            PD_FacilityCategories = new HashSet<PD_FacilityCategory>();
        }

        public long CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryDescription { get; set; }
        public string? ImageURL { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }

        public virtual ICollection<PD_FacilityCategory> PD_FacilityCategories { get; set; }
    }
}
