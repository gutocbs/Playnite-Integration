# Revisão de simplificação — Playnite VN Manager

Data: 2026-10-19 · Branch: `review-e-correção` · Status: aprovado parte a parte pelo usuário.

## Objetivo

Simplificar e limpar o código do Playnite VN Manager **sem alterar comportamento**,
removendo código morto e testes sem valor, e acrescentando testes de caracterização
onde um refactor precisa de proteção.

## Fora do escopo

- `PlayniteInstallation/`, `PlayniteInstallation.Tests/`, `PlaynitePlugin/Installation/`,
  `NuGet.Config`, `docs/superpowers/` existentes e a seção de protocolo de instalação do
  README: em desenvolvimento por outro agente (`codex/vn-orchestrator-plugin`). Revisão
  adiada (Parte 5).
- Qualquer arquivo do repositório Playnite VN Protocol.
- Correção de bugs.

## Decisões aprovadas

### Remoção do caminho PowerShell (Partes 1 e 4)
O Adapter PowerShell não é mais usado; o plugin chama o `LauncherSupervisor`.
Remover: `Adapter/` inteiro, `RunWithLocaleEmulator.ps1`, `Tests/AdapterIntegrationTests.cs`,
`Tests/PlayniteAdapterHarness.ps1`, e todo código do Host que só existe para o `Host.exe`:
`Program`, `HostApplication`, `HostInvocation`, `HostOptions(+Loader)`, `HostResult`,
`HostExitCodes`, `JsonFileTransport`, `TemporaryTransportCleaner`, `CrossProcessCancellation`,
`Dispatcher`, `DispatcherRequestValidator(+Exception)`, `Configuration/host.json` e
`Abstractions/DispatcherRequest`. O `Host` vira biblioteca com `HostComposition`,
`LauncherRegistry`, `LauncherRegistration` e `LauncherResolutionException`.

### Logging e composição (Parte 1)
- `AppLogger.FormatValue` reaproveita `EscapeLineBreaks` (mesma saída); remover o
  `?? AppContext.BaseDirectory` inalcançável. Teste de caracterização do formato.
- `HostComposition`: uma função local para logger/monitor/cleanup; carregamento antecipado
  das 3 configurações preservado.

### ProcessManagement (Parte 2)
Remover a checagem inalcançável `configuredProcesses.Length == 0`; unificar
`Build*CleanupPlan`; helper único para "entradas de `executables` deste processo";
`preferredProcesses` como nomes; separar `ProcessMonitor` em fase de detecção e fase de
supervisão; `ProcessCleanup` com construtor primário único e `IServiceStopper? = null`;
simplificar `IsRunning`. Apagar `ProcessCleanup_AcceptsLegacyNullServiceStopperWhenNoServicesAreConfigured`.
Novo teste: `timeout` de `executables` sobrepõe `globalTimeout`.

### Launchers (Parte 3)
`LocaleEmulatorLauncher` e `NoRegionLoaderLauncher` **continuam classes e projetos separados**.
Novo projeto `Launcher.Supervision` com o que é comum: `ILaunchedProcess`, o wrapper de
processo, as validações comuns e o pipeline iniciar → monitorar → mapear → cleanup.
`LaunchErrorCodes` em `Abstractions` (`LAUNCH_CANCELLED`, `PROCESS_START_TIMEOUT`,
`INVALID_GAME_EXECUTABLE`, `GAME_EXECUTABLE_NOT_FOUND`, `INVALID_POLLING_INTERVAL`).
Testes de caracterização dos argumentos do starter LE e do `WorkingDirectory` do NRL.
Nenhum teste de launcher é apagado.

### LauncherSupervisor (Parte 4)
Extrair `SupervisorArguments` com testes de caracterização; referências do csproj reduzidas
a `Host` + `Abstractions`; configurações movidas para `LauncherSupervisor/Configuration/`.

### Plugin — só launch (Parte 6)
Extrair a lógica de `GetPlayActions` para `Launching/SupervisorPlayAction.cs`; constante
`LauncherFeaturePrefix`; remover checagem redundante, variável `controller` e o bloco de
desembrulho de ações que já apontam para o `LauncherSupervisor.exe`; checar o supervisor
antes; atualizar o comentário do filtro File; remover `LangVersion` duplicado. Novo projeto
`PlaynitePlugin.Tests`.

### Build, configuração e docs (Parte 7)
Plataformas x64/x86 fora da solução; `Launcher.sln.DotSettings.user` fora do git; entrada
inócua `rugp.exe` em `executables` removida. Docs: apagar `ADAPTER_HOST_PROTOCOL.md`;
alinhar `ARCHITECTURE.md`, `Launcher Contract.md`, `LAUNCHER_SCOPE.md`,
`LAUNCHER_RESOLUTION.md`, `logging.md`, `Principios de Design.md`; ADR-001/003 como
Superseded e nova ADR-005; README atualizado. Instruções de IA consolidadas em `AGENTS.md`
na raiz (conteúdo de `docs/AGENT.md` + `codex.md`, corrigido); `codex.md` e um novo
`CLAUDE.md` apenas mandam ler o `AGENTS.md`.

### Instalação de VNs — Parte 5 (aprovada em 2026-10-20)
O desenvolvimento do agente terminou; o Installer não referencia o Manager (só o
pacote `VNInstallation.Protocol` alpha.7), então nada aqui muda protocolo, eventos ou
arquivos trocados.
- Código morto: `OrchestratorDispatchService.DispatchAsync`, `DispatchResult.RequestPersisted`,
  `PendingInstallCompletionRegistry.Remove`, `InstallationConfigurationForm.ExtraFilesDirectory`,
  o enum `DirectGameActionType` (com o campo `Type` e a checagem), e o `IsSupported` do
  `PlayniteInteractionValidator` (o processor já filtra).
- Duplicação: as 3 classes `*Synchronization` viram `UiSignalSubscription`;
  `DispatchCommandAsync` reaproveita `ActivateAsync`; `ResultApplicationPolicy` faz as
  checagens comuns uma vez (ordem preservada) com `IsManaged`; no
  `PluginInstallWorkflowCoordinator`, métodos de sinal e agendados unificados (mesmos logs),
  `Dispose` com `?.`, um só wake signal, `using` no lugar de nomes qualificados; `"[Launcher]"`
  via `SupervisorPlayAction.LauncherFeaturePrefix`; constante para o filtro `*.exe`;
  `using` redundantes.
- Build: `Directory.Build.props` igual ao do Installer (feed em `..\` ou `..\..\..\`);
  `NuGet.Config` mantido só com nuget.org (exigido pelo `verify-consumers.ps1`).
- README: seção de verificação do protocolo com alpha.7 e caminhos do checkout normal.
- Testes: apagar `AtomicTransportFileStoreTests`, `ProtocolV1Tests`, `SharedProtocolDependencyTests`
  (cobertos no repositório do Protocol); juntar `ManagedPlayActionPolicyTests` em
  `ResultApplicationPolicyTests`; caracterização dos conflitos com jogo instalado e do sinal
  terminal.
- Não muda (comportamento, só registrado): `Snapshot` do applier usa request/fingerprint do
  próprio resultado; `NoActiveWorkflowLookup` sempre falso.

### Commits
Commitar tudo na `review-e-correção`, incluindo as alterações do usuário em `.gitignore` e
`codex.md`. Nunca subir para `release`. O usuário valida manualmente no Playnite ao fim.
