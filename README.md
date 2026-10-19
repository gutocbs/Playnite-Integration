# Playnite-Integration

Playnite-Integration is a Windows launcher compatibility layer for visual novels in [Playnite](https://playnite.link/). It keeps Playnite-specific integration at the edge and runs launch, process supervision, cleanup, and error handling in a standalone .NET runtime.

It is intended for games that need a launcher such as **Locale Emulator** or **NoRegionLoader**, especially when the original game executable is only a short-lived bootstrapper.

## Features

- Selects a launcher from a Playnite feature such as `[Launcher] Locale Emulator`.
- Provides the built-in `Locale Emulator` and `NoRegionLoader` launch strategies.
- Keeps the Playnite action alive while the game session is supervised, preserving playtime tracking.
- Prefers configured engine processes during a short detection window, so a bootstrapper exiting early does not end the session.
- Uses JSON configuration for tool paths, profiles, process detection, timeouts, and cleanup.
- Supports graceful cancellation without terminating the game; only explicitly configured cleanup processes or services can be stopped.
- Writes structured logs and returns structured launch results.

## Architecture

```text
Playnite
  -> Playnite plugin (dynamic Play Action)
  -> LauncherSupervisor.exe (.NET)
  -> registered launcher (Launcher.Supervision + ProcessManagement)
  -> Locale Emulator or NoRegionLoader
  -> game process
```

The plugin reads the game's `[Launcher]` Feature and File action and starts `LauncherSupervisor.exe --executable <game> --launcher <name> [-- <arguments>]`. The supervisor resolves an explicitly registered launcher, supervises the game until it exits and returns exit code `0` or `1`. Playnite tracks playtime for the supervisor's lifetime.

Playnite APIs are isolated to the plugin. The supervisor and launcher projects do not depend on Playnite.

## Requirements

- Windows
- [.NET SDK 10](https://dotnet.microsoft.com/download) for the supervisor, launchers, and tests
- Playnite 6.x to use the plugin (`PlayniteSDK` 6.16.0)
- Locale Emulator and/or NoRegionLoader when using those launch strategies

## Quick start

Clone the repository and build the solution:

```powershell
git clone <repository-url>
cd "Playnite Integration"
dotnet build .\VNManager\Launcher.sln
```

Update the configuration copied beside `LauncherSupervisor.exe` before launching a game:

- `VNManager/LauncherSupervisor/Configuration/locale-emulator.json` — Locale Emulator executable and profile.
- `VNManager/LauncherSupervisor/Configuration/no-region-loader.json` — absolute path to `NoRegion Loader.exe`.
- `VNManager/LauncherSupervisor/Configuration/process-management.json` — monitored executables, start timeouts, and cleanup rules.

The checked-in tool paths are examples. Do not commit local installation paths or game-specific configuration.

### Shared installation protocol verification

`PlayniteInstallation` and `PlaynitePlugin` consume the exact
`VNInstallation.Protocol` package version `1.0.0-alpha.2` from the local feed
configured in `NuGet.Config`. Run the non-deploying cross-repository gate from
the protocol checkout:

```powershell
pwsh -File "F:\Playnite Integration\Playnite VN Protocol\scripts\verify-consumers.ps1" `
  -ProtocolRoot "F:\Playnite Integration\Playnite VN Protocol" `
  -OrchestratorRoot "F:\Playnite Integration\Playnite VN Installer\.worktrees\vn-orchestrator-plugin" `
  -PlayniteIntegrationRoot "F:\Playnite Integration\Playnite VN Manager\.worktrees\vn-orchestrator-plugin"
```

The plugin validation artifact is written to
`artifacts\plugin-release` in this worktree, and the Orchestrator publish is
written to its own repository-local `artifacts\orchestrator-publish` folder.
No installed Playnite files are modified. This repository owns Playnite
integration, launcher supervision, and persistence; the protocol repository
owns the shared wire contracts and transport; the Orchestrator owns the
installation workflow and UI. The gate can use `-SkipIntegrationFixtures`
when the process-fixture runner is intentionally excluded from a local check.
The Install-button/no-log regression is deferred to the first task of Plan 2.

### Configure a Playnite game

Add exactly one feature to the game in Playnite:

```text
[Launcher] Locale Emulator
```

or:

```text
[Launcher] NoRegionLoader
```

The name after `[Launcher]` is a logical launcher identifier, not a file path. Available identifiers are registered in `HostComposition` and are matched case-insensitively.

### Build the Playnite plugin

The plugin package includes the published supervisor output. Publish the supervisor first, then build the plugin in Release mode:

```powershell
dotnet publish .\VNManager\LauncherSupervisor\LauncherSupervisor.csproj -c Release
dotnet build .\VNManager\PlaynitePlugin\PlaynitePlugin.csproj -c Release
```

Copy the resulting plugin build output, including its dependency files and `extension.yaml`, to the Playnite extensions directory. Deploy the complete output set together; copying a single DLL is not supported.

To build and deploy over an installed copy in one step (close Playnite first):

```powershell
.\scripts\Publish-Extension.ps1          # add -WhatIf to preview
```

The script backs up `%APPDATA%\Playnite\Extensions\VNManager` and the plugin data folder (`%APPDATA%\Playnite\ExtensionsData\0e1b3f2c-56f4-4eaf-bb7b-7f2c02aa5c5a`, where the plugin starts `LauncherSupervisor.exe` from) to `%APPDATA%\Playnite\VNManager-backups\<timestamp>`, then copies the new files over both. It never deletes files and never overwrites an existing `Configuration\*.json`.

## Process monitoring and cleanup

`process-management.json` determines which processes may be observed and which resources may be cleaned up. `monitorProcesses` are checked during the configured `preferredProcessDetectionWindowSeconds` before accepting the requested executable. This lets Playnite-Integration track a real engine started by a short-lived launcher.

Cleanup is deliberately opt-in:

- `globalCleanupProcesses` and `globalCleanupServices` apply to all monitored executions.
- An item in `executables` may define its own cleanup processes and services.
- Cancellation only stops supervision. It does **not** close the game.

Services may require elevation to stop. Configure only resources the launcher owns.

## Development

The solution is at `VNManager/Launcher.sln`.

```powershell
# Build every project
dotnet build .\VNManager\Launcher.sln

# Run the test suites
dotnet run --project .\VNManager\Tests\Tests.csproj
dotnet run --project .\VNManager\PlaynitePlugin.Tests\PlaynitePlugin.Tests.csproj
dotnet run --project .\VNManager\PlayniteInstallation.Tests\PlayniteInstallation.Tests.csproj
```

The `Tests` suite includes Windows process-integration fixtures. It cleans up its `VnTest*` helper processes, but do not interrupt it midway unless necessary.

## Repository layout

```text
VNManager/
  Abstractions/                Shared request, result, error codes, and launcher contracts
  Host/                        Composition library: configuration loading and launcher registry
  LauncherSupervisor/          Executable started by the plugin, with Configuration/*.json
  Supervision/                 Launcher.Supervision: lifecycle shared by tool-based launchers
  LocaleEmulator/              Locale Emulator launcher
  NoRegionLoader/              NoRegionLoader launcher
  ProcessManagement/           Monitoring and explicit cleanup
  Logging/                     Structured text logging
  PlaynitePlugin/              Playnite 6 generic plugin (launch and install workflow)
  PlayniteInstallation/        Install-workflow rules and transport (VN Orchestrator)
  Tests/                       xUnit tests and process integration coverage
  TestAssets/                  VnTest* process fixtures
  PlaynitePlugin.Tests/        Plugin play-action tests
  PlayniteInstallation.Tests/  Install-workflow tests
docs/                          Architecture, contracts, design decisions, and launcher scope
```

## Further documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Launcher resolution](docs/LAUNCHER_RESOLUTION.md)
- [Launcher scope](docs/LAUNCHER_SCOPE.md)
- [Launcher contract](docs/Launcher%20Contract.md)
- [Design decisions](docs/DECISIONS.md)
- [Logging](docs/logging.md)

## License

This project is licensed under the [GNU GPL v3.0](LICENSE).
