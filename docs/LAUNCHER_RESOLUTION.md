# Launcher Resolution

## Purpose

This document defines how `LauncherRegistry` (Host composition library) resolves
the logical launcher name received by `LauncherSupervisor` (`--launcher`) into a
registered .NET `ILauncher` implementation.

## Responsibilities

The PlaynitePlugin selects the logical launcher name from frontend metadata.
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

Available launchers are explicitly registered in `HostComposition`.

Conceptually:

```text
"Locale Emulator"
        ↓
LocaleEmulatorLauncher : ILauncher
```

Adding a launcher requires adding its class library reference and its
registration to the Host. Dynamic assembly discovery, reflection-based loading
and external plugin directories are out of scope.

## Case sensitivity

Launcher matching is case-insensitive:

```text
Locale Emulator
locale emulator
LOCALE EMULATOR
```

The Host preserves the value originally supplied by the frontend for logs and
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

The Host raises `LauncherResolutionException` with one of these codes:

```text
INVALID_LAUNCHER_NAME
LAUNCHER_NOT_FOUND
AMBIGUOUS_LAUNCHER
```

## Design principle

Launcher names are stable logical identifiers. The frontend requests a launcher;
the Host owns the mapping to an in-process `ILauncher` implementation.
