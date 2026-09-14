using AutoMapper;
using DudeMeds.Models.DTOs.Tickets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Subscriptions;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{
    public class SubscriptionsRepo :BaseRepo , ISubscriptionsRepo
    {
        private readonly IMapper _mapper;
        public SubscriptionsRepo(IMapper mapper)
        {
            _mapper = mapper;
        }

        public List<GetAllSubscriptionsResponseDTO> GetAllSubscriptions()
        {
            List<GetAllSubscriptionsResponseDTO> finalresponse = new List<GetAllSubscriptionsResponseDTO>();
            List<SYS_Subscription> subscription = _db.SYS_Subscriptions.Where(x => x.IsActive == true).ToList();
            finalresponse = _mapper.Map< List<GetAllSubscriptionsResponseDTO>>(subscription);
            return finalresponse;
        }

        public GetSubscriptionByIdResponseDTO GetSubscriptionById(long SubscriptionId)
        {
            GetSubscriptionByIdResponseDTO response = new GetSubscriptionByIdResponseDTO();
            SYS_Subscription subscription = _db.SYS_Subscriptions.Where(x => x.SubscriptionId == SubscriptionId).FirstOrDefault();
            response = _mapper.Map<GetSubscriptionByIdResponseDTO>(subscription);
            return response;
        }
        public bool SaveSubscription(SaveSubscriptionRequestDTO request, long UserId)
        {
            try
            {
                SYS_Subscription subscription = new SYS_Subscription();
                if (request.SubscriptionId == 0)
                {
                    subscription = _mapper.Map<SYS_Subscription>(request);
                    subscription.CreatedBy = UserId;
                    subscription.CreatedDate = DateTime.UtcNow;
                    subscription.IsActive = true;
                    _db.SYS_Subscriptions.Add(subscription);
                    _db.SaveChanges();
                }
                else
                {
                    subscription = _db.SYS_Subscriptions.Where(x => x.SubscriptionId == request.SubscriptionId).FirstOrDefault();
                    _mapper.Map(request, subscription);

                    subscription.ModifiedBy = UserId;
                    subscription.ModifiedDate = DateTime.UtcNow;
                    _db.SaveChanges();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteSubscription(long SubscriptionId)
        {
            try
            {
                SYS_Subscription subscription = _db.SYS_Subscriptions.Where(x => x.SubscriptionId == SubscriptionId).FirstOrDefault();
                subscription.IsActive = false;
                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool UpdateSubscriptionStatus(UpdateSubscriptionStatusRequestDTO request)
        {
            SYS_Subscription subscription = _db.SYS_Subscriptions.Where(x => x.SubscriptionId == request.SubscriptionId).FirstOrDefault();
            subscription.Status = request.Status;
            _db.SaveChanges();
            return true;

        }

        public bool UpdateGlobalSubscription(UpdateGlobalSubscriptionRequestDTO request, long userId)
        {
            if (request == null) return false;

            var globalSubscription = _db.SYS_Subscriptions
                .FirstOrDefault(x => x.IsGlobal == true);

            if (globalSubscription == null)
            {
                globalSubscription = new SYS_Subscription
                {
                    PlanName = "Global Monthly Fee",
                    MonthlyPrice = request.MonthlyPrice,
                    IsActive = true,
                    IsGlobal = true,
                    Status = "Active",
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow
                };
                _db.SYS_Subscriptions.Add(globalSubscription);
            }
            else
            {
                globalSubscription.MonthlyPrice = request.MonthlyPrice;
                globalSubscription.ModifiedBy = userId;
                globalSubscription.ModifiedDate = DateTime.UtcNow;
            }

            _db.SaveChanges();
            return true;
        }

        public GetSubscriptionByIdResponseDTO? GetGlobalSubscription()
        {
            var globalSubscription = _db.SYS_Subscriptions
                .FirstOrDefault(x => x.IsGlobal == true);
            if (globalSubscription == null) return null;
            return _mapper.Map<GetSubscriptionByIdResponseDTO>(globalSubscription);
        }

        public GetSubscriptionByFacilityIdResponseDTO GetSubscriptionByFacilityId(string? FacilityGuid)
        {
            GetSubscriptionByFacilityIdResponseDTO response = new GetSubscriptionByFacilityIdResponseDTO();
            SYS_Facility facility = _db.SYS_Facilities.Where(x => x.Guid == FacilityGuid).FirstOrDefault();
            if (facility != null)
            {
                SYS_Subscription subscription = _db.SYS_Subscriptions.Where(x => x.SubscriptionId == facility.SubscriptionPlanId).FirstOrDefault();
                if (subscription != null)
                {
                    response = _mapper.Map<GetSubscriptionByFacilityIdResponseDTO>(subscription);
                }
                response.TotalUsers = _db.FC_UsersInFacilities.Join(_db.SYS_UserDetails,
                  urf => urf.UserId,
                  u => u.UserId,
                  (urf, u) => new { UR_F = urf, U = u }).Where(x => x.UR_F.FacilityId == facility.FacilityId && x.U.Login != null && x.U.Login.RoleId != 4 && x.U.IsActive == true).Count();
                response.TotalProviders = _db.FC_UsersInFacilities.Join(_db.SYS_UserDetails,
                  urf => urf.UserId,
                  u => u.UserId,
                  (urf, u) => new { UR_F = urf, U = u }).Where(x => x.UR_F.FacilityId == facility.FacilityId && x.U.Login != null && x.U.Login.RoleId == 4 && x.U.IsActive == true).Count();
                response.TotalPatients = _db.PT_Patients.Where(x => x.FacilityId == facility.FacilityId && x.IsActive == true).Count();

            }
            return response;
        }
    }
}
