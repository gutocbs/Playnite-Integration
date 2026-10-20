# VN Manager — Parte 5 (instalação de VNs): plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplificar `PlayniteInstallation`, `PlayniteInstallation.Tests` e `PlaynitePlugin/Installation` sem mudar comportamento, e corrigir a resolução do feed local do protocolo.

**Architecture:** Primeiro a build (feed do protocolo) e a remoção dos testes que pertencem ao Protocol; depois os testes de caracterização; por último os refactors de Domain, Transport e plugin, cada um protegido pela suíte.

**Tech Stack:** netstandard2.0 (`PlayniteInstallation`), .NET Framework 4.6.2 + PlayniteSDK 6.16.0 (plugin), .NET 10 + xUnit v3 3.2.2 (testes), `VNInstallation.Protocol` 1.0.0-alpha.7.

**Spec:** `docs/superpowers/specs/2026-10-19-vn-manager-cleanup-design.md`, seção "Instalação de VNs — Parte 5".

## Global Constraints

- Branch `review-e-correção`; nunca push nem merge para `release`. Confirmar `git branch --show-current` antes de cada commit; cada commit e cada tarefa exigem autorização do usuário.
- Não tocar: o pacote/repositório Playnite VN Protocol, o repositório Playnite VN Installer, os arquivos existentes em `docs/superpowers/`.
- Nenhuma mudança de protocolo, nomes de eventos, arquivos de transporte, mensagens de log, notificações ou códigos de erro: texto idêntico, byte a byte.
- Comandos a partir de `F:\Playnite Integration\Playnite VN Manager`. Suíte da área: `dotnet run --project VNManager/PlayniteInstallation.Tests/PlayniteInstallation.Tests.csproj`.
- Estilo dos arquivos tocados: campos sem `_`, construtor primário, `if (...) return ...;` curto onde já é o padrão do arquivo.

## Review Focus

1. **Ordem das checagens da `ResultApplicationPolicy`:** com o jogo instalado, `RequestMismatch` vem antes de `SourceFingerprintMismatch`, que vem antes de `DuplicateManagedAction`; sem o jogo instalado, `InstallDirectoryChanged` vem primeiro. Teste na Task 3 (casos com jogo instalado) e na teoria existente.
2. **Sinal terminal depois da unificação:** o aviso de outcome terminal continua sendo enfileirado na UI, não executado inline. Teste na Task 3, mantido equivalente na Task 5.
3. **Logs do coordinator acionados por sinal:** `"... started from signal."`, `"... completed from signal. Processed='N'."` e `"... failed after signal. Exception='…'."` ficam idênticos. Sem teste automatizado (o plugin depende do Playnite); verificação por comparação de texto na Task 6, Step 3.
4. **Restore em checkout normal e em worktree:** o feed do protocolo é achado nos dois layouts sem `NU1301`/`NU1801`. Verificação na Task 1.
5. **Falha ao iniciar o Orchestrator:** continua virando `DispatchResult` com `OrchestratorActivated = false` e `ActivationError`, sem exceção. Teste na Task 5.

---

### Task 0: Linha de base

- [ ] **Step 1:** `dotnet run --project VNManager/PlayniteInstallation.Tests/PlayniteInstallation.Tests.csproj` → `Total: 64, Failed: 0`
- [ ] **Step 2:** `dotnet run --project VNManager/Tests/Tests.csproj` → `Total: 32, Failed: 0`; `dotnet run --project VNManager/PlaynitePlugin.Tests/PlaynitePlugin.Tests.csproj` → `Total: 8, Failed: 0`
- [ ] **Step 3: Commit** — spec atualizada + este plano: `docs: plan installation workflow cleanup`

---

### Task 1: Feed do protocolo via Directory.Build.props

**Files:**
- Create: `Directory.Build.props` (raiz do repositório), com conteúdo idêntico ao `F:\Playnite Integration\Playnite VN Installer\Directory.Build.props` (propriedades `_VNInstallationProtocolCanonicalSource` = `..\Playnite VN Protocol\artifacts\packages`, `_VNInstallationProtocolWorktreeSource` = `..\..\..\Playnite VN Protocol\artifacts\packages`, a primeira que existir entra em `RestoreAdditionalProjectSources`)
- Modify: `NuGet.Config` — manter `<clear />` e só `nuget.org`; remover a fonte local e os dois comentários de caminho. O arquivo continua existindo (o `verify-consumers.ps1` do Protocol exige e usa como `--configfile`).

