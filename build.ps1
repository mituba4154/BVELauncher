# SPDX-FileCopyrightText: 2026 BVELauncher contributors
# SPDX-License-Identifier: GPL-3.0-or-later
param([ValidateSet('Debug','Release')][string]$Configuration='Debug',[switch]$SkipTests)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$sdk=Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $sdk)){$sdk=(Get-Command dotnet -ErrorAction Stop).Source}
Push-Location $PSScriptRoot
try {
    & $sdk restore BVELauncher.sln -p:Platform=x64
    if($LASTEXITCODE -ne 0){throw 'restore failed'}
    & $sdk build BVELauncher.sln --no-restore -c $Configuration -p:Platform=x64
    if($LASTEXITCODE -ne 0){throw 'build failed'}
    if(-not $SkipTests){
        & $sdk test BVELauncher.sln --no-build -c $Configuration -p:Platform=x64 --logger trx --results-directory (Join-Path $PSScriptRoot "TestResults/$Configuration")
        if($LASTEXITCODE -ne 0){throw 'tests failed'}
    }
} finally {Pop-Location}
