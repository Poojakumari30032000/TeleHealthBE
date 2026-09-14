using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Subscriptions;

namespace Vitality.Models.Repos.Interfaces
{
    public interface ISubscriptionsRepo
    {
        public List<GetAllSubscriptionsResponseDTO> GetAllSubscriptions();
        public GetSubscriptionByIdResponseDTO GetSubscriptionById(long SubscriptionId);
        public bool SaveSubscription(SaveSubscriptionRequestDTO request, long UserId);
        public bool DeleteSubscription(long SubscriptionId);
        public bool UpdateSubscriptionStatus(UpdateSubscriptionStatusRequestDTO request);
        public GetSubscriptionByFacilityIdResponseDTO GetSubscriptionByFacilityId(string? FacilityGuid);
        bool UpdateGlobalSubscription(UpdateGlobalSubscriptionRequestDTO request, long userId);
        GetSubscriptionByIdResponseDTO? GetGlobalSubscription();
    }
}
