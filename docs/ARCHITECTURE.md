# Architecture

## Status and migration context

This document describes the current target architecture.

The project began as a collection of PowerShell scripts integrated directly
with Playnite. Those scripts are legacy implementations and may remain in use
while their behavior is migrated incrementally to .NET.

New application logic should follow the hybrid runtime boundary documented
below:

> PowerShell integrates with the environment. .NET implements the application
> logic.

## Overview

The current scope is custom game launcher selection and execution.

```text
Playnite
   │
   ▼
Playnite Adapter
(PowerShell)
   │ DispatcherRequest
   ▼
Launcher-Host
(.NET executable)
   │
   ▼
Dispatcher
   │ LaunchRequest
   ▼
ILauncher
(.NET Class Library)
   │
   ▼
External Process
```

The Playnite Adapter is the only component that understands Playnite metadata,
Features, APIs and lifecycle behavior.

The .NET Host is independently executable and does not depend on Playnite.

## Architectural goals

### Metadata-driven frontend configuration

Games are normally configured through Playnite Features or equivalent frontend
metadata. For example:

```text
[Launcher] Locale Emulator
```

The Adapter interprets that metadata and sends the logical launcher identifier
to the Host. The Host and Launchers do not parse Features.

A future Feature-processing component may be introduced, but it is a separate
architectural concern and will not become part of Launcher-Host.

### Frontend independence

Playnite-specific data is translated at the Adapter boundary. Code below that
boundary must be executable without Playnite installed.

Another frontend should be able to create the same `DispatcherRequest` and reuse
the Host and Launchers without modifying them.

### Explicit, small contracts

The system uses two request contracts for different boundaries:

- `DispatcherRequest`: serialized request from the Adapter to the Host.
- `LaunchRequest`: validated internal request from the Host to an `ILauncher`.

Launchers return a structured `LaunchResult` containing `Success`, optional
`Data` and optional `Error`.

### Small orchestration components

The Host and its Dispatcher coordinate behavior. They do not contain concrete
launcher execution logic.

The initial architecture deliberately avoids plugin frameworks, reflection-based
discovery, dynamic assembly loading and unnecessary application layers.

## Runtime and technology boundaries

### Playnite Adapter — PowerShell

PowerShell is retained because it provides a simple boundary with Playnite and
the Windows environment.

The Adapter is responsible for:

- reading Playnite game data and Features;
- selecting exactly one logical launcher identifier;
- resolving the game executable and arguments exposed by Playnite;
- creating a unique `ExecutionId` at the start of each attempt;
- building and serializing a `DispatcherRequest`;
- invoking `Launcher-Host`;
- remaining active while the Host is active;
- initiating cancellation when requested by Playnite;
- receiving the Host exit code and, once the transport is defined, its
  structured result;
- presenting relevant failures through the frontend boundary.

The Adapter does not own launcher validation, resolution, dispatching, process
management or service orchestration.

### Launcher-Host — .NET executable

`Launcher-Host` is the standalone executable entry point.

It is responsible for:

- receiving and deserializing `DispatcherRequest`;
- validating process-boundary input;
- resolving the requested logical identifier to exactly one registered
  `ILauncher`;
- building the normalized `LaunchRequest`;
- invoking the selected Launcher;
- propagating cancellation;
- receiving and preserving `LaunchResult`;
- coordinating application-level logging and error handling;
- mapping the final outcome to a process exit code;
- returning the structured result once its transport is defined.

The Host does not:

- access Playnite APIs or objects;
- interpret Features;
- discover or execute PowerShell launcher scripts;
- contain concrete launcher execution behavior;
- assume a universal child-process lifecycle.

### Dispatcher

The Dispatcher is an internal routing responsibility of the Host.

It validates the launcher identifier, resolves a registered implementation and
invokes it through `ILauncher`. It has no knowledge of how the identifier was
selected or which frontend originated the request.

### Launcher-Abstractions — .NET Class Library

`Launcher-Abstractions` contains only the contracts shared between Host and
Launchers:

```text
Launcher-Abstractions
├── ILauncher
├── DispatcherRequest, if shared with transport code
├── LaunchRequest
├── LaunchResult
└── LaunchError
```

