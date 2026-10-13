# VN Orchestrator: Portable RAR Installation Design

## Status

Approved conversational design for the first implementation plan. This document defines architecture and MVP behavior only. It does not authorize project creation or implementation.

## Context

Visual novel entries already exist in Playnite as uninstalled custom games. Their current `InstallDirectory` points to a compressed source package, for example:

```text
K:\VNs Download\Nukige\Example.rar
```

Playnite already displays its standard Install action, but no installation controller handles these entries. The goal is to use that existing interaction to start a local installation workflow while keeping archive inspection, extraction, persistence, recovery, and user decisions outside Playnite.

The current VNManager launcher integration remains responsible for game execution concerns. Installation is a separate subsystem. Locale Emulator and other launch strategies are outside this MVP.

## Goals

- Start one installation from Playnite's existing Install action.
- Continue the workflow if Playnite closes.
- Inspect before making permanent filesystem changes.
- Install a portable visual novel contained directly in a RAR archive.
- Require review before extraction and confirmation of the Play Action executable.
- Preserve the original RAR without moving, renaming, or deleting it.
- Update the existing Playnite entry after successful validation.
- Reconcile the update automatically when Playnite next opens.
- Persist enough state to resume or recover without guessing which effects occurred.
- Reject unsafe archive paths and avoid silent overwrite or destructive rollback.

## Non-goals

The MVP does not include:

- ZIP or 7z execution support;
- installer execution;
- disk image mounting;
- nested archive extraction;
- multipart RAR extraction;
- encrypted archives;
- Locale Emulator configuration;
- any other launch strategy;
- batch installation;
- archive deletion;
- destination merging or overwrite;
- uninstall orchestration;
- VNDB or metadata management;
- installer UI automation;
- dynamic plugin discovery.

Unsupported structures must be recognized and reported explicitly rather than treated as generic failures.

## Architectural decision

Use two separate solutions and processes:

```text
F:\Playnite Integration
└── VNManager\Launcher.sln
    ├── PlaynitePlugin
    ├── LauncherSupervisor
    ├── Host
    ├── LocaleEmulator
    └── existing launcher projects

F:\Playnite VN Installer
└── VNOrchestrator.sln
    ├── VNOrchestrator.App
    ├── VNOrchestrator.Core
    ├── VNOrchestrator.Infrastructure
    └── VNOrchestrator.Tests
```

The existing VNManager Playnite plugin is extended with installation integration. A second Playnite plugin is not introduced.

The Orchestrator does not reference launcher implementation projects. The two solutions share a versioned JSON protocol, not a common binary project. This avoids cross-repository project references and permits independent deployment. A shared contracts package may be considered only if protocol growth demonstrates a need.

## Component responsibilities

### VNManager Playnite plugin

The plugin is the only installation component that uses Playnite APIs. It:

- decides whether to offer an `InstallController`;
- captures the existing game ID, name, and archive path;
- creates durable installation requests;
- starts or activates the Orchestrator;
- observes results without blocking the Playnite UI;
- updates the existing Playnite entry;
- reconciles pending results at Playnite startup;
- acknowledges successful synchronization;
- reports conflicts and synchronization failures.

It does not inspect archives, select an installation strategy, extract files, rank executable candidates, or own workflow persistence.

### VN Orchestrator application

The Orchestrator is a standalone, single-instance Windows desktop application. It:

- imports requests idempotently;
- owns workflow state and persistence;
- inspects source packages;
- creates and presents installation plans;
- coordinates strategy execution;
- records external effects;
- presents user decisions;
- validates the installed result;
- publishes a durable Playnite result;
- performs bounded cleanup of artifacts it owns.

It never edits Playnite database files directly.

### Core

The Core project contains domain types and policies independent of WPF, SQLite, SharpCompress, Playnite, and concrete filesystem APIs. It owns:

- workflow transitions;
- package classification;
- plan rules;
- executable candidate ranking rules;
- conflict rules;
- installation strategy contracts;
- result construction.

### Infrastructure

Infrastructure implements:

- SQLite persistence;
- atomic JSON inbox, outbox, and acknowledgement transport;
- filesystem and free-space checks;
- RAR inspection and extraction through SharpCompress;
- process activation and single-instance signaling;
- structured logging.

### WPF application

The WPF application presents the workflow and delegates decisions to Core services. It contains no archive classification or extraction policy in view models or code-behind.

WPF on .NET 10 is selected for the MVP because the application is Windows-only and values simple deployment, mature binding, and maintainability over adoption of a newer UI framework.

## End-to-end flow

### 1. Playnite eligibility

The plugin offers its install action when all initial conditions hold:

- the game is not installed;
- `InstallDirectory` points to an existing file;
- the source extension is `.rar`;
- the game is recognizable as a visual novel through the existing `Visual Novel` genre;
- no active workflow already exists for the same Playnite game ID.

