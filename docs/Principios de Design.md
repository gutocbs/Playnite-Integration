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
