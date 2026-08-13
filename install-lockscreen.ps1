param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'AgentCatScreenSaver'),
    [string]$TaskName = 'AgentCatLockScreenUpdater'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $InstallDirectory 'AgentCatScreenSaver.exe'
$updater = Join-Path $InstallDirectory 'update-lockscreen.ps1'
$setter = Join-Path $InstallDirectory 'set-lockscreen-image.ps1'
$output = Join-Path $InstallDirectory 'LockScreen'

New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item (Join-Path $root 'bin\AgentCatScreenSaver.exe') $exe -Force
Copy-Item (Join-Path $root 'update-lockscreen.ps1') $updater -Force
Copy-Item (Join-Path $root 'set-lockscreen-image.ps1') $setter -Force

$backup = Join-Path $InstallDirectory 'previous-lockscreen.json'
if (-not (Test-Path -LiteralPath $backup)) {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $lockType = [Windows.System.UserProfile.LockScreen,Windows.System.UserProfile,ContentType=WindowsRuntime]
    $original = $lockType::OriginalImageFile
    [ordered]@{
        originalImageUri = if ($null -ne $original) { $original.AbsoluteUri } else { $null }
        originalImagePath = if ($null -ne $original -and $original.IsFile) { $original.LocalPath } else { $null }
        backedUpAt = (Get-Date).ToString('o')
    } | ConvertTo-Json | Set-Content $backup -Encoding UTF8
}

$powerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}" -ScreenSaverExe "{1}" -OutputDirectory "{2}"' -f $updater,$exe,$output
$action = New-ScheduledTaskAction -Execute $powerShell -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes 1)
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 1) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) `
    -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Description 'Refresh Agent Cat + Herdr lock-screen snapshot every minute.' `
    -Force | Out-Null

$initial = Start-Process -FilePath $powerShell -ArgumentList $arguments `
    -WindowStyle Hidden -Wait -PassThru
if ($initial.ExitCode -ne 0) { throw "Initial lock-screen update failed: $($initial.ExitCode)" }

Write-Host "Installed lock-screen updater '$TaskName' at the supported 1-minute minimum interval."
