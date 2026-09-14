using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_State
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public short CountryId { get; set; }
        public string? ShortName { get; set; }
    }
}
