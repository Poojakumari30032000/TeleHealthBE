using Vitality.Services.Auth;

namespace Vitality.Models.Repos.Services;

public interface IJwtTokenService
{
    string CreateToken(TokenSubject subject);
}
