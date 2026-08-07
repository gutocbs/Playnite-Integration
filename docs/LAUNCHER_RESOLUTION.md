# Launcher Resolution

## Purpose

This document defines how `Launcher-Host` resolves the logical launcher name
received in a `DispatcherRequest` into a registered .NET `ILauncher`
implementation.

The original filesystem-based discovery of PowerShell scripts is legacy and is
not part of the current Host architecture.

## Responsibilities

The Playnite Adapter selects the logical launcher name from frontend metadata.
It does not resolve .NET types or implementation locations.

The Host is responsible for:

- validating the launcher identifier;
- matching it against registered `ILauncher` implementations;
- rejecting missing or ambiguous registrations;
- invoking the selected implementation through the shared contract.

The Host does not interpret Features. Feature processing belongs to the
frontend today and may become a separate component in the future.

## Input

The Host receives a logical name such as:

```text
Locale Emulator
```

The value is an identifier. It is not a filesystem path, URI, assembly name or
.NET type name.

## Registration model

For the initial implementation, available Launchers are explicitly registered
in `Launcher-Host`.

Conceptually:

```text
"Locale Emulator"
        ↓
LocaleEmulatorLauncher : ILauncher
```

Adding a Launcher currently requires adding its Class Library reference and its
registration to the Host. Dynamic assembly discovery, reflection-based loading
and external plugin directories are outside the initial scope.

## Case sensitivity

Launcher matching is case-insensitive:

```text
Locale Emulator
locale emulator
LOCALE EMULATOR
```

The Host preserves the value originally supplied by the Adapter for logs and
error reporting.

## Invalid launcher names

The Host must reject an empty or whitespace-only launcher name.

Because launcher names are identifiers rather than paths, values that look like
paths or external resources are also invalid. At minimum, the Host rejects names
containing or representing:

```text
/
\
..
:
C:\External\Launcher.dll
\\server\share\Launcher.dll
https://example.com/Launcher.dll
```

Invalid values must not be silently changed into valid identifiers.

## Resolution cardinality

Resolution must produce exactly one registered implementation.

The Host fails when:

- no registered launcher matches the requested identifier; or
- more than one registration matches under case-insensitive comparison.

It must not select an arbitrary implementation or fall back to another
Launcher.

## Resolution failures

The Host returns a structured error for resolution failures. Initial categories
may include:

```text
INVALID_LAUNCHER_NAME
LAUNCHER_NOT_FOUND
AMBIGUOUS_LAUNCHER
```

The final error-code naming convention belongs to the .NET contract and may be
refined when the contracts are implemented.

## Design principle

Launcher names are stable logical identifiers. The Adapter requests a launcher;
the Host owns the mapping to an in-process `ILauncher` implementation.

Filesystem details from the legacy PowerShell implementation do not cross the
current Dispatcher contract.
