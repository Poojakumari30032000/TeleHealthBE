using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Subscriptions
{
    public class GetSubscriptionByFacilityIdResponseDTO
    {
        public long SubscriptionId { get; set; }
        public string? PlanName { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxProviders { get; set; }
        public int? MaxPatients { get; set; }
        public int? TotalUsers { get; set; }
        public int? TotalProviders { get; set; }
        public int? TotalPatients { get; set; }
        public int? Storage { get; set; }
        public string? Status { get; set; }
    }
}
