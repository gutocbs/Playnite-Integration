# VN Manager — Revisão de simplificação: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplificar o Playnite VN Manager e remover o caminho PowerShell sem alterar o comportamento do caminho Plugin → LauncherSupervisor → launcher.

**Architecture:** Primeiro remove-se o que está morto (Adapter, `Host.exe`), depois cada área recebe testes de caracterização **antes** do refactor que eles protegem. O código comum dos launchers vai para um projeto novo, `Launcher.Supervision`, sem fundir os launchers.

**Tech Stack:** .NET 10 / C# (nullable), .NET Framework 4.6.2 + PlayniteSDK 6.16.0 (plugin), xUnit v3 3.2.2.

**Spec:** `docs/superpowers/specs/2026-10-19-vn-manager-cleanup-design.md`

## Global Constraints

- Branch: `review-e-correção`. Nunca fazer push nem merge para `release`. Confirmar com `git branch --show-current` antes de cada commit.
- Não tocar: `VNManager/PlayniteInstallation/**`, `VNManager/PlayniteInstallation.Tests/**`, `VNManager/PlaynitePlugin/Installation/**`, `NuGet.Config`, os arquivos já existentes em `docs/superpowers/`, nem nada no repositório Playnite VN Protocol.
- Nenhuma mudança de comportamento: códigos de erro, mensagens, chaves de `Data`, mensagens de log e exit codes ficam idênticos, byte a byte.
- Todos os comandos rodam a partir de `F:\Playnite Integration\Playnite VN Manager`.
- Suíte canônica: `dotnet run --project VNManager/Tests/Tests.csproj`. Ela inicia processos `VnTest*`; não interromper no meio.
- Estilo: seguir o código ao redor (`_campo` ou construtor primário, `ArgumentException.ThrowIfNullOrWhiteSpace`, nomes de teste `Metodo_Condicao_Resultado`).

## Review Focus

1. **Mensagens dos launchers depois da extração do pipeline:** o texto de cancelamento de cada launcher continua exato (`"Locale Emulator process supervision was cancelled."` / `"NoRegionLoader process supervision was cancelled."`). Os testes ficam na Task 5.
2. **Feature `[Launcher]` sem nome depois do prefixo:** continua gerando `--launcher ""`, como hoje. O teste fica na Task 7.
3. **Configurações no publish do supervisor:** depois de mover os JSONs, `dotnet publish` precisa continuar levando os 3 arquivos para `Configuration/`. A verificação fica na Task 6.
4. **Argumentos originais vazios ou só com espaços:** não podem gerar ` -- `. O teste fica na Task 7.
5. **Cleanup depois do cancelamento:** continua rodando com um token próprio de 30s. Os testes atuais `WhenMonitoringIsCancelled` cobrem isso no LE; a Task 5 acrescenta o mesmo teste no NRL.

---

### Task 0: Linha de base e commit das alterações pendentes

**Files:** nenhum novo. Commita `.gitignore`, `codex.md` e os documentos de spec e plano.

- [ ] **Step 1: Rodar a linha de base**

Run: `dotnet run --project VNManager/Tests/Tests.csproj`
Expected: `Total: 27, Errors: 0, Failed: 0`

Run: `dotnet run --project VNManager/PlayniteInstallation.Tests/PlayniteInstallation.Tests.csproj`
Expected: `Failed: 0`. Anotar o total: ele precisa ser o mesmo na Task 9.

- [ ] **Step 2: Commit**

```bash
git add .gitignore codex.md docs/superpowers/specs/2026-10-19-vn-manager-cleanup-design.md docs/superpowers/plans/2026-10-19-vn-manager-cleanup.md
git commit -m "docs: add VN Manager cleanup spec and plan"
```

---

### Task 1: Remover o caminho PowerShell e o Host.exe

**Files:**
- Delete: `VNManager/Adapter/` (pasta inteira), `RunWithLocaleEmulator.ps1`
- Delete: `VNManager/Host/{Program,HostApplication,HostInvocation,HostOptions,HostOptionsLoader,HostResult,HostExitCodes,JsonFileTransport,TemporaryTransportCleaner,CrossProcessCancellation,Dispatcher,DispatcherRequestValidator,DispatcherRequestValidationException}.cs`, `VNManager/Host/Configuration/host.json`
- Delete: `VNManager/Abstractions/DispatcherRequest.cs`
- Delete: `VNManager/Tests/{AdapterIntegrationTests,HostOptionsLoaderTests,TemporaryTransportCleanerTests}.cs`, `VNManager/Tests/PlayniteAdapterHarness.ps1`
- Modify: `VNManager/Host/Host.csproj` (remover `OutputType` e o item de `host.json`; os outros itens de configuração saem na Task 6)
- Modify: `VNManager/Tests/Tests.csproj` (remover os `None` de `Logging.psm1`, `PlayniteAdapter.ps1` e `PlayniteAdapterHarness.ps1`, e os itens `HostFiles`/`TestAssets\Host` do target)

