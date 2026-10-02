# SPDX-FileCopyrightText: 2026 BVELauncher contributors
# SPDX-License-Identifier: GPL-3.0-or-later
param([Parameter(Mandatory)][int]$AppPid,[string]$DataDirectory,[string]$OutputDirectory=(Join-Path $PSScriptRoot '../ui-test-results'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$results=[Collections.Generic.List[object]]::new()
$windows=winapp ui list-windows -a $AppPid --json | ConvertFrom-Json
$main=@($windows|Where-Object title -eq 'BVELauncher')
if($main.Count -ne 1){throw 'Exactly one main window is required'}
$mainHwnd=$main[0].hwnd
function Run-Ui([string[]]$Arguments) {
    $output=& winapp ui @Arguments 2>&1
    if($LASTEXITCODE -ne 0){throw ($output -join [Environment]::NewLine)}
    return ($output -join [Environment]::NewLine)
}
function Test-Step([string]$Name,[scriptblock]$Body) {
    try{& $Body;$results.Add([pscustomobject]@{Name=$Name;Passed=$true;Error=$null});Write-Host "PASS $Name"}
    catch{$results.Add([pscustomobject]@{Name=$Name;Passed=$false;Error=$_.Exception.Message});Write-Host "FAIL $Name : $_"}
}
function Wait-Ui([string]$Id,[string]$Value,[long]$Window=$mainHwnd) {
    $arguments=@('wait-for',$Id,'-w',[string]$Window,'-t','3000')
    if($Value){$arguments+=@('--value',$Value)}
    Run-Ui $arguments | Out-Null
}
function Invoke-Ui([string]$Id,[long]$Window=$mainHwnd){
    if($Id -eq 'CloseButton'){Wait-Ui $Id '' $Window}
    Run-Ui @('invoke',$Id,'-w',[string]$Window)|Out-Null
    if($Id -eq 'CloseButton' -and $Window -eq $mainHwnd){Run-Ui @('wait-for',$Id,'--gone','-w',[string]$Window,'-t','3000')|Out-Null}
}
function Get-Elements($Nodes) {foreach($node in $Nodes){$node;if($node.children){Get-Elements $node.children}}}
function Open-Actions {
    $tree=Run-Ui @('inspect','-w',[string]$mainHwnd,'--json','--depth','12')|ConvertFrom-Json
    $button=Get-Elements $tree.windows[0].elements|Where-Object {$_.automationId -like 'ScenarioActions*'}|Select-Object -First 1
    if(-not $button){throw 'Scenario action button missing'}
    Invoke-Ui $button.automationId
}
function Settings-Window {
    Invoke-Ui 'SettingsButton'
    $window=winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object {$_.title -match '^(設定|Settings) ·'}|Select-Object -First 1
    if(-not $window){throw 'Settings window missing'}
    Invoke-Ui 'SettingsGeneral' $window.hwnd
    return [long]$window.hwnd
}
function Select-Choice([string]$Id,[string]$Name,[long]$Window) {
    Invoke-Ui $Id $Window
    $tree=Run-Ui @('inspect','-w',[string]$Window,'--json','--depth','16')|ConvertFrom-Json
    $choice=Get-Elements $tree.windows[0].elements|Where-Object {$_.type -eq 'ListItem' -and $_.name -eq $Name}|Select-Object -First 1
    if(-not $choice){throw "Choice '$Name' missing"}
    Invoke-Ui $choice.selector $Window
}
Test-Step 'Startup shows scenario library' {Wait-Ui 'ScenarioGrid';Wait-Ui 'StatusText' '1 シナリオ'}
if($DataDirectory){
    Test-Step 'Second process redirects to the existing single instance' {
        $savedData=$env:BVELAUNCHER_TEST_DATA
        try{
            $env:BVELAUNCHER_TEST_DATA=$DataDirectory
            $exe=(Get-Process -Id $AppPid).Path
            $second=Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
            if(-not $second.WaitForExit(5000)){throw 'Second instance did not exit'}
            if(@(winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object title -eq BVELauncher).Count -ne 1){throw 'Main window count changed'}
        } finally {
            $env:BVELAUNCHER_TEST_DATA=$savedData
            if($second -and -not $second.HasExited){$second.CloseMainWindow()|Out-Null;$second.WaitForExit(5000)|Out-Null}
        }
    }
}
Test-Step 'Search filters Japanese scenario' {
    Run-Ui @('set-value','TextBox','該当しない検索','-w',[string]$mainHwnd)|Out-Null
    Wait-Ui 'StatusText' '0 シナリオ'
    Run-Ui @('set-value','TextBox','自動テスト','-w',[string]$mainHwnd)|Out-Null
    Wait-Ui 'StatusText' '1 シナリオ'
    Run-Ui @('set-value','TextBox','','-w',[string]$mainHwnd)|Out-Null
}
Test-Step 'List and grid commands work' {Invoke-Ui 'ListButton';Wait-Ui 'ScenarioList';Invoke-Ui 'GridButton';Wait-Ui 'ScenarioGrid'}
Test-Step 'Groups collapse and expand' {Invoke-Ui 'GroupToggle';Invoke-Ui 'GroupToggle';Wait-Ui 'GroupToggle'}
Test-Step 'Vehicle library opens' {Invoke-Ui 'VehiclesButton';Wait-Ui 'VehicleList';Invoke-Ui 'CloseButton'}
Test-Step 'Scenario context menu exposes diagnostics' {
    Open-Actions
    Wait-Ui 'MenuDiagnostics';Invoke-Ui 'MenuDiagnostics';Invoke-Ui 'CloseButton'
}
Test-Step 'Scenario settings preserve source file' {
    if($DataDirectory){$scenario=Join-Path (Split-Path $DataDirectory -Parent) 'SmokeScenarios/日本語 scenario.txt';$before=(Get-FileHash $scenario).Hash}
    Open-Actions
    Invoke-Ui 'MenuScenarioSettings'
    $details=winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object {$_.title -match '^(シナリオ設定|Scenario settings) ·'}|Select-Object -First 1
    if(-not $details){throw 'Scenario settings window missing'}
    Wait-Ui 'ScenarioAlias' '' $details.hwnd;Wait-Ui 'ScenarioRuntime' '' $details.hwnd;Invoke-Ui 'CloseButton' $details.hwnd
    if($DataDirectory -and (Get-FileHash $scenario).Hash -ne $before){throw 'Original scenario changed'}
}
Test-Step 'Settings window opens and persists view preference' {
    Invoke-Ui 'SettingsButton'
    $window=winapp ui list-windows -a $AppPid --json|ConvertFrom-Json|Where-Object title -Like '設定*'|Select-Object -First 1
    if(-not $window){throw 'Settings window missing'}
    Wait-Ui 'SettingClickBehavior' '' $window.hwnd;Invoke-Ui 'SaveSettings' $window.hwnd
    Wait-Ui 'SettingsStatus' '保存しました' $window.hwnd
    Run-Ui @('screenshot','-w',[string]$window.hwnd,'-o',(Join-Path $OutputDirectory 'settings-smoke.png'))|Out-Null
    Invoke-Ui 'Close' $window.hwnd
    if($DataDirectory -and -not(Test-Path (Join-Path $DataDirectory 'Settings/appsettings.json'))){throw 'Settings file missing'}
}
if($DataDirectory){
    Test-Step 'BVE settings delegate to native BVE without writing preferences' {
        $settings=Get-Content (Join-Path $DataDirectory 'Settings/appsettings.json') -Raw|ConvertFrom-Json
        $hashes=@{};foreach($runtime in $settings.Runtimes){if(Test-Path -LiteralPath $runtime.PreferencesPath){$hashes[$runtime.PreferencesPath]=(Get-FileHash -LiteralPath $runtime.PreferencesPath).Hash}}
        $window=Settings-Window;Invoke-Ui 'SettingsBve' $window
        Wait-Ui 'OpenBveBve6' '' $window
        $tree=Run-Ui @('inspect','-w',[string]$window,'--json','--depth','18')|ConvertFrom-Json
        $elements=@(Get-Elements $tree.windows[0].elements)
        if($elements|Where-Object {$_.automationId -in @('BveCategory','BveWindowWidth','BveKeyboardPreset')}){throw 'Internal BVE editor is still exposed'}
        Wait-Ui 'InstallTsHud' '' $window;Wait-Ui 'InstallOtherExtension' '' $window
        Invoke-Ui 'SaveSettings' $window;Invoke-Ui 'Close' $window
        foreach($path in $hashes.Keys){if((Get-FileHash -LiteralPath $path).Hash -ne $hashes[$path]){throw 'BVE preferences were changed by launcher settings'}}
    }
    Test-Step 'Language switches to English and back' {
        $window=Settings-Window;Select-Choice 'SettingLanguage' 'English' $window;Invoke-Ui 'SaveSettings' $window
        Wait-Ui 'SettingsStatus' 'Saved' $window;Invoke-Ui 'Close' $window
        $settings=Get-Content (Join-Path $DataDirectory 'Settings/appsettings.json') -Raw|ConvertFrom-Json
        if($settings.Language -ne 'en-US'){throw 'English preference not saved'}
        $window=Settings-Window;Select-Choice 'SettingLanguage' '日本語' $window;Invoke-Ui 'SaveSettings' $window
        Wait-Ui 'SettingsStatus' '保存しました' $window;Invoke-Ui 'Close' $window
    }
    Test-Step 'Dark theme saves and restores System' {
        $window=Settings-Window;Invoke-Ui 'SettingsAppearance' $window;Select-Choice 'SettingTheme' 'ダーク' $window;Invoke-Ui 'SaveSettings' $window
        $settings=Get-Content (Join-Path $DataDirectory 'Settings/appsettings.json') -Raw|ConvertFrom-Json
        if($settings.Theme -ne 'Dark'){throw 'Dark preference not saved'}
        Select-Choice 'SettingTheme' 'システム' $window;Invoke-Ui 'SaveSettings' $window;Invoke-Ui 'Close' $window
    }
}
Run-Ui @('screenshot','-w',[string]$mainHwnd,'-o',(Join-Path $OutputDirectory 'library-smoke.png'))|Out-Null
$results|ConvertTo-Json -Depth 6|Set-Content (Join-Path $OutputDirectory 'results.json') -Encoding utf8
$failed=@($results|Where-Object {-not $_.Passed}).Count
Write-Host "Tests: $($results.Count), failed: $failed"
if($failed -gt 0){exit 1}