- [ ] **Step 1: Aplicar as duas mudanças**
- [ ] **Step 2: Restore forçado no checkout normal**

Run: `dotnet restore VNManager/Launcher.sln --force`
Expected: sem `NU1301` e sem `NU1801` na saída.

- [ ] **Step 3: Restore com o NuGet.Config explícito (como o verify-consumers faz)**

Run: `dotnet restore VNManager/PlayniteInstallation.Tests/PlayniteInstallation.Tests.csproj --configfile NuGet.Config --force`
Expected: sem erros.

- [ ] **Step 4: Build e as 3 suítes** — `dotnet build VNManager/Launcher.sln` → `0 Error(s)`; 64 / 32 / 8 passando.
- [ ] **Step 5: Commit** — `build: resolve protocol feed for checkout and worktree layouts`

---

### Task 2: Apagar os testes que pertencem ao Protocol

**Files:**
- Delete: `VNManager/PlayniteInstallation.Tests/AtomicTransportFileStoreTests.cs`, `ProtocolV1Tests.cs`, `SharedProtocolDependencyTests.cs`

- [ ] **Step 1: Apagar os 3 arquivos**
- [ ] **Step 2:** suíte da área → `Total: 48, Failed: 0` (64 − 5 − 9 − 2)
- [ ] **Step 3: Commit** — `test: drop protocol tests owned by the protocol repository`

---

### Task 3: Testes de caracterização

**Files:**
- Modify: `VNManager/PlayniteInstallation.Tests/ResultApplicationPolicyTests.cs`
- Modify: `VNManager/PlayniteInstallation.Tests/OrchestratorDispatchServiceTests.cs`

Os testes desta tarefa precisam **passar contra o código atual**: eles fixam o comportamento que as Tasks 4 e 5 vão preservar.

- [ ] **Step 1: Conflitos com o jogo instalado (Review Focus 1)** — nova teoria em `ResultApplicationPolicyTests`:

```csharp
[Theory]
[InlineData(ResultApplicationConflict.RequestMismatch)]
[InlineData(ResultApplicationConflict.SourceFingerprintMismatch)]
[InlineData(ResultApplicationConflict.DuplicateManagedAction)]
public void Evaluate_refuses_each_unsafe_case_for_an_installed_game(ResultApplicationConflict expected)
// current = Current() with { IsInstalled = true, InstallDirectory = Result().InstallationDirectory }
// RequestMismatch: + PendingRequestId = Guid.NewGuid()
// SourceFingerprintMismatch: + ExpectedSourceFingerprint = "changed"
// DuplicateManagedAction: + GameActions com 2 ações ResultApplicationPolicy.ManagedActionName
// Assert.False(decision.IsApplicable); Assert.Equal(expected, decision.Conflict);
```

- [ ] **Step 2: Sinal terminal (Review Focus 2)** — em `OrchestratorDispatchServiceTests`, `Terminal_outcome_synchronization_queues_reconciliation_on_the_ui_dispatcher`: cópia de `Result_ready_synchronization_queues_reconciliation_on_the_ui_dispatcher` usando `TerminalOutcomeSynchronization`.
- [ ] **Step 3:** suíte da área → `Total: 52, Failed: 0`
- [ ] **Step 4: Commit** — `test: characterize installed-game conflicts and terminal signal`

---

### Task 4: Domain

**Files:**
- Modify: `VNManager/PlayniteInstallation/Domain/ResultApplicationPolicy.cs`, `InstallationConfigurationForm.cs`, `PendingInstallCompletionRegistry.cs`
- Modify: `VNManager/PlaynitePlugin/Installation/PlayniteResultApplier.cs` (só o construtor de `GameActionSnapshot`), `InstallationConfigurationDialog.cs` (`ExtraFilesDirectory = string.Empty`)
- Modify: `VNManager/PlayniteInstallation.Tests/ResultApplicationPolicyTests.cs`, `InstallationConfigurationFormTests.cs` (remover `Assert.Null(form.ExtraFilesDirectory)`)
- Delete: `VNManager/PlayniteInstallation.Tests/ManagedPlayActionPolicyTests.cs` (os 4 testes vão, sem mudança de asserções, para `ResultApplicationPolicyTests`)

