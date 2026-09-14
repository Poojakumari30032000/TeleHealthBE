using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class BD_ChartColor
    {
        public long ChartColorId { get; set; }
        public long? BrandId { get; set; }
        public string? ChartColor { get; set; }
    }
}
