using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_ForgetPassword
    {
        public long ForgetPasswordId { get; set; }
        public long? LoginId { get; set; }
        public string? Code { get; set; }
    }
}
