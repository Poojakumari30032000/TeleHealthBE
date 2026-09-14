using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class UR_ProviderCategory
    {
        public long ProviderCategoryId { get; set; }
        public long? ProviderId { get; set; }
        public long? CategoryId { get; set; }
    }
}
