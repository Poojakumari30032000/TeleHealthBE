using Vitality.Models.Helpers;
using Xunit;

namespace Vitality.Models.Tests;

public class FacilityBulkImportValidationTests
{
    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("user.name+tag@example.com", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("not-an-email", false)]
    [InlineData("@nodomain.com", false)]
    public void IsValidEmail_accepts_common_cases(string? email, bool expected) =>
        Assert.Equal(expected, FacilityBulkImportValidation.IsValidEmail(email));

    [Fact]
    public void BuildTitleAddressKey_is_case_insensitive_on_title_and_address()
    {
        var k1 = FacilityBulkImportValidation.BuildTitleAddressKey("  Acme Clinic ", " 123 Main ");
        var k2 = FacilityBulkImportValidation.BuildTitleAddressKey("acme clinic", "123 main");
        Assert.Equal(k1, k2);
    }
}