**Interfaces:**
- Produces: `Launcher.Host` vira biblioteca e expõe só `HostComposition.CreateRegistry(string configurationDirectory, string logPath) : LauncherRegistry`, `LauncherRegistry`, `LauncherRegistration`, `LauncherResolutionException`.

- [ ] **Step 1: Apagar os arquivos e ajustar os 3 csproj listados**
- [ ] **Step 2: Build**

Run: `dotnet build VNManager/Launcher.sln`
Expected: `0 Error(s)`

- [ ] **Step 3: Testes**

Run: `dotnet run --project VNManager/Tests/Tests.csproj`
Expected: `Total: 23, Failed: 0` (27 − 2 do Adapter − 1 do HostOptions − 1 do TemporaryTransportCleaner)

- [ ] **Step 4: Procurar referências órfãs no código**

Run: `git grep -n -E "DispatcherRequest|HostApplication|JsonFileTransport|PlayniteAdapter|Logging\.psm1" -- VNManager`
Expected: nenhuma linha

- [ ] **Step 5: Commit** — `git commit -am "refactor: remove PowerShell adapter and Host executable"` (usar `git add -A` antes, por causa dos arquivos apagados)

---

### Task 2: Logging e HostComposition

**Files:**
- Create: `VNManager/Tests/AppLoggerTests.cs`
- Modify: `VNManager/Logging/AppLogger.cs:74-96,118-127`
- Modify: `VNManager/Host/HostComposition.cs`

- [ ] **Step 1: Escrever o teste de caracterização (ele deve passar com o código atual)**

```csharp
[Fact]
public void Write_EscapesMessageAndDataValues()
{
    using var files = new TemporaryFiles();
    var logPath = Path.Combine(files.DirectoryPath, "launcher.log");

    new AppLogger("exec-1", logPath).Write(
        LogLevel.Info, "Component", "line1\nline2",
        new Dictionary<string, object?> { ["Path"] = "a;b\\c\nd", ["Missing"] = null });

    var line = Assert.Single(File.ReadAllLines(logPath));
    Assert.Contains("[INFO] [exec-1] [Component] line1\\nline2 | ", line);
    Assert.EndsWith(@"Path=a\;b\\c\nd; Missing=null", line);
}
```

- [ ] **Step 2: Rodar e confirmar que PASSA** — `dotnet run --project VNManager/Tests/Tests.csproj` → `Total: 24, Failed: 0`
- [ ] **Step 3: Refatorar `AppLogger`**: `FormatValue` = `EscapeLineBreaks(text.Replace("\\", "\\\\")).Replace(";", "\\;")`, mantendo a ordem de escape (barra → quebras → ponto e vírgula). Em `TryWriteFallback`, usar `Path.GetDirectoryName(_logPath)!`.
- [ ] **Step 4: Refatorar `HostComposition`**: colocar os `Path.Combine` direto nas chamadas dos loaders e criar uma função local `(IAppLogger Logger, ProcessMonitor Monitor, ProcessCleanup Cleanup) CreateSupervision(string executionId)`, usada pelas duas registrations. Ordem de carregamento preservada: LE, NRL, process-management.
- [ ] **Step 5: Testes** — mesmo comando → `Total: 24, Failed: 0`
- [ ] **Step 6: Commit** — `refactor: simplify logger escaping and host composition`

---

### Task 3: ProcessManagement

**Files:**
- Create: `VNManager/ProcessManagement/ExecutableEntries.cs`
- Modify: `VNManager/ProcessManagement/ProcessMonitor.cs`, `ProcessCleanup.cs`
- Modify: `VNManager/Tests/ProcessIntegrationTests.cs` (apagar `ProcessCleanup_AcceptsLegacyNullServiceStopperWhenNoServicesAreConfigured` e acrescentar o teste abaixo)

