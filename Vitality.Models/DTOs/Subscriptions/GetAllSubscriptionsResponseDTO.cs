using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Subscriptions
{
    public class GetAllSubscriptionsResponseDTO
    {
        public long SubscriptionId { get; set; }
        public string? PlanName { get; set; }
        public decimal? MonthlyPrice { get; set; }
        public decimal? AnnualPrice { get; set; }
        public decimal? SetupFee { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxProviders { get; set; }
        public int? MaxPatients { get; set; }
        public int? Storage { get; set; }
        public string? Status { get; set; }
        public string? BillingCycle { get; set; }
        public int? ContractTeam { get; set; }
        public string? SupportLevel { get; set; }
        public int? TrainingHours { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsGlobal { get; set; }
    }
}
