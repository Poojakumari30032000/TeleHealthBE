using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.DTOs.FailedEmailLogs;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services;

public class FailedEmailLogsRepo : IFailedEmailLogsRepo
{
    private readonly MainContext _db;

    public FailedEmailLogsRepo(MainContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<(List<FailedEmailLogItemDTO> Items, int TotalCount)> GetPagedAsync(GetFailedEmailLogsRequestDTO request)
    {
        var query = _db.SYS_FailedEmailLogs.AsNoTracking();

        if (request.StartDateUtc.HasValue)
            query = query.Where(x => x.FailedAtUtc >= request.StartDateUtc.Value);

        if (request.EndDateUtc.HasValue)
            query = query.Where(x => x.FailedAtUtc <= request.EndDateUtc.Value);

        if (!string.IsNullOrWhiteSpace(request.ToEmail))
        {
            var email = request.ToEmail.Trim();
            query = query.Where(x => x.ToEmail != null && x.ToEmail.Contains(email));
        }

        var totalCount = await query.CountAsync().ConfigureAwait(false);

        var items = await query
            .OrderByDescending(x => x.FailedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new FailedEmailLogItemDTO
            {
                FailedEmailLogId = x.FailedEmailLogId,
                ToEmail = x.ToEmail,
                ToName = x.ToName,
                Subject = x.Subject,
                ErrorMessage = x.ErrorMessage,
                ExceptionType = x.ExceptionType,
                FailedAtUtc = x.FailedAtUtc
            })
            .ToListAsync().ConfigureAwait(false);

        return (items, totalCount);
    }

    public async Task<FailedEmailLogItemDTO?> GetByIdAsync(long id)
    {
        var entity = await _db.SYS_FailedEmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FailedEmailLogId == id).ConfigureAwait(false);

        if (entity == null)
            return null;

        return new FailedEmailLogItemDTO
        {
            FailedEmailLogId = entity.FailedEmailLogId,
            ToEmail = entity.ToEmail,
            ToName = entity.ToName,
            Subject = entity.Subject,
            ErrorMessage = entity.ErrorMessage,
            ExceptionType = entity.ExceptionType,
            FailedAtUtc = entity.FailedAtUtc
        };
    }
}