**Interfaces:**
- Produces: `internal static class ExecutableEntries { public static IEnumerable<ExecutableCleanupOptions> For(ProcessManagementOptions options, string normalizedProcessName); }`, que compara com `ProcessName.Normalize(entry.Name)` sem diferenciar maiúsculas.
- Produces: `public sealed class ProcessCleanup(ProcessManagementOptions options, IAppLogger logger, IServiceStopper? serviceStopper = null) : IProcessCleanup`, com `null` substituído por `new ElevatedServiceStopper()` e `ArgumentNullException` para `options`/`logger`.

- [ ] **Step 1: Escrever o teste de caracterização (ele deve passar com o código atual)**

```csharp
[Fact]
public async Task ProcessMonitor_ExecutableTimeoutOverridesGlobalTimeout()
{
    var processName = $"VnMissing{Guid.NewGuid():N}";
    var monitor = new ProcessMonitor(
        new ProcessManagementOptions
        {
            GlobalTimeout = 10,
            Executables = [new ExecutableCleanupOptions { Name = $"{processName}.exe", Timeout = 1 }]
        },
        new NullLogger());
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var exception = await Assert.ThrowsAsync<ProcessStartTimeoutException>(() =>
        monitor.WaitForStartAndExitAsync($"{processName}.exe", TimeSpan.FromMilliseconds(25), timeout.Token));

    Assert.Contains($"{processName} (1s)", exception.Message);
}
```

- [ ] **Step 2: Apagar o teste legado e rodar** → `Total: 24, Failed: 0`
- [ ] **Step 3: Refatorar `ProcessCleanup`**: construtor primário único (assinatura acima). `BuildProcessCleanupPlan` e `BuildServiceCleanupPlan` viram `BuildCleanupPlan(IReadOnlyList<string>? global, Func<ExecutableCleanupOptions, IReadOnlyList<string>?> specific, string? monitoredProcessName, bool normalize)`, mantendo `Split(',')` → `Trim().Trim('"')` → filtro de vazios → (`Normalize` só quando `normalize`) → `Distinct(OrdinalIgnoreCase)`. Usar `ExecutableEntries.For`.
- [ ] **Step 4: Refatorar `ProcessMonitor`**:
  - remover a checagem `configuredProcesses.Length == 0`;
  - `preferredNames` vira um array de nomes (normalizado, distinto, sem o jogo, na ordem da configuração) mais um `HashSet<string>(OrdinalIgnoreCase)` para pertinência;
  - `configuredProcesses` = `[gameProcess, ..preferredNames]` com `ResolveTimeout`;
  - extrair `private async Task<string> WaitForStartAsync(...)` (o laço de detecção, sem nenhuma mudança de lógica) e `private static async Task WaitWhileRunningAsync(string processName, TimeSpan pollingInterval, CancellationToken token)`;
  - `IsRunning`: obter os processos, guardar `Length > 0`, fazer `Dispose` de cada um e retornar;
  - `ResolveTimeout` passa a usar `ExecutableEntries.For`.
- [ ] **Step 5: Testes** → `Total: 24, Failed: 0`
- [ ] **Step 6: Commit** — `refactor: simplify process monitoring and cleanup`

---

### Task 4: Caracterização dos starters

**Files:**
- Modify: `VNManager/LocaleEmulator/LocaleEmulatorProcessStarter.cs`, `VNManager/NoRegionLoader/NoRegionLoaderProcessStarter.cs`
- Create: `VNManager/Tests/ProcessStarterTests.cs`

**Interfaces:**
- Produces: `public static ProcessStartInfo LocaleEmulatorProcessStarter.CreateStartInfo(LocaleEmulatorOptions options, LaunchRequest request)` e `public static ProcessStartInfo NoRegionLoaderProcessStarter.CreateStartInfo(NoRegionLoaderOptions options, LaunchRequest request)`. `Start` passa a chamar esses métodos.

- [ ] **Step 1: Extrair `CreateStartInfo` nos dois starters** (refactor puro: o corpo é o trecho atual que monta o `ProcessStartInfo`)
- [ ] **Step 2: Escrever os testes**

