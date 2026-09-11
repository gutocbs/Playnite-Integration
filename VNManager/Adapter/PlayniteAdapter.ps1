[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [object]$Game,

    [object]$PlayniteApi,

    [string]$ConfigPath = (Join-Path $PSScriptRoot 'PlayniteAdapter.json'),

    # Playnite's embedded PowerShell may not expose ProcessStartInfo.ArgumentList.
    # This switch exists only to cover that compatible path in integration tests.
    [switch]$ForceLegacyProcessStartInfo
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'Logging.psm1') -Force

function Resolve-ConfiguredPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$ConfigurationDirectory
    )

    if ([IO.Path]::IsPathRooted($Value)) {
        return [IO.Path]::GetFullPath($Value)
    }

    return [IO.Path]::GetFullPath((Join-Path $ConfigurationDirectory $Value))
}

function Show-AdapterError {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if ($null -ne $PlayniteApi) {
        $PlayniteApi.Dialogs.ShowErrorMessage($Message, 'VNManager')
    }
    else {
        Write-Warning $Message
    }
}

function Get-LauncherName {
    param([Parameter(Mandatory = $true)][object]$Game)

    $matches = @(
        foreach ($feature in @($Game.Features)) {
            $featureName = [string]$feature.Name
            $match = [regex]::Match(
                $featureName,
                '^\[Launcher\]\s+(.+)$',
                [Text.RegularExpressions.RegexOptions]::IgnoreCase)

            if ($match.Success -and -not [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
                $match.Groups[1].Value.Trim()
            }
        }
    )

    if ($matches.Count -eq 0) {
        throw 'Nenhuma Feature no formato [Launcher] Nome foi encontrada.'
    }

    if ($matches.Count -gt 1) {
        throw "Mais de uma Feature de launcher foi encontrada: $($matches -join ', ')"
    }

    return $matches[0]
}

function Get-PlayAction {
    param([Parameter(Mandatory = $true)][object]$Game)

    $action = @($Game.GameActions) |
        Where-Object { $_.IsPlayAction } |
        Select-Object -First 1

    if ($null -eq $action) {
        throw 'Nenhuma ação principal foi encontrada para o jogo.'
    }

    return $action
}

function Resolve-GameExecutable {
    param(
        [Parameter(Mandatory = $true)][object]$Action
    )

    if ([string]::IsNullOrWhiteSpace([string]$Action.Path)) {
        throw 'A ação principal não possui um executável configurado.'
    }

    $actionPath = ([string]$Action.Path).Trim().Trim('"')
    if (-not [IO.Path]::IsPathRooted($actionPath)) {
        throw "O Playnite não expandiu o caminho da ação principal: $actionPath"
    }

    return [IO.Path]::GetFullPath($actionPath)
}

function ConvertFrom-WindowsCommandLine {
    param([AllowEmptyString()][string]$CommandLine)

    if ($null -eq ('VNManager.CommandLine' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace VNManager
{
    public static class CommandLine
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string commandLine,
            out int argumentCount);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        public static string[] Split(string commandLine)
        {
            int count;
            IntPtr pointer = CommandLineToArgvW("vnmanager.exe " + commandLine, out count);
            if (pointer == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                var result = new string[Math.Max(0, count - 1)];
                for (int index = 1; index < count; index++)
                {
                    IntPtr argument = Marshal.ReadIntPtr(pointer, index * IntPtr.Size);
                    result[index - 1] = Marshal.PtrToStringUni(argument);
                }

                return result;
            }
            finally
            {
                LocalFree(pointer);
            }
        }

        public static string Join(string[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }

                AppendArgument(builder, arguments[index] ?? string.Empty);
            }

            return builder.ToString();
        }

        private static void AppendArgument(System.Text.StringBuilder builder, string argument)
        {
            if (argument.Length == 0)
            {
                builder.Append("\"\"");
                return;
            }

            bool requiresQuotes = false;
            foreach (char character in argument)
            {
                if (char.IsWhiteSpace(character) || character == '\"')
                {
                    requiresQuotes = true;
                    break;
                }
            }

            if (!requiresQuotes)
            {
                builder.Append(argument);
                return;
            }

            builder.Append('\"');
            int backslashCount = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '\"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append(character);
                    backslashCount = 0;
                    continue;
                }

                builder.Append('\\', backslashCount);
                builder.Append(character);
                backslashCount = 0;
            }

            builder.Append('\\', backslashCount * 2);
            builder.Append('\"');
        }
    }
}
'@
    }

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return @()
    }

    return @([VNManager.CommandLine]::Split($CommandLine))
}

