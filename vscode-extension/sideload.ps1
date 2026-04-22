#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build, package, and sideload the BookStack MCP Server VS Code extension locally.
.DESCRIPTION
    Detects the current platform, publishes the .NET binary, copies it into
    vscode-extension/bin/, packages the VSIX, and installs it into VS Code.
.EXAMPLE
    ./sideload.ps1
.EXAMPLE
    ./sideload.ps1 -SkipDotnetPublish   # reuse an already-built binary
#>

param(
    [switch]$SkipDotnetPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Resolve repo root (script lives in vscode-extension/)
# ---------------------------------------------------------------------------
$repoRoot      = Split-Path $PSScriptRoot -Parent
$extensionDir  = $PSScriptRoot
$binDir        = Join-Path $extensionDir 'bin'
$publishOut    = Join-Path $repoRoot 'publish-local'
$csproj        = Join-Path $repoRoot 'src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj'

# ---------------------------------------------------------------------------
# Platform detection
# ---------------------------------------------------------------------------
if ($IsWindows) {
    $rid        = 'win-x64'
    $srcBinary  = Join-Path $publishOut 'BookStack.Mcp.Server.exe'
    $destBinary = Join-Path $binDir     'bookstack-mcp-server.exe'
} elseif ($IsLinux) {
    $rid        = 'linux-x64'
    $srcBinary  = Join-Path $publishOut 'BookStack.Mcp.Server'
    $destBinary = Join-Path $binDir     'bookstack-mcp-server-linux'
} else {
    Write-Error 'macOS is not yet supported. See https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/54'
}

Write-Host "`n=== BookStack MCP Server — local sideload ===" -ForegroundColor Cyan
Write-Host "Platform : $rid"
Write-Host "Repo root: $repoRoot`n"

# ---------------------------------------------------------------------------
# 1. dotnet publish
# ---------------------------------------------------------------------------
if (-not $SkipDotnetPublish) {
    Write-Host '--- [1/5] dotnet publish ---' -ForegroundColor Yellow
    if (Test-Path $publishOut) { Remove-Item $publishOut -Recurse -Force }

    dotnet publish $csproj `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:DebugType=embedded `
        -o $publishOut

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host '--- [1/5] dotnet publish SKIPPED ---' -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# 2. Copy binary into vscode-extension/bin/
# ---------------------------------------------------------------------------
Write-Host "`n--- [2/5] Copy binary ---" -ForegroundColor Yellow

if (-not (Test-Path $srcBinary)) {
    Write-Error "Binary not found at: $srcBinary`nRun without -SkipDotnetPublish to rebuild."
}

New-Item -ItemType Directory -Force -Path $binDir | Out-Null
Copy-Item $srcBinary $destBinary -Force
Write-Host "Copied: $destBinary"

if ($IsLinux) {
    chmod +x $destBinary
    Write-Host 'chmod +x applied'
}

# ---------------------------------------------------------------------------
# 3. npm install (only if node_modules is missing)
# ---------------------------------------------------------------------------
Write-Host "`n--- [3/5] npm install ---" -ForegroundColor Yellow
Push-Location $extensionDir
try {
    if (-not (Test-Path 'node_modules')) {
        npm ci
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Host 'node_modules present — skipping npm ci'
    }

    # ---------------------------------------------------------------------------
    # 4. Build extension
    # ---------------------------------------------------------------------------
    Write-Host "`n--- [4/5] npm run build ---" -ForegroundColor Yellow
    npm run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # ---------------------------------------------------------------------------
    # 5. Package + install VSIX
    # ---------------------------------------------------------------------------
    Write-Host "`n--- [5/5] vsce package + install ---" -ForegroundColor Yellow
    $vsix = Join-Path $extensionDir 'bookstack-mcp-server.vsix'
    npx vsce package --out $vsix
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    code --install-extension $vsix
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "'code' command not found in PATH."
        Write-Warning "Install manually: Extensions panel → '...' → Install from VSIX → $vsix"
    }
} finally {
    Pop-Location
}

Write-Host "`n=== Done! ===" -ForegroundColor Green
Write-Host "Reload VS Code: Ctrl+Shift+P → 'Developer: Reload Window'"
Write-Host "Then set bookstack.url / bookstack.tokenId / bookstack.tokenSecret in Settings.`n"
