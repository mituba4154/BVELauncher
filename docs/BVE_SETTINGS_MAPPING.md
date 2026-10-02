# BVE標準設定の対応表

## 調査方法

対象は公式配布のBVE 5.8.7554.391（x86）と6.0.7554.619（x64）です。
5 / 6のPreferences XML、標準設定画面、設定項目の型・制約を調査し、独立したアダプターへ定義しました。
以下の表は実装の定義から生成し、118項目が両方のXML fixtureで読み出せることを自動テストします。
標準画面の選択肢と補助表示の細かなXML値を含みます。57項目はキー割り当てです。

`Verified`はXML構造・型・アダプターでの対応確認を示します。全118項目を実機GUIで個別変更済みという意味ではありません。
実機の操作確認、保存・復元、未検証範囲は[TESTING.md](TESTING.md)に区別して記録します。

## 保存先と保護

5: Documents\BveTs\Settings\Preferences.xml、6: Documents\BveTs\Settings\BveTs6.Preferences.xml。
DocumentsはWindowsのKnown Folderで解決するため、OneDriveや移動済みDocumentsにも対応します。
元ファイルのhashを照合し、バックアップ後に変更値だけをatomic replaceします。
未知XML、属性、コメント、改行、空白、要素順は保持します。DTD / 外部entityは拒否します。
BVEがそのPreferencesを使用中の場合は保存を拒否します。
設定ファイルがない場合は実機で初回設定を保存するか、選択したXMLを登録します。

## 項目対応

