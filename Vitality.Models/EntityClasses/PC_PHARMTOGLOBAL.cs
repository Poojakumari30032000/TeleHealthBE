using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PC_PHARMTOGLOBAL
    {
        public PC_PHARMTOGLOBAL()
        {
            PC_GATOCLINICs = new HashSet<PC_GATOCLINIC>();
        }

        public long PharmToGlobalId { get; set; }
        public long? DrugId { get; set; }
        public decimal? PharmacyPrice { get; set; }
        public decimal? MarkupPercent { get; set; }
        public string? MarkupType { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public decimal? WholesalePrice { get; set; }

        public virtual PD_Drug? Drug { get; set; }
        public virtual ICollection<PC_GATOCLINIC> PC_GATOCLINICs { get; set; }
    }
}
