using Vitality.Models.EntityClasses;
using Vitality.Models.Helpers;
using Xunit;

namespace Vitality.Models.Tests;

public class ClinicalCodeSearchTests
{
    private static ClinicalCodeSearchTerms Icd(string? q) => ClinicalCodeSearch.Parse(q, ClinicalCodeSystem.Icd10Cm);
    private static ClinicalCodeSearchTerms Cpt(string? q) => ClinicalCodeSearch.Parse(q, ClinicalCodeSystem.Cpt);

    [Theory]
    [InlineData("E11.65", "E1165")]
    [InlineData("e1165", "E1165")]
    [InlineData(" E11.6 ", "E116")]
    [InlineData("E11.", "E11")]
    [InlineData("QA0.0101", "QA00101")] // FY2026: letter in the second position
    [InlineData("S72.001A", "S72001A")]
    public void Icd10_code_is_normalised_without_the_dot(string query, string expected)
    {
        Assert.Equal(expected, Icd(query).Code);
    }

    [Theory]
    [InlineData("type 2 diabetes")] // has spaces
    [InlineData("12345")]           // ICD codes start with a letter
    [InlineData("E11.65.123")]      // too long once the dots go
    [InlineData("diabetes")]        // eight letters: longer than any ICD code
    [InlineData("E11-65")]          // characters no code has
    public void Icd10_query_that_cannot_be_a_code_has_no_code(string query)
    {
        Assert.Null(Icd(query).Code);
    }

    [Theory]
    [InlineData("99213", "99213")]
    [InlineData("0001f", "0001F")]
    [InlineData("992", "992")]
    public void Cpt_code_shape(string query, string expected)
    {
        Assert.Equal(expected, Cpt(query).Code);
    }

    [Fact]
    public void Cpt_code_is_at_most_five_characters()
    {
        Assert.Null(Cpt("992131").Code);
    }

    [Fact]
    public void Phrase_is_upper_cased_with_whitespace_collapsed()
    {
        var terms = Icd("  type   2\tdiabetes ");
        Assert.Equal("TYPE 2 DIABETES", terms.Phrase);
        Assert.Equal(new[] { "TYPE", "2", "DIABETES" }, terms.Words);
    }

    [Fact]
    public void Words_drop_edge_punctuation_and_duplicates_but_keep_inner_hyphens()
    {
        var terms = Icd("(non-pressure) ulcer, ulcer");
        Assert.Equal(new[] { "NON-PRESSURE", "ULCER" }, terms.Words);
    }

    [Fact]
    public void Words_are_capped()
    {
        var terms = Icd("one two three four five six seven eight");
        Assert.Equal(ClinicalCodeSearch.MaxWords, terms.Words.Count);
    }

    [Fact]
    public void Query_is_truncated_to_the_maximum_length()
    {
        var terms = Icd(new string('a', 500));
        Assert.Equal(ClinicalCodeSearch.MaxQueryLength, terms.Phrase.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public void Too_short_to_search(string? query)
    {
        Assert.False(Icd(query).IsSearchable);
    }

    [Fact]
    public void Two_characters_is_searchable()
    {
        Assert.True(Icd("E1").IsSearchable);
    }

    [Theory]
    [InlineData("100%", "100[%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[x]", "[[]x]")]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    public void Like_wildcards_in_user_input_are_escaped(string input, string expected)
    {
        Assert.Equal(expected, ClinicalCodeSearch.EscapeLike(input));
    }

    [Theory]
    [InlineData(0, ClinicalCodeSearch.DefaultPageSize)]
    [InlineData(-5, ClinicalCodeSearch.DefaultPageSize)]
    [InlineData(10, 10)]
    [InlineData(1000, ClinicalCodeSearch.MaxPageSize)]
    public void Page_size_is_clamped(int requested, int expected)
    {
        Assert.Equal(expected, ClinicalCodeSearch.ClampPageSize(requested));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(3, 3)]
    public void Page_number_starts_at_one(int requested, int expected)
    {
        Assert.Equal(expected, ClinicalCodeSearch.ClampPageNumber(requested));
    }

    [Fact]
    public void Match_ranks_are_ordered_best_first()
    {
        Assert.True(ClinicalCodeMatchRank.ExactCode < ClinicalCodeMatchRank.CodePrefix);
        Assert.True(ClinicalCodeMatchRank.CodePrefix < ClinicalCodeMatchRank.DescriptionStartsWith);
        Assert.True(ClinicalCodeMatchRank.DescriptionStartsWith < ClinicalCodeMatchRank.DescriptionWordStartsWith);
        Assert.True(ClinicalCodeMatchRank.DescriptionWordStartsWith < ClinicalCodeMatchRank.DescriptionContains);
        Assert.True(ClinicalCodeMatchRank.DescriptionContains < ClinicalCodeMatchRank.DescriptionAllWords);
    }
}
