using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using OpenTokSDK;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Vitality.Models.CommonMethods
{
    public static class CommonMethods
    {

        public static bool IsUniqueConstraintViolation(Exception? ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SqlException sql && (sql.Number == 2627 || sql.Number == 2601))
                    return true;
            }
            return false;
        }

        public static string GenerateMRNNumber(long? PatientId)
        {
            DateTime currentDate = DateTime.Now;
            int year = currentDate.Year % 100;
            int month = currentDate.Month;

            string MRNNumber = $"D{year:D2}{month:D2}{PatientId:D2}";
            return MRNNumber;
        }
        public static string GenerateRandomTemporaryPassword(int length = 12)
        {
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string numbers = "0123456789";
            const string symbols = "!@#$%^&*";
            var allChars = upper + lower + numbers + symbols;

            var chars = new char[length];
            chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
            chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
            chars[2] = numbers[RandomNumberGenerator.GetInt32(numbers.Length)];
            chars[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

            for (int i = 4; i < length; i++)
            {
                chars[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
            }

            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }
        public static byte[] Encrypt(string plainText, byte[] key, byte[] IV)
        {
            byte[] encrypted;
            using (AesManaged aes = new AesManaged())
            {
                aes.Key = key;
                aes.IV = IV;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                    }
                    encrypted = ms.ToArray();
                }
            }
            return encrypted;
        }

        // ---------------------------------------------------------------------
        //  OpenTok / Vonage Video credentials.
        //
        //  These used to be literals inside GetSessionIdAndToken() below. They
        //  now come from configuration - "OpenTok:ApiKey" plus the secret
        //  "OpenTok:ApiSecret" - and are handed in by Program.cs at startup.
        //
        //  This class is static and lives outside the DI container, so a static
        //  initialiser is the least invasive way to feed it configuration
        //  without changing GetSessionIdAndToken()'s signature or touching any
        //  of its callers.
        //
        //  The previously hardcoded pair is in source history: treat it as
        //  compromised and rotate it in the Vonage dashboard.
        // ---------------------------------------------------------------------

        private static int _openTokApiKey;
        private static string? _openTokApiSecret;

        /// <summary>
        /// Supplies the OpenTok credentials. Called once during application
        /// startup, before any video session is created.
        /// </summary>
        /// <param name="apiKey">Vonage Video project id ("OpenTok:ApiKey").</param>
        /// <param name="apiSecret">Vonage Video secret ("OpenTok:ApiSecret").</param>
        public static void ConfigureOpenTok(int apiKey, string? apiSecret)
        {
            _openTokApiKey = apiKey;
            _openTokApiSecret = apiSecret;
        }

        /// <summary>
        /// Creates an OpenTok session and a publisher token for a telehealth
        /// video call.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the credentials were never configured, so the failure
        /// names the missing setting instead of surfacing as an opaque
        /// authentication error from the Vonage API.
        /// </exception>
        public static (string, string) GetSessionIdAndToken()
        {
            if (_openTokApiKey <= 0 || string.IsNullOrWhiteSpace(_openTokApiSecret))
            {
                throw new InvalidOperationException(
                    "OpenTok credentials are not configured, so a video session cannot be created. " +
                    "Set 'OpenTok:ApiKey' in appsettings.json and supply the secret 'OpenTok:ApiSecret' via the " +
                    "OpenTok__ApiSecret environment variable (staging/production), or run " +
                    "dotnet user-secrets set \"OpenTok:ApiSecret\" \"<value>\" --project .\\Vitality\\Vitality.csproj " +
                    "(development). See SECRETS.md.");
            }

            OpenTok opentok = new OpenTok(_openTokApiKey, _openTokApiSecret);

            Session session = opentok.CreateSession();
            string sessionId = session.Id;

            string tokenId = session.GenerateToken(role: Role.PUBLISHER);

            return (sessionId, tokenId);
        }

        public static DateTime ToLocalTime(DateTime utcDateTime)
        {
            if (utcDateTime == DateTime.MinValue || utcDateTime == DateTime.MaxValue)
                return utcDateTime;

            return utcDateTime.ToLocalTime();
        }

        public static DateTime? ToLocalTime(DateTime? utcDateTime)
        {
            if (!utcDateTime.HasValue)
                return null;

            if (utcDateTime.Value == DateTime.MinValue || utcDateTime.Value == DateTime.MaxValue)
                return utcDateTime;

            return utcDateTime.Value.ToLocalTime();
        }

        public static DateTime? LocalToUtc(DateTime? localDateTime, int? clientOffsetMinutes)
        {
            if (!localDateTime.HasValue)
                return null;
            if (localDateTime.Value == DateTime.MinValue || localDateTime.Value == DateTime.MaxValue)
                return localDateTime;
            if (!clientOffsetMinutes.HasValue)
                return localDateTime;
            return localDateTime.Value.AddMinutes(-clientOffsetMinutes.Value);
        }

        public static DateTime? UtcToClientLocal(DateTime? utcDateTime, int? clientOffsetMinutes)
        {
            if (!utcDateTime.HasValue)
                return null;
            if (utcDateTime.Value == DateTime.MinValue || utcDateTime.Value == DateTime.MaxValue)
                return utcDateTime;
            if (clientOffsetMinutes.HasValue)
                return utcDateTime.Value.AddMinutes(clientOffsetMinutes.Value);
            return utcDateTime.Value.ToLocalTime();
        }

        public static DateTime UtcToClientLocal(DateTime utcDateTime, int? clientOffsetMinutes)
        {
            if (utcDateTime == DateTime.MinValue || utcDateTime == DateTime.MaxValue)
                return utcDateTime;
            if (clientOffsetMinutes.HasValue)
                return utcDateTime.AddMinutes(clientOffsetMinutes.Value);
            return utcDateTime.ToLocalTime();
        }
    }
}
