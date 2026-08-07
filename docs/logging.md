# Logging

## Objetivo

O sistema deve possuir um mecanismo de logging compartilhado entre todos os componentes.

Adapters, Dispatchers, Launchers e outros módulos devem utilizar o mesmo modelo
lógico de logging, garantindo formato e propriedades consistentes.

Como o sistema atravessa dois runtimes, existirão duas implementações:

- uma implementação PowerShell para o Playnite Adapter;
- uma implementação .NET para o Host e os Launchers.

As duas implementações devem respeitar o mesmo contrato de evento, mas não
compartilham código de runtime.

O módulo de logging deve ser independente dos demais componentes da aplicação.

A dependência deve seguir esta direção:

```text
Adapter ───────┐
Dispatcher ────┼──> Logging
Launcher ──────┘
```

Cada implementação de `Logging` não deve conhecer ou depender de Adapters,
Dispatchers ou Launchers concretos.

---

## Interface

No PowerShell, o logger será inicialmente exposto através de uma função
compartilhada:

```powershell
Write-AppLog
```

A chamada básica deve seguir este formato:

```powershell
Write-AppLog `
    -Level Info `
    -Component "Dispatcher" `
    -Message "Launcher selected"
```

O caller deve fornecer somente as informações relacionadas ao evento ocorrido.

Informações como timestamp, formatação, arquivo de destino e encoding são responsabilidade exclusiva do módulo de logging.

---

## Propriedades

### Level

Representa a severidade do evento.

Valores inicialmente suportados:

```text
Debug
Info
Warning
Error
```

Exemplo:

```powershell
-Level Info
```

O logger é responsável por converter ou formatar o valor conforme necessário no arquivo final.

---

### Component

Identifica qual componente gerou o evento.

Exemplos:

```text
PlayniteAdapter
Dispatcher
LocaleEmulator
NoRegionLoader
```

Exemplo de chamada:

```powershell
-Component "LocaleEmulator"
```

O componente não precisa necessariamente representar um script físico. Ele representa a origem lógica do evento.

---

### Message

Descrição legível do evento ocorrido.

Exemplo:

```powershell
-Message "Process started successfully"
```

A mensagem deve descrever o evento sem precisar incluir manualmente informações como level, timestamp ou component.

Evitar:

```powershell
Write-AppLog "[INFO] [LocaleEmulator] Process started"
```

Preferir:

```powershell
Write-AppLog `
    -Level Info `
    -Component "LocaleEmulator" `
    -Message "Process started"
```

---

## Data

O logger deve permitir opcionalmente associar dados estruturados ao evento através da propriedade `Data`.

Exemplo:

```powershell
Write-AppLog `
    -Level Info `
    -Component "Dispatcher" `
    -Message "Launcher selected" `
    -Data @{
        Launcher   = "LocaleEmulator"
        Executable = "M:\Games\Eustia\Eustia.exe"
    }
```

`Data` deve aceitar um conjunto arbitrário de propriedades relacionadas ao evento.

Isso permite manter a mensagem simples para leitura humana enquanto informações adicionais permanecem disponíveis para debugging ou processamento futuro.

Exemplos de informações adequadas para `Data`:

```text
Launcher
Executable
Arguments
ProcessId
ExitCode
ErrorCode
Duration
```

O formato interno de `Data` não deve fazer parte do contrato dos Launchers.

O logger é responsável por decidir como esses dados serão persistidos.

---

## ExecutionId

Cada solicitação de execução deve possuir um identificador único chamado `ExecutionId`.

O objetivo é permitir que todos os eventos relacionados à mesma tentativa de iniciar um jogo sejam facilmente correlacionados.

Exemplo:

```text
ExecutionId: A81F32
```

Uma execução poderia produzir:

```text
[19:55:01] [INFO]  [A81F32] [PlayniteAdapter] Received launch request
[19:55:01] [INFO]  [A81F32] [Dispatcher] Selected LocaleEmulator
[19:55:01] [DEBUG] [A81F32] [LocaleEmulator] Building command
[19:55:02] [INFO]  [A81F32] [LocaleEmulator] Process started
[19:55:02] [INFO]  [A81F32] [Dispatcher] Launcher finished successfully
```

Uma nova tentativa de execução deve gerar outro `ExecutionId`.

```text
A81F32
B71C09
C429AF
```

### Responsabilidade pela criação

O `ExecutionId` deve ser criado pelo Playnite Adapter no início da execução,
antes que a solicitação seja enviada ao Host.

Ele deve ser propagado durante toda a execução.

O identificador é um campo obrigatório do `DispatcherRequest`. O Host deve
propagá-lo para o `LaunchRequest` e para o contexto de logging, evitando que cada
componente gere seu próprio identificador.

O formato exato do `ExecutionId` ainda pode ser definido posteriormente.

Possibilidades incluem:

```text
GUID completo
GUID reduzido
identificador hexadecimal curto
```

O requisito importante é que ele seja suficientemente único para distinguir execuções diferentes dentro dos logs.

---

## Formato inicial do log

Na primeira versão, os logs podem utilizar um formato textual simples.

Exemplo:

```text
[2026-08-06 19:55:01.183] [INFO] [A81F32] [Dispatcher] Launcher selected
```

Formato conceitual:

```text
[Timestamp] [Level] [ExecutionId] [Component] Message
```

Quando houver `Data`, uma possível representação inicial seria:

```text
[2026-08-06 19:55:01.183] [INFO] [A81F32] [Dispatcher] Launcher selected | Launcher=LocaleEmulator; Executable=M:\Games\Eustia\Eustia.exe
```

A representação de `Data` ainda não faz parte do contrato público e poderá ser alterada.

