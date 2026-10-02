# SPDX-FileCopyrightText: 2026 BVELauncher contributors
# SPDX-License-Identifier: GPL-3.0-or-later
param([Parameter(Mandatory)][string]$SnapshotDirectory,[string]$OutputPath=(Join-Path $PSScriptRoot '../docs/BVE_SETTINGS_MAPPING.md'))
$ErrorActionPreference='Stop'
$snapshots=@('Bve5','Bve6')|ForEach-Object {Get-Content (Join-Path $SnapshotDirectory "$_.json") -Raw|ConvertFrom-Json}
if(($snapshots[0].Definitions.Property -join '|') -ne ($snapshots[1].Definitions.Property -join '|')){throw 'Adapter definition mismatch'}
$ja=[xml](Get-Content (Join-Path $PSScriptRoot '../src/BVELauncher.App/Strings/ja-JP/Resources.resw') -Raw)
$labels=@{};foreach($entry in $ja.root.data){$labels[$entry.name]=[string]$entry.value}
$lines=[Collections.Generic.List[string]]::new()
$lines.AddRange([string[]]@('# BVE標準設定の対応表','','## 調査方法','','対象は公式配布のBVE 5.8.7554.391（x86）と6.0.7554.619（x64）です。','5 / 6のPreferences XML、標準設定画面、設定項目の型・制約を調査し、独立したアダプターへ定義しました。','以下の表は実装の定義から生成し、118項目が両方のXML fixtureで読み出せることを自動テストします。','標準画面の選択肢と補助表示の細かなXML値を含みます。57項目はキー割り当てです。','','`Verified`はXML構造・型・アダプターでの対応確認を示します。全118項目を実機GUIで個別変更済みという意味ではありません。','実機の操作確認、保存・復元、未検証範囲は[TESTING.md](TESTING.md)に区別して記録します。','','## 保存先と保護','','5: Documents\BveTs\Settings\Preferences.xml、6: Documents\BveTs\Settings\BveTs6.Preferences.xml。','DocumentsはWindowsのKnown Folderで解決するため、OneDriveや移動済みDocumentsにも対応します。','元ファイルのhashを照合し、バックアップ後に変更値だけをatomic replaceします。','未知XML、属性、コメント、改行、空白、要素順は保持します。DTD / 外部entityは拒否します。','BVEがそのPreferencesを使用中の場合は保存を拒否します。','設定ファイルがない場合は実機で初回設定を保存するか、選択したXMLを登録します。','','## 項目対応','','| Property | UI表示 | 分類 | XPath | 型 | 制約 / 選択肢 | 既定値 | 5 / 6 |','| --- | --- | --- | --- | --- | --- | --- | --- |'))
$types=@('Boolean','Integer','Decimal','Text','Choice','Path','Key')
foreach($d in $snapshots[0].Definitions){
    $restriction=if($d.Choices){$d.Choices -join ', '}elseif($null -ne $d.Minimum){"$($d.Minimum) .. $($d.Maximum)"}else{'—'}
    $default=if([string]::IsNullOrEmpty($d.Default)){'空'}else{$d.Default}
    $lines.Add("| $($d.Property) | $($labels[$d.ResourceKey]) | $($d.Category) | ``$($d.XmlPath)`` | $($types[$d.Type]) | $restriction | $default | 対応 / 対応 |")
}
$lines.AddRange([string[]]@('','## 標準画面と詳細値の関係','','標準「グラフィック」画面はWindowed、WindowSize、ScreenSize、DrawDistance、Anisotropyへ対応します。','「補助表示」はFontName、FontStyle、文字サイズ、各Visibleへ対応します。','FontSizeは9種類の補助表示とTimetableのScaleを一括変更する標準操作です。個別Scale / AutoLocation / X / Yも詳細値として保持します。','「既定位置に戻す」は表示中の補助表示のAutoLocationをtrueに戻し、座標の再配置はBVEに任せます。','「シミュレーション」はHeadMotion、「入力デバイス」はInputPluginsと57キー、「高度な設定」はDevelopmentEnabledへ対応します。','LanguageはBVEの独立したLanguageメニュー、シナリオ選択の場所はScenarioForm.Address先頭stringへ対応し、後続履歴は保持します。','入力plugin固有の設定画面、アドオン独自設定、BVE内部値BackDrawDistanceなどは標準設定の対象外です。','第三者input pluginのDLLはランチャーからロードしません。','','キー割り当てはキーの捕捉と数値入力、BVE標準TSV形式のローカルプリセット読み込みに対応します。','標準プリセットの内容自体は同梱しません。','画面サイズは整数、距離と異方性は確認した固定候補、HeadMotionは0 .. 100、文字サイズは8 .. 96です。','未知の既存選択値は読み出して表示しますが、アダプターの許容候補外に変更して保存することはできません。'))
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputPath),$lines,[Text.UTF8Encoding]::new($false))
