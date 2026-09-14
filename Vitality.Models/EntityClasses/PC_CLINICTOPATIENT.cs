using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PC_CLINICTOPATIENT
    {
        public long ClinicToPatientId { get; set; }
        public long DrugId { get; set; }
        public long FacilityId { get; set; }
        public long? GAtoClinicId { get; set; }
        public decimal ClinicSuggestedRetailPrice { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public virtual PD_Drug Drug { get; set; } = null!;
        public virtual PC_GATOCLINIC? GAtoClinic { get; set; }
    }
}
