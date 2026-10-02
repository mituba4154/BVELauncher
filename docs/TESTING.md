# テストと実機検証

## 実行環境

検証日: 2026-10-02、Windows x64。
.NET SDK 10.0.401、Windows App SDK 2.5.1 Stableを使用しました。
SDKは作業ディレクトリの`.tools/dotnet`へ配置し、既存のグローバルSDKを変更していません。
ビルドスクリプトはローカルSDKがなければインストール済みの`dotnet`を使います。

```powershell
./build.ps1 -Configuration Debug
./build.ps1 -Configuration Release
./publish.ps1 -SkipBuild
```

Debug / Releaseともにrestore、build、testsが成功しています。
ビルドは警告0 / エラー0です。
unit tests 27件、integration tests 34件、合計61件、skip 0 / failure 0です。
TRXは`TestResults/<configuration>/`に出力します。

## 自動テストの対象

| 対象 | 確認内容 |
| --- | --- |
| Parser | UTF-8 / UTF-16 / CP932 / Windows-1252、BOM、改行、未知キー、コメント、重み付き参照、Map複数行 |
| ViewModel / query | metadata検索、alias、グループ、お気に入り、非表示、ソート、通知 |
| Localization | ja-JP / en-USのキー一致・非空値、表示名 |
| SQLite | fresh / corrupt、schema 1→2とbackup、移動ID、複数root、古いscanによるユーザー変更の上書き防止 |
| XML settings | 両実機由来fixtureの118項目・57キー、未知XML・CRLF維持、hash競合、入力範囲、backup / restore |
| Launch | 元ファイル不変、生成Vehicle・相対Route、ArgumentList / working directory、保存車両と明示標準車両の区別 |
| Cleanup | 実行中PID＋開始時刻のlease維持、終了済みleaseの清掃 |
| Import | ZIP / 7z / folder / single、ネスト保持、Scenarios root、traversal / device / ADS / 同名拒否、サイズ制限 |
| Transaction | skip / overwrite、事前hash検査、途中キャンセルrollback、未完了journal復旧、改ざんjournalの外部パス拒否 |
| Ownership | Existing / modified / shared保護、Managedの条件付き削除 |
| BveEX | 自作の無効DLLのファイル存在検出、未知DLLをロードしない、非exe installer拒否 |
| Keyboard | 実機のTSV schemaに沿った自作preset、範囲外・重複・未知キー拒否 |

## WinUI自動操作

`scripts/ui-tests.ps1`はwinapp CLIのUI Automationを使います。
テスト用データrootと自作最小シナリオで実行します。
`BVELAUNCHER_TEST_DATA`により通常のアプリデータから分離します。

```powershell
$env:BVELAUNCHER_TEST_DATA='C:\Test\BVELauncherData'
# Settings/appsettings.jsonに自作FixtureのScenario rootとローカルBVEを登録する
# PreferencesPathには実機Preferencesのテスト用コピーを登録する
$app=Start-Process '<publish>\BVELauncher.App.exe' -PassThru
./scripts/ui-tests.ps1 -AppPid $app.Id -DataDirectory $env:BVELAUNCHER_TEST_DATA
```

12項目のバッチはRelease版で全件成功しました。
初期Scenario Library、日本語検索、Grid / List、グループ開閉、車両画面、操作メニューと診断、元ファイル不変、設定保存を確認します。
BVE6数値入力の即保存、カテゴリ変更後の値保持、日本語→英語→日本語、Dark→Systemの保存も確認します。
画像とJSON結果は`ui-test-results/`に出力します。
保存時のNumberBox未確定入力と、ContentDialog内の選択Popupで操作が停滞する問題を検出して修正しました。
操作メニューは右クリック / Shift+F10 / 明示ボタンから同じMenuFlyoutを開きます。
CIの非対話runnerではUI操作やBVE実行を成功扱いにせず、build / tests / publishを実行します。

## BVE実機起動

公式配布ページの条件を確認して取得したMSIを、検証用ディレクトリへ展開しました。
配布物の再同梱・BVEバイナリーの書き換えはありません。

