# Playnite VN Compatibility Layer

## Goal

Build a launcher compatibility layer for Visual Novels.

Games should normally be configured through metadata/features. The current
architecture uses a thin PowerShell adapter at the Playnite boundary and a
standalone .NET launcher system for application logic.

## Architectural principles

- Playnite-specific code must remain inside the PowerShell Adapter or the
  isolated PlaynitePlugin project.
- PowerShell integrates with Playnite and the Windows environment; it does not
  own launcher orchestration or service logic.
- The .NET Host owns request parsing, validation, launcher resolution,
  dispatching and application-level error handling.
- Launcher implementations are .NET Class Library projects that implement the
  shared `ILauncher` contract.
- Adding a launcher currently requires registering its implementation in the
  Host. Dynamic discovery is outside the initial scope.
- The Host and Launchers must not depend on Playnite APIs or objects.
- AutoHotkey is used only when OS/application automation is necessary.
- Existing PowerShell launchers are legacy implementations and may be migrated
  incrementally to .NET.

## Documentation

Read before making architectural changes:

- `docs/ARCHITECTURE.md`
- `docs/DECISIONS.md`
- `docs/Launcher Contract.md`
- `docs/LAUNCHER_RESOLUTION.md`
- `docs/LAUNCHER_SCOPE.md`
- `docs/logging.md`
- `docs/Principios de Design.md`
- `docs/ADAPTER_HOST_PROTOCOL.md`

`docs/FEATURES.md` and `docs/LAUNCHERS.md` are planned but do not exist yet.

## Development rules

- Keep the PowerShell Adapter thin.
- Prefer small .NET components over a large Host or Dispatcher.
- Do not introduce Playnite dependencies into the Host, Supervisor or Launcher
  projects; only PlaynitePlugin may reference the Playnite SDK.
- Do not add Feature interpretation to the Host; Feature processing is a
  separate future concern.
- Preserve backwards compatibility unless explicitly told otherwise.
- Treat PowerShell launcher scripts and filesystem script discovery as legacy.
- Keep environment-specific and operational values in external configuration by
  default. Hard-coded values require a concrete reason and should be the
  exception rather than the standard configuration mechanism.
- Explain architectural changes before implementing large refactors.
- If implementation contradicts current documentation, stop and explain the
  conflict instead of guessing.

## Testing

The current canonical test command is:

```text
dotnet run --project VNManager/Tests/Tests.csproj
```

The test suite contains unit tests and Windows process integration tests. The
integration fixtures use only the dedicated `VnTest*` console executables under
`VNManager/TestAssets` and must clean up every persistent process they start.

## Current focus

Establishing the .NET Host, shared contracts and the first .NET Launcher while
preserving the PowerShell integration boundary during migration.
