# Logging

## Objetivo

Todos os componentes .NET usam o mesmo modelo lógico de logging, com formato e
propriedades consistentes. A implementação fica no projeto `Logging`
(`IAppLogger` / `AppLogger`), que não conhece nenhum componente consumidor:

```text
Launchers ──────────┐
Launcher.Supervision ┼──> Logging
ProcessManagement ──┘
```

O plugin do Playnite registra seus próprios eventos no log do Playnite
(`LogManager.GetLogger`), fora deste modelo.

---

## Interface

```csharp
logger.Write(
    LogLevel.Info,
    "LocaleEmulator",
    "Locale Emulator command started",
    new Dictionary<string, object?> { ["ProcessId"] = 18432 });
```

O caller informa somente o evento. Timestamp, formatação, arquivo de destino e
encoding são responsabilidade do logger.

## Propriedades

### Level

`Debug`, `Info`, `Warning` e `Error` (`LogLevel`). No arquivo aparece em maiúsculas.

### Component

Origem lógica do evento, por exemplo `LocaleEmulator`, `NoRegionLoader`,
`ProcessMonitor` e `ProcessCleanup`.

### Message

Descrição legível do evento, sem prefixos manuais como `[INFO]`.

### Data

Dicionário opcional de propriedades do evento (`Executable`, `ProcessId`,
`ProcessName`, `Error`…). O formato de `Data` não faz parte do contrato dos
launchers.

## ExecutionId

Cada tentativa de execução tem um `ExecutionId` único, criado pelo
`LauncherSupervisor` (GUID) e propagado no `LaunchRequest` e no logger criado
pelo `HostComposition` para aquela execução. Todos os eventos da mesma tentativa
compartilham o mesmo identificador.

---

## Formato

```text
[Timestamp] [LEVEL] [ExecutionId] [Component] Message | Chave=Valor; Chave=Valor
```

Exemplo:

```text
[2026-08-06 19:55:01.183 -03:00] [INFO] [3f6c…] [ProcessMonitor] Target process started | ProcessName=Eustia
```

- Timestamp local `yyyy-MM-dd HH:mm:ss.fff zzz`, cultura invariante.
- Quebras de linha em `ExecutionId`, `Component` e `Message` viram `\r` / `\n`.
- Em valores de `Data`, `\` vira `\\`, quebras de linha viram `\r` / `\n` e `;`
  vira `\;`. `null` é escrito como `null`.

O formato físico é interno ao logger; consumidores não devem depender dele.

## Arquivo de log

Por padrão, `launcher.log` ao lado do `LauncherSupervisor.exe` (configurável com
`--log`). Cada escrita abre o arquivo em append com compartilhamento de leitura e
escrita, grava a linha em UTF-8 sem BOM e fecha o handle; nenhum lock é mantido
durante o jogo.

## Falhas no logger

Uma falha de log nunca impede o launch. Se a escrita falhar, o logger tenta um
arquivo por processo (`launcher.<pid>.log`) no mesmo diretório; se isso também
falhar, o evento é descartado silenciosamente.

## Evoluções futuras

Rotação e retenção de arquivos, logs em JSON, filtros e nível mínimo
configurável podem ser adicionados sem mudar os callers.
