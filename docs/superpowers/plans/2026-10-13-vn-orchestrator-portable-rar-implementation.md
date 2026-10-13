# VN Orchestrator Portable RAR MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver a single-game, durable RAR-to-portable-directory workflow started by Playnite's standard Install action, without coupling the installer process to Playnite uptime or changing launcher behavior.

**Architecture:** Extend the existing `VNManager` Playnite plugin only at the Playnite boundary: it writes and consumes v1 JSON messages and updates games after a guarded result reconciliation. Build the standalone WPF Orchestrator as a separate solution with a Core domain layer, SQLite persistence, filesystem/archive infrastructure, and a workflow-oriented UI. The two solutions exchange duplicated, versioned wire DTOs only; they never reference one another's projects or binaries.

**Tech Stack:** Playnite SDK 6.16.0 / .NET Framework 4.6.2 (existing plugin); .NET 10 WPF; C#; SQLite via `Microsoft.Data.Sqlite` 10.0.0; `SharpCompress` 0.50.5; `System.Text.Json`; xUnit v3 3.2.2; standard Windows filesystem, named mutex, and named event APIs.

**Spec:** `F:\Playnite Integration\docs\superpowers\specs\2026-10-13-vn-orchestrator-portable-rar-design.md`

## Global Constraints

- Keep the two solutions independent. The only contract across `F:\Playnite Integration` and `F:\Playnite VN Installer` is the JSON protocol in this plan.
- Retain `PlayniteSDK` at 6.16.0 for the MVP. The installer work must not become an SDK-upgrade project.
- Support only a simple, non-encrypted, non-multipart `.rar` source archive. Treat ZIP, 7z, installers, images, nested-only payloads, and encrypted or multipart RARs as recognized unsupported conditions.
- Never move, rename, delete, or modify the source RAR. Never execute an archive entry during inspection, extraction, or validation.
- Do not add launch strategies, Locale Emulator decisions, batch workflows, installer execution, or nested archive extraction.
- The plugin must remain responsive: it writes a durable request, signals/starts the Orchestrator, begins observation, and returns. It must not wait for inspection, extraction, validation, or synchronization.
- `VN Orchestrator` is the exact managed Play Action name. Synchronization may update exactly one `GameAction` with that name; zero creates it, and more than one is a visible conflict that must not be guessed through.
- Use protocol `schemaVersion: 1`. Reject a different major version; accept unknown JSON properties for major version 1.
- Use `%LocalAppData%\VNOrchestrator` as the canonical data root. Its initial subpaths are `Transport`, `Logs`, `State`, and `Settings`.
- Default archive policy is: at most 30,000 file entries; at most 100 GiB declared expanded bytes; at least `declaredExpandedBytes + 2 GiB` free space before extraction; and all normalized output paths must remain within the workflow staging directory. Keep these values editable only in the Orchestrator settings file for this MVP.
- Default retention is 30 days for structured logs and 90 days for a fully acknowledged request/result/acknowledgement triplet. Pending, failed, conflict, or recovery-required messages are never auto-pruned.
- During `Extracting` or `Committing`, an app-close request opens a confirmation that explains the operation cannot be stopped safely. Choosing **Keep working** cancels close; no in-app cancellation is offered at that point. An OS-forced termination is recovered as `RecoveryRequired`, never silently resumed. Before side effects begin, the user may cancel normally.
- Staging is `<LibraryRoot>\.vn-orchestrator-staging\{WorkflowId}` and contains a manifest with `WorkflowId`. The final destination is `<LibraryRoot>\<sanitized Playnite game name>` unless changed during review. Preserve Unicode; alter only Windows-invalid characters, reserved names, and invalid trailing dots/spaces.
- A synthetic, non-copyright, small RAR fixture is required before the real RAR infrastructure task. Do not add user game archives to source control.

## Protocol v1

All files are UTF-8 JSON, first written as a uniquely named `.part` sibling and then renamed into their final name.

