# BVELauncher

BVE Trainsim 5.8 / 6向けの非公式Windowsランチャーです。
シナリオを選んですぐ運転を始められる、WinUI 3のシナリオ・車両ライブラリを提供します。
BVE本体、BveEX、路線・車両アドオンは同梱しません。

## 日本語

### 使い方

1. 配布用`BVELauncher-win-x64.zip`を任意のフォルダーへ展開し、`BVELauncher.App.exe`を起動します。
2. BVEと標準のシナリオフォルダーを自動検出します。見つからない場合は「設定」でBVE実行ファイルとフォルダーを登録します。
3. ライブラリのシナリオをクリックすると、そのシナリオを引数にBVEが起動します。
4. 車両を変更する場合は右クリックから「車両を変更して運転」を選びます。元シナリオは変更されません。
5. ZIP / 7z / フォルダー / 単独シナリオをドロップするか「インポート」で選び、配置先と競合をプレビューして確定します。

検索、グループ表示、Grid / List、お気に入り、非表示、最近の起動順、表示名・グループ・既定車両・BVEの個別設定に対応します。
「設定」から日本語 / English、System / Light / Dark、BVE標準設定、インポート履歴、バックアップ、ログを操作できます。
`Ctrl+F`で検索、`Ctrl+R`で再スキャン、`Ctrl+,`で設定、`Enter`で選択シナリオを起動します。

### 動作環境とデータ

Windows 10 2004以降 / Windows 11、x64専用です。
自己完結型配布には.NETとWindows App SDKランタイムを含みます。
BVE側のDirectXなどの前提条件は[BVE公式配布ページ](https://www.bvets.net/jp/download/)を参照してください。
ランチャーはBVEバイナリーを変更せず、アドオンDLLをロードしません。
診断の警告があっても、シナリオとBVE実行ファイルが存在すれば起動できます。

データは`%LOCALAPPDATA%\BVELauncher`に保存します。
SQLiteは再作成可能な索引で、元のBVEファイルが正本です。表示上の変更はランチャー内に保存します。
BVE設定は変更箇所だけを保存し、事前バックアップを作成します。
インポートでは既存・共有・ユーザーが変更したファイルを保護します。
自動ダウンロード、テレメトリー、オンラインアカウントはありません。

### 開発・検証・配布

Windows、.NET 10 SDK、Windows SDKを用意して実行します。
プロジェクトはWindows App SDK **2.5.1 Stable**を固定使用します。
`global.json`のSDK要件は.NET 10です。

```powershell
./build.ps1 -Configuration Debug
./build.ps1 -Configuration Release
./publish.ps1 -SkipBuild
```

配布物は`artifacts/BVELauncher-win-x64.zip`、展開済みアプリは`artifacts/publish`です。
GitHub ActionsもDebug / Release / testsを実行し、配布ZIPとTRXを成果物として保存します。
署名なしの自己完結ZIPを配布します。MSIXインストールは必要ありません。

設計・制約・再現手順は[architecture](docs/ARCHITECTURE.md)、[formats](docs/FORMATS.md)、[import](docs/IMPORT.md)、[BVE settings mapping](docs/BVE_SETTINGS_MAPPING.md)、[testing](docs/TESTING.md)を参照してください。

## English

BVELauncher is an **unofficial** Windows launcher and library manager for BVE Trainsim 5.8 and 6.
It opens directly into the scenario library. Click a scenario to launch BVE with its full scenario path; use its context menu to change the vehicle without editing the original file.

Extract `BVELauncher-win-x64.zip` and run `BVELauncher.App.exe` on Windows 10 version 2004 or later / Windows 11 x64.
The distribution is self-contained. BVE and its prerequisites must be installed separately.
Register additional executables and scenario folders in Settings if automatic detection does not find them.
Drop ZIP, 7z, a folder or a scenario file into the library, review the destination and conflicts, then import.
Use Settings to choose Japanese or English, change the theme, edit BVE preferences, inspect import history and restore backups.

Application data is stored in `%LOCALAPPDATA%\BVELauncher`.
Original BVE files remain authoritative; aliases, groups, favorites and overrides belong to the launcher.
BVE preferences are backed up before atomic updates, and unknown XML fields are preserved.
Managed uninstall protects existing, modified and externally referenced files.
The launcher never patches BVE, loads unknown plug-in DLLs, downloads add-ons automatically or sends telemetry.
BVE, BveEX and third-party add-ons are not included.

Build on Windows with .NET 10 SDK and Windows SDK using the PowerShell commands above.
`publish.ps1` generates the unsigned, unpackaged self-contained x64 ZIP.
See [testing](docs/TESTING.md) for automated and native BVE validation, including limits that have not been verified.

## License / ライセンス

Original BVELauncher source is licensed under [MPL-2.0](LICENSE).
Dependency licenses and required notices are included in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and `licenses/`.
BVE Trainsim and BveEX remain the property of their respective authors and are separate products.
