using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vitality.Models.EntityClasses;
using Vitality.Services.Notifications;
using Xunit;

namespace Vitality.Models.Tests.Notifications;

/// <summary>
/// Checks what the SQL Server provider actually makes of the invoice lookup.
/// <para>
/// InvoiceLookupTests runs the predicate against an in-memory set, which proves the
/// expression tree is right but says nothing about how EF Core 8 translates it - and
/// translation is the whole of TEL-13. The bug being guarded against is a silent one:
/// a conversion inside the WHERE that stops matching because of width or collation,
/// with no exception and no log line.
/// </para>
/// <para>
/// So these tests read the SQL. ToQueryString compiles the query through the real
/// SqlServer provider and returns the command text with its parameter declarations,
/// and it opens no connection - the connection string below is never dialled. That
/// makes the translation checkable in CI, where there is no database.
/// </para>
/// <para>
/// What a live run still adds, and these cannot: that the rows are there and come
/// back. That is smoke test row 8 of UPGRADE-NET8.md section 3, which needs the
/// environment from TEL-10.
/// </para>
/// </summary>
public class InvoiceLookupTranslationTests
{
    /// <summary>Parsed for its shape only; no connection is opened.</summary>
    private const string UnusedConnectionString =
        "Server=localhost;Database=TranslationOnly;Trusted_Connection=True;TrustServerCertificate=True";

    private static string SqlFor(string? invoiceNumber)
    {
        var options = new DbContextOptionsBuilder<MainContext>()
            .UseSqlServer(UnusedConnectionString)
            .Options;

        using var db = new MainContext(options);

        return db.Sys_Invoices.Where(InvoiceLookup.MatchesNumberOrId(invoiceNumber)).ToQueryString();
    }

    /// <summary>
    /// Returns the type a parameter is DECLAREd with, e.g. "varchar(50)" - the line
    /// reads <c>DECLARE @__invoiceNumber_0 varchar(50) = '...';</c>.
    /// </summary>
    private static string DeclaredTypeOf(string sql, string parameterName)
    {
        var match = Regex.Match(
            sql,
            $@"DECLARE\s+{Regex.Escape(parameterName)}\s+(?<type>[A-Za-z]+(\(\s*[-\dA-Za-z, ]+\s*\))?)");

        Assert.True(match.Success, $"No DECLARE found for {parameterName} in:\n{sql}");

        return match.Groups["type"].Value;
    }

    private static string ParameterComparedTo(string sql, string column)
    {
        var match = Regex.Match(sql, $@"\[{Regex.Escape(column)}\]\s*=\s*(?<param>@[A-Za-z0-9_]+)");

        Assert.True(match.Success, $"[{column}] is not compared against a parameter in:\n{sql}");

        return match.Groups["param"].Value;
    }

    [Fact]
    public void An_id_reference_compares_int_to_int_and_varchar_to_varchar()
    {
        var sql = SqlFor("42");

        var idParameter = ParameterComparedTo(sql, "InvoiceId");
        var numberParameter = ParameterComparedTo(sql, "InvoiceNumber");

        // InvoiceId is int and InvoiceNumber is varchar(50) (IsUnicode(false) in
        // MainContext). Each parameter is sent as its column's own type, so neither
        // side of either comparison needs converting.
        Assert.Equal("int", DeclaredTypeOf(sql, idParameter));
        Assert.Equal("varchar(50)", DeclaredTypeOf(sql, numberParameter));
    }

    [Fact]
    public void No_conversion_is_left_anywhere_in_the_translated_query()
    {
        var sql = SqlFor("42");

        // This is the regression that TEL-13 is about. The old predicate put
        // InvoiceId.ToString() inside the WHERE, which the provider had to render as
        // a conversion; EF Core 8 changed those translations, and a change in width
        // or collation makes the comparison quietly match nothing.
        Assert.DoesNotContain("CONVERT(", sql, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CAST(", sql, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_non_numeric_reference_sends_no_id_term_to_the_server()
    {
        var sql = SqlFor("INV-2026-0042");

        // A reference that is not an id cannot match one, so the term is dropped from
        // the tree rather than translated into a comparison that can never be true.
        Assert.DoesNotContain("[InvoiceId] =", sql);
        Assert.Matches(@"\[InvoiceNumber\]\s*=\s*@", sql);
    }

    [Fact]
    public void Both_terms_reach_the_server_as_one_query()
    {
        var sql = SqlFor("42");

        // Number and id are one OR against one table scan, not two round trips, and
        // the filtering stays on the server - nothing here is evaluated client-side.
        // Parenthesisation around the two terms is the provider's business, so it is
        // not asserted on.
        Assert.Matches(@"\[InvoiceNumber\]\s*=\s*@", sql);
        Assert.Matches(@"\[InvoiceId\]\s*=\s*@", sql);
        Assert.Contains(" OR ", sql);
        Assert.Contains("WHERE", sql);
    }
}
