using System;
using System.Threading;
using System.Threading.Tasks;
using DudeMeds.Models.DTOs.ProviderHours;

namespace DudeMeds.Models.Repos.Interfaces
{

    public interface IProviderHoursRepo
    {

        Task<WeekViewDto> GetWeekAsync(long providerId, DateTime weekStartDate, long organizationId, string? clientTimezone = null, CancellationToken ct = default);

        Task SaveDayAsync(long providerId, byte dayOfWeek, SaveDayRequest request, long userId, long organizationId, CancellationToken ct = default);

        Task SaveDateOverrideAsync(long providerId, DateOverrideRequest request, long userId, long organizationId, CancellationToken ct = default);

        Task DeleteDateOverrideAsync(long providerId, DateTime date, long userId, long organizationId, CancellationToken ct = default);

        Task EnsureTemplateExistsAsync(long providerId, long organizationId, long createdByUserId, string? clientTimezone = null, CancellationToken ct = default);
    }
}
