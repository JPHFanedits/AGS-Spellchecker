param([string]$AgsPath='C:\Program Files (x86)\Adventure Game Studio 3.6.0',[string]$OutputPath=$PSScriptRoot)
$ErrorActionPreference='Stop'
# Build releases against the oldest supported SDK (AGS 3.6.0.47).
if(!(Test-Path (Join-Path $AgsPath 'AGS.Types.dll'))){throw 'AGS.Types.dll not found. Pass -AgsPath with the extracted official AGS 3.6.0.47 folder.'}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Web.Extensions.dll')
$argsList=@('/nologo','/target:library','/platform:anycpu','/optimize+',('/out:'+(Join-Path $OutputPath 'AGS.Plugin.Spellchecker.dll')))
foreach($ref in $refs){$argsList+='/r:'+(Join-Path $framework $ref)}
$argsList+='/r:'+(Join-Path $AgsPath 'AGS.Types.dll')
$argsList+='/resource:'+(Join-Path $PSScriptRoot 'Assets\spellchecker.ico')+',AGSSpellchecker.Icon'
foreach($source in @('AssemblyInfo.cs','Plugin.cs','EditorBridge.cs','ReviewSession.cs','ReviewPanel.cs','Proof.cs','Extraction.cs','OfflineProof.cs')){$argsList+=(Join-Path $PSScriptRoot $source)}
& (Join-Path $framework 'csc.exe') @argsList
if($LASTEXITCODE -ne 0){throw 'Plugin build failed.'}






