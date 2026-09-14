using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_DrugVarientsInBundle
    {
        public long DrugVarientBundleId { get; set; }
        public long? BundleId { get; set; }
        public long? DrugId { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public int? OrderCount { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
