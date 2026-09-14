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
- Writes structured logs and returns structured launch results across the PowerShell/.NET process boundary.

## Architecture

```text
Playnite
  -> PowerShell Adapter or Playnite plugin
  -> Launcher Supervisor / Host (.NET)
  -> registered launcher
  -> Locale Emulator or NoRegionLoader
  -> game process
```

The adapter turns Playnite metadata into a `DispatcherRequest`. The Host validates the request, resolves an explicitly registered launcher, and returns a `LaunchResult` through per-execution JSON files. The PowerShell adapter and Host communicate cancellation through a named Windows event.

Playnite APIs are isolated to the integration boundary. Launcher projects do not depend on Playnite or PowerShell.

## Requirements

- Windows
- [.NET SDK 10](https://dotnet.microsoft.com/download) for the Host, launchers, supervisor, and tests
- Playnite 6.x to use the plugin (`PlayniteSDK` 6.16.0)
- Locale Emulator and/or NoRegionLoader when using those launch strategies

## Quick start

Clone the repository and build the solution:

```powershell
git clone <repository-url>
cd "Playnite Integration"
dotnet build .\VNManager\Launcher.sln
```

Update the configuration copied beside the Host executable before launching a game:

- `VNManager/Host/Configuration/locale-emulator.json` — Locale Emulator executable and profile.
- `VNManager/Host/Configuration/no-region-loader.json` — absolute path to `NoRegion Loader.exe`.
- `VNManager/ProcessManagement/process-management.json` — monitored executables, start timeouts, and cleanup rules.

The checked-in tool paths are examples. Do not commit local installation paths or game-specific configuration.

### Configure a Playnite game

Add exactly one feature to the game in Playnite:

```text
[Launcher] Locale Emulator
```

or:

```text
[Launcher] NoRegionLoader
```

The name after `[Launcher]` is a logical launcher identifier, not a file path. Available identifiers are registered by the Host and are matched case-insensitively.

### Build the Playnite plugin

The plugin package includes the published supervisor output. Publish the supervisor first, then build the plugin in Release mode:

```powershell
dotnet publish .\VNManager\LauncherSupervisor\LauncherSupervisor.csproj -c Release
dotnet build .\VNManager\PlaynitePlugin\PlaynitePlugin.csproj -c Release
```

Copy the resulting plugin build output, including its dependency files and `extension.yaml`, to the Playnite extensions directory. Deploy the complete output set together; copying a single DLL is not supported.

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

# Run the canonical test suite
dotnet run --project .\VNManager\Tests\Tests.csproj
```

The test suite includes Windows process-integration fixtures. It cleans up its `VnTest*` helper processes, but do not interrupt it midway unless necessary.

## Repository layout

```text
VNManager/
  Abstractions/        Shared request, result, error, and launcher contracts
  Adapter/             PowerShell boundary for Playnite
  Host/                Request validation, launcher resolution, transport, and exit codes
  LauncherSupervisor/  Standalone executable used by the plugin
  LocaleEmulator/      Locale Emulator launcher
  NoRegionLoader/      NoRegionLoader launcher
  ProcessManagement/   Monitoring and explicit cleanup
  PlaynitePlugin/      Optional Playnite 6 generic plugin
  Tests/               xUnit tests and integration coverage
docs/                  Architecture, protocol, design decisions, and launcher scope
```

## Further documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Adapter–Host protocol](docs/ADAPTER_HOST_PROTOCOL.md)
- [Launcher scope](docs/LAUNCHER_SCOPE.md)
- [Launcher contract](docs/Launcher%20Contract.md)
- [Design decisions](docs/DECISIONS.md)
- [Logging](docs/logging.md)

## License

This project is licensed under the [GNU GPL v3.0](LICENSE).
