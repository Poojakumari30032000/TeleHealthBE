using System.IO;
using System.Linq;
using Vitality.Models.EntityClasses;
using Vitality.Models.Helpers;
using Xunit;

namespace Vitality.Models.Tests;

/// <summary>
/// TEL-20. The mapping API takes codes typed by an administrator; the TEL-19
/// importer takes them from a published file. These assert the two end up with
/// the same string, because the mapping table joins to the reference table on
/// exactly that string.
/// </summary>
public class ClinicalCodeFormatTests
{
    [Theory]
    [InlineData("E11.65", "E1165")]
    [InlineData("e11.65", "E1165")]
    [InlineData("  E11.65  ", "E1165")]
    [InlineData("S72.001A", "S72001A")]
    [InlineData("A00", "A00")]
    [InlineData("99213", "99213")]
    [InlineData("0042t", "0042T")]
    public void Normalize_strips_the_dot_whitespace_and_case(string typed, string expected) =>
        Assert.Equal(expected, ClinicalCodeFormat.Normalize(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_of_nothing_is_an_empty_string(string? typed) =>
        Assert.Equal(string.Empty, ClinicalCodeFormat.Normalize(typed));

    [Theory]
    [InlineData("A00")]
    [InlineData("C4A")]
    [InlineData("E1165")]
    [InlineData("S72001A")]
    [InlineData("U071")]
    [InlineData("QA00101")]      // FY2026 introduced a letter in the second position
    public void Accepts_icd10cm_codes(string code) =>
        Assert.True(ClinicalCodeFormat.IsValid(ClinicalCodeSystem.Icd10Cm, code));

    [Theory]
    [InlineData("E1")]           // too short
    [InlineData("E116500Z")]     // too long
    [InlineData("1165")]         // does not start with a letter
    [InlineData("E11.65")]       // not normalised - the dot must be gone first
    [InlineData("e1165")]        // not normalised - case must be folded first
    [InlineData("")]
    public void Rejects_malformed_icd10cm_codes(string code) =>
        Assert.False(ClinicalCodeFormat.IsValid(ClinicalCodeSystem.Icd10Cm, code));

    [Theory]
    [InlineData("99213")]
    [InlineData("0001F")]
    [InlineData("0042T")]
    [InlineData("0509U")]
    public void Accepts_cpt_codes(string code) =>
        Assert.True(ClinicalCodeFormat.IsValid(ClinicalCodeSystem.Cpt, code));

    [Theory]
    [InlineData("9921")]         // four characters
    [InlineData("992133")]       // six characters
    [InlineData("0001G")]        // not a category II / III suffix
    [InlineData("E1165")]        // an ICD code is not a CPT code
    public void Rejects_malformed_cpt_codes(string code) =>
        Assert.False(ClinicalCodeFormat.IsValid(ClinicalCodeSystem.Cpt, code));

    [Theory]
    [InlineData(null)]
    [InlineData("ICD9")]
    [InlineData("SNOMED")]
    public void Rejects_a_code_under_an_unknown_code_system(string? codeSystem) =>
        Assert.False(ClinicalCodeFormat.IsValid(codeSystem, "E1165"));

    [Theory]
    [InlineData("E1165", "E11.65")]
    [InlineData("S72001A", "S72.001A")]
    [InlineData("A00", "A00")]        // three characters take no dot
    [InlineData("C4A", "C4A")]
    public void Icd10cm_display_form_puts_the_dot_after_the_category(string stored, string expected) =>
        Assert.Equal(expected, ClinicalCodeFormat.ToDisplayCode(ClinicalCodeSystem.Icd10Cm, stored));

    [Theory]
    [InlineData("99213")]
    [InlineData("0042T")]
    public void Cpt_display_form_is_the_code_itself(string stored) =>
        Assert.Equal(stored, ClinicalCodeFormat.ToDisplayCode(ClinicalCodeSystem.Cpt, stored));

    [Theory]
    [InlineData("ICD10CM", "ICD10CM")]
    [InlineData("icd10cm", "ICD10CM")]
    [InlineData(" cpt ", "CPT")]
    public void Normalizes_a_known_code_system(string typed, string expected) =>
        Assert.Equal(expected, ClinicalCodeFormat.NormalizeCodeSystem(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ICD-10-CM")]
    [InlineData("HCPCS")]
    public void Refuses_an_unknown_code_system(string? typed) =>
        Assert.Null(ClinicalCodeFormat.NormalizeCodeSystem(typed));

    [Theory]
    [InlineData("Category", "Category")]
    [InlineData("category", "Category")]
    [InlineData("SERVICE", "Service")]
    [InlineData(" package ", "Package")]
    public void Normalizes_a_known_target_type(string typed, string expected) =>
        Assert.Equal(expected, ClinicalCodeFormat.NormalizeTargetType(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bundle")]
    [InlineData("Encounter")]
    public void Refuses_an_unknown_target_type(string? typed) =>
        Assert.Null(ClinicalCodeFormat.NormalizeTargetType(typed));

    /// <summary>
    /// The load-bearing one: what the importer stores and what an administrator
    /// types have to normalise to the same string, or the mapping joins to
    /// nothing and every code silently reads back as "not in force".
    /// </summary>
    [Fact]
    public void A_typed_code_normalises_to_what_the_importer_stored()
    {
        var line = $"{1:D5} {"E1165",-7} 1 {"Type 2 diabetes with hyperglycemia",-60} "
                 + "Type 2 diabetes mellitus with hyperglycemia";

        var parsed = ClinicalCodeFileParser.ParseIcd10CmOrderFile(new StringReader(line));
        Assert.True(parsed.IsValid);

        var stored = parsed.Codes.Single();
        Assert.Equal("E1165", stored.Code);
        Assert.Equal("E11.65", stored.DisplayCode);

        Assert.Equal(stored.Code, ClinicalCodeFormat.Normalize("E11.65"));
        Assert.Equal(stored.Code, ClinicalCodeFormat.Normalize("e11.65"));
        Assert.Equal(stored.DisplayCode,
            ClinicalCodeFormat.ToDisplayCode(ClinicalCodeSystem.Icd10Cm, stored.Code));
    }
}