```csharp
[Fact]
public void LocaleEmulatorStartInfo_PassesProfileGameAndArguments()
{
    var startInfo = LocaleEmulatorProcessStarter.CreateStartInfo(
        new LocaleEmulatorOptions { LocaleEmulatorExecutable = @"C:\LE\LEProc.exe", ProfileId = "profile" },
        new LaunchRequest("id", @"C:\Game\game.exe", ["-a", "b c"]));

    Assert.Equal(@"C:\LE\LEProc.exe", startInfo.FileName);
    Assert.False(startInfo.UseShellExecute);
    Assert.Equal(["-runas", "profile", @"C:\Game\game.exe", "-a", "b c"], startInfo.ArgumentList);
}

[Fact]
public void NoRegionLoaderStartInfo_RunsFromGameDirectory()
{
    var startInfo = NoRegionLoaderProcessStarter.CreateStartInfo(
        new NoRegionLoaderOptions { NoRegionLoaderExecutable = @"C:\NRL\NoRegion Loader.exe" },
        new LaunchRequest("id", @"C:\Game\game.exe", ["-a", "b c"]));

    Assert.Equal(@"C:\NRL\NoRegion Loader.exe", startInfo.FileName);
    Assert.Equal(@"C:\Game", startInfo.WorkingDirectory);
    Assert.False(startInfo.UseShellExecute);
    Assert.Equal([@"C:\Game\game.exe", "-a", "b c"], startInfo.ArgumentList);
}
```

- [ ] **Step 3: Testes** → `Total: 26, Failed: 0`
- [ ] **Step 4: Commit** — `test: characterize launcher process start info`

---

### Task 5: Projeto Launcher.Supervision

**Files:**
- Create: `VNManager/Supervision/Supervision.csproj` (net10.0, `RootNamespace` `Launcher.Supervision`, `AssemblyName` `Launcher.Supervision`, refs: Abstractions, Logging, ProcessManagement)
- Create: `VNManager/Supervision/ILaunchedProcess.cs`, `ToolProcess.cs`, `SupervisedTool.cs`, `LaunchValidation.cs`, `SupervisedLaunch.cs`
- Create: `VNManager/Abstractions/LaunchErrorCodes.cs`
- Delete: `VNManager/LocaleEmulator/ILaunchedProcess.cs`
- Modify: `INoRegionLoaderProcessStarter.cs` (remover `INoRegionLoaderProcess`), os dois `*ProcessStarter.cs`, `ILocaleEmulatorProcessStarter.cs`, os dois `*Launcher.cs`, os dois `.csproj` (referenciar Supervision), `VNManager/Launcher.sln` (`dotnet sln VNManager/Launcher.sln add VNManager/Supervision/Supervision.csproj`)
- Modify: `VNManager/Tests/TestDoubles.cs`, `NoRegionLoaderLauncherTests.cs` (usar um único `FakeLaunchedProcess(int id)`) e `Tests.csproj` (referenciar Supervision)

**Interfaces:**
- Produces (`Launcher.Abstractions`): `public static class LaunchErrorCodes { Cancelled = "LAUNCH_CANCELLED"; ProcessStartTimeout = "PROCESS_START_TIMEOUT"; InvalidGameExecutable = "INVALID_GAME_EXECUTABLE"; GameExecutableNotFound = "GAME_EXECUTABLE_NOT_FOUND"; InvalidPollingInterval = "INVALID_POLLING_INTERVAL"; }` (todas `const string`)
- Produces (`Launcher.Supervision`):
  - `public interface ILaunchedProcess : IDisposable { int Id { get; } }`
  - `public static class ToolProcess { public static ILaunchedProcess Start(ProcessStartInfo startInfo, string toolDisplayName); }`. Lança `InvalidOperationException($"{toolDisplayName} did not return a process instance.")`.
  - `public sealed record SupervisedTool(string DisplayName, string Component, string FailureCode, string ProcessIdDataKey);` LE = `("Locale Emulator", "LocaleEmulator", "LOCALE_EMULATOR_LAUNCH_FAILED", "LocaleEmulatorProcessId")`; NRL = `("NoRegionLoader", "NoRegionLoader", "NO_REGION_LOADER_LAUNCH_FAILED", "NoRegionLoaderProcessId")`.
  - `public static class LaunchValidation`:
    - `LaunchResult? ValidateGameExecutable(LaunchRequest request)`: devolve os códigos/mensagens atuais de jogo não absoluto e de jogo não encontrado;
    - `LaunchResult? ValidateToolExecutable(string? path, string notFoundCode, string toolDisplayName)`: mensagem `$"The {toolDisplayName} executable was not found: {path}"`;
    - `LaunchResult? ValidatePollingInterval(TimeSpan interval)`.
  - `public static class SupervisedLaunch { public static Task<LaunchResult> RunAsync(SupervisedTool tool, LaunchRequest request, TimeSpan pollingInterval, Func<ILaunchedProcess> startTool, IProcessMonitor monitor, IProcessCleanup cleanup, IAppLogger logger, CancellationToken cancellationToken); }`. O corpo é o `try/catch/finally` atual do `LaunchAsync`, com os textos parametrizados:
    - logs: `$"{tool.DisplayName} command started"`, `"Process supervision was cancelled"`, `"Game process start timed out"`, `$"{tool.DisplayName} launch failed"`, `"Process cleanup did not complete"`;
    - mensagem de cancelamento: `$"{tool.DisplayName} process supervision was cancelled."`;
    - o comentário do token próprio de 30s do cleanup vai junto;
    - o `using` do processo continua dentro do `try`.
