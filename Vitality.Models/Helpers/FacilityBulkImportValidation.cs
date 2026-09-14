using System.Net.Mail;

namespace Vitality.Models.Helpers
{
    public static class FacilityBulkImportValidation
    {
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                _ = new MailAddress(email.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string BuildTitleAddressKey(string titleLong, string address) =>
            $"{titleLong.Trim().ToUpperInvariant()}|{address.Trim().ToUpperInvariant()}";
    }
}
