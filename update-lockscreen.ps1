param(
    [Parameter(Mandatory=$true)][string]$ScreenSaverExe,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateRange(2,30)][int]$KeepImages = 5
)

$ErrorActionPreference = 'Stop'
$mutex = New-Object System.Threading.Mutex($false, 'Local\AgentCatLockScreenUpdater')
$ownsMutex = $false

function Await-WinRt {
    param(
        [Parameter(Mandatory=$true)]$Operation,
        [Parameter(Mandatory=$true)][Type]$ResultType
    )

    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and
            $_.GetParameters().Count -eq 1
        } |
        Select-Object -First 1
    if ($null -eq $method) { throw 'Windows Runtime AsTask bridge was not found.' }

    $task = $method.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    $task.GetAwaiter().GetResult()
}

function Await-WinRtAction {
    param([Parameter(Mandatory=$true)]$Operation)

    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq 'AsTask' -and -not $_.IsGenericMethodDefinition -and
            $_.GetParameters().Count -eq 1
        } |
        Select-Object -First 1
    if ($null -eq $method) { throw 'Windows Runtime action bridge was not found.' }

    $task = $method.Invoke($null, @($Operation))
    $task.GetAwaiter().GetResult() | Out-Null
}

try {
    $ownsMutex = $mutex.WaitOne(0)
    if (-not $ownsMutex) { exit 0 }
    if (-not (Test-Path -LiteralPath $ScreenSaverExe -PathType Leaf)) {
        throw "Snapshot generator not found: $ScreenSaverExe"
    }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $imagePath = Join-Path $OutputDirectory "agentcat-lock-$stamp.png"
    $process = Start-Process -FilePath $ScreenSaverExe `
        -ArgumentList @('/snapshot', ('"' + $imagePath + '"')) `
        -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $imagePath)) {
        throw "Lock-screen snapshot generation failed with exit code $($process.ExitCode)."
    }

    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $storageFileType = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
    $lockScreenType = [Windows.System.UserProfile.LockScreen,Windows.System.UserProfile,ContentType=WindowsRuntime]

    $file = Await-WinRt -Operation ($storageFileType::GetFileFromPathAsync($imagePath)) -ResultType $storageFileType
    Await-WinRtAction -Operation ($lockScreenType::SetImageFileAsync($file))

    Get-ChildItem -LiteralPath $OutputDirectory -Filter 'agentcat-lock-*.png' -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip $KeepImages |
        Remove-Item -Force -ErrorAction SilentlyContinue

    [ordered]@{
        updatedAt = (Get-Date).ToString('o')
        image = $imagePath
        applied = $true
        currentImageUri = [string]$lockScreenType::OriginalImageFile
        intervalMinutes = 1
    } | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'status.json') -Encoding UTF8
}
catch {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    [ordered]@{
        updatedAt = (Get-Date).ToString('o')
        applied = $false
        error = $_.Exception.Message
        intervalMinutes = 1
    } | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'status.json') -Encoding UTF8
    throw
}
finally {
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
