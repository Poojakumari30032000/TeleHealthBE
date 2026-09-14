using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{

    public sealed class StripePlatformGaChargeContext
    {

        public string? ConnectedAccountId { get; init; }

        public long? FacilityId { get; init; }
    }

    public interface IStripePlatformCharge
    {

        Task<string?> ChargeCustomerAsync(
            string platformCustomerId,
            long amountInCents,
            string currency,
            CancellationToken ct = default,
            StripePlatformGaChargeContext? gaContext = null);
    }
}