function Set-ProcessStartInfoArguments {
    param(
        [Parameter(Mandatory = $true)]
        [Diagnostics.ProcessStartInfo]$StartInfo,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $argumentListProperty = $StartInfo.PSObject.Properties['ArgumentList']
    if (-not $ForceLegacyProcessStartInfo -and $null -ne $argumentListProperty) {
        foreach ($argument in $Arguments) {
            [void]$StartInfo.ArgumentList.Add($argument)
        }

        return
    }

    # Windows PowerShell/.NET Framework does not provide ArgumentList. Build the
    # same Windows command line explicitly so paths and values with spaces remain
    # individual arguments when the Host process starts.
    if ($null -eq ('VNManager.CommandLine' -as [type])) {
        [void](ConvertFrom-WindowsCommandLine -CommandLine '')
    }

    $StartInfo.Arguments = [VNManager.CommandLine]::Join($Arguments)
}

function Get-GameArguments {
    param([Parameter(Mandatory = $true)][object]$Action)

    if ($null -eq $Action.Arguments) {
        return @()
    }

    if ($Action.Arguments -is [string]) {
        return @(ConvertFrom-WindowsCommandLine -CommandLine ([string]$Action.Arguments))
    }

    return @($Action.Arguments | ForEach-Object { [string]$_ })
}

function Write-JsonAtomic {
    param(
        [Parameter(Mandatory = $true)][object]$Value,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $temporaryPath = "$Path.$([Guid]::NewGuid().ToString('N')).part"
    $json = $Value | ConvertTo-Json -Depth 10

    try {
        [IO.File]::WriteAllText($temporaryPath, $json, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryPath, $Path)
    }
    finally {
        if ([IO.File]::Exists($temporaryPath)) {
            [IO.File]::Delete($temporaryPath)
        }
    }
}

$executionId = [Guid]::NewGuid().ToString('D')
$requestPath = $null
$resultPath = $null
$startedPath = $null
$executionDirectory = $null
$logPath = $null

try {
    $fullConfigPath = [IO.Path]::GetFullPath($ConfigPath)
    $configurationDirectory = [IO.Path]::GetDirectoryName($fullConfigPath)
    $configuration = Get-Content -LiteralPath $fullConfigPath -Raw | ConvertFrom-Json
    $hostExecutable = Resolve-ConfiguredPath $configuration.hostExecutable $configurationDirectory
    $hostConfigurationDirectory = Resolve-ConfiguredPath $configuration.hostConfigurationDirectory $configurationDirectory
    $logPath = Resolve-ConfiguredPath $configuration.logPath $configurationDirectory

    if (-not [IO.File]::Exists($hostExecutable)) {
        throw "Launcher Host não encontrado: $hostExecutable"
    }

    $launcherName = Get-LauncherName -Game $Game
    $playAction = Get-PlayAction -Game $Game
    $action = if ($null -ne $PlayniteApi) {
        $PlayniteApi.ExpandGameVariables($Game, $playAction)
    }
    else {
        $playAction
    }

    if ($null -eq $action) {
        throw 'O Playnite não retornou a ação expandida.'
    }

    $gameExecutable = Resolve-GameExecutable -Action $action
    $gameArguments = @(Get-GameArguments -Action $action)

    if (-not [IO.File]::Exists($gameExecutable)) {
        throw "Executável do jogo não encontrado: $gameExecutable"
    }

    $transportDirectory = Join-Path ([IO.Path]::GetTempPath()) 'VNManager'
    [IO.Directory]::CreateDirectory($transportDirectory) | Out-Null
    $executionDirectory = Join-Path $transportDirectory $executionId
    [IO.Directory]::CreateDirectory($executionDirectory) | Out-Null
    $requestPath = Join-Path $executionDirectory 'request.json'
    $resultPath = Join-Path $executionDirectory 'result.json'
    $startedPath = Join-Path $executionDirectory 'started.json'

    $request = [ordered]@{
        executionId = $executionId
        executable  = $gameExecutable
        arguments   = $gameArguments
        launcher    = $launcherName
    }

    Write-JsonAtomic -Value $request -Path $requestPath
    Write-AppLog -Path $logPath -Level Info -ExecutionId $executionId `
        -Component 'PlayniteAdapter' -Message 'Starting Launcher Host' `
        -Data @{ Launcher = $launcherName; Executable = $gameExecutable }

    $hostArguments = @(
        '--request', $requestPath,
        '--result', $resultPath,
        '--started', $startedPath,
        '--log', $logPath,
        '--configuration-directory', $hostConfigurationDirectory,
        '--transport-root', $transportDirectory
    )

    $hostStartInfo = [Diagnostics.ProcessStartInfo]::new()
    $hostStartInfo.FileName = $hostExecutable
    $hostStartInfo.UseShellExecute = $false
    Set-ProcessStartInfoArguments -StartInfo $hostStartInfo -Arguments @($hostArguments | ForEach-Object { [string]$_ })

    $hostProcess = [Diagnostics.Process]::Start($hostStartInfo)
    if ($null -eq $hostProcess) {
        throw 'Não foi possível iniciar o Launcher Host.'
    }
    $startupTimeout = [TimeSpan]::FromSeconds(30)
    $startedAt = [Diagnostics.Stopwatch]::StartNew()

    while (-not [IO.File]::Exists($startedPath)) {
        if ([IO.File]::Exists($resultPath)) {
            $hostResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            $errorMessage = if ($null -ne $hostResult.error) {
                "[$($hostResult.error.code)] $($hostResult.error.message)"
            }
            else {
                'Launcher Host terminou antes de detectar o processo do jogo.'
            }

            throw $errorMessage
        }

        if ($hostProcess.HasExited) {
            throw "Launcher Host terminou com o código $($hostProcess.ExitCode) sem detectar o processo do jogo."
        }

        if ($startedAt.Elapsed -ge $startupTimeout) {
            throw "O processo do jogo não foi detectado em $($startupTimeout.TotalSeconds) segundos."
        }

        Start-Sleep -Milliseconds 50
        $hostProcess.Refresh()
    }

    $started = Get-Content -LiteralPath $startedPath -Raw | ConvertFrom-Json
    Write-AppLog -Path $logPath -Level Info -ExecutionId $executionId `
        -Component 'PlayniteAdapter' -Message 'Game process detected; returning control to Playnite' `
        -Data @{ HostProcessId = $hostProcess.Id; ProcessName = $started.processName }

    # Playnite counts a script-based session only while this script is running.
    # Keep the adapter alive until the supervisor has observed the game exit.
    $hostProcess.WaitForExit()
    $hostExitCode = $hostProcess.ExitCode

    if (-not [IO.File]::Exists($resultPath)) {
        throw "Launcher Host terminou com o código $hostExitCode sem produzir resultado."
    }

    $hostResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    Write-AppLog -Path $logPath -Level $(if ($hostResult.success) { 'Info' } else { 'Error' }) `
        -ExecutionId $executionId -Component 'PlayniteAdapter' `
        -Message 'Launcher Host finished' -Data @{ ExitCode = $hostExitCode }

    if (-not $hostResult.success) {
        $errorMessage = if ($null -ne $hostResult.error) {
            "[$($hostResult.error.code)] $($hostResult.error.message)"
        }
        else {
            "Launcher Host falhou com o código $hostExitCode."
        }

        Show-AdapterError -Message $errorMessage
    }

    return [PSCustomObject]@{
        ExecutionId = $executionId
        Matched     = [bool]$started
        Started     = [bool]$started
        IsRunning   = $false
        ExitCode    = $hostExitCode
        Success     = [bool]$hostResult.success
        Data        = $hostResult.data
        Error       = $hostResult.error
    }
}
catch {
    if ($null -ne $logPath) {
        Write-AppLog -Path $logPath -Level Error -ExecutionId $executionId `
            -Component 'PlayniteAdapter' -Message 'Adapter failed' `
            -Data @{ Error = $_.Exception.Message }
    }

    Show-AdapterError -Message $_.Exception.Message

    return [PSCustomObject]@{
        ExecutionId = $executionId
        Matched     = $false
        Started     = $false
        IsRunning   = $false
        Success     = $false
        Data        = $null
        Error       = [PSCustomObject]@{
            Code    = 'ADAPTER_ERROR'
            Message = $_.Exception.Message
        }
    }
}
finally {
    foreach ($path in @($requestPath, $resultPath)) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and [IO.File]::Exists($path)) {
            try { [IO.File]::Delete($path) } catch { }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($executionDirectory) -and
        [IO.Directory]::Exists($executionDirectory)) {
        try { [IO.Directory]::Delete($executionDirectory, $false) } catch { }
    }
}