- Os launchers continuam com as assinaturas públicas atuais. `LaunchAsync` valida na ordem de hoje (LE: jogo → ferramenta → `profileId` → polling; NRL: jogo → ferramenta → polling) e chama `SupervisedLaunch.RunAsync(..., () => processStarter.Start(options, request), ...)`.
- `ILocaleEmulatorProcessStarter.Start` e `INoRegionLoaderProcessStarter.Start` passam a devolver `ILaunchedProcess` e usam `ToolProcess.Start(CreateStartInfo(...), "<DisplayName>")`.

- [ ] **Step 1: Escrever os testes de mensagem (Review Focus 1 e 5); eles devem passar com o código atual**

Em `LocaleEmulatorLauncherTests.LaunchAsync_WhenMonitoringIsCancelled_ReturnsFailureAndRunsCleanup`, acrescentar
`Assert.Equal("Locale Emulator process supervision was cancelled.", result.Error?.Message);`.
Em `NoRegionLoaderLauncherTests`, novo teste `LaunchAsync_WhenMonitoringIsCancelled_ReturnsFailureAndRunsCleanup`, espelhando o do LE, com
`Assert.Equal("LAUNCH_CANCELLED", result.Error?.Code)`, `Assert.Equal("NoRegionLoader process supervision was cancelled.", result.Error?.Message)` e `Assert.Null(cleanup.MonitoredProcessName)`.

- [ ] **Step 2: Rodar** → `Total: 27, Failed: 0`
- [ ] **Step 3: Criar o projeto e mover o código conforme as Interfaces acima**
- [ ] **Step 4: Build** — `dotnet build VNManager/Launcher.sln` → `0 Error(s)`
- [ ] **Step 5: Testes** → `Total: 27, Failed: 0`
- [ ] **Step 6: Conferir que não sobrou literal duplicado** — `git grep -n -E "\"(LAUNCH_CANCELLED|PROCESS_START_TIMEOUT|INVALID_GAME_EXECUTABLE|GAME_EXECUTABLE_NOT_FOUND|INVALID_POLLING_INTERVAL)\"" -- VNManager ':!VNManager/Tests'` → só `LaunchErrorCodes.cs`
- [ ] **Step 7: Commit** — `refactor: extract shared launcher supervision project`

---

### Task 6: LauncherSupervisor

**Files:**
- Create: `VNManager/LauncherSupervisor/SupervisorArguments.cs`, `VNManager/Tests/SupervisorArgumentsTests.cs`
- Move (`git mv`): `VNManager/Host/Configuration/locale-emulator.json`, `VNManager/Host/Configuration/no-region-loader.json` e `VNManager/ProcessManagement/process-management.json` → `VNManager/LauncherSupervisor/Configuration/`
- Modify: `VNManager/LauncherSupervisor/Program.cs`, `LauncherSupervisor.csproj` (refs só `Host` e `Abstractions`; `<None Update="Configuration\*.json" CopyToOutputDirectory="PreserveNewest" />`), `VNManager/Host/Host.csproj` (remover os itens de configuração que sobraram), `VNManager/Tests/Tests.csproj` (referenciar LauncherSupervisor)

