using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class FC_UsersInFacility
    {
        public long FacilityUserId { get; set; }
        public long? OrganizationId { get; set; }
        public long? FacilityId { get; set; }
        public long? UserId { get; set; }
        public bool? IsAssign { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
    }
}
