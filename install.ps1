param([ValidateRange(60,86400)][int]$TimeoutSeconds=300)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1')
$dir=Join-Path $env:LOCALAPPDATA 'AgentCatScreenSaver'
New-Item -ItemType Directory -Path $dir -Force | Out-Null
Copy-Item (Join-Path $root 'bin\AgentCatScreenSaver.scr') (Join-Path $dir 'AgentCatScreenSaver.scr') -Force
$key='HKCU:\Control Panel\Desktop'
$backup=Join-Path $dir 'previous-settings.json'
if (!(Test-Path $backup)) {
    $p=Get-ItemProperty $key -ErrorAction SilentlyContinue
    [ordered]@{SCRNSAVE_EXE=$p.'SCRNSAVE.EXE';ScreenSaveActive=$p.ScreenSaveActive;ScreenSaveTimeOut=$p.ScreenSaveTimeOut;ScreenSaverIsSecure=$p.ScreenSaverIsSecure}|ConvertTo-Json|Set-Content $backup -Encoding UTF8
}
Set-ItemProperty $key 'SCRNSAVE.EXE' (Join-Path $dir 'AgentCatScreenSaver.scr')
Set-ItemProperty $key 'ScreenSaveActive' '1'
Set-ItemProperty $key 'ScreenSaveTimeOut' ([string]$TimeoutSeconds)
Set-ItemProperty $key 'ScreenSaverIsSecure' '1'
& (Join-Path $env:WINDIR 'System32\rundll32.exe') user32.dll,UpdatePerUserSystemParameters 1,$true
Write-Host "Installed Agent Cat animated screen saver; timeout ${TimeoutSeconds}s; sign-in on resume enabled."
