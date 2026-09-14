using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{

    public partial class Sys_FacilityStripeConnect
    {
        public long FacilityStripeConnectId { get; set; }

        public long? FacilityId { get; set; }

        public string? StripeAccountId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual SYS_Facility? Facility { get; set; }
    }
}
