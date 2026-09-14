using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Login
    {
        public SYS_Login()
        {
            SYS_UserDetails = new HashSet<SYS_UserDetail>();
        }

        public long LoginId { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public int? RoleId { get; set; }
        public string? ProfileUrl { get; set; }
        public bool? IsBroadcastMessageRead { get; set; }

        public virtual ICollection<SYS_UserDetail> SYS_UserDetails { get; set; }
    }
}
