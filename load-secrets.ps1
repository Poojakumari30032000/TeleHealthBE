<#
    load-secrets.ps1
    ============================================================================
    One-time migration: loads secrets.local.json into this machine's
    dotnet user-secrets store, so the API can start in Development.

    Run it from anywhere:

        powershell -ExecutionPolicy Bypass -File D:\teleHealthComplete\TeleHealthBE\load-secrets.ps1

    or, from the TeleHealthBE folder:

        .\load-secrets.ps1

    Afterwards the values live in
        %APPDATA%\Microsoft\UserSecrets\b7f4c2e1-9a63-4d18-8c5f-2e1a7d940c36\secrets.json
    which is outside the repository, so they cannot be committed by accident.

    EVERY VALUE LOADED HERE STILL NEEDS ROTATING. They sat in plaintext in a
    folder with no version control. See the checklist in SECRETS.md section 5.
    ============================================================================
#>

#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$root        = Split-Path -Parent $MyInvocation.MyCommand.Path
$project     = Join-Path $root 'Vitality\Vitality.csproj'
$secretsFile = Join-Path $root 'secrets.local.json'

Write-Host ''
Write-Host 'TeleHealth - loading local development secrets' -ForegroundColor Cyan
Write-Host '=============================================='

# --- Preconditions --------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The dotnet CLI is not on PATH. Install the .NET 8 SDK, or run this from a Developer PowerShell prompt.'
}

if (-not (Test-Path $project)) {
    throw "Project not found: $project`nRun this script from the TeleHealthBE folder."
}

if (-not (Test-Path $secretsFile)) {
    Write-Host ''
    Write-Host "secrets.local.json is not here: $secretsFile" -ForegroundColor Yellow
    Write-Host 'If you already ran this script and deleted the file, that is expected.'
    Write-Host 'Check what is currently stored with:'
    Write-Host "    dotnet user-secrets list --project `"$project`"" -ForegroundColor Gray
    exit 1
}

# Fail early on malformed JSON rather than letting the CLI report it obscurely.
try {
    $parsed = Get-Content -Raw -Path $secretsFile | ConvertFrom-Json
} catch {
    throw "secrets.local.json is not valid JSON: $($_.Exception.Message)"
}

$keyCount = ($parsed.PSObject.Properties | Measure-Object).Count
Write-Host ''
Write-Host "Found $keyCount secret(s) to load." -ForegroundColor Gray

# --- Initialise the store if this is the first run ------------------------
# Safe to repeat: `init` is a no-op when UserSecretsId already resolves.
& dotnet user-secrets init --project $project | Out-Null

# --- Load ------------------------------------------------------------------
# `dotnet user-secrets set` reads a flat JSON object from stdin and stores
# every key/value pair. -Raw keeps it as one string, so it arrives intact.
Get-Content -Raw -Path $secretsFile | & dotnet user-secrets set --project $project

if ($LASTEXITCODE -ne 0) {
    throw "dotnet user-secrets set failed with exit code $LASTEXITCODE."
}

# --- Verify ----------------------------------------------------------------
Write-Host ''
Write-Host 'Stored secrets:' -ForegroundColor Cyan
$listed = & dotnet user-secrets list --project $project
$listed | ForEach-Object {
    # Print key names only. Never echo the values to a console or CI log.
    if ($_ -match '^(?<key>[^=]+)=') { Write-Host ('  ' + $matches['key'].Trim()) }
}

$storedCount = ($listed | Where-Object { $_ -match '=' } | Measure-Object).Count
Write-Host ''
if ($storedCount -ge $keyCount) {
    Write-Host "OK - $storedCount secret(s) stored." -ForegroundColor Green
} else {
    Write-Host "WARNING - expected at least $keyCount, found $storedCount. Check the output above." -ForegroundColor Yellow
}

# --- Next steps ------------------------------------------------------------
Write-Host ''
Write-Host 'Next:' -ForegroundColor Cyan
Write-Host '  1. Delete the plaintext file, now that the values are in the secret store:'
Write-Host "         Remove-Item `"$secretsFile`"" -ForegroundColor Gray
Write-Host '  2. Run the API:'
Write-Host '         dotnet run --project .\Vitality\Vitality.csproj' -ForegroundColor Gray
Write-Host ''
Write-Host '  Expect startup warnings for Stripe:WebhookSecret and Stripe:WebhookSecretThin.'
Write-Host '  Those are deliberately blank - the old values were not webhook signing'
Write-Host '  secrets at all. See SECRETS.md section 7.'
Write-Host ''
Write-Host '  Then rotate everything: SECRETS.md section 5.' -ForegroundColor Yellow
Write-Host ''
