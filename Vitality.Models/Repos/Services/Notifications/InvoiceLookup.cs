using System;
using System.Globalization;
using System.Linq.Expressions;
using Vitality.Models.EntityClasses;

namespace Vitality.Services.Notifications;

/// <summary>
/// Builds the "find this invoice by number or by id" predicate used across
/// <see cref="NotificationService"/>.
/// <para>
/// These lookups used to read <c>i.InvoiceNumber == invoiceNumber || i.InvoiceId.ToString() == invoiceNumber</c>,
/// which puts a <c>int</c> to <c>string</c> conversion inside a server-side
/// <c>Where</c>. EF Core 8 adjusted several <c>ToString()</c> translations, and a
/// change in width or collation makes that comparison stop matching <b>silently</b>:
/// no exception, no log entry, simply no rows. On the invoice notification path that
/// means invoice mail quietly stops reaching some recipients.
/// </para>
/// <para>
/// So the conversion happens here, on the client, and the query compares typed
/// values: an integer against an integer column, a string against a string column.
/// Nothing is left for the provider to translate.
/// </para>
/// </summary>
public static class InvoiceLookup
{
    /// <summary>
    /// Parses an invoice reference into an <c>InvoiceId</c>, or null when it is not
    /// one.
    /// </summary>
    /// <remarks>
    /// The parse is deliberately strict. The old comparison was against
    /// <c>InvoiceId.ToString()</c>, so only the exact rendering of an id ever
    /// matched: <c>"007"</c>, <c>" 42"</c> and <c>"+42"</c> did not, and a value too
    /// large for the column did not either. Round-tripping the parsed value keeps
    /// that exactly, so moving the conversion client-side widens nothing.
    /// </remarks>
    public static int? ParseInvoiceId(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return null;
        }

        if (!int.TryParse(invoiceNumber, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var invoiceId))
        {
            return null;
        }

        return invoiceId.ToString(CultureInfo.InvariantCulture) == invoiceNumber ? invoiceId : (int?)null;
    }

    /// <summary>
    /// A predicate matching an invoice whose <c>InvoiceNumber</c> equals
    /// <paramref name="invoiceNumber"/>, or whose <c>InvoiceId</c> it names.
    /// </summary>
    /// <remarks>
    /// When the reference is not an id, the id term is dropped from the tree
    /// altogether rather than compared against a null parameter, so the provider
    /// never sees a term that cannot match.
    /// </remarks>
    public static Expression<Func<Sys_Invoice, bool>> MatchesNumberOrId(string? invoiceNumber)
    {
        var parsed = ParseInvoiceId(invoiceNumber);

        if (parsed is null)
        {
            return i => i.InvoiceNumber == invoiceNumber;
        }

        var invoiceId = parsed.Value;

        return i => i.InvoiceNumber == invoiceNumber || i.InvoiceId == invoiceId;
    }
}
