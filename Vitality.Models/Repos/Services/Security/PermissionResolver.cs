using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Services.Security
{
    /// <summary>
    /// Database-backed <see cref="IPermissionResolver"/>.
    /// <para>
    /// Registered scoped, because it takes the DI-scoped <see cref="MainContext"/>.
    /// The cache it uses is the application-wide <see cref="IMemoryCache"/> that
    /// Program.cs already registers.
    /// </para>
    /// </summary>
    public sealed class PermissionResolver : IPermissionResolver
    {
        /// <summary>RoleId of Super Admin, which holds every permission implicitly.</summary>
        private const int SuperAdminRoleId = 1;

        private const string RoleCacheKeyPrefix = "rbac:role-permissions:";
        private const string RoleCacheKeyIndex = "rbac:role-permissions:index";

        /// <summary>
        /// How long a role's grant set is cached. Short, because it is only a safety net:
        /// saving a role calls <see cref="InvalidateRole"/> and the change is immediate.
        /// The expiry only matters if a row is changed directly in SQL.
        /// </summary>
        private static readonly TimeSpan RoleCacheLifetime = TimeSpan.FromMinutes(10);

        private readonly MainContext _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PermissionResolver> _logger;

        public PermissionResolver(MainContext db, IMemoryCache cache, ILogger<PermissionResolver> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger;
        }

        public async Task<UserPermissions> GetForUserAsync(long userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return UserPermissions.None;
            }

            // Read the CURRENT role from the database. Deliberately not the RoleId claim:
            // see IPermissionResolver for why.
            //
            // Written as predicate joins rather than `join ... equals ...` because the
            // keys differ in nullability - SYS_UserDetail.LoginId is long?, SYS_Login.RoleId
            // is int? - and a query-syntax join requires both sides to be the same type.
            // EF still translates this to INNER JOINs.
            var role = await (from userDetail in _db.SYS_UserDetails.AsNoTracking()
                              from login in _db.SYS_Logins.AsNoTracking()
                              from lkRole in _db.LK_Roles.AsNoTracking()
                              where userDetail.UserId == userId
                                    && login.LoginId == userDetail.LoginId
                                    && lkRole.RoleId == login.RoleId
                              select new { lkRole.RoleId, lkRole.RoleName, lkRole.IsActive, lkRole.IsDeleted })
                             .FirstOrDefaultAsync(cancellationToken)
                             .ConfigureAwait(false);

            if (role is null)
            {
                _logger?.LogWarning(
                    "Permission resolution found no role for UserId {UserId}. Treating as no permissions.", userId);
                return UserPermissions.None;
            }

            // A disabled or deleted role grants nothing, even to a user still assigned to it.
            if (role.IsDeleted || !role.IsActive)
            {
                _logger?.LogWarning(
                    "UserId {UserId} holds RoleId {RoleId} which is inactive or deleted. Granting no permissions.",
                    userId, role.RoleId);
                return new UserPermissions(role.RoleId, role.RoleName, false, EmptyCodes());
            }

            if (role.RoleId == SuperAdminRoleId)
            {
                return new UserPermissions(role.RoleId, role.RoleName, isSuperAdmin: true, codes: EmptyCodes());
            }

            var codes = await GetRoleCodesAsync(role.RoleId, cancellationToken).ConfigureAwait(false);

            return new UserPermissions(role.RoleId, role.RoleName, isSuperAdmin: false, codes: codes);
        }

        /// <summary>
        /// Loads and caches one role's permission codes. Only active permissions are
        /// returned, so retiring a catalog entry revokes it without touching grant rows.
        /// </summary>
        private async Task<HashSet<string>> GetRoleCodesAsync(int roleId, CancellationToken cancellationToken)
        {
            var cacheKey = RoleCacheKeyPrefix + roleId.ToString();

            if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
            {
                // Copy: the cached set is shared across requests and must stay immutable.
                return new HashSet<string>(cached, StringComparer.Ordinal);
            }

            var codes = await _db.SYS_RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == roleId && rp.Permission.IsActive)
                .Select(rp => rp.Permission.PermissionCode)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var set = new HashSet<string>(codes, StringComparer.Ordinal);

            _cache.Set(cacheKey, new HashSet<string>(set, StringComparer.Ordinal),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = RoleCacheLifetime });

            TrackCachedRole(roleId);

            return set;
        }

        public void InvalidateRole(int roleId)
        {
            _cache.Remove(RoleCacheKeyPrefix + roleId.ToString());
            _logger?.LogInformation("Invalidated cached permissions for RoleId {RoleId}.", roleId);
        }

        public void InvalidateAll()
        {
            if (_cache.TryGetValue(RoleCacheKeyIndex, out HashSet<int>? roleIds) && roleIds is not null)
            {
                foreach (var roleId in roleIds.ToList())
                {
                    _cache.Remove(RoleCacheKeyPrefix + roleId.ToString());
                }
            }

            _cache.Remove(RoleCacheKeyIndex);
            _logger?.LogInformation("Invalidated all cached role permissions.");
        }

        /// <summary>
        /// IMemoryCache cannot enumerate its keys, so remember which roles have been
        /// cached in order to make <see cref="InvalidateAll"/> possible.
        /// </summary>
        private void TrackCachedRole(int roleId)
        {
            var index = _cache.GetOrCreate(RoleCacheKeyIndex, entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return new HashSet<int>();
            }) ?? new HashSet<int>();

            lock (index)
            {
                index.Add(roleId);
            }
        }

        private static HashSet<string> EmptyCodes() => new HashSet<string>(StringComparer.Ordinal);
    }
}
