param(
    [Parameter(Mandatory = $true)]
    [string]$VisualNovelExePath,
	
	[string]$LocaleEmulator = 'D:\Applocale\LEProc.exe',
    [string]$ExeToMonitor,
    [string[]]$CleanupProcesses = @(),
	
    [switch]$VerboseMode
)

$CleanupProcesses = @(
    $CleanupProcesses |
        ForEach-Object { $_ -split "," } |
        ForEach-Object { $_.Trim().Trim('"') } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)

$checkIntervalSeconds = 1

function Log($message) {
    $timestamp = Get-Date -Format "dd/MM/yyyy HH:mm:ss"
    $line = "[$timestamp] $message"

    if ($VerboseMode) {
        Write-Host $line
		Add-Content -Path "monitor.log" -Value $line
    }
	
}

function Get-ProcessByExecutablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return @()
    }

    $cleanPath = $Path.Trim().Trim('"')

    try {
        $normalizedPath = [System.IO.Path]::GetFullPath($cleanPath)
    }
    catch {
        Log "Path inválido recebido: [$Path]"
        return @()
    }

    return @(
        Get-CimInstance Win32_Process |
            Where-Object {
                $_.ExecutablePath -and
                ([System.IO.Path]::GetFullPath($_.ExecutablePath) -ieq $normalizedPath)
            }
    )
}

function Get-ProcessByName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProcessName
    )

    if ([string]::IsNullOrWhiteSpace($ProcessName)) {
        return @()
    }

    $cleanName = $ProcessName.Trim().Trim('"')

    # Remove .exe se vier no argumento
    if ($cleanName.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        $cleanName = [System.IO.Path]::GetFileNameWithoutExtension($cleanName)
    }

    return @(
        Get-Process -Name $cleanName -ErrorAction SilentlyContinue
    )
}

try {
    Log "Starting launcher..."
    Log "Starting $LocaleEmulator"
	Log "$LocaleEmulator -ArgumentList `"-runas `"7dd64309-a1a6-4ef5-97d0-6c7e74631e08`" `"$VisualNovelExePath`"`""
    Start-Process -FilePath $LocaleEmulator -ArgumentList "-runas `"7dd64309-a1a6-4ef5-97d0-6c7e74631e08`" `"$VisualNovelExePath`""

	if ([string]::IsNullOrWhiteSpace($ExeToMonitor)) {
		$ExeToMonitor = $VisualNovelExePath
	}

    Log "Waiting for $ExeToMonitor to start..."

    do {
		Start-Sleep -Seconds 1
		Log "Aguardando $ExeToMonitor ..."

		$targetProcesses = Get-ProcessByName -ProcessName $ExeToMonitor
	} while (-not $targetProcesses)
		
	Log "Processos encontrados: $($targetProcesses.Count)"

	Get-CimInstance Win32_Process |
		Where-Object { $_.ExecutablePath -like "*$([System.IO.Path]::GetFileName($ExeToMonitor))*" } |
		ForEach-Object {
			Log "Target match: Name=[$($_.Name)] PID=[$($_.ProcessId)] Path=[$($_.ExecutablePath)]"
		}
		
    Log "Target program started. Monitoring..."
	
    while ($true) {
		Start-Sleep -Seconds $checkIntervalSeconds
		$targetProcesses = Get-ProcessByName -ProcessName $ExeToMonitor
        if ($targetProcesses) {
        }
        else {
            Log "Target program closed. Running cleanup..."
            break
        }
    }
}
catch {
    Log "ERRO DETECTADO!"

    Log "Mensagem: $($_.Exception.Message)"
    Log "Tipo: $($_.Exception.GetType().FullName)"

    if ($_.InvocationInfo) {
        Log "Linha: $($_.InvocationInfo.ScriptLineNumber)"
        Log "Comando: $($_.InvocationInfo.Line)"
    }

    Log "StackTrace:"
    Log $_.Exception.StackTrace
}
finally {
    Log "Iniciando cleanup..."

    if ($CleanupProcesses -and $CleanupProcesses.Count -gt 0) {
        foreach ($processName in $CleanupProcesses) {

            $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

            if ($processes) {
                Log "Encerrando processos: $processName"

                foreach ($proc in $processes) {
                    try {
						Get-Process | Where-Object {$_.Name -eq $proc.Name} | ForEach-Object {$_.Kill()}
                        Log "Encerrado: $($proc.Name) (PID $($proc.Id))"
                    }
                    catch {
                        Log "Erro ao encerrar $($proc.Name): $($_.Exception.Message)"
                    }
                }
            }
            else {
                Log "Nenhum processo encontrado: $processName"
            }
        }
    }
    else {
        Log "Nenhum processo de cleanup definido."
    }

    Log "Cleanup finalizado."
}