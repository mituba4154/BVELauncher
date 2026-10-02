# アーキテクチャ

## 責務と依存方向

| Project | 責務 | 参照 |
| --- | --- | --- |
| Core | 不変モデル、列挙型、診断、設定定義 | なし |
| Application | ライブラリ検索・索引・起動ユースケース、ポート | Core |
| Bve | テキスト解析、参照抽出、PE検査、BVE検出、設定アダプター | Core / Application |
| Infrastructure | SQLite、atomic write、監視、プロセス、インポート、バックアップ、ログ | Core / Application |
| Integrations | BveEXの浅い検出とローカルインストーラー起動 | Core / Application |
| App | WinUI 3、MVVM、MRTCore、日本語・英語、DI composition root | 上記各層 |

Core / ApplicationからWinUIを参照しません。
UIはViewModelの状態を表示し、ファイルI/Oをサービスへ渡します。
外部DLLの検査は`PEReader`とファイルバージョン情報を使い、`Assembly.Load`やDLL実行は行いません。

## 起動と索引

`LauncherSession`が設定、SQLite、未完了インポート、一時シナリオを復旧し、保存済み索引を表示します。
`RuntimeDetector`は登録先、既定インストール先、32 / 64 bitのUninstall registryを調べます。
バージョンと製品名、PE architectureを組み合わせて5 / 6を識別します。
シナリオrootは登録rootとBVE標準フォルダー・Preferencesから取得します。

`LibraryScanner`は再入をSemaphoreで制御し、バックグラウンドで走査します。
小さなテキストの先頭ヘッダーで種別を識別し、参照先をキューへ追加します。
未知バイナリーを全読み込みせず、タイムスタンプ・サイズで解析キャッシュを再利用します。
解析結果と未知テキストの判定をSQLiteへ保存し、アプリの再起動後も再利用します。
元ファイルが不変でも、参照先の追加・削除は走査時に再確認します。
初回は検出したシナリオを8件ずつUIへ通知します。
画像は同時2件までバックグラウンドで縮小し、縮小PNGをキャッシュします。
大きすぎる画像・デコード不能な画像はプレースホルダーになります。
Grid / Listはグループ付きの仮想化コントロールを使います。
UI通知は120 msでまとめ、項目とグループの差分だけ反映します。
同一検索結果の再通知ではコレクションを変更せず、既存コンテナーを維持します。
シナリオ設定は専用ウィンドウに表示し、検索可能な車両リストを有限のGrid領域で仮想化します。

`FileSystemWatcher`は600 ms debounceで再スキャンを要求します。
overflow時にも同じ再スキャン経路を使います。
操作中の変更は保留し、完了後に1回の再スキャンへまとめます。
探索は全rootを列挙し直しますが、変更のないファイルは再解析しません。
現在のパスが消失し、一意に同じ内容のファイルが見つかった場合のみIDを移動先へ引き継ぎます。
重複コピーには別IDを付けます。

## 永続化

固定内部ID / データパスは`BVELauncher`、表示名は`Branding`とreswで管理します。
`%LOCALAPPDATA%\BVELauncher`以下にSettings、Library、Cache、Logs、Backups、Staging、Generatedを作成します。
テスト用`BVELAUNCHER_TEST_DATA`を指定した場合は、その別rootだけを使用します。

SQLite schema 3はEntries、Metadata、LaunchHistory、ScenarioOverrides、Installations、AssetCacheを保持します。
インデックスは元ファイルの代替ではありません。
favorite / alias / hidden / runtime / vehicleなどのユーザー変更は独立したoverrideとして保存し、同時スキャンで上書きしません。
migration前にはSQLite backup APIでWALを含めたバックアップを作成します。
破損DB・設定は保存してから新規作成し、ログに記録します。

設定JSON、XML、インポートの書き込みは同じディレクトリの一時ファイル、flush、replace / moveで確定します。
Preferencesは元hashを保存し、外部変更を検出したら保存を中止します。
ログはローカルで日付・サイズによりローテートし、設定日数で保持します。
インストール記録から参照されるバックアップは保持期限の自動削除対象から外します。

## 起動と復旧

`LaunchScenario`は個別指定、互換性推奨、既定BVE、検出済みBVEの順で実行環境を決めます。
`ProcessRunner`はshellを介さず`ArgumentList`へシナリオを1引数として追加し、exeのディレクトリをworking directoryにします。
プロセス終了を待ち、履歴・実行中表示・ウィンドウ復帰・生成シナリオ清掃を行います。
生成ファイルのleaseはPIDだけでなく開始時刻も照合します。
クラッシュ直後でPID未記録の場合は、BVEが動いている間の削除を延期します。
single instanceはWindows App SDK `AppInstance`で制御します。

## 境界

BVEプロセスの変更、hook、injection、renderer置換はありません。
BveEXは既知の場所のファイル名・製品情報を読むだけです。
入力デバイスは`Input Devices`内のDLLメタデータから表示名・versionを取得し、標準の拡張子なし小文字名を保存します。
登録されているが検出できない名前も保持・解除できるため、未知の設定を走査で消しません。
ローカルinstallerはユーザーが選択・確定したときだけ起動します。
第三者input plugin固有の設定UIは呼び出しません。
ネットワーク接続、テレメトリー、パッケージ検索は製品機能に含めません。
