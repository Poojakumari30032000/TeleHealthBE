using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vitality.Models.Repos.Services;

namespace Vitality.Services.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtSettings> settings, ILogger<JwtTokenService> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.Key))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        if (_settings.ExpiryMinutes <= 0)
        {
            _settings.ExpiryMinutes = 60;
        }
    }

    public string CreateToken(TokenSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("LoginId", subject.LoginId.ToString()),
            new Claim("UserId", subject.UserId.ToString())
        };

        if (subject.OrganizationId.HasValue)
        {
            claims.Add(new Claim("OrganizationId", subject.OrganizationId.Value.ToString()));
        }

        if (subject.RoleId.HasValue)
        {
            claims.Add(new Claim("RoleId", subject.RoleId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(subject.RoleName))
        {
            claims.Add(new Claim("RoleName", subject.RoleName));
            claims.Add(new Claim(ClaimTypes.Role, subject.RoleName));
        }

        if (!string.IsNullOrWhiteSpace(subject.RoleTitle))
        {
            claims.Add(new Claim("RoleTitle", subject.RoleTitle));
        }

        if (subject.PatientId.HasValue)
        {
            claims.Add(new Claim("PatientId", subject.PatientId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(_settings.Issuer) ? null : _settings.Issuer,
            audience: string.IsNullOrWhiteSpace(_settings.Audience) ? null : _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: signingCredentials);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.WriteToken(token);

        _logger.LogInformation("Issued JWT for user {UserId} valid until {ExpiresUtc}", subject.UserId, expires.UtcDateTime);

        return jwt;
    }
}