The genre condition prevents the plugin from claiming every uninstalled custom game. Eligibility rules may evolve later through an explicit marker, but that is outside the MVP.

### 2. Request creation

When the user selects Install, the controller:

1. creates a unique `RequestId`;
2. captures `PlayniteGameId`, game name, and source path;
3. writes a request to a unique `.part` file;
4. atomically renames it into the inbox;
5. starts or activates the Orchestrator;
6. begins non-blocking result observation;
7. returns control to Playnite.

The Playnite entry is not modified at this point.

### 3. Request import

The Orchestrator imports the request into SQLite in a transaction. Import is idempotent by `RequestId`. A repeated request does not create a second workflow.

If a workflow already exists for the game ID, the Orchestrator opens that workflow rather than creating a competing installation.

### 4. Inspection

Inspection treats the RAR as read-only. It records source size, timestamps, and a fingerprint sufficient to detect replacement. It lists the external archive layer without permanent extraction or execution.

The classifier applies this precedence:

1. An exposed installer such as `setup.exe` or `install.exe` produces `InstallerDetected`.
2. A plausible non-installer executable produces `PortableCandidate`.
3. No usable exposed executable plus nested archives produces `NestedPayloadRequiresInspection`.
4. No recognized candidate produces `UnknownPackage`.

Nested ZIP, RAR, or 7z files do not automatically block a package. If the external layer contains a usable portable executable, nested archives may be Drama CDs, artwork, or other optional material. They remain ordinary files and are never opened recursively by the MVP.

An executable name is evidence, not proof. Final executable selection occurs after extraction.

### 5. Plan review

For a portable candidate, the Orchestrator presents:

- source archive;
- Playnite title;
- detected package structure;
- classification and confidence;
- proposed destination;
- exposed executable candidates;
- nested archive warnings;
- conflicts and unsupported conditions.

The user must confirm or change the destination before extraction.

### 6. Portable extraction

The strategy reconfirms the source fingerprint, creates a unique staging directory inside the configured library root, writes an ownership manifest, and extracts only the external RAR layer.

Nested archives are copied as archive files but are not themselves extracted.

After extraction, the Orchestrator validates the actual directory structure and recomputes executable candidates. The user confirms the Play Action executable. The MVP does not execute it during validation.

### 7. Commit

The destination must not already exist. Staging resides on the same volume as the destination, so a successful installation can be promoted by directory rename.

Promotion is journaled as an external operation. A failure after promotion does not cause automatic deletion of the installed files.

### 8. Result publication

After validation and commit, the Orchestrator writes an immutable result into the outbox. If Playnite is open, the active controller may apply it immediately. If Playnite is closed, the result remains pending.

### 9. Playnite synchronization

Before applying a result, the plugin retrieves the current game by ID and verifies:

- the entry still exists;
- it was not installed by another mechanism;
- its installation directory still refers to the expected source;
- the result matches the request and source fingerprint.

On success it updates:

```text
InstallDirectory = installed game directory
IsInstalled      = true
GameActions      = managed direct Play Action
```

The managed Play Action points to the selected executable and has a stable identity or name so repeated synchronization updates it rather than duplicating it. Unrelated actions, features, and metadata are preserved.

After the Playnite database update succeeds, the plugin writes an acknowledgement. If a concurrency check fails, the entry is not overwritten and synchronization remains pending with a visible notification.

## Domain model

### InstallationRequest

Contains:

- protocol schema version;
- request ID;
- Playnite game ID;
- game name snapshot;
- source path;
- creation timestamp.

### SourcePackage

Contains the canonical source path, detected format, size, timestamps, and fingerprint. The source retention policy is fixed to `KeepAlways` in the MVP.

### PackageLayer

Represents an observed archive, directory, image, or installable-content layer and its relationship to a parent layer. The MVP records nested layers but does not open them.

### ArchiveSet

Represents entries that appear to form a multipart archive. Multipart sets are detected and reported but not extracted in the MVP.

### InspectionReport

Contains package structure, findings, exposed executable candidates, classification, confidence, warnings, and any inspection limits reached.

### InstallationPlan

Contains plan revision, selected strategy, destination, required interaction, warnings, and candidate executable information. The approved plan is persisted before execution.

### InstallationWorkflow

Owns the current state, references to the source, report and plan, timestamps, and current error or recovery reason.

### InstalledGameResult

Contains request and workflow identity, Playnite game ID, final installation directory, selected executable, working directory, source fingerprint, and completion timestamp.

Launch strategy is deliberately absent from the MVP result.

### PlayniteSync

Tracks pending, applied, or failed synchronization, attempt count, acknowledgement, and last error.

### OperationRecord and Artifact

