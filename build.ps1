$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$bin = Join-Path $root 'bin'
$asset = Join-Path $root 'assets\cute-cat-orange-sprite.png'
if (!(Test-Path $asset)) { & (Join-Path $root 'prepare-assets.ps1') }
if (!(Test-Path $asset)) { throw 'Agent Cat sprite preparation failed.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path $compiler)) { throw 'C# compiler not found' }
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$exe = Join-Path $bin 'AgentCatScreenSaver.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /resource:"$asset,CheeseCatRunSheet" /out:$exe (Join-Path $root 'AgentCatScreenSaver.cs')
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Copy-Item $exe (Join-Path $bin 'AgentCatScreenSaver.scr') -Force
Write-Host "Built $($bin)\AgentCatScreenSaver.scr"
