param(
    [Parameter(Mandatory = $true)][string]$AdapterPath,
    [Parameter(Mandatory = $true)][string]$AdapterConfigPath,
    [Parameter(Mandatory = $true)][string]$GameExecutable,
    [switch]$ForceLegacyProcessStartInfo
)

$game = [PSCustomObject]@{
    Name = 'VNManager integration test'
    InstallDirectory = [IO.Path]::GetDirectoryName($GameExecutable)
    Features = @(
        [PSCustomObject]@{ Name = '[Launcher] Locale Emulator' }
    )
    GameActions = @(
        [PSCustomObject]@{
            IsPlayAction = $true
            Path = $GameExecutable
            Arguments = '500'
        }
    )
}

$playniteApi = [PSCustomObject]@{
    Dialogs = [PSCustomObject]@{}
    ExpandCalls = 0
}
$playniteApi | Add-Member -MemberType ScriptMethod -Name ExpandGameVariables -Value {
    param($gameToExpand, $actionToExpand)
    $this.ExpandCalls++
    return $actionToExpand
}
$playniteApi.Dialogs | Add-Member -MemberType ScriptMethod -Name ShowErrorMessage -Value {
    param($message, $title)
}

$result = & $AdapterPath -Game $game -PlayniteApi $playniteApi -ConfigPath $AdapterConfigPath -ForceLegacyProcessStartInfo:$ForceLegacyProcessStartInfo
$result | ConvertTo-Json -Depth 10 -Compress

if (-not $result.Success) {
    exit 1
}

if ($playniteApi.ExpandCalls -ne 1) {
    exit 2
}
