# Launcher Contracts

## Status

The former PowerShell `LauncherRequest`/`LauncherResponse` contract is legacy.
Its core ideas are retained in the .NET contracts described here, using the .NET
naming established by the current architecture.

## Contract boundaries

There are two distinct request contracts:

```text
Playnite Adapter
      │ DispatcherRequest
      ▼
Launcher-Host
      │ LaunchRequest
      ▼
ILauncher
      │ LaunchResult
      ▼
Launcher-Host
```

`DispatcherRequest` is the serialized process-boundary contract sent from the
Adapter to the Host. `LaunchRequest` is the validated internal contract sent by
the Host to the selected Launcher.

## DispatcherRequest

The PowerShell Adapter creates the `DispatcherRequest` after reading and
interpreting Playnite metadata and Features.

Initial fields:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `ExecutionId` | string | Yes | Correlation identifier created once by the Adapter. |
| `Executable` | string | Yes | Absolute path of the game executable. |
| `Arguments` | string[] | Yes | Game arguments; may be empty. |
| `Launcher` | string | Yes | Logical name of the requested Launcher. |

The Host does not receive Playnite objects and does not interpret Features.

The exact serialization and transport framing are still to be defined. JSON is
the preferred initial representation.

## LaunchRequest

After deserializing and validating the `DispatcherRequest`, the Host creates the
`LaunchRequest` passed to `ILauncher.LaunchAsync`.

Initial fields:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `ExecutionId` | string | Yes | The same identifier created by the Adapter. |
| `Executable` | string | Yes | Validated game executable path. |
| `Arguments` | string[] | Yes | Validated game arguments; may be empty. |

The launcher identifier is used by the Host for resolution and does not need to
be part of the initial `LaunchRequest` unless a concrete Launcher requirement
demonstrates otherwise.

Future compatible additions may include:

- `WorkingDirectory`;
- `EnvironmentVariables`;
- `Metadata`.

## LaunchResult

Every Launcher returns a `LaunchResult`.

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `Success` | bool | Yes | Whether the Launcher completed its responsibility successfully. |
| `Data` | object? | No | Optional structured result data. |
| `Error` | `LaunchError`? | No | Failure information; null when `Success` is true. |

Possible future `Data` fields include process identifiers and lifecycle details,
but they are not part of the initial public contract.

## LaunchError

When `Success` is false, `Error` contains at least:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `Code` | string | Yes | Stable machine-readable error code. |
| `Message` | string | Yes | Technical description of the failure. |

## Result across the process boundary

Internally, Launchers and the Host use the structured `LaunchResult`. The Host
also terminates with an exit code so that the Adapter can report the overall
process outcome to Playnite.

The transport used to return complete structured result details to the Adapter
is not defined yet. It must be planned before implementation. The design must
keep protocol output separate from logs and diagnostic console output.

Until that transport is defined, implementations must not assume that stdout is
available simultaneously for logs and result serialization.

## Cancellation

During graceful cancellation, the signal originates at the Adapter and is
propagated to the Host, which passes a `CancellationToken` to
`ILauncher.LaunchAsync`.

If the Adapter is terminated before it can send a signal, the Host must detect
the lost Adapter connection or process lifetime through the transport mechanism
and trigger the same cancellation token.

If Playnite independently terminates or cancels the Adapter:

1. the Adapter signals cancellation to the Host;
2. the Host propagates cancellation to the active Launcher;
3. the Launcher stops monitoring or validating the game process lifecycle;
4. the game process remains running;
5. only resources explicitly owned by the Launcher and safe to clean up may be
   disposed according to that Launcher's policy.

Cancellation is therefore a request to stop supervision, not a request to
terminate the game.

The transport used to send cancellation and detect unexpected Adapter
termination across the process boundary is still to be defined.

## Responsibilities

### Adapter

- Interpret Playnite metadata and Features.
- Create `ExecutionId`.
- Build and serialize `DispatcherRequest`.
- Start the Host and propagate cancellation.
- Remain active while the Host is active.
- Receive the final process exit code and, once defined, the structured result.

### Host and Dispatcher

- Deserialize and validate `DispatcherRequest`.
- Resolve exactly one registered Launcher.
- Build `LaunchRequest`.
- Invoke `ILauncher` and pass cancellation.
- Receive and preserve `LaunchResult`.
- Map the overall outcome to the documented process exit code.

They do not interpret Features or discover PowerShell scripts.

### Launcher

- Validate Launcher-specific requirements.
- Execute its launch strategy.
- Define and monitor its own execution lifecycle.
- Stop supervision without terminating the game when cancellation is requested.
- Return a structured `LaunchResult`.

The Launcher does not depend on Playnite or interpret Features.