```text
%LocalAppData%\VNOrchestrator\Transport\
  inbox\{RequestId}.json
  outbox\{RequestId}.result.json
  acknowledgements\{RequestId}.ack.json
```

`InstallationRequest` fields: `schemaVersion`, `requestId`, `playniteGameId`, `gameName`, `sourcePath`, `sourceFingerprint`, `createdAtUtc`.

`InstalledGameResult` fields: `schemaVersion`, `requestId`, `workflowId`, `playniteGameId`, `sourcePath`, `sourceFingerprint`, `installationDirectory`, `executablePath`, `workingDirectory`, `completedAtUtc`.

`PlayniteAcknowledgement` fields: `schemaVersion`, `requestId`, `workflowId`, `playniteGameId`, `appliedAtUtc`.

Fingerprint v1 is `SHA-256` over the canonical full source path, length, and last-write UTC ticks separated by NUL characters. It detects replacement between request and extraction; it is not a whole-file content hash.

## File Map

### Existing repository — `F:\Playnite Integration`

```text
VNManager\
  Launcher.sln                                      # add plugin integration projects
  PlaynitePlugin\
    PlaynitePlugin.csproj                           # reference integration core
    VnManagerPlugin.cs                              # install controller and startup hook only
    Installation\PlayniteInstallAdapter.cs         # maps Playnite games to integration snapshots
    Installation\OrchestratorProcessActivator.cs   # starts/signals external app
    Installation\PlayniteResultApplier.cs          # guarded game update + acknowledgement
  PlayniteInstallation\
    PlayniteInstallation.csproj                     # new, netstandard2.0; no Playnite SDK
    Contracts\ProtocolV1.cs                         # request/result/ack wire DTOs
    Domain\InstallEligibility.cs                    # pure eligibility rules
    Domain\ResultApplicationPolicy.cs               # pure conflict/action mutation rules
    Transport\AtomicTransportFileStore.cs           # atomic JSON transport implementation
    Transport\TransportPaths.cs
  PlayniteInstallation.Tests\
    PlayniteInstallation.Tests.csproj               # new, net10.0 + xUnit v3
    InstallEligibilityTests.cs
    AtomicTransportFileStoreTests.cs
    ResultApplicationPolicyTests.cs
    ProtocolV1Tests.cs
```

The `PlayniteInstallation` project exists solely to make the plugin edge testable from .NET 10. It is not referenced by the Orchestrator; matching wire DTOs are intentionally implemented there independently.

### New repository — `F:\Playnite VN Installer`

```text
VNOrchestrator.sln
Directory.Build.props
README.md
VNOrchestrator.Core\
  VNOrchestrator.Core.csproj
  Contracts\ProtocolV1.cs
  Domain\*.cs                                       # state, plans, reports, artifacts, results
  Policies\*.cs                                     # classifier, candidates, paths, transitions
  Services\IArchiveService.cs
  Services\IWorkflowRepository.cs
VNOrchestrator.Infrastructure\
  VNOrchestrator.Infrastructure.csproj
  Persistence\SqliteWorkflowRepository.cs
  Persistence\SqliteSchema.cs
  Transport\AtomicTransportFileStore.cs
  Archive\SharpCompressArchiveService.cs
  Filesystem\SafeExtractionPathPolicy.cs
  Runtime\SingleInstanceCoordinator.cs
  Runtime\OrchestratorProcessActivation.cs
  Logging\StructuredLogWriter.cs
VNOrchestrator.App\
  VNOrchestrator.App.csproj
  App.xaml / App.xaml.cs
  CompositionRoot.cs
  ViewModels\WorkflowViewModel.cs
  Views\WorkflowWindow.xaml
  Views\SettingsWindow.xaml
VNOrchestrator.Tests\
  VNOrchestrator.Tests.csproj
  Core\*.cs
  Infrastructure\*.cs
  Integration\*.cs
  TestAssets\PortableGame.rar                        # synthetic fixture only
  TestAssets\*.json                                 # inspection/unsupported manifests
```

