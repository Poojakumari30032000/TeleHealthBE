using System;
using System.ComponentModel.DataAnnotations;

namespace Vitality.Models.EntityClasses
{
    public partial class PC_DRUGFACILITYEXCLUSION
    {
        [Key]
        public long DrugFacilityExclusionId { get; set; }

        public long DrugId { get; set; }
        public long FacilityId { get; set; }

        public bool? IsActive { get; set; }

        public long? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }
}
