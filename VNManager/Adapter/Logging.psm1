function Write-AppLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Info', 'Warning', 'Error')]
        [string]$Level,

        [Parameter(Mandatory = $true)]
        [string]$ExecutionId,

        [Parameter(Mandatory = $true)]
        [string]$Component,

        [Parameter(Mandatory = $true)]
        [string]$Message,

        [System.Collections.IDictionary]$Data
    )

    try {
        $timestamp = [DateTimeOffset]::Now.ToString(
            'yyyy-MM-dd HH:mm:ss.fff zzz',
            [Globalization.CultureInfo]::InvariantCulture)
        $safeExecutionId = $ExecutionId.Replace("`r", '\r').Replace("`n", '\n')
        $safeComponent = $Component.Replace("`r", '\r').Replace("`n", '\n')
        $safeMessage = $Message.Replace("`r", '\r').Replace("`n", '\n')
        $line = "[$timestamp] [$($Level.ToUpperInvariant())] [$safeExecutionId] [$safeComponent] $safeMessage"

        if ($null -ne $Data -and $Data.Count -gt 0) {
            $serializedData = @(
                foreach ($entry in $Data.GetEnumerator()) {
                    $value = if ($null -eq $entry.Value) { 'null' } else { [string]$entry.Value }
                    $value = $value.Replace('\', '\\').Replace("`r", '\r').Replace("`n", '\n').Replace(';', '\;')
                    "$($entry.Key)=$value"
                }
            ) -join '; '

            $line = "$line | $serializedData"
        }

        Write-LogLine -Path $Path -Line $line
    }
    catch {
        try {
            $directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))
            $baseName = [IO.Path]::GetFileNameWithoutExtension($Path)
            $extension = [IO.Path]::GetExtension($Path)
            $fallbackPath = Join-Path $directory "$baseName.$PID$extension"
            Write-LogLine -Path $fallbackPath -Line $line
        }
        catch {
            # Logging must never block launching.
        }
    }
}

function Write-LogLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Line
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $directory = [IO.Path]::GetDirectoryName($fullPath)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $encoding = [Text.UTF8Encoding]::new($false)
    $bytes = $encoding.GetBytes($Line + [Environment]::NewLine)
    $stream = [IO.FileStream]::new(
        $fullPath,
        [IO.FileMode]::Append,
        [IO.FileAccess]::Write,
        [IO.FileShare]::ReadWrite,
        1,
        [IO.FileOptions]::WriteThrough)

    try {
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        $stream.Dispose()
    }
}

Export-ModuleMember -Function Write-AppLog
