## Project Overview

VNManager is a Windows launcher compatibility layer for Visual Novels. It integrates Playnite game metadata with a standalone .NET launcher runtime, currently providing the Locale Emulator launch strategy and lifecycle/process supervision.

## Tech Stack

- .NET 10 / C# with nullable reference types and implicit usings.
- .NET Framework 4.6.2 Playnite plugin using PlayniteSDK 6.16.0.
- PowerShell adapter for the Playnite and Windows boundary.
- JSON configuration and request/result transport files.
- xUnit v3 tests, including Windows process integration tests.

## Architecture

The Playnite-facing layer translates game information into a `DispatcherRequest` and launches the .NET Host. The Host validates the request, resolves an explicitly registered `ILauncher`, and dispatches a normalized `LaunchRequest`. A launcher owns strategy-specific external process startup, monitoring, cleanup, and result creation.

The Adapter/Host boundary uses per-execution JSON files and a named Windows cancellation event. The Adapter remains alive for the game session; cancellation stops supervision but does not terminate the game. PowerShell is an integration boundary, while application logic belongs in .NET.

## Main Components

- `VNManager/Abstractions`: shared launcher contracts (`DispatcherRequest`, `LaunchRequest`, `LaunchResult`, `LaunchError`, `ILauncher`).
- `VNManager/Host`: executable entry point, validation, launcher registry/dispatch, transport, cancellation, temporary-file cleanup, and exit-code mapping.
- `VNManager/LocaleEmulator`: Locale Emulator launcher implementation and typed options.
- `VNManager/ProcessManagement`: configurable process monitoring and explicitly configured cleanup.
- `VNManager/Logging`: shared structured application logging abstractions.
- `VNManager/LauncherSupervisor`: Windows executable that supervises the Host.
- `VNManager/Adapter`: PowerShell adapter; legacy environment/Playnite integration boundary.
- `VNManager/PlaynitePlugin`: optional isolated Playnite SDK plugin that packages the supervisor.
- `VNManager/Tests` and `VNManager/TestAssets`: xUnit suite and dedicated process fixtures.

## Project Conventions

- Keep Playnite SDK/API usage isolated to `PlaynitePlugin`; the Adapter is the only PowerShell component that understands Playnite metadata.
- Keep the Adapter thin: it selects the logical launcher and transports requests, but does not implement launcher orchestration.
- Keep the Host and concrete launchers independent of Playnite and PowerShell.
- Add launchers as class libraries implementing `ILauncher` and explicitly register them in the Host; do not add dynamic launcher discovery without a validated requirement.
- Preserve the boundary contracts and JSON file protocol. Use structured `LaunchResult`/`LaunchError` values and stable Host exit-code categories.
- Put operational values (paths, profiles, timeouts, process lists) in typed JSON-backed configuration rather than hard-coding them.
- Treat PowerShell launcher-script discovery as legacy and migrate behavior incrementally to .NET.
- Process cleanup may terminate only explicitly configured processes. Cancellation must not terminate the game.
- Run the canonical test command: `dotnet run --project VNManager/Tests/Tests.csproj`. Persistent `VnTest*` integration fixtures must always be cleaned up.

For global engineering conventions, code patterns, architectural standards, and organizational guidance, consult `.codex/rules/company.md`.

## Instructions for AI Assistants

- Always follow the conventions defined in `.codex/rules/company.md`.
- Do not alter external contracts without validation.
- Respect the existing architecture and dependency direction.
- Prioritize consistency with the current code.
- Read `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/Launcher Contract.md`, `docs/LAUNCHER_SCOPE.md`, and `docs/ADAPTER_HOST_PROTOCOL.md` before architectural changes.
- If code and architecture documentation conflict, stop and describe the conflict instead of guessing.
