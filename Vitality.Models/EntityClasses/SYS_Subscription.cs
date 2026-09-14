using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Subscription
    {
        public SYS_Subscription()
        {
            Sys_Invoices = new HashSet<Sys_Invoice>();
        }

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
        public bool? IsActive { get; set; }
        public bool? IsGlobal { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual ICollection<Sys_Invoice> Sys_Invoices { get; set; }
    }
}
