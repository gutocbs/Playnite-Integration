## ADR-001 - Playnite must not own launcher logic

Status: Accepted

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
`ILauncher` and is explicitly registered in `Launcher-Host`.

Dynamic assembly or plugin discovery may be reconsidered only when a concrete
requirement appears.

## ADR-003 - PowerShell is the integration boundary and .NET owns application logic

Status: Accepted

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
