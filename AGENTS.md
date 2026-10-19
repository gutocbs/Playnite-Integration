# AGENTS.md — Playnite VN Manager

Instructions for AI assistants (Codex, Claude and others) working in this repository.

## Project overview

Playnite VN Manager is a Windows launcher compatibility layer for Visual Novels in
Playnite. The Playnite plugin selects a launcher from the game's `[Launcher] <Name>`
Feature and runs the game through `LauncherSupervisor.exe`, a standalone .NET executable
that starts the launcher tool (Locale Emulator or NoRegionLoader), supervises the game
process and runs only explicitly configured cleanup.

```text
Playnite
  -> PlaynitePlugin (dynamic Play Action)
  -> LauncherSupervisor.exe
  -> HostComposition / LauncherRegistry
  -> ILauncher (LocaleEmulator, NoRegionLoader)
  -> Launcher.Supervision + ProcessManagement
  -> game process
```

The plugin also hosts the VN Orchestrator install workflow (`PlayniteInstallation`,
`PlaynitePlugin/Installation`), which talks to a separate installer process through the
`VNInstallation.Protocol` package.

## Tech stack

- .NET 10 / C# with nullable reference types and implicit usings.
- .NET Framework 4.6.2 Playnite plugin (PlayniteSDK 6.16.0); `PlayniteInstallation` targets netstandard2.0.
- JSON configuration loaded into typed options.
- xUnit v3 tests, including Windows process integration tests.

## Components

- `VNManager/Abstractions`: `ILauncher`, `LaunchRequest`, `LaunchResult`, `LaunchError`, `LaunchErrorCodes`.
- `VNManager/Host`: composition library — `HostComposition` loads configuration and registers launchers in `LauncherRegistry`.
- `VNManager/LauncherSupervisor`: the executable started by the plugin; owns `Configuration/*.json`.
- `VNManager/Supervision` (`Launcher.Supervision`): what launchers share — start the tool, monitor, map the result, clean up.
- `VNManager/LocaleEmulator`, `VNManager/NoRegionLoader`: one launcher each; kept as separate projects.
- `VNManager/ProcessManagement`: configurable process monitoring and explicitly configured cleanup.
- `VNManager/Logging`: structured text logging.
- `VNManager/PlaynitePlugin`: the only project that references the Playnite SDK.
- `VNManager/PlayniteInstallation`: install-workflow rules and transport (Playnite-free).
- Tests: `VNManager/Tests` (+ `VNManager/TestAssets` fixtures), `VNManager/PlaynitePlugin.Tests`, `VNManager/PlayniteInstallation.Tests`.

## Conventions

- Keep Playnite SDK usage inside `PlaynitePlugin`. Host, Supervision, launchers and ProcessManagement must not depend on Playnite.
- Add a launcher as a class library implementing `ILauncher`, build it on `Launcher.Supervision`, and register it explicitly in `HostComposition`. No dynamic discovery without a validated requirement.
- Launcher names are case-insensitive logical identifiers, never paths.
- Keep operational values (tool paths, profiles, timeouts, process lists) in JSON configuration, not in code.
- Process cleanup may terminate only explicitly configured processes or services. Cancellation must not terminate the game.
- Use structured `LaunchResult`/`LaunchError` values with stable error codes.
- Preserve backwards compatibility unless explicitly told otherwise.

## Testing

```text
dotnet run --project VNManager/Tests/Tests.csproj
dotnet run --project VNManager/PlaynitePlugin.Tests/PlaynitePlugin.Tests.csproj
dotnet run --project VNManager/PlayniteInstallation.Tests/PlayniteInstallation.Tests.csproj
```

The `Tests` suite starts only the dedicated `VnTest*` executables under `VNManager/TestAssets`
and must clean up every persistent process it starts. Do not interrupt it midway.

## Documentation

Read before architectural changes:

- `docs/ARCHITECTURE.md`
- `docs/DECISIONS.md`
- `docs/Launcher Contract.md`
- `docs/LAUNCHER_RESOLUTION.md`
- `docs/LAUNCHER_SCOPE.md`
- `docs/logging.md`
- `docs/Principios de Design.md`

## Instructions for AI assistants

- Do not alter external contracts without validation.
- Respect the existing architecture and dependency direction.
- Prioritize consistency with the current code.
- Explain architectural changes before implementing large refactors.
- If code and documentation conflict, stop and describe the conflict instead of guessing.
