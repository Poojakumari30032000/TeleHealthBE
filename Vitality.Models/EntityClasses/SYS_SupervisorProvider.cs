using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_SupervisorProvider
    {
        public long SupervisorProviderId { get; set; }
        public long? SupervisorId { get; set; }
        public long? ProviderId { get; set; }
    }
}
