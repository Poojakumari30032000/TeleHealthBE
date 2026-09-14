using System;

namespace Vitality.Models.EntityClasses
{
    public partial class PC_CATALOGFACILITYASSIGNMENT
    {
        public long CatalogFacilityAssignmentId { get; set; }
        public long CatalogId { get; set; }
        public long FacilityId { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual PD_Catalog? Catalog { get; set; }
    }
}
