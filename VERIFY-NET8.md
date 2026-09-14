# .NET 8 upgrade — build verification record

Closes the verification gap described in UPGRADE-NET8.md section 3: the .NET 6 → 8
migration was authored without an SDK, without NuGet access and without a shell, and
had been checked only by static analysis over 546 `.cs` files. Static analysis is not
a build. This document records the first actual build, test and publish of the
upgraded solution, so that the estimates resting on it have something verified
underneath them.

**No source or project file needed changing.** `dotnet restore` succeeded as
committed, so the conditional fix clause on TEL-9 — amend
`MicrosoftEntityFrameworkCoreVersion`, `MicrosoftAspNetCoreVersion` or
`MicrosoftExtensionsVersion` in `Directory.Build.props` and never an individual
`.csproj` — did not apply and was not used.

## Toolchain

| | |
|---|---|
| SDK | 8.0.131 (`/usr/lib/dotnet/sdk`) |
| Runtime / ASP.NET Core runtime | 8.0.31 |
| Host OS | Ubuntu 24.04 (linux-x64) |
| Target framework | `net8.0` — all three projects |

The solution builds on Linux as well as Windows; the commands below are the
POSIX-path equivalents of the Windows invocations on the ticket.

## Results

All four acceptance criteria pass.

### 1. Restore

    dotnet restore ./Vitality/Vitality.sln

Succeeded. No `NU1605` (downgrade) anywhere — the single-point EF Core version in
`Directory.Build.props` is doing its job and `Vitality` and `Vitality.Models` stay
aligned on 8.0.10.

### 2. Debug build

    dotnet build ./Vitality/Vitality.sln -c Debug

**0 errors, 543 warnings.** No project sets `TreatWarningsAsErrors` (verified by
grep over all `.csproj` and `.props`), so the warnings do not fail the build.

Warning breakdown, all three projects, clean non-incremental build:

| Code | Count | Note |
|---|---|---|
| `CS86xx` / `CS8600` nullable-reference family | 900 | pre-existing; nullable context enabled by the upgrade |
| `CS0168` / `CS0219` unused locals | 50 | pre-existing |
| `NU1603` approximate version match | 18 | see finding 1 |
| `NU1701` net48 package on net8.0 | 12 | expected per ticket (`JWT 2.3.2`, `OpenTok 3.0.0`) |
| `SYSLIB0021` obsolete derived crypto types | 8 | expected per ticket |
| `NU1902` moderate advisory | 6 | `AngleSharp 1.1.2`, `MailKit 4.7.1` |
| `NU1903` high advisory | 4 | see finding 2 |
| `SYSLIB0013`, `CS1998`, `CS0105`, `CS0618`, others | 22 | pre-existing |

`SYSLIB0021`, `NU1701` and `NU1902` are the three the ticket names as expected and
acceptable. `NU1603` and `NU1903` are not on that list; both are recorded as
findings below rather than changed here.

### 3. Tests

    dotnet test ./Vitality.Models.Tests/Vitality.Models.Tests.csproj

    Passed! - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 92 ms

**31 passing**, matching the count on the ticket exactly.

### 4. Release publish

    dotnet publish ./Vitality/Vitality.csproj -c Release

Succeeded. `Vitality.dll` produced; no Release-only errors.

## Verified package versions

Resolved versions, read back from `obj/project.assets.json` after a successful
restore — these are what actually restored, not what the `.csproj` files ask for.

### Centrally managed (`Directory.Build.props`)

| Property | Value | Resolved |
|---|---|---|
| `MicrosoftEntityFrameworkCoreVersion` | 8.0.10 | `Microsoft.EntityFrameworkCore`, `.SqlServer`, `.Design`, `.Tools`, `.Relational` all 8.0.10 |
| `MicrosoftAspNetCoreVersion` | 8.0.10 | `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.10 |
| `MicrosoftExtensionsVersion` | 8.0.1 | `Microsoft.Extensions.Http`, `.Configuration.UserSecrets` both 8.0.1 |

All three properties restore exactly as pinned. No amendment required.

### Per-project

| Package | Requested | Resolved |
|---|---|---|
| AutoMapper | 13.0.1 | 13.0.1 |
| AWSSDK.S3 | 3.7.400.38 | **3.7.401** ⚠ |
| BCrypt.Net-Next | 0.1.0 | **2.0.0** ⚠ |
| Twilio | 6.7.3 | **6.8.0** ⚠ |
| AngleSharp | 1.1.2 | 1.1.2 |
| ClosedXML | 0.102.3 | 0.102.3 |
| CsvHelper | 33.1.0 | 33.1.0 |
| Dapper | 2.1.35 | 2.1.35 |
| JWT | 2.3.2 | 2.3.2 (via net48 assets) |
| MailKit | 4.7.1 | 4.7.1 |
| Newtonsoft.Json | 13.0.3 | 13.0.3 |
| NodaTime | 3.1.11 | 3.1.11 |
| OpenTok | 3.0.0 | 3.0.0 (via net48 assets) |
| QuestPDF | 2025.7.4 | 2025.7.4 |
| Square | 23.0.0 | 23.0.0 |
| Stripe.net | 50.3.0 | 50.3.0 |
| Swashbuckle.AspNetCore | 6.5.0 | 6.5.0 |
| System.Linq.Dynamic.Core | 1.6.9 | 1.6.9 |
| Microsoft.NET.Test.Sdk | 17.11.1 | 17.11.1 |
| xunit | 2.9.2 | 2.9.2 |
| xunit.runner.visualstudio | 2.8.2 | 2.8.2 |

## Findings — not changed under this ticket

Both are recorded rather than fixed: TEL-9 confines version edits to the three
`Directory.Build.props` properties, and neither finding touches those.

### 1. Three pinned versions do not exist on nuget.org (`NU1603`, 18 warnings)

`AWSSDK.S3 3.7.400.38`, `BCrypt.Net-Next 0.1.0` and `Twilio 6.7.3` are not
published versions. A `PackageReference` is a minimum-version constraint, so NuGet
silently floats each one up to the nearest available release instead of failing.
Restore therefore depends on what the feed happens to offer and is **not
reproducible** — a later restore can resolve a different version with no diff and
no warning escalation.

`BCrypt.Net-Next` is the sharpest case: 0.1.0 resolves to 2.0.0, while the current
line is 4.x. It hashes passwords, so which version is in the image is a security
question and not one to settle silently.

Recommended follow-up: pin all three to versions that exist, `BCrypt.Net-Next`
first, and re-run this verification.

### 2. `AutoMapper 13.0.1` carries a high-severity advisory (`NU1903`, 4 warnings)

[GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x). The
ticket lists `NU1902` (moderate) as acceptable but does not mention `NU1903`.
Flagged for triage; a major-version move is well outside a build-verification
ticket.

## Reproducing

From the repository root, with the .NET 8 SDK on `PATH`:

    dotnet restore ./Vitality/Vitality.sln
    dotnet build   ./Vitality/Vitality.sln -c Debug
    dotnet test    ./Vitality.Models.Tests/Vitality.Models.Tests.csproj
    dotnet publish ./Vitality/Vitality.csproj -c Release

Expect: restore clean, build 0 errors, 31 tests passing, publish clean.
