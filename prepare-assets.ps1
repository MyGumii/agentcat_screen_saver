param([string]$AgentCatExe)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$asset = Join-Path $root 'assets\cute-cat-orange-sprite.png'
if (Test-Path $asset) {
    Write-Host "Agent Cat sprite already prepared: $asset"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($AgentCatExe)) {
    $running = Get-Process -Name 'agent-cat-windows' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($running -and $running.Path) { $AgentCatExe = $running.Path }
}
if ([string]::IsNullOrWhiteSpace($AgentCatExe)) {
    $candidate = Join-Path $env:LOCALAPPDATA 'Agent Cat\agent-cat-windows.exe'
    if (Test-Path $candidate) { $AgentCatExe = $candidate }
}
if ([string]::IsNullOrWhiteSpace($AgentCatExe) -or !(Test-Path $AgentCatExe)) {
    throw 'Agent Cat Windows app was not found. Install/open Agent Cat, or pass -AgentCatExe C:\path\to\agent-cat-windows.exe.'
}

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path $compiler)) { throw 'Windows .NET Framework C# compiler was not found.' }

$toolExe = Join-Path $root 'tools\ExtractNamedPng.exe'
& $compiler /nologo /optimize+ /out:$toolExe (Join-Path $root 'tools\ExtractNamedPng.cs')
if ($LASTEXITCODE -ne 0) { throw "Asset extractor compilation failed: $LASTEXITCODE" }

& $toolExe $AgentCatExe 'cute-cat-orange-sprite-' $asset
if ($LASTEXITCODE -ne 0 -or !(Test-Path $asset)) { throw 'Could not extract the Agent Cat orange-cat sprite.' }