**Interfaces:**
- Produces: `public sealed record SupervisorArguments(string Executable, string LauncherName, string ConfigurationDirectory, string LogPath, IReadOnlyList<string> GameArguments)` com `public static bool TryParse(IReadOnlyList<string> args, string baseDirectory, out SupervisorArguments? arguments, out string? error)`. A regra é a mesma de hoje:
  - tudo depois do primeiro `--` vai para `GameArguments`;
  - um par `--nome valor` repetido sobrescreve o anterior;
  - erro `"Argumentos inválidos. Use --executable <caminho> [--launcher <nome>] [-- ...argumentos]."` quando um item não começa com `--` ou não tem valor;
  - erro `"O argumento --executable é obrigatório."`;
  - `LauncherName` padrão `"Locale Emulator"`;
  - `ConfigurationDirectory` e `LogPath` com `Path.GetFullPath` e os padrões `<base>\Configuration` e `<base>\launcher.log`;
  - `Executable` sai **sem** `GetFullPath`: o `Program` continua aplicando `GetFullPath` dentro do `try`.
- `Program.cs` fica com: `TryParse(args, AppContext.BaseDirectory, ...)`, o `Fail` atual (mesmo prefixo `LauncherSupervisor: ` no stderr e exit code 1) e o mesmo `try/catch`.

- [ ] **Step 1: Escrever os testes** (`baseDirectory = @"C:\Plugin"`)

```csharp
[Fact] public void TryParse_CollectsArgumentsAfterSeparator()
// ["--executable", @"C:\G\g.exe", "--launcher", "NoRegionLoader", "--", "-a", "--b"]
// → true; Executable == @"C:\G\g.exe"; LauncherName == "NoRegionLoader"; GameArguments == ["-a", "--b"]

[Fact] public void TryParse_DefaultsLauncherConfigurationAndLog()
// ["--executable", @"C:\G\g.exe"] → LauncherName == "Locale Emulator";
// ConfigurationDirectory == @"C:\Plugin\Configuration"; LogPath == @"C:\Plugin\launcher.log"; GameArguments vazio

[Fact] public void TryParse_RequiresExecutable()
// ["--launcher", "X"] → false; error == "O argumento --executable é obrigatório."

[Fact] public void TryParse_RejectsOptionWithoutValue()
// ["--executable"] → false; error == "Argumentos inválidos. Use --executable <caminho> [--launcher <nome>] [-- ...argumentos]."
```

- [ ] **Step 2: Implementar `SupervisorArguments`, ajustar `Program.cs` e os csproj, mover os JSONs**
- [ ] **Step 3: Testes** → `Total: 31, Failed: 0`
- [ ] **Step 4: Publish (Review Focus 3)**

Run: `dotnet publish VNManager/LauncherSupervisor/LauncherSupervisor.csproj -c Release`
Then: `ls VNManager/LauncherSupervisor/bin/Release/net10.0/publish/Configuration`
Expected: exatamente `locale-emulator.json`, `no-region-loader.json` e `process-management.json`

- [ ] **Step 5: Commit** — `refactor: extract supervisor argument parsing and own configuration`

---

### Task 7: Plugin — ação de launch

**Files:**
- Create: `VNManager/PlaynitePlugin/Launching/SupervisorPlayAction.cs`
- Create: `VNManager/PlaynitePlugin.Tests/PlaynitePlugin.Tests.csproj` (`OutputType Exe`, `net472`, xunit.v3 3.2.2, `<Using Include="Xunit" />`, referência a `..\PlaynitePlugin\PlaynitePlugin.csproj`), `VNManager/PlaynitePlugin.Tests/SupervisorPlayActionTests.cs`
- Modify: `VNManager/PlaynitePlugin/VnManagerPlugin.cs` (só `GetPlayActions` e os `using`), `VNManager/PlaynitePlugin/PlaynitePlugin.csproj` (remover o 2º `<LangVersion>`), `VNManager/Launcher.sln` (`dotnet sln add`)

**Interfaces:**
- Produces: `public static class SupervisorPlayAction`
  - `public const string LauncherFeaturePrefix = "[Launcher]";`
  - `public static string? FindLauncherName(IEnumerable<string> featureNames)`: primeiro nome que começa com o prefixo (sem diferenciar maiúsculas) → `name.Substring(LauncherFeaturePrefix.Length).Trim()`; `null` se nenhum. Ignora nomes `null`: o `GameFeature.Name` do Playnite nunca é nulo, então isso não muda o comportamento real.
  - `public static GameAction? FindFileAction(IEnumerable<GameAction> actions)`: primeira ação com `Type == GameActionType.File` e `Path` não vazio.
  - `public static string BuildArguments(string executable, string launcherName, string? originalArguments)`: `$"--executable \"{executable}\" --launcher \"{launcherName}\""` + `$" -- {originalArguments}"` só quando `!string.IsNullOrWhiteSpace(originalArguments)`.
