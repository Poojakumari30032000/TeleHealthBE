using BCrypt.Net;

namespace Vitality.Models.Repos.Services.Auth;

public class PasswordHasher : IPasswordHasher
{

    private const int WorkFactor = 12;

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        }

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            return false;
        }

        try
        {

            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch
        {

            return false;
        }
    }

    public bool IsHashed(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return password.StartsWith("$2a$", StringComparison.Ordinal) ||
               password.StartsWith("$2b$", StringComparison.Ordinal) ||
               password.StartsWith("$2x$", StringComparison.Ordinal) ||
               password.StartsWith("$2y$", StringComparison.Ordinal);
    }
}
