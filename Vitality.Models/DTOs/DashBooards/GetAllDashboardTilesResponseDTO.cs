using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.DashBooards
{
    public class GetAllDashboardTilesResponseDTO
    {
        public int? Index { get; set; }
        public string? TileName { get; set; }
        public object? Value { get; set; }
    }
}
