using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.Enums;
using Vitality.Models.EntityClasses;
using Vitality.Services.Stripe;
using Vitality.Models.Security;

namespace Vitality.Controllers
{

    [Route("api/admin/stripe")]
    [ApiController]
    [Authorize]
    [RequiresPermission(Permissions.Facility.View)]
    public class AdminStripeController : ControllerBase
    {
        private readonly IStripeConnectService _stripeConnect;
        private readonly MainContext _db;

        public AdminStripeController(IStripeConnectService stripeConnect, MainContext db)
        {
            _stripeConnect = stripeConnect;
            _db = db;
        }

        [HttpGet("facilities")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<List<StripeFacilityRowDto>>> GetOnboardedFacilities()
        {
            var response = new ApiResponse<List<StripeFacilityRowDto>>();
            var result = new List<StripeFacilityRowDto>();

            var connects = await _db.Sys_FacilityStripeConnects
                .Where(x => x.IsActive == true && x.FacilityId != null && !string.IsNullOrEmpty(x.StripeAccountId))
                .Include(x => x.Facility)
                .ToListAsync();

            foreach (var c in connects)
            {
                if (c.FacilityId == null || string.IsNullOrEmpty(c.StripeAccountId))
                    continue;

                StripeAccountStatusResult? status = null;
                try
                {
                    status = await _stripeConnect.GetAccountStatusAsync(c.StripeAccountId);
                }
                catch
                {

                    continue;
                }

                if (status == null || !string.IsNullOrEmpty(status.Error))
                    continue;

                if (!status.OnboardingComplete && !status.ReadyToProcessPayments)
                    continue;

                result.Add(new StripeFacilityRowDto
                {
                    FacilityId = c.FacilityId!.Value,
                    FacilityName = c.Facility?.TitleLong ?? c.Facility?.TitleShort ?? $"Facility {c.FacilityId}",
                    StripeAccountId = c.StripeAccountId,
                    ChargesEnabled = status.ChargesEnabled,
                    PayoutsEnabled = status.PayoutsEnabled,
                    DetailsSubmitted = status.OnboardingComplete,
                    UpdatedAt = c.ModifiedDate ?? c.CreatedDate
                });
            }

            response.Status = 1;
            response.Message = "OK";
            response.Data = result;
            return response;
        }
    }

    public class StripeFacilityRowDto
    {
        public long FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public string? StripeAccountId { get; set; }
        public bool ChargesEnabled { get; set; }
        public bool PayoutsEnabled { get; set; }
        public bool DetailsSubmitted { get; set; }
        public System.DateTime? UpdatedAt { get; set; }
    }
}
