using System.Collections.Generic;
using System.Linq;
using Vitality.Models.EntityClasses;
using Vitality.Services.Notifications;
using Xunit;

namespace Vitality.Models.Tests.Notifications;

/// <summary>
/// Covers the invoice lookup used across NotificationService, which used to compare
/// InvoiceId.ToString() inside a server-side Where. The predicate is built by
/// InvoiceLookup and run here against an in-memory set: the expression tree under
/// test is the same one handed to EF Core, so what passes here is what the provider
/// is asked to translate.
/// </summary>
public class InvoiceLookupTests
{
    private static readonly List<Sys_Invoice> Invoices = new()
    {
        new Sys_Invoice { InvoiceId = 42, InvoiceNumber = "INV-2026-0042" },
        new Sys_Invoice { InvoiceId = 77, InvoiceNumber = null },
        new Sys_Invoice { InvoiceId = 4200, InvoiceNumber = "42" },
    };

    private static Sys_Invoice? Find(string? invoiceNumber)
    {
        return Invoices.AsQueryable().FirstOrDefault(InvoiceLookup.MatchesNumberOrId(invoiceNumber));
    }

    [Fact]
    public void Lookup_by_invoice_number_finds_the_invoice()
    {
        var invoice = Find("INV-2026-0042");

        Assert.NotNull(invoice);
        Assert.Equal(42, invoice!.InvoiceId);
    }

    [Fact]
    public void Lookup_by_invoice_id_finds_the_invoice()
    {
        var invoice = Find("77");

        Assert.NotNull(invoice);
        Assert.Equal(77, invoice!.InvoiceId);
    }

    [Fact]
    public void Non_numeric_reference_that_matches_nothing_returns_nothing()
    {
        Assert.Null(Find("not-an-invoice"));
        Assert.Null(Find("INV-2026-9999"));
    }

    [Fact]
    public void An_invoice_number_wins_over_an_id_that_reads_the_same()
    {
        // "42" is invoice 4200's InvoiceNumber and also invoice 42's id, so both
        // rows match — exactly as they did under the old two-term expression.
        var matches = Invoices.AsQueryable().Where(InvoiceLookup.MatchesNumberOrId("42")).ToList();

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, i => i.InvoiceId == 4200);
        Assert.Contains(matches, i => i.InvoiceId == 42);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("0", 0)]
    [InlineData("-42", -42)]
    [InlineData("2147483647", 2147483647)]
    public void ParseInvoiceId_accepts_the_exact_rendering_of_an_id(string invoiceNumber, int expected)
    {
        Assert.Equal((int?)expected, InvoiceLookup.ParseInvoiceId(invoiceNumber));
    }

    [Theory]
    [InlineData("not-an-invoice")]
    [InlineData("INV-2026-0042")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("007")]        // parses, but 7.ToString() is "7"
    [InlineData(" 42")]        // parses, but the old ToString() comparison rejected it
    [InlineData("42 ")]
    [InlineData("+42")]
    [InlineData("42.0")]
    [InlineData("2147483648")] // one past int.MaxValue: no id can render as this
    [InlineData("9999999999999999999999")]
    public void ParseInvoiceId_rejects_anything_that_is_not_one(string? invoiceNumber)
    {
        Assert.Null(InvoiceLookup.ParseInvoiceId(invoiceNumber));
    }

    [Fact]
    public void A_reference_that_is_not_an_id_still_matches_on_number_only()
    {
        // The id term must be absent from the tree, not compared against a null
        // parameter, so a row whose InvoiceNumber is null cannot fall through.
        Assert.Null(Find("007"));
        Assert.NotNull(Find("INV-2026-0042"));
    }
}
