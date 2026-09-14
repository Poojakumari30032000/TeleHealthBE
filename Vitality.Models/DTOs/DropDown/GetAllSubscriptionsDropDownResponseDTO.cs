using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.DropDown
{
    public class GetAllSubscriptionsDropDownResponseDTO
    {
        public long? SubscriptionId { get; set; }
        public string? PlanName { get; set; }
    }
}