**Interfaces:**
- Produces: `public sealed record GameActionSnapshot(string Name, string Path, string WorkingDirectory, bool IsPlayAction = false);` (sem `Type`; enum `DirectGameActionType` removido).
- Produces: `ResultApplicationPolicy.Evaluate` com esta ordem: `null` → `MissingGame`; não instalado e diretório ≠ `SourcePath` → `InstallDirectoryChanged`; depois, em ambos os ramos, `RequestMismatch` → `SourceFingerprintMismatch` → `DuplicateManagedAction`; então o ramo instalado (corrige a ação existente ou `AlreadyInstalled`) ou o ramo não instalado (cria a ação). Helper `private static bool IsManaged(GameActionSnapshot action)`.
- `InstallationConfigurationForm.ExtraFilesDirectory` e `PendingInstallCompletionRegistry.Remove` removidos.

- [ ] **Step 1: Mover os 4 testes de `ManagedPlayActionPolicyTests` para `ResultApplicationPolicyTests`** (helpers `Current()`/`Result()` únicos) e apagar o arquivo; suíte → 52
- [ ] **Step 2: Aplicar as mudanças das Interfaces** (remover o argumento `DirectGameActionType.File` em todas as chamadas)
- [ ] **Step 3: Build** — `dotnet build VNManager/Launcher.sln` → `0 Error(s)`
- [ ] **Step 4:** suíte da área → `Total: 52, Failed: 0`
- [ ] **Step 5: Commit** — `refactor: simplify installation domain policies`

---

### Task 5: Transport

**Files:**
- Create: `VNManager/PlayniteInstallation/Transport/UiSignalSubscription.cs`, `IResultReadyUiDispatcher.cs` (interface movida, mesmo nome)
- Delete: `ResultReadySynchronization.cs`, `InteractionReadySynchronization.cs`, `TerminalOutcomeSynchronization.cs`
- Modify: `OrchestratorDispatchService.cs`, `InteractionRequestProcessor.cs`, `OrchestratorWakeSignal.cs`, `ResultReadyListener.cs` (só `using` redundantes)
- Modify: `VNManager/PlaynitePlugin/Installation/PluginInstallWorkflowCoordinator.cs` (3 campos viram `UiSignalSubscription`), `PlayniteInteractionValidator.cs` (remover `IsSupported` e a primeira checagem)
- Modify: `VNManager/PlayniteInstallation.Tests/OrchestratorDispatchServiceTests.cs`; Delete: `InteractionReadySynchronizationTests.cs` (o teste vai para `OrchestratorDispatchServiceTests` usando `UiSignalSubscription`)

**Interfaces:**
- Produces: `public sealed class UiSignalSubscription : IDisposable { public UiSignalSubscription(string eventName, IResultReadyUiDispatcher dispatcher, Action onSignal); }` — `ArgumentNullException` para `dispatcher`/`onSignal`, e `listener = new ResultReadyListener(eventName, () => dispatcher.BeginInvoke(onSignal))`.
- Produces: `public sealed record DispatchResult(bool OrchestratorActivated, Exception? ActivationError);` e `public void PersistRequest(InstallationRequest request)`. `DispatchAsync` removido. `DispatchCommandAsync` = `persist(); return await ActivateAsync(cancellationToken).ConfigureAwait(false);`.

- [ ] **Step 1: Ajustar os testes primeiro**
  - `DispatchAsync_writes_before_activating_and_returns_when_activation_fails` vira `Activation_failure_is_returned_after_the_request_was_persisted` (Review Focus 5): `PersistRequest(Request())`, `ThrowOnActivate = true`, `await ActivateAsync(...)`; asserções: `RequestWasPresentWhenActivated`, `!result.OrchestratorActivated`, `result.ActivationError` não nulo.
  - `Persisting_a_request_does_not_activate...`: sem as asserções sobre o retorno de `PersistRequest`.
  - Os testes de `ResultReadySynchronization`, `TerminalOutcomeSynchronization` e `InteractionReadySynchronization` passam a construir `UiSignalSubscription`. O de `TerminalOutcome` fica idêntico ao de `ResultReady` e é apagado.