- `GetPlayActions` passa a seguir esta ordem:
  1. Debug "Evaluating…";
  2. `FindLauncherName(args.Game.Features?.Select(f => f.Name) ?? [])`; se for `null`, Debug "No [Launcher]…" e `yield break`;
  3. `FindFileAction(args.Game.GameActions ?? [])`; se for `null`, o mesmo Warn de hoje;
  4. `File.Exists(supervisor)`; se não existir, o mesmo Error de hoje e `yield break`;
  5. `ExpandGameVariables`;
  6. `BuildArguments(action.Path, launcherName, action.Arguments)`;
  7. o mesmo Info "Injecting…" e `yield return new AutomaticPlayController(...)` com as mesmas propriedades.

  **Removido:** o bloco de desembrulho (`LauncherSupervisor.exe` + `Regex`) e o `using System.Text.RegularExpressions`. O comentário do filtro File passa a dizer: *"Only File actions provide the game executable; Script actions are not launch sources."*

- [ ] **Step 1: Escrever os testes**

```csharp
[Fact] public void FindLauncherName_ReturnsNull_When_NoLauncherFeature()        // ["Action", "Visual Novel"] → null
[Fact] public void FindLauncherName_TrimsNameAfterPrefix()                        // ["Other", "[launcher]  Locale Emulator "] → "Locale Emulator"
[Fact] public void FindLauncherName_KeepsEmptyName_When_PrefixHasNoName()         // ["[Launcher]"] → ""  (Review Focus 2)
[Fact] public void FindFileAction_SkipsNonFileAndEmptyPathActions()
// [Script "C:\s.ps1", File Path "", File "C:\G\g.exe"] → a ação "C:\G\g.exe"
[Fact] public void BuildArguments_AppendsOriginalArgumentsAfterSeparator()
// (@"C:\G\g.exe", "NoRegionLoader", "-w 1") → "--executable \"C:\\G\\g.exe\" --launcher \"NoRegionLoader\" -- -w 1"
[Theory, InlineData(null), InlineData(""), InlineData("   ")]
public void BuildArguments_OmitsSeparator_When_NoOriginalArguments(string? original)  // Review Focus 4
// → "--executable \"C:\\G\\g.exe\" --launcher \"Locale Emulator\""
```

- [ ] **Step 2: Implementar `SupervisorPlayAction` e reescrever `GetPlayActions`**
- [ ] **Step 3: Build** — `dotnet build VNManager/Launcher.sln` → `0 Error(s)`
- [ ] **Step 4: Testes** — `dotnet run --project VNManager/PlaynitePlugin.Tests/PlaynitePlugin.Tests.csproj` → `Total: 8, Failed: 0`
- [ ] **Step 5: Confirmar que `Installation/` ficou intocado** — `git diff --stat HEAD -- VNManager/PlaynitePlugin/Installation` → vazio
- [ ] **Step 6: Commit** — `refactor: extract supervisor play action and add plugin tests`

---

### Task 8: Higiene da solução e da configuração

**Files:**
- Modify: `VNManager/Launcher.sln` (remover `Debug|x64`, `Debug|x86`, `Release|x64`, `Release|x86` de `SolutionConfigurationPlatforms` e todas as linhas `.Debug|x64`/`.Debug|x86`/`.Release|x64`/`.Release|x86` de `ProjectConfigurationPlatforms`)
- Modify: `.gitignore` (acrescentar `*.DotSettings.user`); `git rm --cached VNManager/Launcher.sln.DotSettings.user`
- Modify: `VNManager/LauncherSupervisor/Configuration/process-management.json` (remover a entrada `rugp.exe` de `executables`, deixando `"executables": []`; `rugp.exe` continua em `monitorProcesses`)

- [ ] **Step 1: Aplicar as mudanças**
- [ ] **Step 2: Build e testes** — `dotnet build VNManager/Launcher.sln` → `0 Error(s)`; `dotnet run --project VNManager/Tests/Tests.csproj` → `Total: 31, Failed: 0`
- [ ] **Step 3: Commit** — `chore: trim solution platforms and local settings`

---

### Task 9: Documentação e instruções de IA

