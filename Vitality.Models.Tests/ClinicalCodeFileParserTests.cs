using System;
using System.IO;
using System.Linq;
using System.Text;
using Vitality.Models.Helpers;
using Xunit;

namespace Vitality.Models.Tests;

public class ClinicalCodeFileParserTests
{
    /// <summary>A line in the CMS order-file layout: 5 / 7 / 1 / 60 / rest, single-space separated.</summary>
    private static string OrderLine(int order, string code, char flag, string shortDesc, string longDesc) =>
        $"{order:D5} {code,-7} {flag} {shortDesc,-60} {longDesc}";

    private static ClinicalCodeParseResult ParseIcd(params string[] lines) =>
        ClinicalCodeFileParser.ParseIcd10CmOrderFile(new StringReader(string.Join("\n", lines)));

    [Fact]
    public void OrderLine_helper_matches_the_published_column_positions()
    {
        var line = OrderLine(1, "A00", '0', "Cholera", "Cholera");
        Assert.Equal("A00", line.Substring(6, 7).Trim());
        Assert.Equal('0', line[14]);
        Assert.Equal("Cholera", line.Substring(16, 60).Trim());
        Assert.Equal("Cholera", line.Substring(77));
    }

    [Fact]
    public void Parses_header_and_billable_codes()
    {
        var result = ParseIcd(
            OrderLine(1, "A00", '0', "Cholera", "Cholera"),
            OrderLine(2, "A000", '1', "Cholera due to Vibrio cholerae 01, biovar cholerae",
                "Cholera due to Vibrio cholerae 01, biovar cholerae"),
            OrderLine(9999, "E1165", '1', "Type 2 diabetes mellitus with hyperglycemia",
                "Type 2 diabetes mellitus with hyperglycemia"));

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Codes.Count);

        var header = result.Codes[0];
        Assert.Equal("A00", header.Code);
        Assert.Equal("A00", header.DisplayCode);
        Assert.False(header.IsBillable);
        Assert.Equal(1, header.SortOrder);

        var e1165 = result.Codes[2];
        Assert.Equal("E1165", e1165.Code);
        Assert.Equal("E11.65", e1165.DisplayCode);
        Assert.True(e1165.IsBillable);
        Assert.Equal("Type 2 diabetes mellitus with hyperglycemia", e1165.LongDescription);
        Assert.Equal(9999, e1165.SortOrder);
    }

    [Fact]
    public void Accepts_fy2026_codes_with_a_letter_in_second_position()
    {
        // Real FY2026 codes: category QA0 is the first with a letter second.
        var result = ParseIcd(
            OrderLine(30493, "QA0", '0', "Neurodev disord related to specific genetic patho variants",
                "Neurodevelopmental disorders related to specific genetic pathogenic variants"),
            OrderLine(30497, "QA00101", '1', "Neurodev disord", "Neurodevelopmental disorder"));

        Assert.True(result.IsValid);
        Assert.Equal("QA0.0101", result.Codes[1].DisplayCode);
    }

    [Fact]
    public void Skips_blank_lines_and_tolerates_crlf()
    {
        var text = OrderLine(1, "A00", '0', "Cholera", "Cholera") + "\r\n\r\n"
                 + OrderLine(2, "A000", '1', "Cholera due to", "Cholera due to Vibrio cholerae") + "\r\n";

        var result = ClinicalCodeFileParser.ParseIcd10CmOrderFile(new StringReader(text));

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Codes.Count);
    }

    [Fact]
    public void Rejects_bad_lines_with_their_line_numbers()
    {
        var result = ParseIcd(
            OrderLine(1, "A00", '0', "Cholera", "Cholera"),
            "too short",
            OrderLine(3, "A000", '2', "Bad flag", "Bad flag"),
            OrderLine(4, "123", '1', "Not a code", "Not a code"),
            OrderLine(5, "A00", '0', "Duplicate", "Duplicate"));

        Assert.False(result.IsValid);
        Assert.Equal(4, result.ErrorCount);
        Assert.StartsWith("Line 2:", result.Errors[0]);
        Assert.StartsWith("Line 3:", result.Errors[1]);
        Assert.StartsWith("Line 4:", result.Errors[2]);
        Assert.Contains("more than once", result.Errors[3]);
    }

    [Fact]
    public void Empty_file_is_not_valid()
    {
        var result = ParseIcd("", "   ");
        Assert.False(result.IsValid);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void Error_list_is_capped_but_the_count_is_exact()
    {
        var lines = Enumerable.Range(1, 120).Select(_ => "short").ToArray();
        var result = ParseIcd(lines);

        Assert.Equal(120, result.ErrorCount);
        Assert.Equal(ClinicalCodeParseResult.MaxErrorsReported, result.Errors.Count);
    }

    [Theory]
    [InlineData("A00", "A00")]
    [InlineData("A000", "A00.0")]
    [InlineData("E1165", "E11.65")]
    [InlineData("S72001A", "S72.001A")]
    public void Display_code_puts_the_dot_after_the_category(string code, string expected) =>
        Assert.Equal(expected, ClinicalCodeFileParser.ToIcd10DisplayCode(code));

    [Fact]
    public void Fiscal_year_label_maps_to_october_through_september()
    {
        var window = ClinicalCodeFileParser.Icd10CmFiscalYearWindow("FY2026");

        Assert.NotNull(window);
        Assert.Equal(new DateTime(2025, 10, 1), window!.Value.Effective);
        Assert.Equal(new DateTime(2026, 9, 30), window.Value.Termination);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2026")]
    [InlineData("FY26")]
    [InlineData("April 2026 update")]
    public void Other_labels_have_no_default_window(string? label) =>
        Assert.Null(ClinicalCodeFileParser.Icd10CmFiscalYearWindow(label));

    [Fact]
    public void Cpt_tab_file_with_header_is_parsed()
    {
        var text = new StringBuilder()
            .AppendLine("Code\tLong description\tShort description")
            .AppendLine("99213\tOffice or other outpatient visit, established patient\tOffice visit est pt")
            .AppendLine("0001F\tHeart failure assessed")
            .ToString();

        var result = ClinicalCodeFileParser.ParseCptTabDelimited(new StringReader(text));

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Codes.Count);
        Assert.Equal("99213", result.Codes[0].Code);
        Assert.Equal("Office visit est pt", result.Codes[0].ShortDescription);
        Assert.Null(result.Codes[1].ShortDescription);
    }

    [Fact]
    public void Cpt_rejects_codes_that_are_not_cpt()
    {
        var result = ClinicalCodeFileParser.ParseCptTabDelimited(new StringReader("E1165\tNot a CPT code\n9921\tToo short"));

        Assert.False(result.IsValid);
        Assert.Equal(2, result.ErrorCount);
    }
}
