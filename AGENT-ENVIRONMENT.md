# Agent build environment

How the TeleHealth backend gets compiled and tested, and what had to change to
make that true.

## The problem

Until this change, nothing in the pipeline ever built the code.

`.github/workflows/branch-policy.yml` validates branch names, PR targets and PR
titles. That is all it does. A green check meant "the branch was named
correctly" - not "it compiles".

The unattended nightly ticket runner could not fill the gap either. From the
2026-09-21 run log:

```
ls: cannot access '/usr/share/dotnet': No such file or directory
curl: (22) The requested URL returned error: 403
[agent-proxy] builds.dotnet.microsoft.com:443 - connect_rejected
              (the egress proxy denied the CONNECT (organization policy))
```

No .NET SDK in the container, and the installer download refused by policy. So
`dotnet build` and `dotnet test` could not run at all.

The consequence is concrete. TEL-13 replaced seven `InvoiceId.ToString()`
comparisons and added eight tests. None of it was ever compiled, the PR was
opened non-draft, and it merged into `development` that way. Two of TEL-9's
acceptance criteria - "zero NU1903 warnings" and "31 tests still passing" -
were recorded as unverified for the same reason.

This also quietly weakened the handover rule. The nightly runner may only move
a ticket from In Progress to PR Review once "all required checks on that PR's
head commit have completed and passed". When the only check is a name
validator, that condition is nearly free.

## Fix 1 - CI builds every PR

`.github/workflows/build.yml` restores, builds in **Debug** and runs the test
suite on every PR into `development` or `main`, and on every push to those
branches.

Debug is deliberate: TEL-9's criteria are written against that configuration.

This needs no environment changes. GitHub-hosted runners ship the .NET SDK and
reach nuget.org without a proxy. It is also the load-bearing fix - with it,
"the checks passed" finally means the code builds and the tests pass, so the
nightly runner's handover condition becomes real evidence.

### NuGet advisories are reported, not enforced

The workflow counts `NU1903`, `NU1902` and `NU1603` and writes them to the job
summary. It does not fail on them.

That is on purpose. AutoMapper 13.0.1 currently raises 4 `NU1903` warnings
(TEL-49), so gating on it today would red-light every PR in the repository
until that ticket's licensing decision is made.

**Once TEL-49 lands**, turn it into a hard gate by adding this to
`Directory.Build.props`:

```xml
<WarningsAsErrors>NU1903</WarningsAsErrors>
```

TEL-9's "zero NU1903 warnings" criterion is then enforced by the machine
permanently, instead of being re-checked by hand each time someone remembers.

## Fix 2 - the agent can build before it pushes

Fix 1 proves correctness *after* a push. This one lets the nightly runner catch
its own mistakes *before* opening a PR.

`agent-setup.sh` installs the .NET 8 SDK, persists `DOTNET_ROOT` and `PATH` for
the agent's later shells, and then runs a restore to prove the toolchain
actually works. Point the **Telehealth-agents** environment's setup script at
it. The environment currently reports *"No setup script configured"*.

### Required egress allowlist

The script cannot work until these are allowed through the proxy:

| Domain | Needed for |
| --- | --- |
| `dot.net` | the `dotnet-install.sh` redirect |
| `builds.dotnet.microsoft.com` | SDK payload - **this is the one currently refused** |
| `dotnetcli.blob.core.windows.net` | SDK payload fallback |
| `dotnetcli.azureedge.net` | SDK payload CDN |
| `api.nuget.org` | `dotnet restore` |

`api.nuget.org` matters as much as the SDK domains. Without it the SDK installs
fine and every restore still fails, which would not surface until the agent was
already mid-ticket. `agent-setup.sh` restores at the end specifically so that
failure happens during setup instead.

To see what the proxy is refusing, from inside the sandbox:

```bash
curl -sS "$HTTPS_PROXY/__agentproxy/status"
```

### If the allowlist is not an option

Use a prebuilt environment image with the .NET 8 SDK already installed. That
removes the SDK downloads entirely - but `api.nuget.org` is still required for
restore, so that one cannot be avoided either way.

## Fix 3 - unverified work must not look reviewed

Not a repository change; it belongs in the nightly routine prompt. Recorded
here because it is the same failure.

Step 3 of the per-ticket procedure says "Build and run the repo's test suite
before committing" but does not say what to do when that is impossible. The
agent improvised, and it improvised toward opening a normal PR anyway. PR #8
sat in the human review queue looking finished, having never been compiled.

Add to the routine prompt:

> If you could not build and run the test suite, the PR MUST be opened as a
> DRAFT and the ticket stays In Progress. State in the PR body exactly why
> verification was impossible.

With Fix 1 in place this should become rare - but it is the backstop for when
the sandbox is degraded, and it costs nothing.

## Verifying

After merging Fix 1, the first PR into `development` should show a **Build and
test** check alongside **Branch Policy**. Confirm in its job summary that:

- the solution builds in Debug with 0 errors
- the test count is **39** (31 before TEL-13, plus its 8 new tests)
- `NU1903` reports **4** until TEL-49 is resolved, then **0**

The TEL-13 change is already merged into `development` unverified, so that
first run is also the first real check of it.

After wiring Fix 2, the next nightly run log should show a `dotnet --version`
line from setup rather than the `No such file or directory` above.