## Task 0: Establish repository and fixture boundaries

**Files:**
- Create: `F:\Playnite VN Installer\README.md`
- Create: `F:\Playnite VN Installer\.gitignore`
- Create: `F:\Playnite VN Installer\VNOrchestrator.sln`
- Modify: `F:\Playnite Integration\VNManager\Launcher.sln`

1. Confirm `F:\Playnite VN Installer` is a dedicated repository boundary before initializing Git there; do not put Orchestrator files into `F:\Playnite Integration`.
2. Create the empty .NET 10 solution and the README that names the repository purpose, hard MVP boundaries, data root, and the protocol document location in the Integration repository.
3. Add a `.gitignore` for `bin`, `obj`, local SQLite databases, local logs, transport files, and user settings. It must not ignore versioned test assets.
4. Obtain a tiny synthetic RAR fixture that contains only `PortableGame.exe` and `data\readme.txt`, has no copyrighted game content, is not encrypted/multipart, and can be committed. If no local RAR-authoring tool is available, pause this task for the fixture rather than substituting a ZIP or real user archive.
5. Add empty project entries to each solution only when their project files exist; do not add a cross-solution project reference.
6. Verify the Integration working tree still contains only the pre-existing unrelated `codex.md` modification plus files created by this plan's execution. Do not stage `codex.md`.
7. Commit the new Installer repository bootstrap separately. In the Integration repository, stage only solution/project files owned by the plugin work.

## Task 1: Define and test the independent protocol and plugin-side pure policies

**Files:**
- Create: `VNManager\PlayniteInstallation\PlayniteInstallation.csproj`
- Create: `VNManager\PlayniteInstallation\Contracts\ProtocolV1.cs`
- Create: `VNManager\PlayniteInstallation\Domain\InstallEligibility.cs`
- Create: `VNManager\PlayniteInstallation\Domain\ResultApplicationPolicy.cs`
- Create: `VNManager\PlayniteInstallation.Tests\PlayniteInstallation.Tests.csproj`
- Create: `VNManager\PlayniteInstallation.Tests\ProtocolV1Tests.cs`
- Create: `VNManager\PlayniteInstallation.Tests\InstallEligibilityTests.cs`
- Create: `VNManager\PlayniteInstallation.Tests\ResultApplicationPolicyTests.cs`
- Modify: `VNManager\Launcher.sln`

1. Create `PlayniteInstallation` as `netstandard2.0` and a `net10.0` xUnit v3 test project. Do not reference Playnite SDK from either.
2. Write failing tests for protocol round-trip, schema-major rejection, unknown-property tolerance, and deterministic v1 source fingerprint creation.
3. Implement immutable DTOs and `ProtocolV1` validation. Define explicit error codes for incompatible schema, malformed payload, missing field, and fingerprint mismatch. Use `System.Text.Json` with unknown-property tolerance.
4. Write failing eligibility tests for: uninstalled Visual Novel + existing `.rar` source accepted; installed game rejected; missing file rejected; non-RAR rejected; non-Visual-Novel rejected; active game workflow rejected. Test extension comparison case-insensitively.
5. Implement a pure `InstallEligibility.Evaluate(GameInstallSnapshot, IFileProbe, IActiveWorkflowLookup)` that returns an explicit allowed/rejected result and reason. The Playnite adapter will supply the snapshot later.
6. Write failing result-policy tests that prove an applicable result produces `IsInstalled=true`, replaces/creates exactly one `VN Orchestrator` direct file action, preserves all unrelated action snapshots and metadata snapshots, and refuses missing game, already-installed game, changed install directory, request mismatch, source/fingerprint mismatch, or duplicate managed action.
7. Implement pure `ResultApplicationPolicy.Evaluate(CurrentGameSnapshot, InstalledGameResult)` returning either a proposed mutation or a typed conflict. Never mutate a live Playnite object from this layer.
8. Run `dotnet test .\VNManager\PlayniteInstallation.Tests\PlayniteInstallation.Tests.csproj --no-restore` after restoring once; confirm the tests fail before each implementation and pass afterwards.
9. Commit only the new testable plugin-integration project, its tests, and the solution update in `F:\Playnite Integration`.

