using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Subscriptions
{
    public class UpdateSubscriptionStatusRequestDTO
    {
        public long? SubscriptionId { get; set; }
        public string? Status { get; set; }
    }
}
