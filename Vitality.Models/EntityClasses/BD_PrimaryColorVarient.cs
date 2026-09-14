using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class BD_PrimaryColorVarient
    {
        public long PrimaryColorVarientId { get; set; }
        public long? BrandId { get; set; }
        public string? PrimaryColorVarient { get; set; }
    }
}
