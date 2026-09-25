using DudeMeds.Models.DTOs.ClinicalCodes;
using Vitality.Models.Helpers;
using Xunit;

namespace Vitality.Models.Tests;

public class SoapNoteCodeRequestValidatorTests
{
    private static SoapNoteCodeItemRequestDTO Item(string system, string code, long version = 1) =>
        new() { CodeSystem = system, Code = code, CodeSetVersionId = version };

    [Fact]
    public void Empty_or_missing_list_is_valid_and_clears_the_note()
    {
        Assert.Empty(SoapNoteCodeRequestValidator.Normalize(null).Codes);
        var (codes, errors) = SoapNoteCodeRequestValidator.Normalize(new List<SoapNoteCodeItemRequestDTO>());
        Assert.Empty(codes);
        Assert.Empty(errors);
    }

    [Fact]
    public void Codes_are_normalised_and_numbered_in_request_order()
    {
        var (codes, errors) = SoapNoteCodeRequestValidator.Normalize(new[]
        {
            Item(" icd10cm ", "e11.65", 7),
            Item("CPT", "99213", 9),
        });

        Assert.Empty(errors);
        Assert.Equal(new NormalizedSoapNoteCode("ICD10CM", 7, "E1165", 1), codes[0]);
        Assert.Equal(new NormalizedSoapNoteCode("CPT", 9, "99213", 2), codes[1]);
    }

    [Fact]
    public void Duplicate_code_is_rejected_whatever_its_spelling()
    {
        var (codes, errors) = SoapNoteCodeRequestValidator.Normalize(new[]
        {
            Item("ICD10CM", "E11.65"),
            Item("ICD10CM", "e1165"),
        });

        Assert.Single(codes);
        Assert.Contains(errors, e => e.Contains("E1165") && e.Contains("more than once"));
    }

    [Fact]
    public void Same_code_text_in_two_systems_is_not_a_duplicate()
    {
        var (codes, errors) = SoapNoteCodeRequestValidator.Normalize(new[]
        {
            Item("ICD10CM", "A0001"),
            Item("CPT", "A0001"),
        });

        Assert.Empty(errors);
        Assert.Equal(2, codes.Count);
    }

    [Theory]
    [InlineData("SNOMED", "123", 1, "code system")]
    [InlineData("", "E1165", 1, "code system")]
    [InlineData("ICD10CM", "", 1, "1 to 8 characters")]
    [InlineData("ICD10CM", "E11.651234", 1, "1 to 8 characters")] // nine characters once the dot goes
    [InlineData("ICD10CM", "E1165", 0, "code set version")]
    public void Malformed_items_are_rejected(string system, string code, long version, string expected)
    {
        var (codes, errors) = SoapNoteCodeRequestValidator.Normalize(new[] { Item(system, code, version) });
        Assert.Empty(codes);
        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void Null_item_is_rejected()
    {
        var (_, errors) = SoapNoteCodeRequestValidator.Normalize(new SoapNoteCodeItemRequestDTO[] { null! });
        Assert.Single(errors);
    }

    [Fact]
    public void Too_many_codes_is_rejected_outright()
    {
        var items = Enumerable.Range(0, SoapNoteCodeRequestValidator.MaxCodesPerNote + 1)
            .Select(i => Item("ICD10CM", $"A{i:D3}"))
            .ToList();

        var (codes, errors) = SoapNoteCodeRequestValidator.Normalize(items);
        Assert.Empty(codes);
        Assert.Single(errors);
    }

    [Fact]
    public void The_maximum_is_allowed()
    {
        var items = Enumerable.Range(0, SoapNoteCodeRequestValidator.MaxCodesPerNote)
            .Select(i => Item("ICD10CM", $"A{i:D3}"))
            .ToList();

        var (codes, errors) = SoapNoteCodeRequestValidator.Normalize(items);
        Assert.Empty(errors);
        Assert.Equal(SoapNoteCodeRequestValidator.MaxCodesPerNote, codes.Count);
    }
}