---

## Logging estruturado

A API do logger deve ser desenhada de forma que seja possível migrar futuramente para logs estruturados sem modificar os callers.

Uma chamada como:

```powershell
Write-AppLog `
    -Level Info `
    -Component "Dispatcher" `
    -Message "Launcher selected" `
    -Data @{
        Launcher   = "LocaleEmulator"
        Executable = "M:\Games\Eustia\Eustia.exe"
    }
```

poderia futuramente gerar:

```json
{
    "timestamp": "2026-08-06T19:55:01.183-03:00",
    "level": "Info",
    "executionId": "A81F32",
    "component": "Dispatcher",
    "message": "Launcher selected",
    "data": {
        "launcher": "LocaleEmulator",
        "executable": "M:\\Games\\Eustia\\Eustia.exe"
    }
}
```

O formato do arquivo é uma implementação interna do logger.

Os componentes consumidores não devem depender do formato físico utilizado para persistência.

---

## Arquivo de log

Inicialmente, as implementações PowerShell e .NET devem produzir uma única
timeline de execução e podem escrever no mesmo arquivo de log.

Exemplo:

```text
logs/
└── launcher.log
```

Isso permite visualizar toda a execução como uma única timeline.

Como Adapter e Host são processos diferentes, acesso concorrente ao arquivo
deve ser tratado explicitamente. As implementações devem preferir append atômico
com handles mantidos pelo menor tempo possível e nunca manter um lock durante a
execução do jogo.

Se o arquivo compartilhado não for confiável, o fallback preferencial é usar
arquivos separados por processo, correlacionados pelo mesmo `ExecutionId`, em vez
de introduzir prematuramente um serviço de logging entre runtimes.

A identificação da origem de cada evento será feita através de `Component` e `ExecutionId`.

Separação por componente poderá ser adicionada futuramente caso exista necessidade, mas não deve ser necessária para a primeira versão.

---

## Responsabilidades do Logging

O módulo de logging será responsável por:

```text
Gerar timestamps.
Formatar os níveis de log.
Formatar a saída.
Persistir os eventos.
Criar o diretório de logs quando necessário.
Definir o encoding utilizado.
Serializar Data.
Associar ou utilizar o ExecutionId recebido.
```

Os callers não devem conhecer detalhes de persistência.

---

## Responsabilidades dos Callers

Adapters, Dispatchers e Launchers devem somente informar:

```text
Level
Component
Message
Data, quando necessário
ExecutionId ou contexto da execução
```

O caller não deve:

```text
Montar manualmente timestamps.
Formatar o Level.
Decidir o formato físico do arquivo.
Serializar Data.
Adicionar prefixos como [INFO] ou [ERROR] na Message.
```

---

## Falhas no próprio logger

Uma falha ao escrever um log não deve impedir o jogo de ser iniciado.

Logging é uma infraestrutura auxiliar e não deve se tornar um ponto único de falha do fluxo de launch.

Caso ocorra uma falha interna no logger, ele deve evitar propagar exceções que interrompam a execução principal.

O comportamento de fallback ainda será definido.

Possibilidades futuras incluem:

```text
Escrever no console.
Utilizar Write-Warning.
Utilizar Write-Error.
Ignorar silenciosamente após uma tentativa de fallback.
```

---

## Evoluções futuras

O contrato deve permitir adicionar posteriormente recursos como:

```text
Rotação automática de arquivos.
Limite máximo de tamanho.
Retenção por quantidade de dias.
Logs separados por execução.
Logs em JSON.
Filtros por Level.
Configuração de nível mínimo.
Output simultâneo para arquivo e console.
Medição de duração de operações.
Stack trace de exceptions.
Logs específicos de diagnóstico.
```

Esses recursos não fazem parte da primeira implementação.

---

## Exemplo de execução

Uma inicialização completa poderia produzir:

```text
[2026-08-06 19:55:01.101] [INFO]  [A81F32] [PlayniteAdapter] Launch requested
[2026-08-06 19:55:01.117] [DEBUG] [A81F32] [PlayniteAdapter] LaunchRequest created
[2026-08-06 19:55:01.142] [INFO]  [A81F32] [Dispatcher] Resolving launcher
[2026-08-06 19:55:01.183] [INFO]  [A81F32] [Dispatcher] Launcher selected | Launcher=LocaleEmulator
[2026-08-06 19:55:01.204] [DEBUG] [A81F32] [LocaleEmulator] Preparing process
[2026-08-06 19:55:01.810] [INFO]  [A81F32] [LocaleEmulator] Process started | ProcessId=18432
[2026-08-06 19:55:01.826] [INFO]  [A81F32] [Dispatcher] Launcher completed successfully
```

Esse formato permite reconstruir a sequência completa de eventos de uma execução e localizar rapidamente em qual componente uma falha ocorreu.

---

## Decisões atuais

Para a primeira versão:

* Existirá uma implementação PowerShell e uma implementação .NET de logging.
* As duas utilizarão o mesmo modelo lógico de evento e propriedades equivalentes.
* Os níveis iniciais serão `Debug`, `Info`, `Warning` e `Error`.
* Cada evento terá um `Component`.
* Eventos poderão possuir um objeto opcional `Data`.
* Cada execução possuirá um `ExecutionId`.
* Todos os componentes da mesma execução utilizarão o mesmo `ExecutionId`.
* O Adapter criará o `ExecutionId`, que será enviado no `DispatcherRequest` e propagado no `LaunchRequest`.
* Inicialmente será utilizado um único arquivo de log.
* O formato inicial será textual.
* O design deve permitir migração futura para logging estruturado.
* Falhas no logger não devem interromper o processo de launch.