| Property | UI表示 | 分類 | XPath | 型 | 制約 / 選択肢 | 既定値 | 5 / 6 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Windowed | ウィンドウ表示 | Graphics | `/Preferences/Windowed` | Boolean | — | true | 対応 / 対応 |
| WindowWidth | ウィンドウ幅 | Graphics | `/Preferences/WindowSize/Width` | Integer | 1 .. 9999 | 800 | 対応 / 対応 |
| WindowHeight | ウィンドウ高さ | Graphics | `/Preferences/WindowSize/Height` | Integer | 1 .. 9999 | 600 | 対応 / 対応 |
| ScreenWidth | 画面幅（0＝デスクトップ） | Graphics | `/Preferences/ScreenSize/Width` | Integer | 0 .. 9999 | 0 | 対応 / 対応 |
| ScreenHeight | 画面高さ（0＝デスクトップ） | Graphics | `/Preferences/ScreenSize/Height` | Integer | 0 .. 9999 | 0 | 対応 / 対応 |
| DrawDistance | 描画距離 | Graphics | `/Preferences/DrawDistance` | Choice | 400, 600, 800, 1000, 1200, 1600, 2000, 2600, 3200, 4000, 5000, 6400, 8000, 10000 | 800 | 対応 / 対応 |
| Anisotropy | 異方性フィルター | Graphics | `/Preferences/Anisotropy` | Choice | 0, 1, 2, 4, 8, 16 | 0 | 対応 / 対応 |
| FontName | フォント | Assistants | `/Preferences/FontName` | Text | — | 空 | 対応 / 対応 |
| FontStyle | フォントスタイル | Assistants | `/Preferences/FontStyle` | Choice | Regular, Bold | Bold | 対応 / 対応 |
| FontSize | 補助表示の文字サイズ | Assistants | `/Preferences/Assistants/AssistantSettings[1]/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| HeadMotion | 頭の揺れ | Simulation | `/Preferences/HeadMotion` | Integer | 0 .. 100 | 25 | 対応 / 対応 |
| Language | BVEの言語 | General | `/Preferences/Language` | Choice | Japanese, English | Japanese | 対応 / 対応 |
| ScenarioDirectory | シナリオフォルダー | General | `/Preferences/ScenarioForm/Address/string[1]` | Path | — | 空 | 対応 / 対応 |
| InputPlugins | 入力プラグイン（|区切り） | Input | `/Preferences/InputPlugins` | Text | — | 空 | 対応 / 対応 |
| DevelopmentEnabled | 開発用機能 | Advanced | `/Preferences/DevelopmentEnabled` | Boolean | — | false | 対応 / 対応 |
| Assist_lever | ハンドル位置 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='lever']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_lever | ハンドル位置 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='lever']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_lever | ハンドル位置 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='lever']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_lever | ハンドル位置 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='lever']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_lever | ハンドル位置 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='lever']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_clock | 速度・時刻 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='clock']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_clock | 速度・時刻 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='clock']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_clock | 速度・時刻 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='clock']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_clock | 速度・時刻 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='clock']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_clock | 速度・時刻 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='clock']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_sta | 次駅情報 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sta']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_sta | 次駅情報 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sta']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_sta | 次駅情報 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sta']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_sta | 次駅情報 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sta']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_sta | 次駅情報 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sta']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_stop | 停止位置 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='stop']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_stop | 停止位置 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='stop']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_stop | 停止位置 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='stop']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_stop | 停止位置 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='stop']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_stop | 停止位置 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='stop']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_limit | 制限速度 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='limit']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_limit | 制限速度 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='limit']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_limit | 制限速度 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='limit']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_limit | 制限速度 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='limit']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_limit | 制限速度 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='limit']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_geom | 曲線・勾配 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='geom']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_geom | 曲線・勾配 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='geom']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_geom | 曲線・勾配 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='geom']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_geom | 曲線・勾配 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='geom']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_geom | 曲線・勾配 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='geom']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_sway | 乗客の揺れ 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sway']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_sway | 乗客の揺れ 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sway']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_sway | 乗客の揺れ 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sway']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_sway | 乗客の揺れ 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sway']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_sway | 乗客の揺れ 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='sway']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_inst | 運転指示 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='inst']/Visible` | Boolean | — | true | 対応 / 対応 |
| AssistAuto_inst | 運転指示 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='inst']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_inst | 運転指示 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='inst']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_inst | 運転指示 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='inst']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_inst | 運転指示 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='inst']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| Assist_detail | 内部計算値 表示 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='detail']/Visible` | Boolean | — | false | 対応 / 対応 |
| AssistAuto_detail | 内部計算値 自動配置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='detail']/AutoLocation` | Boolean | — | true | 対応 / 対応 |
| AssistSize_detail | 内部計算値 大きさ | Assistants | `/Preferences/Assistants/AssistantSettings[Name='detail']/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| AssistX_detail | 内部計算値 横位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='detail']/Location/X` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| AssistY_detail | 内部計算値 縦位置 | Assistants | `/Preferences/Assistants/AssistantSettings[Name='detail']/Location/Y` | Integer | -10000 .. 10000 | 0 | 対応 / 対応 |
| TimetableSize | 時刻表の大きさ | Assistants | `/Preferences/Timetable/Scale` | Integer | 8 .. 96 | 18 | 対応 / 対応 |
| KeyAxis0_0 | レバーサー ＋方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[1]/int[1]` | Key | 0 .. 65535 | 40 | 対応 / 対応 |
| KeyAxis0_1 | レバーサー 切方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[1]/int[2]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis0_2 | レバーサー －方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[1]/int[3]` | Key | 0 .. 65535 | 38 | 対応 / 対応 |
| KeyAxis0_3 | レバーサー 最小 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[1]/int[4]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis0_4 | レバーサー 切 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[1]/int[5]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis0_5 | レバーサー 最大 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[1]/int[6]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis1_0 | マスコン ＋方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[2]/int[1]` | Key | 0 .. 65535 | 81 | 対応 / 対応 |
| KeyAxis1_1 | マスコン 切方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[2]/int[2]` | Key | 0 .. 65535 | 65 | 対応 / 対応 |
| KeyAxis1_2 | マスコン －方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[2]/int[3]` | Key | 0 .. 65535 | 90 | 対応 / 対応 |
| KeyAxis1_3 | マスコン 最小 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[2]/int[4]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis1_4 | マスコン 切 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[2]/int[5]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis1_5 | マスコン 最大 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[2]/int[6]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis2_0 | ブレーキ ＋方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[3]/int[1]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis2_1 | ブレーキ 切方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[3]/int[2]` | Key | 0 .. 65535 | 188 | 対応 / 対応 |
| KeyAxis2_2 | ブレーキ －方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[3]/int[3]` | Key | 0 .. 65535 | 190 | 対応 / 対応 |
| KeyAxis2_3 | ブレーキ 最小 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[3]/int[4]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis2_4 | ブレーキ 切 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[3]/int[5]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis2_5 | ブレーキ 最大 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[3]/int[6]` | Key | 0 .. 65535 | 191 | 対応 / 対応 |
| KeyAxis3_0 | ワンハンドル ＋方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[4]/int[1]` | Key | 0 .. 65535 | 81 | 対応 / 対応 |
| KeyAxis3_1 | ワンハンドル 切方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[4]/int[2]` | Key | 0 .. 65535 | 65 | 対応 / 対応 |
| KeyAxis3_2 | ワンハンドル －方向 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[4]/int[3]` | Key | 0 .. 65535 | 90 | 対応 / 対応 |
| KeyAxis3_3 | ワンハンドル 最小 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[4]/int[4]` | Key | 0 .. 65535 | 49 | 対応 / 対応 |
| KeyAxis3_4 | ワンハンドル 切 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[4]/int[5]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeyAxis3_5 | ワンハンドル 最大 | Keyboard | `/Preferences/KeyAssignment/Axis/ArrayOfInt[4]/int[6]` | Key | 0 .. 65535 | 0 | 対応 / 対応 |
| KeySwitch0 | 一時停止 | Keyboard | `/Preferences/KeyAssignment/Switch/int[1]` | Key | 0 .. 65535 | 13 | 対応 / 対応 |
| KeySwitch1 | 警笛1 | Keyboard | `/Preferences/KeyAssignment/Switch/int[2]` | Key | 0 .. 65535 | 107 | 対応 / 対応 |
| KeySwitch2 | 警笛2 | Keyboard | `/Preferences/KeyAssignment/Switch/int[3]` | Key | 0 .. 65535 | 8 | 対応 / 対応 |
| KeySwitch3 | ミュージックホーン | Keyboard | `/Preferences/KeyAssignment/Switch/int[4]` | Key | 0 .. 65535 | 109 | 対応 / 対応 |
| KeyAts0 | 保安装置 0 | Keyboard | `/Preferences/KeyAssignment/Ats/int[1]` | Key | 0 .. 65535 | 32 | 対応 / 対応 |
| KeyAts1 | 保安装置 1 | Keyboard | `/Preferences/KeyAssignment/Ats/int[2]` | Key | 0 .. 65535 | 45 | 対応 / 対応 |
| KeyAts2 | 保安装置 2 | Keyboard | `/Preferences/KeyAssignment/Ats/int[3]` | Key | 0 .. 65535 | 46 | 対応 / 対応 |
| KeyAts3 | 保安装置 3 | Keyboard | `/Preferences/KeyAssignment/Ats/int[4]` | Key | 0 .. 65535 | 36 | 対応 / 対応 |
| KeyAts4 | 保安装置 4 | Keyboard | `/Preferences/KeyAssignment/Ats/int[5]` | Key | 0 .. 65535 | 35 | 対応 / 対応 |
| KeyAts5 | 保安装置 5 | Keyboard | `/Preferences/KeyAssignment/Ats/int[6]` | Key | 0 .. 65535 | 33 | 対応 / 対応 |
| KeyAts6 | 保安装置 6 | Keyboard | `/Preferences/KeyAssignment/Ats/int[7]` | Key | 0 .. 65535 | 34 | 対応 / 対応 |
| KeyAts7 | 保安装置 7 | Keyboard | `/Preferences/KeyAssignment/Ats/int[8]` | Key | 0 .. 65535 | 50 | 対応 / 対応 |
| KeyAts8 | 保安装置 8 | Keyboard | `/Preferences/KeyAssignment/Ats/int[9]` | Key | 0 .. 65535 | 51 | 対応 / 対応 |
| KeyAts9 | 保安装置 9 | Keyboard | `/Preferences/KeyAssignment/Ats/int[10]` | Key | 0 .. 65535 | 52 | 対応 / 対応 |
| KeyAts10 | 保安装置 10 | Keyboard | `/Preferences/KeyAssignment/Ats/int[11]` | Key | 0 .. 65535 | 53 | 対応 / 対応 |
| KeyAts11 | 保安装置 11 | Keyboard | `/Preferences/KeyAssignment/Ats/int[12]` | Key | 0 .. 65535 | 54 | 対応 / 対応 |
| KeyAts12 | 保安装置 12 | Keyboard | `/Preferences/KeyAssignment/Ats/int[13]` | Key | 0 .. 65535 | 55 | 対応 / 対応 |
| KeyAts13 | 保安装置 13 | Keyboard | `/Preferences/KeyAssignment/Ats/int[14]` | Key | 0 .. 65535 | 56 | 対応 / 対応 |
| KeyAts14 | 保安装置 14 | Keyboard | `/Preferences/KeyAssignment/Ats/int[15]` | Key | 0 .. 65535 | 57 | 対応 / 対応 |
| KeyAts15 | 保安装置 15 | Keyboard | `/Preferences/KeyAssignment/Ats/int[16]` | Key | 0 .. 65535 | 48 | 対応 / 対応 |
| KeySimulation0 | 視界を上に移動 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[1]` | Key | 0 .. 65535 | 104 | 対応 / 対応 |
| KeySimulation1 | 視界を下に移動 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[2]` | Key | 0 .. 65535 | 98 | 対応 / 対応 |
| KeySimulation2 | 視界を左に移動 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[3]` | Key | 0 .. 65535 | 100 | 対応 / 対応 |
| KeySimulation3 | 視界を右に移動 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[4]` | Key | 0 .. 65535 | 102 | 対応 / 対応 |
| KeySimulation4 | 視界を元に戻す | Keyboard | `/Preferences/KeyAssignment/Simulation/int[5]` | Key | 0 .. 65535 | 101 | 対応 / 対応 |
| KeySimulation5 | 視界の拡大 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[6]` | Key | 0 .. 65535 | 110 | 対応 / 対応 |
| KeySimulation6 | 視界の縮小 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[7]` | Key | 0 .. 65535 | 96 | 対応 / 対応 |
| KeySimulation7 | 視点の切り替え | Keyboard | `/Preferences/KeyAssignment/Simulation/int[8]` | Key | 0 .. 65535 | 86 | 対応 / 対応 |
| KeySimulation8 | 時刻表の表示 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[9]` | Key | 0 .. 65535 | 114 | 対応 / 対応 |
| KeySimulation9 | 走行速度の変更 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[10]` | Key | 0 .. 65535 | 116 | 対応 / 対応 |
| KeySimulation10 | 時間の早送り | Keyboard | `/Preferences/KeyAssignment/Simulation/int[11]` | Key | 0 .. 65535 | 118 | 対応 / 対応 |
| KeySimulation11 | シナリオの再読込 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[12]` | Key | 0 .. 65535 | 119 | 対応 / 対応 |
| KeySimulation12 | 定速運転 | Keyboard | `/Preferences/KeyAssignment/Simulation/int[13]` | Key | 0 .. 65535 | 80 | 対応 / 対応 |

## 標準画面と詳細値の関係

標準「グラフィック」画面はWindowed、WindowSize、ScreenSize、DrawDistance、Anisotropyへ対応します。
「補助表示」はFontName、FontStyle、文字サイズ、各Visibleへ対応します。
FontSizeは9種類の補助表示とTimetableのScaleを一括変更する標準操作です。個別Scale / AutoLocation / X / Yも詳細値として保持します。
「既定位置に戻す」は表示中の補助表示のAutoLocationをtrueに戻し、座標の再配置はBVEに任せます。
「シミュレーション」はHeadMotion、「入力デバイス」はInputPluginsと57キー、「高度な設定」はDevelopmentEnabledへ対応します。
LanguageはBVEの独立したLanguageメニュー、シナリオ選択の場所はScenarioForm.Address先頭stringへ対応し、後続履歴は保持します。
入力plugin固有の設定画面、アドオン独自設定、BVE内部値BackDrawDistanceなどは標準設定の対象外です。
第三者input pluginのDLLはランチャーからロードしません。

キー割り当てはキーの捕捉と数値入力、BVE標準TSV形式のローカルプリセット読み込みに対応します。
標準プリセットの内容自体は同梱しません。
画面サイズは整数、距離と異方性は確認した固定候補、HeadMotionは0 .. 100、文字サイズは8 .. 96です。
未知の既存選択値は読み出して表示しますが、アダプターの許容候補外に変更して保存することはできません。
