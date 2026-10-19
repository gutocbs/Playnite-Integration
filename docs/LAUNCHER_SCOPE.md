# Launcher Scope

This document defines the responsibilities and boundaries of launcher implementations and the execution lifecycle between Playnite, the plugin, the supervisor and individual launchers.

## Launcher Selection

Launcher selection is the responsibility of the frontend.

The launcher system does not decide which launcher should be used. The plugin
reads the `[Launcher] <Name>` Feature and passes the logical identifier with
`--launcher`. The supervisor resolves that identifier and creates the
launcher-facing `LaunchRequest`.

Each game must resolve to exactly one launcher.

---

## Execution Lifecycle

`LauncherSupervisor.exe` remains running for the entire game session.

This is required because Playnite tracks the plugin's play action with
`TrackingMode.Process`: the lifetime of the supervisor process defines whether
the game is running and how playtime is calculated.

```text
Playnite → PlaynitePlugin → LauncherSupervisor → Launcher → Game
```

The supervisor must not exit immediately after starting the tool. Execution
remains active until the launcher reports that its lifecycle has finished:

```text
Game running → Launcher active → Supervisor alive → Playnite tracks the game as running
```

When the game session finishes or the launcher fails, the result propagates back:

```text
Game closes or launcher fails
        ▼
Launcher returns LaunchResult
        ▼
Supervisor exits with 0 or 1
        ▼
Playnite considers the game session finished
```

---

## Launcher Process Lifecycle

Each launcher decides what "complete" means for its strategy, which may include:

- waiting directly for the game process;
- monitoring a process created by an external launcher;
- waiting for auxiliary processes;
- running automation tools;
- performing cleanup before returning.

Launchers that start an external tool and then supervise the game share that
lifecycle through `Launcher.Supervision`. A launcher with a different strategy may
implement its own lifecycle.

The shared requirement is that a launcher must not return until its execution
lifecycle is considered complete.

---

## Structured Result and Exit Codes

Launchers return a structured `LaunchResult` containing `Success`, optional
`Data` and optional `Error`. The supervisor maps it to exit code `0` (success) or
`1` (failure, with the message on stderr).

---

## Cancellation

The process-management component may impose a configured timeout while waiting
for the game or a monitor candidate to start. This timeout ends as soon as a
process is detected and does not limit how long the running game may remain
open. Startup timeout failures use the specific `PROCESS_START_TIMEOUT` error.

When cancellation is requested through the `CancellationToken`, the launcher stops
monitoring and returns `LAUNCH_CANCELLED`. The game itself remains running and must
not be terminated merely because supervision was cancelled. The current
supervisor does not request cancellation.

---

## Playnite Behavior

Playnite starts the plugin's play action and tracks the supervisor's lifetime.
Because the supervisor stays alive while the game runs, the launcher system does
not need extra protection against Playnite starting the game a second time.

Once the supervisor terminates, Playnite considers the session finished.

---

## External Tools

Some launchers depend on external software such as Locale Emulator,
NoRegionLoader or AutoHotkey. Their configuration and validation belong to the
launcher that depends on them.

Process monitoring and cleanup are shared requirements and belong to the .NET
process-management component. Its JSON configuration keeps monitor processes,
global cleanup lists and executable-specific entries separate. A launcher
initiates supervision, but it does not own or duplicate those lists.
