namespace Vitality.Models.Repos.Services.Auth;

public interface IPasswordHasher
{

    string HashPassword(string password);

    bool VerifyPassword(string password, string hashedPassword);

    bool IsHashed(string password);
}