## Task 2: Implement durable plugin transport and non-blocking dispatch

**Files:**
- Create: `VNManager\PlayniteInstallation\Transport\TransportPaths.cs`
- Create: `VNManager\PlayniteInstallation\Transport\AtomicTransportFileStore.cs`
- Create: `VNManager\PlayniteInstallation\Transport\IOrchestratorLauncher.cs`
- Create: `VNManager\PlayniteInstallation\Transport\OrchestratorDispatchService.cs`
- Create: `VNManager\PlayniteInstallation.Tests\AtomicTransportFileStoreTests.cs`
- Create: `VNManager\PlayniteInstallation.Tests\OrchestratorDispatchServiceTests.cs`

1. Write failing tests that a request is never observable at its final inbox name until serialization succeeds, unique `.part` names cannot collide, final request paths are deterministic by request ID, and a malformed/partial result is not accepted.
2. Implement `AtomicTransportFileStore.WriteRequest`, `ReadResults`, and `WriteAcknowledgement`; create directories on demand and rename within the same directory. Always leave a failed `.part` file for bounded diagnostics only; it is not a message.
3. Write failing dispatcher tests with a fake `IOrchestratorLauncher`: request writing precedes launcher activation, launcher failure is reported without deleting the request, and the asynchronous observation registration returns without waiting for a result.
4. Implement `OrchestratorDispatchService.DispatchAsync` as the composition point for request persistence, best-effort launch/signal, and result observation registration. It must return after dispatch and never consume a result in the caller's UI thread.
5. Add a 90-day acknowledged-triplet cleanup candidate enumerator only; do not schedule cleanup from the plugin. It must exclude any message that lacks a valid matching acknowledgement.
6. Run the focused test project, then `dotnet build .\VNManager\Launcher.sln --nologo`.
7. Commit only Task 2 files in `F:\Playnite Integration`.

## Task 3: Bind the integration to the existing Playnite plugin

**Files:**
- Modify: `VNManager\PlaynitePlugin\PlaynitePlugin.csproj`
- Modify: `VNManager\PlaynitePlugin\VnManagerPlugin.cs`
- Create: `VNManager\PlaynitePlugin\Installation\PlayniteInstallAdapter.cs`
- Create: `VNManager\PlaynitePlugin\Installation\OrchestratorProcessActivator.cs`
- Create: `VNManager\PlaynitePlugin\Installation\PlayniteResultApplier.cs`
- Create: `VNManager\PlaynitePlugin\Installation\PluginInstallWorkflowCoordinator.cs`

1. Add only a project reference from the net462 plugin to `PlayniteInstallation`; do not change launcher project references or update `PlayniteSDK`.
2. Implement `GetInstallActions` (not a second plugin) so it delegates eligibility to the pure layer, creates a `RequestId`, snapshots game ID/name/source path/fingerprint, dispatches, and returns immediately.
3. Add `OrchestratorProcessActivator` that starts the configured executable or signals it if already running. It must show a concise Playnite notification if activation fails while retaining the already-written request.
4. On plugin startup and after dispatch, enumerate result files and map a result to a current Playnite game fetched by ID. Run `ResultApplicationPolicy` first. Only if it proposes a mutation, update `InstallDirectory`, `IsInstalled`, and the one managed direct Play Action, preserving every other action/feature/metadata value, then call `PlayniteApi.Database.Games.Update`.
5. Write the acknowledgement only after `Games.Update` returns successfully. If the policy yields a conflict, leave the result unacknowledged, make no database change, and display the conflict.
6. Ensure all Game API interaction happens in this adapter/coordinator, never in `PlayniteInstallation`.
7. Manually smoke-test the plugin build/deployment format with a temporary transport root and a fake outbox result. Do not use a real game archive yet.
8. Run the focused integration tests and `dotnet build .\VNManager\Launcher.sln --nologo`. Inspect the staged diff so `codex.md` remains unstaged.
9. Commit only plugin-owned files in `F:\Playnite Integration`.

