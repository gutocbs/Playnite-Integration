# Adapter-Host Protocol

## Scope

This document defines the initial Windows process-boundary protocol between the
PowerShell Playnite Adapter and the .NET Launcher Host.

## Invocation

The Adapter starts the Host with these arguments:

```text
--request <absolute request JSON path>
--result <absolute result JSON path>
--cancel-event <named Windows event>
--adapter-pid <PowerShell process identifier>
--log <absolute shared log path>
--configuration-directory <absolute Host configuration directory>
--transport-root <absolute VNManager temporary transport directory>
```

Protocol data is never written to stdout. Logs are written only through the
shared logical logging model.

## Request and result files

The Adapter creates one unique `ExecutionId` and a dedicated execution
directory:

```text
%TEMP%\VNManager\{ExecutionId}\
├── request.json
└── result.json
```

The request is a JSON-serialized `DispatcherRequest`. The Adapter writes it to
a unique `.part` file and atomically moves that file to the final request path
before starting the Host.

The Host writes a JSON result containing:

```text
ExecutionId
Success
Data
Error
```

The Host also uses a unique `.part` file and atomic replacement. The Adapter
reads the complete result after the Host exits and then removes the transport
files and execution directory it owns.

At startup, the Host starts a parallel best-effort cleanup of stale execution
directories. It never removes the current execution, ignores directories whose
names are not GUIDs and removes a previous execution only when the most recent
directory or contained-file modification exceeds the configured maximum age.
The retention is expressed in seconds by
`temporaryDirectoryMaximumAgeSeconds` in `host.json`. Cleanup failure must not
block launching.

## Cancellation and Adapter liveness

For each execution, the Adapter creates a manual-reset Windows named event and
passes its name to the Host. Signalling that event requests graceful
cancellation.

The Adapter also passes its process identifier. The Host monitors that process
and triggers the same `CancellationToken` if the Adapter exits unexpectedly.

Cancellation stops supervision. It does not authorize termination of the game.
Only explicitly configured cleanup processes may be terminated.

## Exit codes

The Host uses these stable process exit codes:

| Code | Meaning |
| ---: | --- |
| `0` | Launch lifecycle completed successfully. |
| `10` | Invalid Host invocation or result transport failure. |
| `20` | Invalid or unreadable `DispatcherRequest`. |
| `30` | Launcher validation or resolution failed. |
| `40` | The selected Launcher returned a failure. |
| `50` | Unexpected Host failure. |
| `60` | Supervision was cancelled. |

The JSON result remains the authoritative source for the stable error code and
technical message. The process exit code communicates only the broad outcome
category.
