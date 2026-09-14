using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetProductDashboardTilesResponseDTO
    {
        public int? TotalOrders { get; set; }
        public int? TotalPatients { get; set; }
        public decimal? TotalRevenue { get; set; }
    }
}