The exact location of `DispatcherRequest` may be refined when the transport
boundary is implemented. Launcher-facing abstractions must remain independent
of Host implementation details.

The core Launcher interface is conceptually:

```csharp
public interface ILauncher
{
    Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default);
}
```

### Launcher projects — .NET Class Libraries

Each launch method is implemented by an independent Class Library project that
implements `ILauncher`.

Example:

```text
Launcher-LocaleEmulator
└── LocaleEmulatorLauncher.cs
```

A Launcher is responsible for:

- validating requirements specific to its strategy;
- building the external command and arguments;
- starting required tools and the game;
- defining what constitutes the active execution session;
- monitoring the processes relevant to that definition;
- responding to cancellation by stopping supervision;
- cleaning up only resources it explicitly owns;
- returning `LaunchResult`.

A Launcher must not depend on Playnite, the Adapter, the Host implementation or
another concrete Launcher project.

### AutoHotkey

AutoHotkey may be used when OS/application UI automation is necessary, including
waiting for windows, interacting with dialogs and sending input.

AutoHotkey does not own metadata processing or application architecture. The
Launcher that invokes an AutoHotkey script owns its configuration and lifecycle.

## Contracts

The detailed contract is documented in `docs/Launcher Contract.md`.

### DispatcherRequest

The Adapter sends the Host a serialized request containing initially:

```text
ExecutionId
Executable
Arguments
Launcher
```

`ExecutionId` is created once by the Adapter and propagated unchanged through
the Host, Launcher and logging context.

### LaunchRequest

The Host validates and normalizes the boundary request, then passes a
`LaunchRequest` to the selected Launcher. It initially contains:

```text
ExecutionId
Executable
Arguments
```

The logical launcher name is consumed during Host resolution and is not required
by the concrete Launcher unless a future requirement demonstrates otherwise.

### LaunchResult

Launchers return:

```text
Success
Data
Error
```

`Error` is null on success and contains at least a stable `Code` and technical
`Message` on failure.

## Launcher resolution and extension model

For the initial .NET implementation, the Host explicitly knows the available
Launcher implementations.

```text
"Locale Emulator"
        ↓
LocaleEmulatorLauncher : ILauncher
```

Adding a Launcher requires:

1. creating a Class Library project;
2. referencing `Launcher-Abstractions`;
3. implementing `ILauncher`;
4. registering the implementation in `Launcher-Host`.

Names are case-insensitive logical identifiers, not paths, URIs, assembly names
or .NET type names. Missing and ambiguous registrations fail explicitly without
fallback.

Filesystem discovery of `.ps1` launchers belongs to the legacy PowerShell
architecture. Dynamic .NET discovery may be considered later only for a
concrete requirement.

## Execution lifecycle

The Adapter remains active for the whole game session because Playnite tracks
the configured action lifetime to determine whether the game is running and to
calculate playtime.

```text
Game running
   │
Launcher monitoring
   │
Host waiting
   │
Adapter waiting
   │
Playnite tracking session
```

Each Launcher defines what its own execution lifecycle means. Depending on the
strategy, it may wait for the direct game process, a process created by an
external launcher, auxiliary automation or launcher-owned cleanup.

The Launcher does not return until its execution lifecycle is complete, fails or
is cancelled.

## Process ownership

The Host provides application infrastructure, but it does not impose a universal
process-tracking strategy.

Some external tools spawn a different process and exit immediately. Therefore,
each Launcher decides which processes it must observe and which resources it
owns.

Processes may be terminated during cleanup only when they are explicitly owned
by the Launcher and termination is part of its documented strategy.

## Cancellation

During a graceful cancellation, the signal originates at the Adapter and is
propagated to the Host and active Launcher through a `CancellationToken`.

If the Adapter process is terminated before it can send that signal, the Host
must detect loss of the Adapter through the chosen transport or parent-process
liveness mechanism and translate that event into the same cancellation path.

If Playnite independently ends or cancels the Adapter, the intended behavior is:

```text
Adapter signals cancellation
        ↓
Host propagates cancellation
        ↓
Launcher stops lifecycle monitoring
        ↓
Game continues running unmonitored
```

Cancellation does not authorize the Host or Launcher to terminate the game.