Record effects such as staging creation, extraction, validation, promotion, and cleanup. Artifacts identify paths owned by a workflow and whether they are temporary or committed.

## Workflow states

Normal states are:

```text
Received
→ Inspecting
→ NeedsReview | Ready
→ Extracting
→ Validating
→ NeedsExecutableSelection
→ Committing
→ AwaitingPlayniteSync
→ Completed
```

Exceptional states are:

```text
Failed
Cancelled
RecoveryRequired
```

A failure records the actual stage and completed effects. It does not reset the workflow to a generic pending state.

## Installation strategies

Strategies are explicit in-process classes registered by composition. There is no plugin directory, reflection discovery, or dynamic assembly loading.

The MVP registers only:

```text
PortableInstallationStrategy
```

Common orchestration owns inspection, planning, state transitions, staging policy, journaling, user interaction, result publication, and cleanup policy. The portable strategy owns only the transformation from approved archive plan to validated portable installation.

Future installer and disk-image strategies must reuse the common workflow without coupling installation type to game launch behavior.

## Archive service

RAR support is implemented behind:

```text
IArchiveService
├── Inspect(source)
└── Extract(source, staging, policy)
```

`SharpCompressArchiveService` is the initial implementation. This isolates the dependency and permits later supplementation or replacement by another archive engine.

Initial policy:

- simple, unencrypted RAR: supported;
- corrupt RAR: explicit inspection or extraction failure;
- encrypted RAR: recognized but unsupported;
- multipart source RAR: recognized but unsupported;
- nested archive: recorded and left closed;
- unsupported link or escaping entry: rejected;
- existing destination: explicit conflict.

## Communication protocol

Transport root:

```text
%LocalAppData%\VNOrchestrator\Transport\
├── inbox\
│   └── {RequestId}.json
├── outbox\
│   └── {RequestId}.result.json
└── acknowledgements\
    └── {RequestId}.ack.json
```

Files are written to unique partial paths and atomically renamed. Payloads include a major schema version. Consumers reject incompatible major versions and tolerate unknown fields within a compatible version.

Transport files are messages, not the workflow database. SQLite remains authoritative for Orchestrator state.

## Single-instance behavior

A named mutex permits one Orchestrator instance per user. The plugin may always write the request and start the executable. If an instance already owns the mutex, the secondary process signals a named event and exits. The primary instance responds by rescanning the inbox.

The event carries no payload. Request and result data remain in durable files.

## Configuration

The Orchestrator owns canonical installation configuration:

- library root;
- staging root or staging policy;
- transport root;
- enabled formats;
- inspection and extraction limits;
- log and transport retention.

The plugin owns only its Orchestrator executable location, transport compatibility information, and eligibility settings.

The first Orchestrator run requires the user to choose `LibraryRoot`. A destination is proposed as:

```text
<LibraryRoot>\<sanitized Playnite game name>
```

Unicode, including Japanese text, is preserved. Only invalid Windows filename characters, reserved names, and invalid trailing characters are changed. Every plan permits destination review.

Future plugin settings may display the configured root and open Orchestrator settings, but must not maintain an independent copy of canonical installation settings.

## User interface

The MVP provides one workflow-oriented WPF window:

```text
Request summary
→ inspection result
→ destination review
→ extraction progress
→ Play Action selection
→ completion and Playnite synchronization status
```

There is no batch dashboard. Unsupported package types show a precise explanation and preserve the workflow for later versions.

Closing the application during a side-effecting operation must require confirmation. The final close behavior, including whether to request cancellation at a safe boundary or leave recovery-required state, is fixed in the implementation plan.

## Persistence and recovery

SQLite stores workflows, plans, operations, artifacts, and Playnite synchronization state. It does not use full event sourcing.

Startup reconciliation follows these rules:

- `Inspecting`: inspection may restart safely.
- `NeedsReview`: restore the same review state.
- `Extracting`: do not blindly resume partial extraction; enter `RecoveryRequired`.
- `Validating`: validation may run again.
- `NeedsExecutableSelection`: restore persisted candidates.
- `Committing`: inspect the destination and ownership manifest.
- `AwaitingPlayniteSync`: do not repeat installation; keep publishing the existing result.
- acknowledgement present: finalize the synchronization record.

If promotion completed before a crash, the Orchestrator validates the destination and reconstructs the result rather than extracting again.

Partial staging recovery choices are explicit: inspect files, discard owned staging and retry, or cancel while preserving files. Automatic cleanup may touch only registered staging artifacts that contain the expected workflow manifest.

The original RAR and a promoted destination are never automatic-cleanup targets.

## Security rules

