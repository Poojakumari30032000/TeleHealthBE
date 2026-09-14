using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class UR_ProviderStateLicense
    {
        public long ProviderStateLicenseId { get; set; }
        public long? ProviderId { get; set; }
        public long? StateId { get; set; }
        public string? StateLicense { get; set; }
    }
}
