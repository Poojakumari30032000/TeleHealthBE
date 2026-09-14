using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{

    public interface IOnboardingCompletePlatformCustomerService
    {

        Task<bool> EnsurePlatformCustomerForConnectedAccountAsync(string stripeConnectedAccountId, CancellationToken ct = default);
    }
}
