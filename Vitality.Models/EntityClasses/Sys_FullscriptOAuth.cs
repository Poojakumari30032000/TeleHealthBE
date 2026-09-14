using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class Sys_FullscriptOAuth
    {
        public long FullscriptOAuthId { get; set; }
        public long? FacilityId { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsActive { get; set; }

        public virtual SYS_Facility? Facility { get; set; }
    }
}
