$ErrorActionPreference='Stop'
$dir=Join-Path $env:LOCALAPPDATA 'AgentCatScreenSaver'; $backup=Join-Path $dir 'previous-settings.json'; $key='HKCU:\Control Panel\Desktop'
if (Test-Path $backup) { $p=Get-Content -Raw $backup|ConvertFrom-Json; foreach($n in 'SCRNSAVE.EXE','ScreenSaveActive','ScreenSaveTimeOut','ScreenSaverIsSecure'){ $v=$p.$n; if($null -eq $v){Remove-ItemProperty $key $n -ErrorAction SilentlyContinue}else{Set-ItemProperty $key $n ([string]$v)} }; Start-Process (Join-Path $env:WINDIR 'System32\rundll32.exe') -ArgumentList @('user32.dll,UpdatePerUserSystemParameters','1','True') -WindowStyle Hidden; Write-Host 'Previous screen saver settings restored.' } else { Write-Warning 'No backup found; settings unchanged.' }

Unregister-ScheduledTask -TaskName 'AgentCatLockScreenUpdater' -Confirm:$false -ErrorAction SilentlyContinue
$shortcutPath=Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)) 'Agent Cat Screen Saver.lnk'
if(Test-Path -LiteralPath $shortcutPath){Remove-Item -LiteralPath $shortcutPath -Force}

$lockBackup=Join-Path $dir 'previous-lockscreen.json'
$setter=Join-Path $dir 'set-lockscreen-image.ps1'
if((Test-Path -LiteralPath $lockBackup) -and (Test-Path -LiteralPath $setter)){
    $lock=Get-Content -Raw $lockBackup|ConvertFrom-Json
    if($lock.originalImagePath -and (Test-Path -LiteralPath $lock.originalImagePath)){
        & (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File $setter -ImagePath $lock.originalImagePath
        Write-Host 'Previous lock-screen image restored.'
    }
}
