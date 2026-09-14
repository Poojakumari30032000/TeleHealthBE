using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vitality.Models.Repos.Services.Security;

namespace Vitality.Filters;

/// <summary>
/// Requires the caller to hold at least one of the named permission codes.
/// <para>
/// This is the server-side half of RBAC, and it is the half that actually protects
/// anything. The reference project had no equivalent - its authorization was
/// <c>*ngIf</c> and <c>[disabled]</c> on buttons, which any client can ignore - so
/// this is written to the existing project's conventions rather than ported.
/// </para>
/// <para>
/// Semantics deliberately mirror the Angular <c>PermissionGuard</c> and
/// <c>[appHasAnyPermission]</c> directive: <b>any-of</b>, not all-of. An endpoint
/// gated on the same codes as the UI element that calls it therefore behaves
/// identically, and hiding a button is backed by a real 403.
/// </para>
/// <example>
/// <code>
/// [HttpPost("deleteProduct")]
/// [RequiresPermission(Permissions.Product.Delete)]
/// public async Task&lt;ApiResponse&lt;bool&gt;&gt; DeleteProduct(...)
/// </code>
/// </example>
/// <para>
/// Composes with <see cref="AuthorizeRolesAttribute"/>: apply that for coarse role
/// gating and this for the specific operation. Both run, and both must pass.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>
    /// Per-request memo key. Several filters can run for one request (a class-level
    /// attribute plus a method-level one), and permission resolution costs a database
    /// round trip, so the resolved set is cached on the HttpContext for the request.
    /// </summary>
    private const string HttpContextItemKey = "__Vitality_RBAC_UserPermissions";

    private readonly string[] _permissionCodes;

    /// <param name="permissionCodes">
    /// Codes from <see cref="Vitality.Models.Security.Permissions"/>. Use the constants,
    /// not literals, so a typo is a build error rather than an endpoint that denies
    /// everyone.
    /// </param>
    public RequiresPermissionAttribute(params string[] permissionCodes)
    {
        _permissionCodes = permissionCodes?
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Respect [AllowAnonymous], the same way AuthorizeRolesAttribute does, so a
        // public endpoint inside an otherwise-protected controller still works.
        if (HasAllowAnonymous(context))
        {
            return;
        }

        // An attribute with no codes protects nothing. Fail closed and say so loudly:
        // silently allowing would turn a typo into an open endpoint.
        if (_permissionCodes.Length == 0)
        {
            GetLogger(context)?.LogError(
                "[RequiresPermission] on {Endpoint} declares no permission codes. Denying the request. " +
                "This is a coding error - name at least one code from Vitality.Models.Security.Permissions.",
                context.ActionDescriptor.DisplayName);

            context.Result = Forbidden();
            return;
        }

        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userId = ResolveUserId(user);
        if (userId <= 0)
        {
            GetLogger(context)?.LogWarning(
                "Authenticated principal carries no usable UserId claim. Denying {Endpoint}.",
                context.ActionDescriptor.DisplayName);

            context.Result = Forbidden();
            return;
        }

        var permissions = await GetUserPermissionsAsync(context, userId).ConfigureAwait(false);

        if (permissions.HasAny(_permissionCodes))
        {
            return;
        }

        // Log the detail server-side; return a generic message. The response deliberately
        // does not name the required permission or the caller's role - that would hand an
        // attacker the permission taxonomy one probe at a time.
        GetLogger(context)?.LogWarning(
            "Permission denied for UserId {UserId} (RoleId {RoleId}) on {Endpoint}. Required any of: {Required}.",
            userId, permissions.RoleId, context.ActionDescriptor.DisplayName, string.Join(", ", _permissionCodes));

        context.Result = Forbidden();
    }

    /// <summary>
    /// Resolves the caller's permissions, memoized for the lifetime of the request.
    /// </summary>
    private static async Task<UserPermissions> GetUserPermissionsAsync(
        AuthorizationFilterContext context, long userId)
    {
        if (context.HttpContext.Items.TryGetValue(HttpContextItemKey, out var cached)
            && cached is UserPermissions cachedPermissions)
        {
            return cachedPermissions;
        }

        var resolver = context.HttpContext.RequestServices.GetRequiredService<IPermissionResolver>();

        var permissions = await resolver
            .GetForUserAsync(userId, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        context.HttpContext.Items[HttpContextItemKey] = permissions;

        return permissions;
    }

    /// <summary>
    /// Reads the caller's UserId from the signed token.
    /// <para>
    /// Prefers the custom <c>"UserId"</c> claim. <c>sub</c> is checked too, but note
    /// that JwtBearer's default inbound claim mapping renames <c>sub</c> to
    /// <c>ClaimTypes.NameIdentifier</c>, so that is checked as well - the same
    /// belt-and-braces order StripeConnectController already uses.
    /// </para>
    /// <para>
    /// This value is only trusted because the JWT signature was verified by the
    /// authentication middleware before any filter ran. Everything else - which role
    /// the user holds, what that role grants - is read from the database.
    /// </para>
    /// </summary>
    private static long ResolveUserId(System.Security.Claims.ClaimsPrincipal user)
    {
        var raw = user.FindFirst("UserId")?.Value
                  ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(raw, out var userId) ? userId : 0L;
    }

    private static bool HasAllowAnonymous(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            return true;
        }

        return context.ActionDescriptor?.FilterDescriptors
            .Any(f => f.Filter is AllowAnonymousAttribute || f.Filter is IAllowAnonymous) == true;
    }

    /// <summary>
    /// 403 shaped like the project's existing authorization failures - the same
    /// <c>{ Status, Message, ErrorCode }</c> body AuthorizeRolesAttribute returns for
    /// FACILITY_DISABLED - so the Angular error handling needs no special case.
    /// </summary>
    private static ObjectResult Forbidden() => new ObjectResult(new
    {
        Status = 0,
        Message = "You do not have permission to perform this action.",
        ErrorCode = "PERMISSION_DENIED"
    })
    {
        StatusCode = StatusCodes.Status403Forbidden
    };

    private static ILogger? GetLogger(AuthorizationFilterContext context) =>
        context.HttpContext.RequestServices
            .GetService<ILoggerFactory>()
            ?.CreateLogger("Vitality.Filters.RequiresPermission");
}
