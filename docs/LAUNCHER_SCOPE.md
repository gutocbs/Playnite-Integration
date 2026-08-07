# Launcher Scope

This document defines the responsibilities and boundaries of launcher implementations and the execution lifecycle between Playnite, the Adapter, the Host and individual Launchers.

## Launcher Selection

Launcher selection is the responsibility of the Frontend.

The launcher system does not decide which launcher should be used. The Adapter
selects the logical launcher identifier and sends it in `DispatcherRequest`.
The Host validates and resolves that identifier, then creates the
Launcher-facing `LaunchRequest`.

Each game must resolve to exactly one launcher.

Validation that a game has a valid launcher configuration is performed by the Frontend before invoking the launcher system.

---

## Execution Lifecycle

The Playnite Adapter remains running for the entire game session.

This behavior is required because Playnite uses the lifetime of the configured launch action to track whether the game is still running and to calculate playtime.

The expected execution chain is:

```text
Playnite
   │
   ▼
Playnite Adapter
   │
   ▼
Launcher.Host
   │
   ▼
Launcher
   │
   ▼
Game
```

The Adapter must not exit immediately after requesting the launch.

Instead, execution remains active through the entire chain until the Launcher reports that its execution lifecycle has finished.

Conceptually:

```text
Game running
   │
Launcher active
   │
Host waiting
   │
Adapter waiting
   │
Playnite tracks game as running
```

When the game session finishes or the Launcher encounters an error, the result propagates back through the same chain:

```text
Game closes or Launcher fails
        │
        ▼
Launcher returns LaunchResult
        │
        ▼
Host preserves LaunchResult and returns exit code
        │
        ▼
Adapter exits
        │
        ▼
Playnite considers the game session finished
```

The Adapter lifecycle therefore mirrors the launcher execution lifecycle.

---

## Launcher Process Lifecycle

The internal process lifecycle is implementation-specific to each Launcher.

Different Launchers may require different behavior, including:

- waiting directly for the game process;
    
- monitoring a process created by an external launcher;
    
- waiting for auxiliary processes;
    
- running automation tools;
    
- performing cleanup before returning.
    

There is no common process-management strategy imposed on all Launchers.

The only shared requirement is that a Launcher must not return control to the Host until its execution lifecycle is considered complete.

Each Launcher is responsible for defining what "complete" means for its own implementation.

---

## Structured Result and Exit Codes

Launchers communicate their final execution state to the Host through a
structured `LaunchResult` containing `Success`, optional `Data` and optional
`Error`.

The Host maps the overall result to a process exit code, which propagates across
the process boundary:

```text
Launcher.Host
   ↓
Playnite Adapter
```

The Host should preserve the Launcher result unless it encounters an error of
its own.

The Adapter uses the returned result to terminate its own execution.

The exact exit-code values and their meanings are defined separately from this
document. The transport used to return the complete structured result to the
Adapter is not defined yet and must keep protocol data separate from logs and
diagnostic output.

---

## Cancellation

During graceful cancellation, the signal originates at the Adapter and is
propagated through the Host to the active Launcher. If the Adapter terminates
before it can signal, the Host must detect the lost Adapter connection or
process lifetime and trigger the same cancellation behavior.

When cancellation is requested, the Launcher stops monitoring or validating the
game process lifecycle and returns control. The game itself remains running and
must not be terminated merely because supervision was cancelled.

The mechanism used to signal cancellation and detect unexpected Adapter
termination is still to be defined.

---

## Playnite Behavior

Playnite is responsible only for starting the configured Adapter and tracking its lifetime.

The Adapter remains alive while the launcher execution is active.

Therefore, the launcher system does not need to implement additional protection against Playnite launching the game a second time.

Fallback execution by Playnite is not part of the current architecture.

Once the Adapter terminates, Playnite considers the execution session finished.

---

## External Tools

Some Launchers may depend on external software such as Locale Emulator or AutoHotkey.

Configuration, validation and lifecycle management of those tools are defined by the Launcher that depends on them.

No shared configuration mechanism is defined at the architectural level at this stage.

A common model should only be introduced if concrete Launcher implementations demonstrate a shared requirement.
