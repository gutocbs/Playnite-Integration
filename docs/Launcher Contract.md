# Launcher Contracts

## Contract boundaries

```text
PlaynitePlugin
      │ command line: --executable <path> --launcher <name> [-- <game arguments>]
      ▼
LauncherSupervisor
      │ LaunchRequest
      ▼
ILauncher
      │ LaunchResult
      ▼
LauncherSupervisor → exit code 0 / 1
```

The command line is the process-boundary contract between the frontend and the
supervisor. `LaunchRequest` is the internal contract passed to the resolved
launcher.

## Supervisor command line

| Argument | Required | Default | Description |
| --- | --- | --- | --- |
| `--executable` | Yes | — | Game executable; converted to an absolute path. |
| `--launcher` | No | `Locale Emulator` | Logical launcher name. |
| `--configuration-directory` | No | `<supervisor>\Configuration` | Directory with the JSON configuration. |
| `--log` | No | `<supervisor>\launcher.log` | Shared log file. |
| `--` | No | — | Every following item is a game argument. |

When Playnite is the frontend, the executable and arguments come from the File
action returned by `PlayniteApi.ExpandGameVariables(game, action)`.

## LaunchRequest

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `ExecutionId` | string | Yes | Correlation identifier created by the supervisor. |
| `Executable` | string | Yes | Absolute game executable path. |
| `Arguments` | string[] | Yes | Game arguments; may be empty. |
| `OnStarted` | callback | No | Invoked with the monitored process name once the game is detected. |

The launcher identifier is consumed during resolution and is not part of
`LaunchRequest`.

## LaunchResult

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `Success` | bool | Yes | Whether the launcher completed its responsibility successfully. |
| `Data` | object? | No | Structured result data (for example `<Tool>ProcessId`, `MonitoredProcess`). |
| `Error` | `LaunchError`? | No | Failure information; null when `Success` is true. |

## LaunchError

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `Code` | string | Yes | Stable machine-readable error code. |
| `Message` | string | Yes | Technical description of the failure. |

Codes shared by launchers are defined in `LaunchErrorCodes`: `LAUNCH_CANCELLED`,
`PROCESS_START_TIMEOUT`, `INVALID_GAME_EXECUTABLE`, `GAME_EXECUTABLE_NOT_FOUND` and
`INVALID_POLLING_INTERVAL`. Tool-specific codes (for example
`LOCALE_EMULATOR_NOT_FOUND`) stay in their launcher.

## Result across the process boundary

The supervisor exits with `0` when `Success` is true. Otherwise it writes
`LauncherSupervisor: <message>` to stderr and exits with `1`.

## Cancellation

`ILauncher.LaunchAsync` accepts a `CancellationToken`. Cancellation stops
supervision, still runs launcher-owned cleanup and returns `LAUNCH_CANCELLED`; it
never terminates the game. The current supervisor does not request cancellation:
it supervises until the game exits.

## Responsibilities

### Frontend (PlaynitePlugin)

- Interpret Playnite metadata and Features.
- Select the File action and expand Playnite variables.
- Start the supervisor and keep Playnite tracking it.

### LauncherSupervisor and Host

- Parse the command line.
- Load configuration and resolve exactly one registered launcher.
- Build `LaunchRequest`, invoke `ILauncher` and map the result to the exit code.

They do not interpret Features.

### Launcher

- Validate launcher-specific requirements.
- Build and start its tool command.
- Delegate the shared lifecycle to `Launcher.Supervision`.
- Return a structured `LaunchResult`.

The launcher does not depend on Playnite or interpret Features.
