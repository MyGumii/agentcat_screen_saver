param(
    [ValidateRange(60,86400)][int]$TimeoutSeconds=300,
    [switch]$SkipLockScreen,
    [switch]$SkipShortcut
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1')
$dir=Join-Path $env:LOCALAPPDATA 'AgentCatScreenSaver'
New-Item -ItemType Directory -Path $dir -Force | Out-Null
Copy-Item (Join-Path $root 'bin\AgentCatScreenSaver.scr') (Join-Path $dir 'AgentCatScreenSaver.scr') -Force
Copy-Item (Join-Path $root 'bin\AgentCatScreenSaver.exe') (Join-Path $dir 'AgentCatScreenSaver.exe') -Force
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
Start-Process (Join-Path $env:WINDIR 'System32\rundll32.exe') `
    -ArgumentList @('user32.dll,UpdatePerUserSystemParameters','1','True') -WindowStyle Hidden

if (-not $SkipLockScreen) {
    & (Join-Path $root 'install-lockscreen.ps1') -InstallDirectory $dir
}

if (-not $SkipShortcut) {
    $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    $shortcutPath = Join-Path $desktop 'Agent Cat Screen Saver.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $dir 'AgentCatScreenSaver.scr'
    $shortcut.Arguments = '/s'
    $shortcut.WorkingDirectory = $dir
    $shortcut.Description = 'Start the Agent Cat + Herdr screen saver now.'
    $shortcut.Hotkey = 'CTRL+ALT+A'
    $shortcut.Save()
}

Write-Host "Installed Agent Cat animated screen saver; timeout ${TimeoutSeconds}s; sign-in on resume enabled."
if (-not $SkipLockScreen) { Write-Host 'Lock-screen snapshot refresh: every 1 minute.' }
if (-not $SkipShortcut) { Write-Host 'Immediate screen saver shortcut: Ctrl+Alt+A (or desktop shortcut).' }
