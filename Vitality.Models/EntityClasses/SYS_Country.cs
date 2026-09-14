using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Country
    {
        public short Id { get; set; }
        public string ShortName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int PhoneCode { get; set; }
    }
}
