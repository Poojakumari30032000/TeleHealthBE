using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{

    public interface IStripeSavePaymentMethod
    {

        Task SavePaymentMethodFromPaymentIntentAsync(string paymentIntentId, string stripeAccountId, long userId, CancellationToken ct = default);

        Task SavePaymentMethodFromSetupIntentAsync(string setupIntentId, string stripeAccountId, long userId, CancellationToken ct = default);
    }
}
