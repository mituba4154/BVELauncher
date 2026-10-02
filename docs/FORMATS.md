# BVE形式と互換性

## 対応範囲

| 形式 | 解析内容 | 制約 |
| --- | --- | --- |
| Scenario 2.00 | Title / RouteTitle / VehicleTitle / Author / Comment / Route / Vehicle / Image、重み付き候補 | 複数候補は参照グラフへ全候補を登録。起動時の抽選はBVEに任せる |
| Vehicle 2.00 | PerformanceCurve / Parameters / Panel / Sound / MotorNoise / ATS / Ats32 / Ats64など | 未知キーは保存、DLLは実行しない |
| Map | Include / Structure.Load / Sound.Load / Station.Load / Signal.Load / Train.Loadなど | 実行・式評価をしない静的best-effort解析。変数経由の動的参照は断定しない |
| Parameters / Performance | ヘッダー識別、INI参照、Force / Current / Params表 | 物理計算やBVE構文の完全validationは行わない |
| Panel | Instrument Panelヘッダー、各セクションの画像参照 | 画像以外のパネル描画はBVEに任せる |
| Vehicle Sound / Motor Noise | 音声・表参照 | 再生や音源の検証はしない |
| Structure / Sound / Station / Signal list、Train | CSV・INIの参照 | 複雑な式はUnknownとして保持 |
| DLL / Image / WAV / X | 種別と静的参照、DLL PE architecture / version | 未知DLLをロードしない |

`BveDocument`は原文の行、改行、BOM、encoding、キー順、キー表記、コメント、未知フィールドを保持します。
`#` / `;`のコメント、大文字小文字、空行、余分な空白、空フィールドを許容します。
UTF-8 / BOM、UTF-16 BOM、宣言されたencodingに対応します。
宣言なしでUTF-8として読めない旧データはCP932を試します。
先頭ヘッダーのencoding指定に続くCSV区切り・コメントは除外します。
再保存のテストではUTF-8、UTF-16、CP932、Windows-1252の変更前バイト列が完全一致することを確認します。

Mapは文字列・コメントを認識するlexerで複数行Loadを調べます。
コメントや引用文字列内のLoadを実行文と誤認しないようにします。
解析できない部分は元データに残し、問題を診断として表示します。
壊れたファイルが1件あっても全走査を止めません。
Scenarioヘッダーを認識できた不正なシナリオはファイル名で表示し、ParseFailedを付けます。
種別すら確認できないファイルはシナリオと推測しません。

## 車両差し替え

元シナリオと同じディレクトリへ`.BVELauncher-<GUID>.txt`を作り、Vehicle / VehicleTitleだけを書き換えます。
同一ディレクトリの相対Route / Image参照はそのままです。
このファイルは通常のライブラリ走査に表示しません。
同じディレクトリに書けない場合は、同一ドライブのGeneratedへRoute / Image / Vehicle参照をリベースします。
別ドライブの絶対パス互換性は未検証のため、安全なリベースができないときは明示的なエラーになります。
BVE起動終了または次回起動時のlease復旧で、所有する生成ファイルだけを清掃します。

## 診断と環境選択

欠損参照、Panel / Sound不足、DLL architecture不一致などを参照グラフに沿って表示します。
ATS32 / Ats64候補と検出済み5 / 6環境から推奨を作りますが、アドオンの完全互換性保証ではありません。
UnknownはUnknownとして表示します。
警告は起動を禁止せず、個別BVE指定で推奨を上書きできます。
シナリオまたは実行ファイルが存在しない場合は起動前にエラーにします。

## 根拠となる公式資料

- [Scenario](https://www.bvets.net/jp/edit/formats/scenario.html)
- [Vehicle](https://www.bvets.net/jp/edit/formats/vehicle/vehicle.html)
- [Map](https://www.bvets.net/jp/edit/formats/route/map.html)
- [Parameters](https://www.bvets.net/jp/edit/formats/vehicle/params.html)
- [Performance](https://www.bvets.net/jp/edit/formats/vehicle/perform.html)
- [Performance table](https://www.bvets.net/jp/edit/formats/vehicle/performtable.html)
- [Instrument panel](https://www.bvets.net/en/edit/formats/vehicle/panel.html)
- [Vehicle sound](https://www.bvets.net/jp/edit/formats/vehicle/sound.html)

仕様と実機で確認したバージョンは5.8.7554.391 / 6.0.7554.619です。
将来のヘッダーは未知データとして扱い、BVEに代わって実行しません。
