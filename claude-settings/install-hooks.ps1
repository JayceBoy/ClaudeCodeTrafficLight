# install-hooks.ps1
# Copy hooks from project settings.json into global Claude Code settings.json
# Global path: $env:USERPROFILE\.claude\settings.json

param(
    [string]$Source = "$PSScriptRoot\settings.json",
    [string]$GlobalDir = "$env:USERPROFILE\.claude",
    [string]$GlobalFile = "$env:USERPROFILE\.claude\settings.json",
    [switch]$Backup = $true,
    [switch]$NonInteractive = $false
)

# ---------- check source ----------
if (-not (Test-Path $Source)) {
    Write-Host "[ERROR] Source file not found: $Source"
    Write-Host ""
    if (-not $NonInteractive) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Host "============================================"
Write-Host "  TrafficLight - Install Claude Code Hooks"
Write-Host "============================================"
Write-Host ""

# ---------- ensure global dir ----------
if (-not (Test-Path $GlobalDir)) {
    Write-Host "  Creating global config directory: $GlobalDir"
    New-Item -ItemType Directory -Path $GlobalDir -Force | Out-Null
}

# ---------- backup existing global config ----------
if ($Backup -and (Test-Path $GlobalFile)) {
    $backupFile = "$GlobalFile.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -Path $GlobalFile -Destination $backupFile -Force
    Write-Host "  [OK] Backed up current config to: $backupFile"
}

# ---------- read source ----------
try {
    $sourceSettings = Get-Content $Source -Raw | ConvertFrom-Json
    $sourceHooks = $sourceSettings.hooks
    if ($null -eq $sourceHooks) {
        Write-Host "[ERROR] No 'hooks' section found in source file"
        if (-not $NonInteractive) { Read-Host "Press Enter to exit" }
        exit 1
    }
    Write-Host "  [OK] Read project settings: $Source"
} catch {
    Write-Host "[ERROR] Failed to read source config: $_"
    if (-not $NonInteractive) { Read-Host "Press Enter to exit" }
    exit 1
}

# ---------- read or create global config ----------
$globalSettings = $null
if (Test-Path $GlobalFile) {
    try {
        $globalSettings = Get-Content $GlobalFile -Raw | ConvertFrom-Json
        Write-Host "  [OK] Read global config: $GlobalFile"
    } catch {
        Write-Warning "  [WARN] Global config parse failed, recreating: $_"
        $globalSettings = $null
    }
}

# ---------- merge hooks ----------
if ($null -ne $globalSettings) {
    $globalSettings | Add-Member -NotePropertyName "hooks" -NotePropertyValue $sourceHooks -Force
} else {
    $globalSettings = [PSCustomObject]@{
        hooks = $sourceHooks
    }
}

# ---------- write global config ----------
try {
    $json = $globalSettings | ConvertTo-Json -Depth 10
    Set-Content -Path $GlobalFile -Value $json -Encoding UTF8
    Write-Host "  [OK] Written to: $GlobalFile"
} catch {
    Write-Host "[ERROR] Write failed: $_"
    if (-not $NonInteractive) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Host ""
Write-Host "[OK] Done! Installed hook events:"
Write-Host ""

$sourceHooks.PSObject.Properties | ForEach-Object {
    Write-Host "    - $($_.Name)"
}

Write-Host ""
Write-Host "Note: Other config items (non-hooks) in your global config"
Write-Host "      were preserved. Restore a backup file to undo."
Write-Host ""
if (-not $NonInteractive) { Read-Host "Press Enter to exit" }
