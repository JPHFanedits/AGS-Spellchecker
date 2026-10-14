param([Parameter(Mandatory=$true)][string]$AgsPath,[string]$InnoCompiler='C:\Program Files (x86)\Inno Setup 6\ISCC.exe',[string]$CachePath=(Join-Path $PSScriptRoot 'downloads'))
$ErrorActionPreference='Stop'
$metadata=Get-Content (Join-Path $PSScriptRoot 'Packaging\DEPENDENCIES.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory $CachePath -Force | Out-Null
function Get-VerifiedArchive($Url,$Name,$ExpectedHash){
  $archive=Join-Path $CachePath $Name
  if(!(Test-Path $archive)){Invoke-WebRequest -UseBasicParsing $Url -OutFile $archive}
  if((Get-FileHash $archive -Algorithm SHA256).Hash -ne $ExpectedHash){throw "Checksum mismatch: $archive"}
  return $archive
}
$lt=Get-VerifiedArchive $metadata.LanguageToolUrl 'LanguageTool.zip' $metadata.LanguageToolSHA256
$java=Get-VerifiedArchive $metadata.JavaUrl 'Java.zip' $metadata.JavaSHA256
$stage=Join-Path $PSScriptRoot ('build\'+[Guid]::NewGuid().ToString('N'))
$offline=Join-Path $stage 'offline'
New-Item -ItemType Directory (Join-Path $offline 'Runtime\LanguageTool'),(Join-Path $offline 'Runtime\Java'),(Join-Path $stage 'installer') -Force | Out-Null
Expand-Archive $lt (Join-Path $stage 'lt')
Expand-Archive $java (Join-Path $stage 'java')
Copy-Item (Join-Path (Get-ChildItem (Join-Path $stage 'lt') -Directory | Select-Object -First 1).FullName '*') (Join-Path $offline 'Runtime\LanguageTool') -Recurse
Copy-Item (Join-Path (Get-ChildItem (Join-Path $stage 'java') -Directory | Select-Object -First 1).FullName '*') (Join-Path $offline 'Runtime\Java') -Recurse
Copy-Item (Join-Path $PSScriptRoot 'Packaging\languages.json') (Join-Path $offline 'Runtime')
Copy-Item (Join-Path $PSScriptRoot 'Packaging\DEPENDENCIES.json') $offline
Copy-Item (Join-Path $PSScriptRoot 'Packaging\*.txt') $offline
Copy-Item (Join-Path $PSScriptRoot 'Source\Assets') $stage -Recurse
Copy-Item (Join-Path $PSScriptRoot 'Installer\Setup.iss') (Join-Path $stage 'installer')
& (Join-Path $PSScriptRoot 'Source\build.ps1') -AgsPath $AgsPath -OutputPath $offline
& $InnoCompiler /Q (Join-Path $stage 'installer\Setup.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
$deliverables=Join-Path $stage 'deliverables'
Compress-Archive (Join-Path $offline '*') (Join-Path $deliverables 'AGS-Spellchecker-Portable.zip')
Write-Host "Installer and portable ZIP: $deliverables"
