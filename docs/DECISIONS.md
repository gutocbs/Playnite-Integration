## ADR-001 - Playnite must not own launcher logic

Status: Superseded by ADR-005

The compatibility layer does not depend on Playnite as its core runtime.

Playnite acts only through a thin PowerShell Adapter that translates its game
metadata into the internal launcher model and invokes the standalone .NET Host.

Reason:

The system should remain portable to another frontend in the future.

## ADR-002 - Filesystem-based PowerShell launcher discovery

Status: Legacy

The original PowerShell architecture discovered launcher scripts by convention.

Example:

```text
[Launcher] Locale Emulator
        ↓
Launchers/Locale Emulator.ps1
```

This decision does not apply to the current .NET architecture. In the initial
.NET implementation, each Launcher is a Class Library that implements
`ILauncher` and is explicitly registered in `HostComposition`.

Dynamic assembly or plugin discovery may be reconsidered only when a concrete
requirement appears.

## ADR-003 - PowerShell is the integration boundary and .NET owns application logic

Status: Superseded by ADR-005

PowerShell is retained for the Playnite Adapter. Request validation, launcher
resolution, dispatching, process coordination and launcher implementations live
in the .NET solution.

Existing PowerShell launchers are transitional and may be migrated incrementally.

## ADR-004 - Playnite integration plugin is an optional frontend adapter

Status: Accepted

The solution may contain a `PlaynitePlugin` project that references the Playnite
SDK and implements dynamic Play Actions based on the `[Launcher]` Feature.
Playnite-specific code must remain isolated in that project. The Host,
LauncherSupervisor, Abstractions and launcher projects remain Playnite-free and
continue to be reusable by other frontends.

## ADR-005 - PowerShell adapter removed

Status: Accepted

The PowerShell Adapter, the `Host.exe` file protocol (request/result JSON files,
named cancellation event and Host exit codes) and the legacy
`RunWithLocaleEmulator.ps1` script were removed. The PlaynitePlugin is the
frontend: it injects a dynamic Play Action that runs `LauncherSupervisor.exe`,
which is the single entry point. The Host project is now a composition library
(`HostComposition`, `LauncherRegistry`) used by the supervisor.

The intent of ADR-001 is kept: Playnite-specific code stays in the plugin, and
everything below it remains Playnite-free and reusable by another frontend.

Reason:

The plugin had replaced the script-based integration; keeping an unused second
entry point duplicated protocol, validation and documentation.

## ADR-006 - Shared launcher supervision project

Status: Accepted

`LocaleEmulatorLauncher` and `NoRegionLoaderLauncher` start different tools and
remain separate classes in separate projects. The lifecycle they share — common
validation, starting the tool process, waiting for the game through
ProcessManagement, mapping the outcome to a `LaunchResult` and running cleanup —
lives in the `Launcher.Supervision` project. Shared error codes live in
`LaunchErrorCodes` in Abstractions.

Reason:

Both launchers were about 90% identical. A shared project removes the
duplication without merging launchers that wrap different programs.
