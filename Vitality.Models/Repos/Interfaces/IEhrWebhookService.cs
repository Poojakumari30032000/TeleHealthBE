using System.Threading;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IEhrWebhookService
    {
        Task NotifyBookingCreatedAsync(
            long? facilityId,
            long? patientId,
            string? bundleName,
            bool isRecurring,
            decimal payment,
            CancellationToken ct = default);
    }
}