| BVE | Version | PE | exe SHA-256 |
| --- | --- | --- | --- |
| 5.8 | 5.8.7554.391 | x86 | `61CE9B7B225CAB9B623A27FCBAB1E9DDF8F139F6513253C64FCE0B12FD9104BE` |
| 6 | 6.0.7554.619 | x64 | `0503D0E973CA4BD24CC8BEE93BCDB4F7C57DBB8A81454130639AA6B7E7C8CD27` |

両方でランチャーから`日本語 scenario.txt`をcommand line引数に渡し、運転HUDまで到達しました。
command lineの実測で、パスが1引数として渡されることを確認しました。
個別runtime overrideで5 / 6を切り替えました。
プロセス作成だけを成功判定にせず、エラーダイアログがなく運転HUDを表示することを画面で確認しています。
最小Mapには景観を含めないため背景は黒です。

BVE6で車両選択UIから生成シナリオを起動し、Vehicle / VehicleTitleと相対参照を確認しました。
元シナリオのSHA-256は操作前後で`C6B81399087F79D1B4D1C398CE995BEC89C79A457A2FD8AD46C836EB488DD613`のままです。
BVEを通常終了すると生成ファイルが消え、ランチャーの実行中表示が解除されることを確認しました。

fixtureのScenario / Map / Vehicle / Parameters / Panel / Sound / Performance tableは最小自作データです。
第三者アドオン・画像・DLLをfixtureとして収録していません。
実機画面の証拠はローカルの検証ディレクトリにだけ保持します。

## BVE標準設定の実機調査

5.8 / 6の両方で以下の標準画面を実際に開き、表示項目を確認しました。

| 標準画面 | 確認した項目 | 実装 |
| --- | --- | --- |
| グラフィック | Window / Fullscreen、window size、resolution、距離、異方性 | Boolean / 数値 / 固定候補 |
| 補助表示 | 9種類の表示、既定位置へ戻す、font、bold、size | 個別Visible、AutoLocation reset、FontName / FontStyle / FontSize |
| シミュレーション | 頭の揺れ | HeadMotion 0..100 |
| 入力デバイス | キー割り当て、追加pluginの有効状態 | 57キー、ローカルTSV preset、InputPlugins |
| 高度な設定 | 開発用機能 | DevelopmentEnabled |
| キー割り当て | preset、4種類handle × 6操作、switch類 | category / キー捕捉 / 数値 / preset |
| Languageメニュー | 日本語 / 英語 | Language |

XML fixtureは実機Preferencesを小さく匿名化したものです。
ユーザーのScenario pathはfixtureに残していません。
XMLの118項目が読めること、変更・backup・復元、空Addressへの登録を自動検証します。
ランチャーGUIからのWindowWidth / HeadMotion保存は、テスト用Preferencesコピーで実測しました。
画面確認・XML対応・自動round-tripの根拠を[対応表](BVE_SETTINGS_MAPPING.md)へ記録しています。

## 未検証範囲

- 118項目すべての設定値を実機で個別変更した際の表示・物理挙動。標準画面の項目照合とXML保存のテストを行っています。
- 別ドライブへ生成シナリオを配置した場合の絶対参照。実装は検証済みの同一ドライブ経路に限定します。
- 実BveEXインストーラーの完了と、第三者input plugin固有の設定。検出・起動入口は実装し、DLLをロードしません。
- 任意の第三者路線・ATS pluginの動作保証。静的診断は推奨情報で、起動を禁止しません。
- Windows 10実機、High Contrast / Narratorの全操作、複数モニターの全組み合わせ。テーマ・標準コントロール・AutomationIdに対応します。

## 公式根拠

- [BVE 5.8の配布・使用条件](https://www.bvets.net/jp/download/main5-8.html)
- [BVE 6の配布・使用条件](https://www.bvets.net/jp/download/main6.html)
- [Windows App SDK release channels](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels)
- [Unpackaged WinUI deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app)
