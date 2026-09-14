using System.Threading.Tasks;
using Vitality.Models.DTOs.EmpowerPharmacy;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IEmpowerWebhookService
    {

        Task<EmpowerWebhookResponseDTO> ProcessWebhookAsync(EmpowerWebhookRequestDTO webhook);

        Task<Sys_EmpowerOrder?> GetOrderByClientOrderIdAsync(string clientOrderId);

        Task<Sys_EmpowerOrder?> GetOrderByEipOrderIdAsync(int eipOrderId);
    }
}
