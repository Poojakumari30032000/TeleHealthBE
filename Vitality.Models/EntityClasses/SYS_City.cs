using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_City
    {
        public int Id { get; set; }
        public int StateId { get; set; }
        public string Name { get; set; } = null!;
    }
}
