# TEL-13 — invoice lookup verification record

What has actually been proven about the EF Core 8 invoice lookup fix, what proved
it, and what is still unproven.

This file exists because the fix merged without evidence. PR #8 replaced seven
`InvoiceId.ToString()` comparisons and added eight test methods, and none of it was
ever compiled — the nightly agent's sandbox has no .NET SDK and the egress proxy
refuses the installer (AGENT-ENVIRONMENT.md). The PR said so in its body and merged
anyway. The build workflow from TEL-9 has since landed, so there is now machine
evidence to record.

## Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | All seven call sites parse client-side and compare on the typed value | **Met** — verified below |
| 2 | Lookup by `InvoiceNumber` and by `InvoiceId` proven by test, including a non-numeric input | **Met** — verified below |
| 3 | Smoke test row 8 of UPGRADE-NET8.md section 3: look up an invoice by invoice number | **Not met** — needs a live database (TEL-10) |

## 1. All seven call sites

`NotificationService.cs` lines 593, 646, 846, 910, 988, 2342 and 2500 each read:

    .Where(InvoiceLookup.MatchesNumberOrId(invoiceNumber))

Those are the only seven uses of `InvoiceLookup` in the codebase and the only seven
`.Where` clauses that took the old comparison. No `InvoiceId.ToString()` remains
inside any `Where`; the remaining occurrences of that expression are string building
on already-materialised entities (`InvoiceRepo`, the schedulers, several
controllers) and one projection in `DashboardsRepo`, none of which filter on the
result.

Those callers pass `invoice.InvoiceId.ToString()` as the reference, which
`ParseInvoiceId` round-trips back to the same id — so the paths that feed the lookup
from an id still match after the change.

## 2. Compiled, and the tests run

First machine-checked build of this code, on `development` after PR #8 merged:

| Run | Commit | Result |
|---|---|---|
| [Build #3](https://github.com/Poojakumari30032000/TeleHealthBE/actions/runs/35965871693) | `e79f796` | success |
| [Build #5](https://github.com/Poojakumari30032000/TeleHealthBE/actions/runs/35998513081) | `9489fc8` | success |

Build #5: `dotnet build -c Debug` — **0 errors**, 532 warnings, all pre-existing
nullable/unused-local families. `dotnet test` — **64 total, 64 passed, 0 failed**.

Twenty-two of those 64 are the invoice lookup's, and all passed:

| Test | Cases | Covers |
|---|---|---|
| `Lookup_by_invoice_number_finds_the_invoice` | 1 | criterion 2, by number |
| `Lookup_by_invoice_id_finds_the_invoice` | 1 | criterion 2, by id |
| `Non_numeric_reference_that_matches_nothing_returns_nothing` | 1 | criterion 2, non-numeric input |
| `A_reference_that_is_not_an_id_still_matches_on_number_only` | 1 | non-numeric input that does match |
| `An_invoice_number_wins_over_an_id_that_reads_the_same` | 1 | `InvoiceNumber` "42" vs `InvoiceId` 42 |
| `ParseInvoiceId_accepts_the_exact_rendering_of_an_id` | 4 | `"42"`, `"0"`, `"-42"`, `int.MaxValue` |
| `ParseInvoiceId_rejects_anything_that_is_not_one` | 12 | `"007"`, `" 42"`, `"+42"`, `"42.0"`, past `int.MaxValue`, null, empty |

So criterion 2 is met: both lookups and a non-numeric input are covered, and the
coverage is now machine-checked rather than asserted.

## 3. The translation, checked without a database

Those tests run the predicate against an in-memory set. That proves the expression
tree is right but not how EF Core 8 renders it — and the rendering is the whole
point of TEL-13, because the failure mode is a conversion in the `WHERE` that stops
matching on a width or collation difference, silently.

`Vitality.Models.Tests/Notifications/InvoiceLookupTranslationTests.cs` reads the SQL
instead. `ToQueryString()` compiles the query through the real SqlServer provider
and returns the command text with its parameter declarations, opening no
connection — so it runs in CI, where there is no database. It asserts:

- `[InvoiceId]` is compared against a parameter DECLAREd `int`, and `[InvoiceNumber]`
  against one DECLAREd `varchar(50)` — each parameter carries its own column's type
  (`InvoiceNumber` is `IsUnicode(false)`, `HasMaxLength(50)` in `MainContext`), so
  neither side of either comparison needs converting;
- no `CONVERT(` or `CAST(` anywhere in the query — the regression this ticket is
  about;
- a non-numeric reference sends no `[InvoiceId]` term at all, rather than one that
  can never be true;
- both terms travel as one `WHERE` against one query, so nothing is filtered
  client-side.

This is the strongest evidence obtainable without a database. It does not replace
criterion 3.

## What is still unproven

**Smoke test row 8 — looking up a real invoice by its number against a real
database.** Blocked on the environment from TEL-10. The translation tests above
remove the translation risk; what a live run still adds is that the rows are there
and come back.

**`ProductsRepo.GetAllDrugsInBundles` was not exercised at runtime**, as the ticket
asked. Its `.ToList()` before `.GroupBy(...)` is a static determination: EF Core
cannot translate a `GroupBy` whose result selector projects a whole entity, which is
what `Select(x => x.First())` does. The change is behaviour-preserving — the `Where`
still runs on the server and only the grouping moved to the client, over rows
already narrowed to one bundle's active variants — but it is reasoning, not a run.

## Reproducing

With the .NET 8 SDK on `PATH` (see `agent-setup.sh`):

    dotnet build Vitality/Vitality.sln -c Debug
    dotnet test  Vitality/Vitality.sln -c Debug --no-build --verbosity normal

Expect 0 errors and 68 tests passing — 64 plus the 4 translation tests added here.