## Task 4: Create the Orchestrator domain, persistence, and single-instance shell

**Files:**
- Create: `VNOrchestrator.Core\VNOrchestrator.Core.csproj`
- Create: `VNOrchestrator.Core\Contracts\ProtocolV1.cs`
- Create: `VNOrchestrator.Core\Domain\InstallationWorkflow.cs`
- Create: `VNOrchestrator.Core\Domain\InstallationPlan.cs`
- Create: `VNOrchestrator.Core\Domain\OperationRecord.cs`
- Create: `VNOrchestrator.Core\Policies\WorkflowStateMachine.cs`
- Create: `VNOrchestrator.Infrastructure\VNOrchestrator.Infrastructure.csproj`
- Create: `VNOrchestrator.Infrastructure\Persistence\SqliteSchema.cs`
- Create: `VNOrchestrator.Infrastructure\Persistence\SqliteWorkflowRepository.cs`
- Create: `VNOrchestrator.Infrastructure\Runtime\SingleInstanceCoordinator.cs`
- Create: `VNOrchestrator.App\VNOrchestrator.App.csproj`
- Create: `VNOrchestrator.App\App.xaml`
- Create: `VNOrchestrator.App\CompositionRoot.cs`
- Create: `VNOrchestrator.Tests\VNOrchestrator.Tests.csproj`
- Create: `VNOrchestrator.Tests\Core\WorkflowStateMachineTests.cs`
- Create: `VNOrchestrator.Tests\Infrastructure\SqliteWorkflowRepositoryTests.cs`

1. Add the four projects to `VNOrchestrator.sln`: WPF App (`net10.0-windows`), Core (`net10.0`), Infrastructure (`net10.0`), and test project (`net10.0`). Keep Core free from WPF, SQLite, SharpCompress, and direct filesystem calls.
2. Write failing state-machine tests for all normal transitions and for invalid transitions; cover `Extracting -> RecoveryRequired` on restart, `Validating` retry, `Committing` reconciliation, and `AwaitingPlayniteSync` without reinstallation.
3. Implement explicit workflow state, error code, source package, inspection report, plan revision, artifact, operation, synchronization, and result domain records. Define `IWorkflowRepository` in Core.
4. Create a SQLite schema with `Workflows`, `Plans`, `Operations`, `Artifacts`, `PlayniteSync`, and `ImportedRequests`. Import by `RequestId` in one transaction; add unique constraints for request ID and the active workflow per game ID.
5. Write failing persistence tests for idempotent import, transaction rollback, restart rehydration, and duplicate game conflict. Implement them using an isolated temporary database per test.
6. Implement a named per-user mutex and a named event. A second instance must signal the event and exit; the primary must rescan durable inbox files. The event must not carry request data.
7. Add the first-run configuration model: require `LibraryRoot`; derive staging inside it; persist settings at `%LocalAppData%\VNOrchestrator\Settings\settings.json`; do not duplicate this configuration in the plugin.
8. Run `dotnet test .\VNOrchestrator.Tests\VNOrchestrator.Tests.csproj` and `dotnet build .\VNOrchestrator.sln --nologo`.
9. Commit Task 4 to the Installer repository.

## Task 5: Add protocol import/export, WPF workflow shell, and startup reconciliation

