using System.Threading.Tasks;
using Vitality.Models.DTOs.FailedEmailLogs;

namespace Vitality.Models.Repos.Interfaces;

public interface IFailedEmailLogsRepo
{
    Task<(System.Collections.Generic.List<FailedEmailLogItemDTO> Items, int TotalCount)> GetPagedAsync(GetFailedEmailLogsRequestDTO request);
    Task<FailedEmailLogItemDTO?> GetByIdAsync(long id);
}
