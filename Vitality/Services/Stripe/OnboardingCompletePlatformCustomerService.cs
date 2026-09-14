using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Services.Stripe
{

    public class OnboardingCompletePlatformCustomerService : IOnboardingCompletePlatformCustomerService
    {
        private readonly MainContext _db;
        private readonly StripeClient _stripeClient;
        private readonly IUsersRepo _usersRepo;

        public OnboardingCompletePlatformCustomerService(MainContext db, StripeClient stripeClient, IUsersRepo usersRepo)
        {
            _db = db;
            _stripeClient = stripeClient ?? throw new System.ArgumentNullException(nameof(stripeClient));
            _usersRepo = usersRepo;
        }

        public async Task<bool> EnsurePlatformCustomerForConnectedAccountAsync(string stripeConnectedAccountId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(stripeConnectedAccountId))
                return false;

            var facilityId = await _db.Sys_FacilityStripeConnects
                .AsNoTracking()
                .Where(x => x.IsActive == true && x.StripeAccountId == stripeConnectedAccountId.Trim())
                .Select(x => x.FacilityId)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);

            if (!facilityId.HasValue || facilityId.Value <= 0)
                return false;

            return await EnsurePlatformCustomerForFacilityAsync(facilityId.Value, ct).ConfigureAwait(false);
        }

        public async Task<bool> EnsurePlatformCustomerForFacilityAsync(long facilityId, CancellationToken ct = default)
        {
            var adminUser = await (from uif in _db.FC_UsersInFacilities
                                   join ud in _db.SYS_UserDetails on uif.UserId equals ud.UserId
                                   join l in _db.SYS_Logins on ud.LoginId equals l.LoginId
                                   where uif.FacilityId == facilityId
                                         && l.RoleId == 3
                                         && (ud.IsActive == true || ud.IsActive == null)
                                         && (ud.Status == "Active" || ud.Status == null)
                                         && (uif.IsAssign == true || uif.IsAssign == null)
                                   select new { ud.UserId, ud.StripePlatformCustomerId })
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);

            if (adminUser == null)
                return false;

            if (!string.IsNullOrWhiteSpace(adminUser.StripePlatformCustomerId))
                return true;

            var customerService = new CustomerService(_stripeClient);
            var options = new CustomerCreateOptions
            {
                Metadata = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["facilityId"] = facilityId.ToString(),
                    ["userId"] = adminUser.UserId.ToString()
                }
            };

            Customer customer;
            try
            {
                customer = await customerService.CreateAsync(options, null, ct).ConfigureAwait(false);
            }
            catch (StripeException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(customer.Id))
                return false;

            var updated = _usersRepo.SetStripePlatformCustomerId(adminUser.UserId, customer.Id);
            return updated;
        }
    }
}
