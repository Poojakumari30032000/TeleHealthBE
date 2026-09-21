# TEL-49 — NU1903 / AutoMapper 13.0.1

Verified 2026-09-21. The advisory **is real and does apply**. The fix is **not small**,
so this is parked for a decision rather than upgraded here, per the ticket's last
acceptance criterion.

## 1. The advisory is confirmed

[GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x), read directly
rather than taken from the build output TEL-9 captured:

| | |
|---|---|
| Package / ecosystem | `AutoMapper`, NuGet |
| Severity | High, CVSS 7.5 |
| Weakness | CWE-674, uncontrolled recursion |
| Affected | `< 15.1.1`, and `>= 16.0.0, < 16.1.1` |
| First patched | `15.1.1`, and `16.1.1` |

This repository pins `AutoMapper 13.0.1` in two projects — `Vitality/Vitality.csproj`
and `Vitality.Models/Vitality.Models.csproj`. `13.0.1 < 15.1.1`, so it is affected.
The warning is not spurious and should not be waived.

The mechanism is stack exhaustion from a deeply nested object graph handed to a mapping
operation. A `StackOverflowException` cannot be caught, so the process dies rather than
returning an error — worth weighing against the fact that this API maps request DTOs.

## 2. There is no small fix

**No patched 13.x or 14.x exists.** The advisory's affected range is open-ended
downwards (`< 15.1.1`); the maintainers did not backport. The minimum move is
`13.0.1` → `15.1.1`, two major versions. Three things make that its own piece of work:

1. **Licensing.** `15.1.1` carries a commercial licensing mechanism: the NuGet listing
   documents registering at AutoMapper.io, setting a license key, and paying-customer
   support. Whether this codebase may take a licensed AutoMapper, and who buys the
   licence, is a procurement and legal question, not an engineering one.

2. **Dependency chain.** `15.1.1` requires `Microsoft.Extensions.Logging.Abstractions
   >= 10.0.0`, `Microsoft.Extensions.Options >= 10.0.0` and
   `Microsoft.IdentityModel.JsonWebTokens >= 8.14.0`. `Directory.Build.props` pins
   `MicrosoftExtensionsVersion` at `8.0.1` centrally, deliberately, to stop the
   `Vitality` and `Vitality.Models` versions drifting apart (NU1605). Upgrading
   AutoMapper drags `Microsoft.Extensions.*` to 10.x transitively — precisely the drift
   that pin exists to prevent.

3. **`15.0.0` was delisted** for breaking changes, per the note on the `15.0.1` release.
   So the path crosses a major the maintainers themselves withdrew.

## 3. What an upgrade would have to cover

Blast radius, for whoever picks up the follow-up:

- **67 `CreateMap` calls**, all in `Vitality.Models/AutoMapper/AutoMapperProfiles.cs`.
- **`Vitality/Program.cs:454`** builds the configuration by hand —
  `new MapperConfiguration(mc => mc.AddProfile(new AutoMapperProfiles()))` — then
  `CreateMapper()` and registers the `IMapper` as a singleton. It does not use
  `AddAutoMapper`, so any change to the `MapperConfiguration` constructor lands
  directly on this line.
- **Nothing calls `AssertConfigurationIsValid()`.** There is no startup check that the
  67 maps are still coherent, and the existing 31 tests do not exercise mappings. This
  is the exact hazard the ticket's fourth acceptance criterion names: a mapping broken
  by a major-version change would surface as a null or default property at runtime, not
  as a build or startup failure.

A follow-up ticket should therefore add mapping coverage — or at minimum an
`AssertConfigurationIsValid()` call — **before** the version moves, not after.

## 4. Not verified here

`dotnet build -c Debug` was not run, so the "zero NU1903 warnings" and "31 tests still
passing" criteria are untested: the nightly runner has no .NET SDK and egress to
`builds.dotnet.microsoft.com` is blocked by network policy. The advisory findings above
come from the advisory and package listings themselves, which is what section 1 of the
ticket asked for, and they stand on their own.