**Files:**
- Create: `VNOrchestrator.Infrastructure\Transport\AtomicTransportFileStore.cs`
- Create: `VNOrchestrator.Infrastructure\Transport\RequestInboxImporter.cs`
- Create: `VNOrchestrator.Infrastructure\Transport\ResultOutboxPublisher.cs`
- Create: `VNOrchestrator.App\Views\SettingsWindow.xaml`
- Create: `VNOrchestrator.App\Views\WorkflowWindow.xaml`
- Create: `VNOrchestrator.App\ViewModels\WorkflowViewModel.cs`
- Create: `VNOrchestrator.Tests\Infrastructure\TransportProtocolTests.cs`
- Create: `VNOrchestrator.Tests\Integration\StartupReconciliationTests.cs`

1. Copy the v1 wire shape exactly from Task 1 rather than referencing its assembly. Add contract compatibility tests that deserialize plugin-produced golden JSON request/result/ack files checked into each repository.
2. Write failing transport tests for atomic inbox import, incompatible-major rejection, unknown-field tolerance, idempotent request import, immutable result publication, and acknowledgement-driven finalization.
3. Implement the Orchestrator transport store and importer. SQLite is authoritative: inbox files are transport messages; importing a duplicate request never starts a second workflow.
4. Implement result publication only after a committed portable installation. Once written, an outbox result is never rewritten; a late acknowledgement records synchronization completion.
5. Implement startup reconciliation exactly by persisted state: restart safe inspection/validation; restore reviews and executable selection; put interrupted extraction into `RecoveryRequired`; inspect a committing destination/manifest before deciding it was promoted; never extract again for `AwaitingPlayniteSync`.
6. Implement the WPF shell: first-run LibraryRoot selection, a single current workflow window, and distinct states for review, recovery, progress, selection, completion, and synchronization pending. No dashboard or batch queue UI.
7. Run the Orchestrator suite and build. Start the app twice manually to confirm the second process signals the first instead of opening a second primary window.
8. Commit Task 5 to the Installer repository.

## Task 6: Inspect RARs safely and classify only the external layer

**Files:**
- Create: `VNOrchestrator.Core\Services\IArchiveService.cs`
- Create: `VNOrchestrator.Core\Policies\PackageClassifier.cs`
- Create: `VNOrchestrator.Core\Policies\ExecutableCandidateRanker.cs`
- Create: `VNOrchestrator.Infrastructure\Archive\SharpCompressArchiveService.cs`
- Create: `VNOrchestrator.Tests\Core\PackageClassifierTests.cs`
- Create: `VNOrchestrator.Tests\Core\ExecutableCandidateRankerTests.cs`
- Create: `VNOrchestrator.Tests\Infrastructure\SharpCompressArchiveServiceTests.cs`
- Create: `VNOrchestrator.Tests\TestAssets\PortableGame.rar`
- Create: `VNOrchestrator.Tests\TestAssets\*.json`

1. Add `SharpCompress` 0.50.5 only to Infrastructure. Define `IArchiveService.InspectAsync(SourcePackage, ArchiveInspectionPolicy)` and `ExtractAsync(ApprovedArchivePlan, ExtractionPolicy)` in Core; both return structured findings rather than UI types.
2. Before implementation, write failing classifier tests for this strict precedence: exposed `setup.exe`/`install.exe` => `InstallerDetected`; otherwise exposed plausible `.exe` => `PortableCandidate`; otherwise nested archive => `NestedPayloadRequiresInspection`; otherwise `UnknownPackage`.
3. Add tests proving nested ZIP/RAR/7z alongside an exposed portable executable still yields `PortableCandidate`; nested archives are recorded but not opened.
4. Add explicit tests/manifests for encrypted, multipart, corrupt, and unsupported extension inputs. They must receive precise unsupported/error codes, not a generic exception.
5. Implement the archive service to list only the external RAR layer. Detect multipart naming patterns and archive metadata; reject encrypted source. Never invoke an executable and never recursively open an entry.
6. Add the versioned synthetic fixture and prove real listing detects `PortableGame.exe`, followed by extraction only after Task 7 supplies safe path policy. Keep real user game archives outside source control.
7. Run the focused test groups and the full Orchestrator suite. Commit Task 6 with the synthetic fixture and manifests.

