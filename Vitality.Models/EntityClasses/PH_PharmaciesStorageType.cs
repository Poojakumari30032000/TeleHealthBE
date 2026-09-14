using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PH_PharmaciesStorageType
    {
        public long PharmacyStorageId { get; set; }
        public long? PharmacyId { get; set; }
        public string? StorageType { get; set; }
    }
}
