# Princípios de Design

## Desacoplamento

O Host não deve conter comportamento específico de um Launcher.

Adicionar um Launcher exige:

1. Criar uma nova Class Library.
2. Referenciar `Abstractions` e, para o ciclo comum de ferramenta externa,
   `Launcher.Supervision`.
3. Implementar `ILauncher`.
4. Registrar a implementação no `HostComposition`.

Cada Launcher continua sendo um projeto separado; o que é comum entre eles fica em
`Launcher.Supervision`, sem fundir as implementações.

Descoberta dinâmica, reflection e infraestrutura de plugins não fazem parte do
escopo.

## Independência do Playnite

Somente o projeto `PlaynitePlugin` deve depender das APIs do Playnite.

O supervisor, o Host e os Launchers recebem contratos internos e não conhecem Features,
objetos ou APIs do frontend. Isso permite reutilizar o sistema com outros
frontends no futuro.

## Separação de processos

O plugin roda dentro do Playnite (.NET Framework 4.6.2). A lógica de launch roda
em um processo separado, o `LauncherSupervisor` (.NET 10), que o Playnite
acompanha para medir o tempo de jogo.

## Contratos estáveis

O plugin chama o `LauncherSupervisor` com `--executable`, `--launcher` e os
argumentos do jogo. O supervisor envia um `LaunchRequest` ao `ILauncher`
selecionado. O resultado é representado por `LaunchResult`, com `Success`,
`Data` e `Error`.

Mudanças futuras devem preferir a adição compatível de campos aos contratos.

## Configuração externa

Valores operacionais e dependentes do ambiente devem, por padrão, ser mantidos
fora do código e carregados em objetos de opções tipados. Isso inclui caminhos
de executáveis, identificadores de perfis, timeouts, intervalos e nomes de
processos.

Literais continuam adequados para elementos estáveis do próprio contrato, como
nomes lógicos registrados, códigos de erro, valores de protocolo e mensagens.
Configuração operacional embutida no código deve ser uma exceção justificada,
não o mecanismo padrão.

Configurações compartilhadas devem pertencer ao componente que implementa a
responsabilidade. Em particular, listas de processos monitorados e processos de
cleanup pertencem ao módulo de gerenciamento de processos, não às opções de um
Launcher concreto.

Da mesma forma, o plugin deve utilizar a API do frontend
(`ExpandGameVariables`) para expandir suas variáveis. Reimplementar a lista ou a
semântica de variáveis do Playnite criaria uma segunda fonte de verdade e não é
permitido.
