param([Parameter(Mandatory)][int]$AppPid,[int]$ExpectedScenarios=83,[string]$TestSearch='自動テスト',[string]$OutputDirectory=(Join-Path $PSScriptRoot '../ui-test-results'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $OutputDirectory|Out-Null
function Ui([string[]]$Arguments){$output=& winapp ui @Arguments 2>&1;if($LASTEXITCODE -ne 0){throw ($output -join [Environment]::NewLine)};return ($output -join [Environment]::NewLine)}
function Elements($Nodes){foreach($node in $Nodes){$node;if($node.children){Elements $node.children}}}
$main=(winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object title -eq BVELauncher).hwnd
if(-not $main){throw 'Main window missing'}
$initial=(Get-Process -Id $AppPid).WorkingSet64
Ui @('wait-for','StatusText','-w',"$main",'--value',"$ExpectedScenarios シナリオ",'-t','30000')|Out-Null
Ui @('set-value','TextBox',$TestSearch,'-w',"$main")|Out-Null
Ui @('wait-for','StatusText','-w',"$main",'--value','1 シナリオ','-t','3000')|Out-Null
function Open-ScenarioSettings {
    $tree=Ui @('inspect','-w',"$main",'--json','--depth','12')|ConvertFrom-Json
    $button=Elements $tree.windows[0].elements|Where-Object automationId -like 'ScenarioActions*'|Select-Object -First 1
    if(-not $button){throw 'Test scenario action button missing'}
    Ui @('invoke',$button.automationId,'-w',"$main")|Out-Null
    Ui @('invoke','MenuScenarioSettings','-w',"$main")|Out-Null
    $window=winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object title -like 'シナリオ設定 ·*'|Select-Object -First 1
    if(-not $window){throw 'Scenario settings window missing'}
    Ui @('wait-for','ScenarioVehicle','-w',"$($window.hwnd)",'-t','3000')|Out-Null
    return $window.hwnd
}
for($iteration=0;$iteration -lt 10;$iteration++){
    $settings=Open-ScenarioSettings
    Ui @('set-value','ScenarioVehicleSearch','該当しない車両','-w',"$settings")|Out-Null
    Ui @('set-value','ScenarioVehicleSearch','','-w',"$settings")|Out-Null
    Ui @('invoke','ScenarioRuntime','-w',"$settings")|Out-Null
    Ui @('invoke','ScenarioRuntime','-w',"$settings")|Out-Null
    if($iteration -eq 0){Ui @('screenshot','-w',"$settings",'-o',(Join-Path $OutputDirectory 'large-scenario-settings.png'))|Out-Null}
    Ui @('invoke','CloseButton','-w',"$settings")|Out-Null
    if(-not (Get-Process -Id $AppPid).Responding){throw 'Application became unresponsive'}
}
$settings=Open-ScenarioSettings
$tree=Ui @('inspect','-w',"$settings",'--json','--depth','10')|ConvertFrom-Json
$alias=Elements $tree.windows[0].elements|Where-Object automationId -eq ScenarioAlias|Select-Object -First 1
$originalAlias=$alias.value
try {
    Ui @('set-value','ScenarioAlias','UI設定保存検証','-w',"$settings")|Out-Null
    Ui @('invoke','PrimaryButton','-w',"$settings")|Out-Null
    $settings=Open-ScenarioSettings
    Ui @('wait-for','ScenarioAlias','-w',"$settings",'--value','UI設定保存検証','-t','3000')|Out-Null
} finally {
    if($settings -and @(winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object hwnd -eq $settings).Count -gt 0){
        Ui @('set-value','ScenarioAlias',([string]$originalAlias),'-w',"$settings")|Out-Null
        Ui @('invoke','PrimaryButton','-w',"$settings")|Out-Null
    }
    Ui @('set-value','TextBox','','-w',"$main")|Out-Null
}
Ui @('wait-for','StatusText','-w',"$main",'--value',"$ExpectedScenarios シナリオ",'-t','3000')|Out-Null
Ui @('invoke','ListButton','-w',"$main")|Out-Null
Ui @('invoke','GridButton','-w',"$main")|Out-Null
Ui @('invoke','GroupToggle','-w',"$main")|Out-Null
Ui @('invoke','GroupToggle','-w',"$main")|Out-Null
$process=Get-Process -Id $AppPid
if(-not $process.Responding -or $process.WorkingSet64 -gt 1500MB){throw 'Unexpected UI hang or memory growth'}
$result=[pscustomobject]@{Passed=$true;Scenarios=$ExpectedScenarios;RepeatedOpenClose=10;SaveAndRestore=$true;WorkingSetBytes=$process.WorkingSet64;InitialWorkingSetBytes=$initial}
$result|ConvertTo-Json|Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'large-library-results.json')
$result|Format-List