## Task 7: Implement safe portable extraction, validation, and recovery boundaries

**Files:**
- Create: `VNOrchestrator.Core\Policies\SafeDestinationPolicy.cs`
- Create: `VNOrchestrator.Core\Policies\ExtractionPolicy.cs`
- Create: `VNOrchestrator.Core\Services\PortableInstallationStrategy.cs`
- Create: `VNOrchestrator.Infrastructure\Filesystem\SafeExtractionPathPolicy.cs`
- Create: `VNOrchestrator.Infrastructure\Filesystem\WorkflowManifestStore.cs`
- Create: `VNOrchestrator.Infrastructure\Filesystem\ArtifactCleanupService.cs`
- Create: `VNOrchestrator.Tests\Infrastructure\SafeExtractionPathPolicyTests.cs`
- Create: `VNOrchestrator.Tests\Integration\PortableExtractionTests.cs`
- Create: `VNOrchestrator.Tests\Integration\RecoveryAndCleanupTests.cs`

1. Write failing security tests for parent traversal, absolute/UNC paths, alternate data streams, normalized paths escaping staging, link/reparse-point materialization, excessive entry count, expanded-size cap, insufficient free space, and existing final destination.
2. Implement a path policy that rejects each unsafe entry before writing it and verifies the final normalized extraction target remains under the normalized staging root. Refuse links rather than materializing them.
3. Recheck the request's v1 fingerprint immediately before extraction. If it changed, stop before staging writes and return a source-changed error.
4. Create staging only at `<LibraryRoot>\.vn-orchestrator-staging\{WorkflowId}`, on the destination volume, and write a manifest before extraction. Journal staging creation/extraction operations and owned artifacts in SQLite.
5. Use `PortableInstallationStrategy` as the only registered in-process strategy. It extracts the external layer exactly once, copies nested archive entries as ordinary files, recomputes actual executable candidates, and never promotes into an existing destination.
6. Write recovery tests proving interrupted extraction becomes `RecoveryRequired`; cleanup refuses paths without a matching workflow manifest; cleanup never targets a source RAR or a promoted destination; retry begins from a fresh owned staging directory.
7. Wire the close policy: a close request during extraction/commit presents the specified keep-working confirmation and does not terminate side effects; normal cancellation remains available before extraction.
8. Run security, recovery, and full Orchestrator tests. Commit Task 7.

## Task 8: Add destination review, executable selection, promotion, and result publication

**Files:**
- Create: `VNOrchestrator.Core\Policies\DestinationNameSanitizer.cs`
- Create: `VNOrchestrator.Core\Policies\InstallationPlanPolicy.cs`
- Create: `VNOrchestrator.Core\Services\WorkflowCommitService.cs`
- Create: `VNOrchestrator.App\Views\WorkflowWindow.xaml.cs`
- Modify: `VNOrchestrator.App\ViewModels\WorkflowViewModel.cs`
- Create: `VNOrchestrator.Tests\Core\InstallationPlanPolicyTests.cs`
- Create: `VNOrchestrator.Tests\Integration\WorkflowCommitServiceTests.cs`

1. Write failing plan tests for Unicode-preserving destination sanitization, invalid Windows names, reserved device names, trailing-dot/space removal, destination change creating a plan revision, and review rejection of unsupported classifications.
2. Implement plan review as a persisted prerequisite to extraction: source, classification, candidates, nested warnings, default/edited destination, and archive limits must be visible. A user edit increments plan revision and invalidates any previous approval.
3. After extraction, display recomputed direct executable candidates and require the user to select one. The candidate must exist under staging, have `.exe` extension, and not be an archive entry path guess. Do not execute it.
4. Write failing commit tests for atomic same-volume directory promotion, existing-destination refusal, manifest verification after a simulated crash, and no automatic deletion after promotion/sync failure.
5. Implement `WorkflowCommitService`: journal intent, prove staging/manifest/selected executable, rename staging to final destination only if it does not exist, validate the moved executable path, record promotion, construct `InstalledGameResult`, and publish it immutably to outbox before entering `AwaitingPlayniteSync`.
6. Show completed-but-awaiting-sync state in WPF. Acknowledgement changes only the sync state to completed; it must never reinstall or alter the destination.
7. Run the full Orchestrator build and test suite. Commit Task 8.

