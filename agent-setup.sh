#!/usr/bin/env bash
#
# Setup script for the unattended agent environment (Telehealth-agents).
#
# Point the cloud environment's "setup script" at this file. It installs the
# .NET 8 SDK so the nightly ticket runner can actually run `dotnet build` and
# `dotnet test` before it pushes, instead of pushing code it has never compiled.
#
# This script needs network egress. See AGENT-ENVIRONMENT.md for the exact
# domains that must be allowlisted; if they are not, the script fails loudly
# with the domain that was refused rather than leaving a half-built container.
#
# Safe to re-run: an existing .NET 8 SDK short-circuits the install.

set -euo pipefail

DOTNET_CHANNEL="8.0"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
INSTALL_SCRIPT="$(mktemp -t dotnet-install-XXXXXX.sh)"

log()  { printf '[agent-setup] %s\n' "$*"; }
fail() { printf '[agent-setup] ERROR: %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# 1. Short-circuit if a usable SDK is already present.
# ---------------------------------------------------------------------------

if command -v dotnet >/dev/null 2>&1; then
  if dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
    log ".NET 8 SDK already present: $(dotnet --version)"
    exit 0
  fi
  log "dotnet found but no 8.x SDK; installing alongside."
fi

# ---------------------------------------------------------------------------
# 2. Fetch the official installer.
#
# The failure here is the one worth reporting well. When egress is denied the
# proxy returns 403 on CONNECT, which curl reports as exit 22 with no useful
# context - that is exactly the wall the 2026-09-21 nightly run hit.
# ---------------------------------------------------------------------------

log "Fetching the .NET install script."

if ! curl -fsSL --max-time 120 https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"; then
  fail "could not download dotnet-install.sh.

This is almost certainly the egress proxy, not a transient network fault.
Allowlist these domains on the agent environment and re-run:

  dot.net
  builds.dotnet.microsoft.com
  dotnetcli.blob.core.windows.net
  dotnetcli.azureedge.net
  api.nuget.org

To confirm what the proxy is refusing, run inside the sandbox:

  curl -sS \"\$HTTPS_PROXY/__agentproxy/status\"

See AGENT-ENVIRONMENT.md for the full rationale and the alternative
(a prebuilt image that already carries the SDK)."
fi

chmod +x "$INSTALL_SCRIPT"

# ---------------------------------------------------------------------------
# 3. Install the SDK.
# ---------------------------------------------------------------------------

log "Installing the .NET ${DOTNET_CHANNEL} SDK into ${DOTNET_ROOT}."

"$INSTALL_SCRIPT" \
  --channel "$DOTNET_CHANNEL" \
  --install-dir "$DOTNET_ROOT" \
  --no-path

rm -f "$INSTALL_SCRIPT"

export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"

# ---------------------------------------------------------------------------
# 4. Persist PATH for the agent's later shells.
#
# The setup script and the agent's Bash calls are separate shells, so an export
# here alone would not survive. Append to the profile, but only once.
# ---------------------------------------------------------------------------

PROFILE="$HOME/.bashrc"
MARKER="# added by agent-setup.sh (TeleHealthBE)"

if [[ -f "$PROFILE" ]] && grep -qF "$MARKER" "$PROFILE"; then
  log "PATH already persisted in ${PROFILE}."
else
  {
    echo ""
    echo "$MARKER"
    echo "export DOTNET_ROOT=\"$DOTNET_ROOT\""
    echo "export PATH=\"\$DOTNET_ROOT:\$PATH\""
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
  } >> "$PROFILE"
  log "Persisted DOTNET_ROOT and PATH in ${PROFILE}."
fi

# ---------------------------------------------------------------------------
# 5. Prove it works end to end.
#
# A restore is the real test: the SDK can install fine and still be useless if
# api.nuget.org is blocked, which would not show up until the agent was already
# mid-ticket.
# ---------------------------------------------------------------------------

log "Installed SDK: $(dotnet --version)"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION="$REPO_ROOT/Vitality/Vitality.sln"

if [[ -f "$SOLUTION" ]]; then
  log "Warming the NuGet cache (this also verifies api.nuget.org is reachable)."
  if ! dotnet restore "$SOLUTION"; then
    fail "the SDK installed but 'dotnet restore' failed.

The most likely cause is that api.nuget.org is not on the egress allowlist.
Without it the agent can compile nothing, so treat the environment as unready."
  fi
  log "Restore succeeded."
else
  log "Solution not found at ${SOLUTION}; skipping the restore check."
fi

log "Environment ready."
