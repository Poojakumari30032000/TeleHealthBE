using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.DashBooards
{
    public class GetAllDashboardTilesRequestDTO
    {
        public long? FacilityId { get; set; }
        public long? UserId { get; set; }
        public long? RoleId { get; set; }
        public int? ClientTimezoneOffsetMinutes { get; set; }
    }
}
