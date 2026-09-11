[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ============================================================================
# Variaveis de teste - altere somente esta secao
# ============================================================================

$gameName = 'Angel Beats! -1st beat-'
$gameExecutable = 'M:\VN\Majikoi A\Majikoi A1.exe'
$gameArguments = ''
$gameInstallDirectory = 'M:\VN\Majikoi A'
$launcherFeature = '[Launcher] Locale Emulator'

$adapterPath = Join-Path $PSScriptRoot 'PlayniteAdapter.ps1'
$adapterConfigPath = Join-Path $PSScriptRoot 'PlayniteAdapter.json'

# ============================================================================
# Validacao da configuracao de teste
# ============================================================================

$adapterPath = [IO.Path]::GetFullPath($adapterPath)
$adapterConfigPath = [IO.Path]::GetFullPath($adapterConfigPath)

if (-not [IO.File]::Exists($adapterPath)) {
    throw "PlayniteAdapter nao encontrado: $adapterPath"
}

if (-not [IO.File]::Exists($adapterConfigPath)) {
    throw "Configuracao do PlayniteAdapter nao encontrada: $adapterConfigPath"
}

if (-not [IO.Path]::IsPathRooted($gameExecutable)) {
    throw "gameExecutable deve ser um caminho absoluto: $gameExecutable"
}

if (-not [IO.File]::Exists($gameExecutable)) {
    throw "Executavel do jogo nao encontrado: $gameExecutable"
}

# ============================================================================
# Objetos minimos equivalentes aos fornecidos pelo Playnite
# ============================================================================

$game = [PSCustomObject]@{
    Name = $gameName
    InstallDirectory = $gameInstallDirectory
    Features = @(
        [PSCustomObject]@{
            Name = $launcherFeature
        }
    )
    GameActions = @(
        [PSCustomObject]@{
            IsPlayAction = $true
            Path = $gameExecutable
            Arguments = $gameArguments
        }
    )
}

$dialogs = [PSCustomObject]@{}
$dialogs | Add-Member -MemberType ScriptMethod -Name ShowErrorMessage -Value {
    param($message, $title)

    Write-Warning "[$title] $message"
}

$playniteApi = [PSCustomObject]@{
    Dialogs = $dialogs
}

$playniteApi | Add-Member -MemberType ScriptMethod -Name ExpandGameVariables -Value {
    param($gameToExpand, $actionToExpand)

    # Neste teste os valores da acao ja devem estar expandidos. No Playnite
    # real, esta chamada e implementada pela propria API do frontend.
    return [PSCustomObject]@{
        IsPlayAction = $actionToExpand.IsPlayAction
        Path = $actionToExpand.Path
        Arguments = $actionToExpand.Arguments
    }
}

# ============================================================================
# Execucao: Adapter -> Host -> Launcher -> monitoramento/cleanup
# ============================================================================

Write-Host "Iniciando teste do VNManager para: $gameName"
Write-Host "Executavel: $gameExecutable"
Write-Host "Launcher: $launcherFeature"

$result = & $adapterPath `
    -Game $game `
    -PlayniteApi $playniteApi `
    -ConfigPath $adapterConfigPath

Write-Host ''
Write-Host 'Resultado do VNManager:'
$result | ConvertTo-Json -Depth 10

if (-not $result.Success) {
    exit 1
}

exit 0
