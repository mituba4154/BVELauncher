# SPDX-FileCopyrightText: 2026 BVELauncher contributors
# SPDX-License-Identifier: GPL-3.0-or-later
param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$ImportZip,[Parameter(Mandatory)][string]$ExpectedScenarioPath,[int]$LibraryCountBefore=1)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $ExpectedScenarioPath){throw 'Use a new self-authored test scenario; destination already exists'}
$ImportZip=[IO.Path]::GetFullPath($ImportZip)
$sourceHash=(Get-FileHash -LiteralPath $ImportZip).Hash
function Ui([string[]]$Arguments){$output=& winapp ui @Arguments 2>&1;if($LASTEXITCODE -ne 0){throw ($output -join [Environment]::NewLine)};return ($output -join [Environment]::NewLine)}
function Elements($Nodes){foreach($node in $Nodes){$node;if($node.children){Elements $node.children}}}
$main=@(winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object title -eq BVELauncher)[0].hwnd
Ui @('invoke','ImportButton','-w',[string]$main)|Out-Null
$picker=$null
for($attempt=0;$attempt -lt 30 -and -not $picker;$attempt++){$picker=winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object className -eq '#32770'|Select-Object -First 1;if(-not $picker){Start-Sleep -Milliseconds 100}}
if(-not $picker){throw 'File picker missing'}
$tree=Ui @('inspect','-w',[string]$picker.hwnd,'--json','--depth','12')|ConvertFrom-Json
$filename=Elements $tree.windows[0].elements|Where-Object {$_.type -eq 'Edit' -and $_.automationId -eq '1148'}|Select-Object -First 1
$open=Elements $tree.windows[0].elements|Where-Object {$_.type -in 'Button','SplitButton' -and $_.automationId -eq '1'}|Select-Object -First 1
if(-not $filename -or -not $open){throw 'Native file picker controls missing'}
Ui @('set-value',$filename.selector,$ImportZip,'-w',[string]$picker.hwnd)|Out-Null
Ui @('invoke',$open.selector,'-w',[string]$picker.hwnd)|Out-Null
Ui @('wait-for','ImportDestination','-w',[string]$main,'-t','5000')|Out-Null
Ui @('invoke','PrimaryButton','-w',[string]$main)|Out-Null
Ui @('wait-for','StatusText','-w',[string]$main,'--value',"$($LibraryCountBefore+1) シナリオ",'-t','5000')|Out-Null
if(-not(Test-Path -LiteralPath $ExpectedScenarioPath)){throw 'Scenario not installed'}
Ui @('invoke','SettingsButton','-w',[string]$main)|Out-Null
$settings=winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object title -like '設定 ·*'|Select-Object -First 1
Ui @('invoke','SettingsImport','-w',[string]$settings.hwnd)|Out-Null
Ui @('invoke','SettingImportHistory','-w',[string]$settings.hwnd)|Out-Null
Ui @('wait-for','Installations','-w',[string]$settings.hwnd,'-t','3000')|Out-Null
$tree=Ui @('inspect','-w',[string]$settings.hwnd,'--json','--depth','14')|ConvertFrom-Json
$record=Elements $tree.windows[0].elements|Where-Object {$_.type -eq 'ListItem' -and $_.name -eq $ImportZip}|Select-Object -First 1
if(-not $record){throw 'Installation record missing'}
Ui @('invoke',$record.selector,'-w',[string]$settings.hwnd)|Out-Null
Ui @('invoke','PrimaryButton','-w',[string]$settings.hwnd)|Out-Null
Ui @('wait-for','PrimaryButton','-w',[string]$settings.hwnd,'--value','OK','-t','3000')|Out-Null
Ui @('invoke','PrimaryButton','-w',[string]$settings.hwnd)|Out-Null
Ui @('wait-for','StatusText','-w',[string]$main,'--value',"$LibraryCountBefore シナリオ",'-t','5000')|Out-Null
if(Test-Path -LiteralPath $ExpectedScenarioPath){throw 'Managed scenario not removed'}
if((Get-FileHash -LiteralPath $ImportZip).Hash -ne $sourceHash){throw 'Source ZIP changed'}
Ui @('invoke','Close','-w',[string]$settings.hwnd)|Out-Null
Write-Host 'PASS Native picker / import preview / immediate library update / managed uninstall / source retention'
