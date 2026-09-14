using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PC_GATOCLINIC
    {
        public PC_GATOCLINIC()
        {
            PC_CLINICTOPATIENTs = new HashSet<PC_CLINICTOPATIENT>();
        }

        public long GAtoClinicId { get; set; }
        public long PharmToGlobalId { get; set; }
        public decimal SuggestedRetailPrice { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public virtual PC_PHARMTOGLOBAL PharmToGlobal { get; set; } = null!;
        public virtual ICollection<PC_CLINICTOPATIENT> PC_CLINICTOPATIENTs { get; set; }
    }
}