The cross-process cancellation and Adapter-liveness mechanism is not yet defined
and must be planned before implementation.

## Results and exit codes

The system uses both a structured result and a process exit code:

- `LaunchResult` carries application-level outcome details inside .NET.
- The Host exit code communicates the overall process outcome to the Adapter and
  Playnite.

The mechanism used to return the complete structured result across the process
boundary is not yet defined. Protocol output must remain separate from logging
and diagnostic console output.

The exact exit-code values and their mapping from `LaunchResult` will be
documented separately before implementation.

## Logging

PowerShell and .NET use separate logging implementations with the same logical
event model:

```text
Level
Component
Message
Data
ExecutionId
```

Both implementations initially write compatible events to the same timeline.
They must use the same timestamp, level and serialization conventions.

Because Adapter and Host are separate processes, concurrent file access must be
handled explicitly. The initial implementation should prefer atomic append
operations with short-lived file handles and a defined fallback that never
blocks game launch. If reliable shared-file writes prove difficult, separate
per-process files correlated by `ExecutionId` are the preferred fallback over a
cross-runtime logging service.

Logging failures must not prevent a game from launching.

## Error boundaries

Errors are handled by the layer with enough context to describe them:

- The Adapter reports frontend metadata and transport-start failures.
- The Host reports deserialization, validation and resolution failures.
- A Launcher reports strategy-specific tool, process and lifecycle failures.
- The Adapter presents the final outcome to Playnite when appropriate.

Errors should use structured codes and retain `ExecutionId` for correlation.

## Solution structure

The intended solution structure is:

```text
Launcher.sln

src/
├── Launcher-Host
├── Launcher-Abstractions
└── Launcher-LocaleEmulator

tests/
├── Launcher-Host.Tests
└── Launcher-LocaleEmulator.Tests
```

Further separation should be introduced only for concrete responsibilities.

## Dependency direction

```text
Playnite Adapter (PowerShell)
        │ process boundary
        ▼
Launcher-Host ───────► Launcher-Abstractions
        │                       ▲
        ▼                       │
Launcher-LocaleEmulator ────────┘
```

Expressed as project references:

```text
Launcher-Host
    → Launcher-Abstractions
    → Launcher-LocaleEmulator

Launcher-LocaleEmulator
    → Launcher-Abstractions
```

Concrete Launcher projects must not reference `Launcher-Host`.

## Configuration philosophy

Game configuration should prefer reusable metadata rather than game-name
branches. Game-specific code should exist only when behavior cannot be expressed
through a reusable Launcher or future Feature component.

External tool configuration belongs initially to the Launcher that requires it.
A shared configuration model should be introduced only after multiple concrete
Launchers demonstrate the same need.

## Initial scope

The first .NET implementation contains only:

```text
Launcher Core
├── Contracts
│   ├── DispatcherRequest
│   ├── LaunchRequest
│   ├── LaunchResult
│   └── LaunchError
├── Dispatcher
└── Launchers
    └── LocaleEmulatorLauncher
```

The following are outside the initial scope:

- Feature-processing architecture;
- dynamic launcher or assembly discovery;
- dependency-injection frameworks unless a concrete need appears;
- external plugin directories;
- runtime installation of Launchers;
- universal process supervision;
- additional frontend abstractions beyond the contract boundary.

## Legacy architecture

The original system used PowerShell dispatcher maps and filesystem scripts such
as:

```powershell
$launchers = [ordered]@{
    "[JP] Locale Emulator" = "Start-LocaleEmulatorLauncher"
    "[JP] NoRegionLoader"  = "Start-NoRegionLoaderLauncher"
}
```

and later resolved conventions such as:

```text
[Launcher] Locale Emulator
        ↓
Launchers/Locale Emulator.ps1
```

These mechanisms may remain operational during migration but are not the design
target for new .NET work.

## Non-goals

The project is not intended to:

- replace Playnite as a library frontend;
- move launcher logic into Playnite;
- turn Launcher-Host into a Feature-processing subsystem;
- provide a generic PowerShell or .NET plugin platform;
- introduce complex abstractions without a demonstrated requirement;
- terminate a running game merely because monitoring was cancelled;
- move UI automation architecture into AutoHotkey.
