param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'artifacts/publish'),[switch]$SkipBuild)
$ErrorActionPreference='Stop'
if(-not $SkipBuild){& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release}
$sdk=Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $sdk)){$sdk=(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
& $sdk publish (Join-Path $PSScriptRoot 'src/BVELauncher.App/BVELauncher.App.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $OutputDirectory
if($LASTEXITCODE -ne 0){throw 'publish failed'}
foreach($file in @('LICENSE','README.md','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $OutputDirectory}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $OutputDirectory -Recurse -Force
$zip=Join-Path (Split-Path $OutputDirectory -Parent) 'BVELauncher-win-x64.zip'
Compress-Archive -Path (Join-Path $OutputDirectory '*') -DestinationPath $zip -Force
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
