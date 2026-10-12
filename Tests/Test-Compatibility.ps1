param([Parameter(Mandatory=$true)][string[]]$AgsPaths,[Parameter(Mandatory=$true)][string]$PluginPath)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$testDirectory=Join-Path ([IO.Path]::GetTempPath()) ('ags-spellchecker-compat-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $testDirectory | Out-Null
$exe=Join-Path $testDirectory 'Compatibility.exe'
& $compiler /nologo /platform:x86 /r:System.Drawing.dll /r:System.Windows.Forms.dll (('/out:')+$exe) (Join-Path $PSScriptRoot 'Compatibility.cs')
if($LASTEXITCODE -ne 0){throw 'Could not build compatibility checker.'}
foreach($ags in $AgsPaths){& $exe $ags (Resolve-Path $PluginPath).Path;if($LASTEXITCODE -ne 0){throw "Compatibility check failed for $ags"}}
Write-Host 'All API, assembly and panel checks passed. This does not perform live game edits.'
