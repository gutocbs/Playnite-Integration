# Princípios de Design

## Desacoplamento

O Host não deve conter comportamento específico de um Launcher.

Adicionar um Launcher na arquitetura .NET atual exige:

1. Criar uma nova Class Library.
2. Referenciar `Launcher-Abstractions`.
3. Implementar `ILauncher`.
4. Registrar a implementação no `Launcher-Host`.

Descoberta dinâmica, reflection e infraestrutura de plugins não fazem parte do
escopo inicial.

## Independência do Playnite

Somente o Adapter PowerShell deve depender das APIs do Playnite.

O Host e os Launchers recebem contratos internos e não conhecem Features,
objetos ou APIs do frontend. Isso permite reutilizar o sistema com outros
frontends no futuro.

## Separação de runtimes

PowerShell integra o sistema com o ambiente. .NET implementa a lógica da
aplicação, incluindo validação, resolução, dispatch e ciclo de vida dos
Launchers.

Launchers PowerShell existentes são considerados legado durante a migração.

## Contratos estáveis

O Adapter envia um `DispatcherRequest` ao Host. Após validar e normalizar a
entrada, o Host envia um `LaunchRequest` ao `ILauncher` selecionado. O resultado
é representado por `LaunchResult`, com `Success`, `Data` e `Error`.

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

Da mesma forma, o Adapter deve utilizar a API do frontend para expandir suas
variáveis. Reimplementar no Adapter a lista ou a semântica de variáveis do
Playnite criaria uma segunda fonte de verdade e não é permitido.