- [ ] **Step 2: Rodar** → falha de compilação (`UiSignalSubscription` não existe)
- [ ] **Step 3: Implementar as Interfaces e apagar os arquivos listados**
- [ ] **Step 4: Build** → `0 Error(s)`; suíte da área → `Total: 51, Failed: 0`
- [ ] **Step 5: Commit** — `refactor: unify UI signal subscriptions and dispatch results`

---

### Task 6: Plugin — fluxo de instalação

**Files:**
- Modify: `VNManager/PlaynitePlugin/Installation/PluginInstallWorkflowCoordinator.cs`, `PlayniteResultApplier.cs`, `PlayniteInteractionDialog.cs`, `InstallationConfigurationDialog.cs`
- Create: `VNManager/PlaynitePlugin/Installation/InstallationDialogs.cs` com `internal static class InstallationDialogs { public const string ExecutableFileFilter = "Executable files|*.exe"; }`

**Interfaces:**
- No coordinator: `private void SynchronizeResults(string trigger)` e `private void ProcessInteractions(string trigger)` contêm o `try/catch` com os logs atuais. Os handlers de sinal passam `"signal"`; os agendados chamam esses métodos dentro do `BeginInvoke`. Um campo `wakeSignal` (`NamedOrchestratorWakeSignal`) é usado pelo applier e pelo processor. `Dispose`: `subscription?.Dispose(); subscription = null;` para os 3 campos. `using System.Threading;` / `System.Threading.Tasks` no lugar dos nomes qualificados; `InstallationRequest` sem namespace.
- No applier: `feature?.Name?.StartsWith(SupervisorPlayAction.LauncherFeaturePrefix, StringComparison.OrdinalIgnoreCase)`.

- [ ] **Step 1: Aplicar as mudanças**
- [ ] **Step 2: Build** → `0 Error(s)`; 51 / 32 / 8 passando
- [ ] **Step 3: Conferir os logs (Review Focus 3)**

Run: `git diff HEAD -- VNManager/PlaynitePlugin/Installation/PluginInstallWorkflowCoordinator.cs`
Expected: todo texto entre aspas removido reaparece idêntico, com `{trigger}` produzindo `signal` nos caminhos de sinal.

- [ ] **Step 4: Commit** — `refactor: simplify plugin installation workflow`

---

### Task 7: Documentação

**Files:**
- Modify: `README.md`, seção "Shared installation protocol verification":
  - versão `1.0.0-alpha.7`;
  - comando com `-OrchestratorRoot "F:\Playnite Integration\Playnite VN Installer"` e `-PlayniteIntegrationRoot "F:\Playnite Integration\Playnite VN Manager"`;
  - o feed é resolvido pelo `Directory.Build.props`;
  - remover a frase sobre "Install-button/no-log regression … Plan 2".

- [ ] **Step 1: Editar**
- [ ] **Step 2:** `git grep -n -E "alpha\.2|\.worktrees" -- README.md AGENTS.md docs ':!docs/superpowers'` → nenhuma linha
- [ ] **Step 3: Commit** — `docs: update protocol verification instructions`

---

### Task 8: Verificação final e revisão

- [ ] **Step 1:** `dotnet build VNManager/Launcher.sln -c Release` → `0 Error(s)`, sem `NU1801`
- [ ] **Step 2:** `PlayniteInstallation.Tests` 51, `Tests` 32, `PlaynitePlugin.Tests` 8, todos passando
- [ ] **Step 3:** `git -C "../Playnite VN Protocol" status --short` e `git -C "../Playnite VN Installer" status --short` → mesma saída de antes da Task 0 (outros repositórios intocados)
- [ ] **Step 4:** revisão final da branch por um revisor novo, com este plano, a spec e o Review Focus
- [ ] **Step 5 (usuário):** com `scripts/Publish-Extension.ps1`, instalar no Playnite uma VN portátil e uma com instalador, e conferir progresso, diálogos e a ação "VN Orchestrator".
