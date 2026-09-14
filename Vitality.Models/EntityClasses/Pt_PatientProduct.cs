using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class Pt_PatientProduct
    {
        public long PatientProductId { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? FacilityId { get; set; }
        public bool? IsActive { get; set; }
    }
}