## Task 9: Validate the two-process integration and real Playnite update

**Files:**
- Create: `F:\Playnite VN Installer\docs\manual-validation.md`
- Modify: `F:\Playnite VN Installer\README.md`
- Modify: `F:\Playnite Integration\VNManager\README.md` (or add `docs\playnite-install-validation.md` if no suitable README section exists)

1. Create golden protocol examples in both repositories and verify a request produced by the plugin is imported by the Orchestrator and a result published by the Orchestrator is accepted by the plugin without binary sharing.
2. Run each repository's focused suites and builds separately. Record commands and output categories separately: Core/infrastructure automated tests, plugin integration tests, Playnite plugin build, WPF build, manual Playnite UI validation, and manual game launch.
3. Deploy the built plugin and Orchestrator to their local development locations. Configure a temporary LibraryRoot; do not use a production library root for initial validation.
4. From the real Playnite Install button on a known synthetic/simple portable RAR entry, prove one request is written and Playnite returns promptly. Review the proposed destination, extract, select the executable, and confirm the original RAR remains unchanged.
5. Repeat with Playnite closed before the workflow completes. Reopen Playnite and confirm startup reconciliation applies the original entry update: correct `InstallDirectory`, `IsInstalled=true`, and exactly one `VN Orchestrator` direct action.
6. Manually launch the resulting Play Action. Treat this as a distinct acceptance result from successful build/test/synchronization.
7. Exercise one intentional conflict (change InstallDirectory or mark game installed before result application) and prove the plugin preserves the current Playnite data, leaves the result unacknowledged, and tells the user why.
8. Commit only validation documents and golden fixtures. Do not commit user library paths, game names, user archives, SQLite state, logs, settings, or transport messages.

## Verification Matrix

| Area | Required proof |
| --- | --- |
| Protocol | v1 round trips; major rejection; unknown fields tolerated; independent golden JSON compatibility |
| Plugin | eligibility, atomic request, non-blocking dispatch, guarded idempotent result apply, acknowledgement after update |
| Core | state transitions, classifier precedence, plan revisions, candidate ranking, duplicate workflow conflict |
| Archive security | traversal/absolute/UNC/ADS/link rejection, limits, free-space check, no destination merge |
| Persistence/recovery | SQLite transaction boundaries, idempotent import, restart cases, manifest-bounded cleanup |
| Runtime | one primary process; secondary signal/rescan; close protection during side effects |
| Manual | Playnite Install click, Playnite closure/reopen synchronization, installed direct action launches, source RAR untouched |

## Final Review Checklist

- [ ] No source file or project reference crosses the two solution boundaries.
- [ ] No Archive/Playnite/SQLite/WPF implementation leaks into Core domain policies.
- [ ] Unsupported archive forms are explicit and do not execute or extract unexpectedly.
- [ ] All user-controlled archive paths are validated before writing a file.
- [ ] No code deletes, renames, moves, or overwrites the original RAR or a final destination.
- [ ] Plugin actions and startup sync do not block the Playnite UI.
- [ ] A conflict cannot overwrite a changed/installed Playnite entry.
- [ ] Acknowledgement is impossible before a successful database update.
- [ ] Existing launcher and Locale Emulator behavior has a targeted regression check and no functional scope change.
- [ ] Build, automated tests, Playnite runtime proof, and manual launch proof are reported separately.