**Files:**
- Create: `AGENTS.md` (raiz). Junta `docs/AGENT.md` e `codex.md`, corrigidos:
  - o fluxo passa a ser Plugin → `LauncherSupervisor` → `HostComposition`/`LauncherRegistry` → launcher → `ProcessManagement`;
  - lista os componentes atuais, incluindo `Supervision`, `PlayniteInstallation(.Tests)` e `PlaynitePlugin.Tests`;
  - lista os comandos dos 3 projetos de teste;
  - sai a menção a `FEATURES.md`/`LAUNCHERS.md` e o "Current focus";
  - a lista de docs passa a apontar só para os que existem.
- Modify: `codex.md` e Create: `CLAUDE.md`. Os dois ficam só com: `Leia e siga as instruções em [AGENTS.md](AGENTS.md).`
- Delete: `docs/AGENT.md`, `docs/ADAPTER_HOST_PROTOCOL.md`
- Modify: `docs/ARCHITECTURE.md`: reescrever no fluxo atual. Remover as seções do Adapter PowerShell, do `Host.exe`, do protocolo de arquivos e do evento de cancelamento, além de "Solution structure" `src/tests`, "Initial scope" e "Legacy architecture". Manter metas, fronteiras de dependência, modelo de registro, ciclo de vida, posse de processos e filosofia de configuração.
- Modify: `docs/Launcher Contract.md`, `docs/LAUNCHER_SCOPE.md`, `docs/LAUNCHER_RESOLUTION.md`, `docs/logging.md`, `docs/Principios de Design.md`: remover `DispatcherRequest`, o resultado via arquivo, as responsabilidades do Adapter e o logger PowerShell; citar `Launcher.Supervision` onde houver responsabilidade compartilhada.
- Modify: `docs/DECISIONS.md`: `Status: Superseded by ADR-005` em ADR-001 e ADR-003; nova ADR-005 *"PowerShell adapter removed"* (o plugin é o frontend; o `LauncherSupervisor` é o ponto de entrada; o Host é a biblioteca de composição) e ADR-006 *"Shared launcher supervision project"* (os launchers continuam separados; o comum fica em `Launcher.Supervision`).
- Modify: `README.md`:
  - arquitetura, quick start e configuração passam a usar `VNManager/LauncherSupervisor/Configuration/*.json`;
  - *Repository layout* com todos os projetos;
  - *Development* com os 3 comandos de teste;
  - links de docs sem o protocolo removido.
  - **Não alterar** a seção *"Shared installation protocol verification"*.

- [ ] **Step 1: Escrever e editar os documentos**
- [ ] **Step 2: Procurar referências órfãs**

Run: `git grep -n -i -E "PlayniteAdapter|ADAPTER_HOST_PROTOCOL|Host\.exe|DispatcherRequest|docs/AGENT\.md|cancel-event|RunWithLocaleEmulator" -- . ':!docs/superpowers'`
Expected: nenhuma linha

- [ ] **Step 3: Commit** — `docs: align documentation with supervisor-only architecture`

---

### Task 10: Verificação final

- [ ] **Step 1:** `dotnet build VNManager/Launcher.sln -c Release` → `0 Error(s)`
- [ ] **Step 2:** `dotnet run --project VNManager/Tests/Tests.csproj` → `Total: 31, Failed: 0`
- [ ] **Step 3:** `dotnet run --project VNManager/PlaynitePlugin.Tests/PlaynitePlugin.Tests.csproj` → `Total: 8, Failed: 0`
- [ ] **Step 4:** `dotnet run --project VNManager/PlayniteInstallation.Tests/PlayniteInstallation.Tests.csproj` → mesmo total da Task 0, `Failed: 0`
- [ ] **Step 5:** `git diff --stat <commit da Task 0>..HEAD -- VNManager/PlayniteInstallation VNManager/PlayniteInstallation.Tests VNManager/PlaynitePlugin/Installation NuGet.Config` → vazio
- [ ] **Step 6:** `git branch --show-current` → `review-e-correção`; `git status` limpo. Sem push.
- [ ] **Step 7: Validação manual (usuário).** Publicar o supervisor, compilar o plugin em Release, reinstalar o plugin (apagando o `Host.exe`/`Host.dll` antigos da pasta de deploy) e, no Playnite, abrir:
  - um jogo `[Launcher] Locale Emulator`;
  - um jogo `[Launcher] NoRegionLoader`;
  - um jogo sem Feature.

  Conferir que o tempo de jogo é contado.
