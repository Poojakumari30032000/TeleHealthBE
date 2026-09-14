using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class Sys_FacilitySquareCred
    {
        public long FacilitySquareCredId { get; set; }
        public long? FacilityId { get; set; }
        public string? ApplicationId { get; set; }
        public string? LocationId { get; set; }
        public bool? IsActive { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public string? MerchantId { get; set; }

        public virtual SYS_Facility? Facility { get; set; }
    }
}