- Treat the source archive as immutable.
- Never execute an archive entry during inspection or portable installation.
- Reconfirm the source fingerprint before extraction.
- Reject absolute, UNC, traversal, alternate-data-stream, and escaping entry paths.
- Refuse archive links and reparse-point materialization.
- Normalize each extraction target and prove that it remains beneath staging.
- Preflight declared expanded size and available disk space.
- Enforce bounded entry count and expanded size.
- Never merge with or overwrite an existing destination.
- Never request elevation or bypass UAC.
- Never infer permission to delete the source.
- Never report success before destination and selected executable validation.

## Logging

Structured logs correlate events using `RequestId` and `WorkflowId`. Important events include:

- request imported;
- source validated;
- archive inspected;
- classification selected;
- plan approved;
- staging created;
- extraction started and completed;
- validation completed;
- executable selected;
- destination promoted;
- result published;
- Playnite synchronization acknowledged;
- cleanup completed;
- warning, failure, or recovery decision.

Logs distinguish domain error codes from technical exceptions. Retention is configurable. Passwords are not accepted in the MVP and therefore cannot be logged.

## Testing strategy

### Core tests

- package classification;
- executable ranking;
- plan revisions;
- state transitions;
- duplicate request handling;
- Playnite synchronization conflicts.

### Security tests

- parent-directory traversal;
- absolute and UNC paths;
- alternate data streams;
- links and reparse entries;
- excessive entry count and expanded size;
- cleanup refusal without a matching manifest.

### RAR infrastructure tests

A small versioned RAR fixture proves real listing and extraction. Additional manifests or fixtures cover exposed portable executables, exposed installers, nested optional archives, nested-only payloads, corruption, encryption, and multipart detection.

### Persistence and protocol tests

- restart from every recoverable state;
- atomic request and result import;
- schema compatibility;
- duplicate result application;
- acknowledgement after successful update only.

### Plugin tests

- install action eligibility;
- request creation;
- non-blocking dispatch;
- current-entry concurrency checks;
- managed Play Action idempotency;
- preservation of unrelated metadata and actions;
- startup outbox reconciliation.

### Manual acceptance

- trigger from the real Playnite Install button;
- inspect and install a known portable RAR;
- close Playnite before completion;
- reopen Playnite and observe automatic synchronization;
- launch the game through the resulting Play Action;
- verify the original RAR remains unchanged.

Build, automated tests, Playnite runtime validation, and manual launch validation are separate evidence categories.

## MVP acceptance criteria

The MVP is accepted when:

1. Playnite Install creates exactly one request and returns promptly.
2. The Orchestrator inspects a simple portable RAR before extraction.
3. The user reviews the proposed destination.
4. Extraction enforces path and overwrite protections.
5. Nested archives remain closed.
6. The original RAR remains unchanged.
7. The user confirms a real executable as the Play Action.
8. The destination is promoted only after validation.
9. Closing Playnite does not interrupt the workflow.
10. Reopening Playnite updates the original entry.
11. `InstallDirectory`, `IsInstalled`, and the managed Play Action are correct.
12. The resulting Play Action launches the installed game in manual validation.

## Incremental delivery

### Stage 1: Protocol and Playnite entry point

Define protocol v1, add the installation controller, implement eligibility, and prove atomic non-blocking request creation with a test consumer.

### Stage 2: Orchestrator shell and persistence

Create the separate solution, configure the library root, import requests into SQLite, implement the state machine, and restore a persisted workflow after restart.

### Stage 3: RAR inspection

Add the archive service, classify external-layer structures, generate reports and plans, and present review without extraction.

### Stage 4: Safe portable extraction

Create owned staging, validate paths and limits, extract one layer, preserve the source and nested archives, and validate the resulting structure.

### Stage 5: Play Action and commit

Confirm an executable, promote staging, and publish the durable installation result.

### Stage 6: Playnite synchronization

Apply active-session results, update the original game idempotently, preserve unrelated data, and acknowledge success.

### Stage 7: Restart and recovery

Reconcile pending results at Playnite startup, recover interrupted workflow states, and enforce safe transport retention.

### Stage 8: End-to-end validation

Run both solutions' builds and suites, execute the real portable-RAR workflow, validate Playnite restart synchronization, and manually launch the resulting action.

## Implementation-plan decisions

The implementation plan must fix these operational values without changing the approved architecture:

- initial library root chosen by the user;
- entry-count and expanded-size limits;
- visible name and identity rule for the managed Play Action;
- SharpCompress version;
- whether the existing PlayniteSDK 6.16 reference remains or moves to 6.18;
- log and transport retention periods;
- exact close behavior during extraction;
- initial RAR test fixtures.

## Explicitly deferred evolution

After the MVP is proven, candidate increments are ZIP support, bounded nested-container inspection, multipart archives, manual external installers, disk images, launch-strategy configuration, and explicit source cleanup. None is included implicitly in the first implementation plan.
