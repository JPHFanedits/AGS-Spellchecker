param([string]$AgsPath='C:\Program Files (x86)\Adventure Game Studio 3.6.1',[string]$OutputPath=$PSScriptRoot)
$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Web.Extensions.dll','System.Xaml.dll')
$argsList=@('/nologo','/target:library','/platform:anycpu','/optimize+',('/out:'+(Join-Path $OutputPath 'AGS.Plugin.Spellchecker.dll')))
foreach($ref in $refs){$argsList+='/r:'+(Join-Path $framework $ref)}
foreach($ref in @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll','WindowsFormsIntegration.dll')){$argsList+='/r:'+(Join-Path (Join-Path $framework 'WPF') $ref)}
$argsList+='/r:'+(Join-Path $AgsPath 'AGS.Types.dll')
$argsList+='/resource:'+(Join-Path $PSScriptRoot 'Assets\spellchecker.ico')+',AGSSpellchecker.Icon'
foreach($source in @('Plugin.cs','EditorBridge.cs','ReviewSession.cs','ReviewPanel.cs','Proof.cs','Extraction.cs')){$argsList+=(Join-Path $PSScriptRoot $source)}
& (Join-Path $framework 'csc.exe') @argsList
if($LASTEXITCODE -ne 0){throw 'Plugin build failed.'}


