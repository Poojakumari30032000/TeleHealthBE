using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_Catalog
    {
        public PD_Catalog()
        {
            PD_Drugs = new HashSet<PD_Drug>();
            PC_CATALOGFACILITYASSIGNMENTs = new HashSet<PC_CATALOGFACILITYASSIGNMENT>();
        }

        public long CatalogId { get; set; }
        public string CatalogName { get; set; } = null!;
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsSystemDefined { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual ICollection<PD_Drug> PD_Drugs { get; set; }
        public virtual ICollection<PC_CATALOGFACILITYASSIGNMENT> PC_CATALOGFACILITYASSIGNMENTs { get; set; }
    }
}
