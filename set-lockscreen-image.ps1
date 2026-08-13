param([Parameter(Mandatory=$true)][string]$ImagePath)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    throw "Lock-screen image not found: $ImagePath"
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$storageFileType = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$lockScreenType = [Windows.System.UserProfile.LockScreen,Windows.System.UserProfile,ContentType=WindowsRuntime]

$method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and
        $_.GetParameters().Count -eq 1
    } |
    Select-Object -First 1
$fileTask = $method.MakeGenericMethod($storageFileType).Invoke(
    $null, @( $storageFileType::GetFileFromPathAsync((Resolve-Path -LiteralPath $ImagePath).Path) ))
$file = $fileTask.GetAwaiter().GetResult()
$actionMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq 'AsTask' -and -not $_.IsGenericMethodDefinition -and
        $_.GetParameters().Count -eq 1
    } |
    Select-Object -First 1
$applyTask = $actionMethod.Invoke($null, @( $lockScreenType::SetImageFileAsync($file) ))
$applyTask.GetAwaiter().GetResult() | Out-Null
