<#
.SYNOPSIS
    Builds the VNManager Playnite extension and deploys it over the installed copy.

.DESCRIPTION
    1. Publishes LauncherSupervisor and builds PlaynitePlugin in Release.
    2. Backs up the installed extension folder and the plugin data folder
       (where the plugin runs LauncherSupervisor.exe from).
    3. Copies the new files over the installed ones. Nothing is deleted, and
       existing Configuration\*.json files are never overwritten.

    Playnite must be closed: it locks the extension DLLs.

.EXAMPLE
    .\scripts\Publish-Extension.ps1

.EXAMPLE
    .\scripts\Publish-Extension.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ExtensionDirectory = (Join-Path $env:APPDATA 'Playnite\Extensions\VNManager'),

    # GetPluginUserDataPath() of plugin 0e1b3f2c-56f4-4eaf-bb7b-7f2c02aa5c5a: the plugin
    # starts LauncherSupervisor.exe from this folder.
    [string]$SupervisorDirectory = (Join-Path $env:APPDATA 'Playnite\ExtensionsData\0e1b3f2c-56f4-4eaf-bb7b-7f2c02aa5c5a'),

    # Outside Playnite\Extensions so Playnite never loads a backup as a second extension.
    [string]$BackupRoot = (Join-Path $env:APPDATA 'Playnite\VNManager-backups'),

    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$supervisorProject = Join-Path $repositoryRoot 'VNManager\LauncherSupervisor\LauncherSupervisor.csproj'
$pluginProject = Join-Path $repositoryRoot 'VNManager\PlaynitePlugin\PlaynitePlugin.csproj'
$supervisorOutput = Join-Path $repositoryRoot 'VNManager\LauncherSupervisor\bin\Release\net10.0\publish'
$pluginOutput = Join-Path $repositoryRoot 'VNManager\PlaynitePlugin\bin\Release\net462'

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Backup-Directory {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        Write-Host "Nothing to back up (not found): $Source"
        return
    }

    if ($PSCmdlet.ShouldProcess($Source, "Back up to $Destination")) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
        Write-Host "Backup: $Source -> $Destination"
    }
}

function Copy-Overlay {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Build output not found: $Source"
    }

    $sourceRoot = (Resolve-Path -LiteralPath $Source).ProviderPath.TrimEnd('\')
    $copied = 0
    $preserved = 0

    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -File -Recurse) {
        $relativePath = $file.FullName.Substring($sourceRoot.Length + 1)
        $target = Join-Path $Destination $relativePath
        $isConfiguration = $relativePath -like 'Configuration\*.json'

        # Local configuration (tool paths, profiles, process rules) belongs to the user.
        if ($isConfiguration -and (Test-Path -LiteralPath $target)) {
            Write-Host "Preserved: $relativePath"
            $preserved++
            continue
        }

        if ($PSCmdlet.ShouldProcess($target, 'Copy')) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            $copied++
        }
    }

    Write-Host "$Destination : $copied file(s) copied, $preserved configuration file(s) preserved."
}

$playnite = Get-Process -Name 'Playnite.DesktopApp', 'Playnite.FullscreenApp' -ErrorAction SilentlyContinue
if ($playnite) {
    throw 'Close Playnite before publishing: it locks the extension files.'
}

if (-not $SkipBuild -and $PSCmdlet.ShouldProcess($repositoryRoot, 'Build Release')) {
    # Clean only the generated publish folder so stale files are not deployed.
    if (Test-Path -LiteralPath $supervisorOutput) {
        Remove-Item -LiteralPath $supervisorOutput -Recurse -Force
    }

    Invoke-DotNet @('publish', $supervisorProject, '-c', 'Release')
    Invoke-DotNet @('build', $pluginProject, '-c', 'Release')
}

$backupDirectory = Join-Path $BackupRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
Backup-Directory -Source $ExtensionDirectory -Destination (Join-Path $backupDirectory 'Extensions-VNManager')
Backup-Directory -Source $SupervisorDirectory -Destination (Join-Path $backupDirectory 'ExtensionsData')

Copy-Overlay -Source $pluginOutput -Destination $ExtensionDirectory
Copy-Overlay -Source $supervisorOutput -Destination $SupervisorDirectory

Write-Host "Done. Backup: $backupDirectory"
