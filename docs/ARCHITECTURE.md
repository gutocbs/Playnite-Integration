# Architecture

## Overview

The current scope is custom game launcher selection and execution for Playnite.

```text
Playnite
   │ Play
   ▼
PlaynitePlugin                (.NET Framework 4.6.2, Playnite SDK)
   │ dynamic Play Action: LauncherSupervisor.exe --executable … --launcher … [-- args]
   ▼
LauncherSupervisor            (.NET executable)
   │ HostComposition → LauncherRegistry
   ▼
ILauncher                     (LocaleEmulator, NoRegionLoader)
   │ Launcher.Supervision: start tool → monitor → map result → cleanup
   ▼
ProcessManagement             (monitoring and explicitly configured cleanup)
   │
   ▼
External tool and game process
```

The PlaynitePlugin is the only component that understands Playnite metadata,
Features, APIs and lifecycle behavior. Everything below it runs without Playnite.

## Architectural goals

### Metadata-driven frontend configuration

Games are configured through a Playnite Feature such as:

```text
[Launcher] Locale Emulator
```

The plugin reads the Feature and passes the logical launcher identifier to the
supervisor. The supervisor and launchers do not parse Features.

### Frontend independence

Playnite-specific data is translated in the plugin. Another frontend can start
`LauncherSupervisor.exe` with the same arguments and reuse every component below
it without modification.

### Explicit, small contracts

`LaunchRequest` carries `ExecutionId`, `Executable`, `Arguments` and an optional
`OnStarted` callback. Launchers return a structured `LaunchResult` with `Success`,
optional `Data` and optional `Error`. Shared error codes live in `LaunchErrorCodes`.
Details are in `docs/Launcher Contract.md`.

### Small composition

`HostComposition` loads configuration and explicitly registers each launcher in a
`LauncherRegistry`. There are no plugin frameworks, reflection-based discovery,
dynamic assembly loading or dependency-injection containers.

## Components

### PlaynitePlugin

- reads the game's `[Launcher]` Feature;
- selects the first File action with a path and expands Playnite variables;
- returns an `AutomaticPlayController` that runs `LauncherSupervisor.exe` with
  `TrackingMode.Process`, so Playnite tracks playtime for the supervisor's lifetime;
- hosts the VN Orchestrator install workflow (see `PlayniteInstallation`).

### LauncherSupervisor

- parses `--executable`, `--launcher`, `--configuration-directory`, `--log` and the
  game arguments after `--` (`SupervisorArguments`);
- creates the registry from `Configuration/*.json` beside the executable;
- resolves the launcher, runs it and returns exit code `0` on success or `1` with
  the error message on stderr.

### Host (composition library)

- `HostComposition` loads `locale-emulator.json`, `no-region-loader.json` and
  `process-management.json` and registers the launchers;
- `LauncherRegistry` resolves a case-insensitive logical name to exactly one
  registration (see `docs/LAUNCHER_RESOLUTION.md`).

### Launchers

Each launch method is an independent class library implementing `ILauncher`
(`LocaleEmulator`, `NoRegionLoader`). A launcher validates its own requirements,
builds the tool command line and delegates the shared lifecycle to
`Launcher.Supervision`. A launcher must not depend on Playnite, the supervisor or
another concrete launcher.

### Launcher.Supervision

The lifecycle shared by launchers that start an external tool:

- `LaunchValidation` — common checks (game executable, tool executable, polling interval);
- `ToolProcess` / `ILaunchedProcess` — start the tool process;
- `SupervisedLaunch.RunAsync` — start the tool, wait for the game through
  `ProcessManagement`, map the outcome to a `LaunchResult` and always run cleanup
  with its own 30-second budget.

### ProcessManagement

Configurable process detection, supervision and cleanup, shared by all launchers.

### AutoHotkey

AutoHotkey may be used when OS/application UI automation is necessary. The launcher
that invokes a script owns its configuration and lifecycle.

## Execution lifecycle

Playnite counts playtime while the supervisor process is alive. The supervisor does
not return until the launcher's execution lifecycle is complete or fails.

```text
Game running → ProcessMonitor waiting → Launcher waiting → Supervisor alive → Playnite tracking
```

Some tools spawn a different process and exit immediately. Configured engine
processes (`monitorProcesses`) are therefore preferred during a short detection
window, so a bootstrapper that exits early does not end the session.

## Process ownership

Processes and services may be stopped during cleanup only when they are explicitly
configured. Ending supervision never authorizes terminating the game.

## Error boundaries

- The plugin reports missing Features, actions or supervisor files in the Playnite log.
- The supervisor reports argument, configuration and resolution failures on stderr.
- A launcher reports tool, process and lifecycle failures as a `LaunchResult` with a
  stable code.

Errors keep the `ExecutionId` for log correlation.

## Dependency direction

```text
PlaynitePlugin ──(process boundary)──► LauncherSupervisor
                                          │
                                          ▼
                                        Host ──► LocaleEmulator ─┐
                                          │      NoRegionLoader ─┤
                                          │                      ▼
                                          │              Launcher.Supervision
                                          │                      │
                                          ▼                      ▼
                                     Abstractions ◄──── ProcessManagement ──► Logging
```

Concrete launchers reference `Abstractions`, `Logging`, `ProcessManagement` and
`Launcher.Supervision`, never the Host or the supervisor.

## Configuration philosophy

Configuration lives in `VNManager/LauncherSupervisor/Configuration/` and is copied
beside the executable. Executable locations, profile identifiers, timeouts, polling
intervals and process names are stored outside the code and loaded into typed
options. String literals remain appropriate for protocol values, error codes and
messages.

Process monitoring and cleanup configuration belongs to ProcessManagement:

- `monitorProcesses` — process names eligible for monitoring;
- `globalCleanupProcesses` / `globalCleanupServices` — cleanup for every execution;
- `executables` — per-executable `timeout` and cleanup lists.

`globalTimeout` applies to the game executable and monitor candidates unless an
entry in `executables` supplies its own `timeout`. Each candidate is eligible only
during its startup window. Once a process is detected, no runtime timeout applies.
Exhausting every candidate window produces `PROCESS_START_TIMEOUT`.

## Non-goals

- replacing Playnite as a library frontend;
- moving launcher logic into Playnite;
- Feature processing inside the supervisor;
- a generic plugin platform or dynamic launcher discovery;
- terminating a running game because supervision ended.
