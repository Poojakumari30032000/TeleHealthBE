using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{

    public interface IStripeAccountResolver
    {
        Task<string?> GetStripeAccountIdForFacilityAsync(long facilityId, CancellationToken ct = default);
    }
}
