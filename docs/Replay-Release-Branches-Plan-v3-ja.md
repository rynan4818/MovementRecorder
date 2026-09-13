# リプレイ版のリリース計画・第3版（確定した5系統）

2026-09-13。第2版、1.37.4の境界追加への了承、および「1.37.4を0.3.2として以降を繰り上げる」という指定をまとめた実施計画です。過去の計画書は経緯として残し、版番号と配布方針は本書を優先します。

## ブランチとリリース

| ブランチ | MOD / タグ | Beat Saber対応範囲 | manifest.gameVersion |
| --- | --- | --- | --- |
| BS1.29.1 | 0.3.0 / v0.3.0 | 1.29.0-1.29.1 | 1.29.0 |
| BS1.37.1 | 0.3.1 / v0.3.1 | 1.37.1-1.37.3 | 1.37.1 |
| BS1.37.4 | 0.3.2 / v0.3.2 | 1.37.4-1.39.1 | 1.37.4 |
| BS1.40.0 | 0.3.3 / v0.3.3 | 1.40.0-1.40.8 | 1.40.0 |
| main | 0.3.4 / v0.3.4 | 1.42.0-1.44.1 | 1.42.0 |

**5つのリリースにZIPを1つずつ添付します。0.3.1に2つのZIPを添付する案は置き換えました。** 下限は1.29.0、1.44.2以上は未対応です。最新版はmainとし、1.44.2以上の対応を始める際に現在のmainからBS1.42.0を作成します。今回はBS1.42.0を作成しません。

利用者の少ない中間世代を独立系統として増やさない判断と、将来の利用増を見込んで1.42系を残す判断を維持します。統計の対象・取得日・利用者数は[調査記録](Replay-Release-Evidence-2026-09-13.json)を参照してください。

## ソースの分離

Legatoは導入せず、各ブランチを対象世代のAPIへ直接移植します。1つのゲーム用DLLに複数世代の呼び出しを切り替える処理は追加しません。Camera2、ScoreSaber、BeatLeader等の任意MODとの連携は、既存の省略可能な連携方式を維持します。

- 1.37.1：BeatmapKey / BeatmapLevel、譜面データの非同期読込、BSMLのDI、Tweening DLL、現在の記録用探索設定に対応。
- 1.37.4：FlowCoordinator / Screenが移動したBeatSaber.ViewSystemを参照し、BSMLの公開プロパティとSaber.movementDataForLogicを使用。
- 1.40.0：開始時のライト色設定、PauseState列挙型、可変NJSのシーク、SongCoreの新しい譜面APIに対応。
- 1.42.0：GameplayAdditionalInformationを使う開始処理、BeatSaber.Destinationsの終了API、シーン内のTimeHelperを使用。

旧標準のCustomAvatars探索設定だけを移行し、編集済み設定とHMDオフセットは保持します。既存のmvrec形式は変更しません。

## manifestとビルド

| MOD | BSIPA | SiraUtil | BeatSaberMarkupLanguage | SongCore |
| --- | --- | --- | --- | --- |
| 0.3.0 | ^4.3.6 | ^3.1.2 | ^1.6.10 | ^3.11.1 |
| 0.3.1 | ^4.3.6 | ^3.1.11 | ^1.11.4 | ^3.14.11 |
| 0.3.2 | ^4.3.6 | ^3.1.12 | ^1.12.2 | ^3.14.14 |
| 0.3.3 | ^4.3.6 | ^3.2.1 | ^1.12.5 | ^3.15.2 |
| 0.3.4 | ^4.3.7 | ^3.3.1 | ^1.14.1 | ^3.16.0 |

承認済みMODの情報と実DLLを照合しました。manifestのversion、AssemblyVersion、AssemblyFileVersionを上表のMOD番号に揃え、gameVersionは対応範囲の開始版にします。Camera2はloadAfterだけに指定し、Camera2 / CameraPlusを必須依存にしません。

Visual Studioとスクリプトは同じ従来形式のcsprojとBeatSaberModdingTools.Tasks 2.0.0-beta1を使用します。ZIP名はModding Toolsに任せ、生成後の名前変更や独自のZIP作成は行いません。

| MOD | 標準ZIP名（commitはビルド時のGit短縮ID） |
| --- | --- |
| 0.3.0 | MovementRecorder-0.3.0-bs1.29.0-commit.zip |
| 0.3.1 | MovementRecorder-0.3.1-bs1.37.1-commit.zip |
| 0.3.2 | MovementRecorder-0.3.2-bs1.37.4-commit.zip |
| 0.3.3 | MovementRecorder-0.3.3-bs1.40.0-commit.zip |
| 0.3.4 | MovementRecorder-0.3.4-bs1.42.0-commit.zip |

配布物はPlugins/MovementRecorder.dll、利用説明と必要なライセンスです。LiteDB / System.BuffersはDLLへ埋め込みます。1.37系以降のビルド用Publicizerはobj内の参照コピーだけを処理し、ゲーム本体のDLLを改変・同梱しません。通常のビルドで実ゲームへDLLを配置する処理は無効です。

## Git操作と公開準備

了承済みの順序でBS1.29.1からBS1.37.1、BS1.37.4、BS1.40.0を作成し、専用作業ツリーで移植します。既存mainにはBS1.40.0をno-fast-forwardでマージして0.2.7までの履歴・記録機能を残し、1.42系へ移植します。各移植とリリース準備を日本語でローカルコミットします。Playerブランチ・旧タグは保持します。

GitHubの本文は[release-notes](release-notes)にv0.3.0からv0.3.4まで用意します。既存の「Support for Beat Saber」「日本語の変更箇条書き」「他の対応版への案内」「README」「Full Changelog」の形式を引き継ぎます。

この作業で行うのはローカルでの準備です。push、タグ作成、GitHubリリース公開、実ゲームへのインストールは含みません。公開時には各タグが対応ブランチの確定コミットを指すようにし、そのコミットから生成した標準ZIPを1つ添付します。

## 検証と残る確認

5系統のテスト・Releaseビルド・埋め込みmanifest・パッケージ内容を検証します。対象範囲にある手元の26バージョンについて、コンパイル済みDLLの型とメソッドの定義アセンブリ・署名を照合します。内部APIの型やポーズ状態、モデル・UI・保存抑止の契約も別途確認します。

ゲームのみのフォルダーにはMODを書き込まず、作業ツリー内の参照専用MODを使います。DLLの静的照合で確認できるのはAPI互換性です。HMD上の表示・操作、シーク後の音声・モデル・NJS、各アバターMOD、床ミラー、Camera2のREPLAY切替、通常プレイとリプレイの保存抑止切替は実機で確認します。確認結果とビルド手順は[リリース準備](Replay-Release-Preparation-ja.md)にまとめます。
