# MovementRecorder再生機能：新しいチャットへの引き継ぎ

整理日：2026-09-13。元チャットID：`01a09417-fe49-7281-8877-391f7f2e4099`。

## 再開する目的

MovementRecorderの再生機能の実機テストと、不具合が見つかった箇所の修正を続ける。途中で調査していたCustomMenuMusicの暴走はAutoScreenShot側の修正で解消し、修正前へ戻すと再発することまで確認できた。新しいチャットではMovementRecorderを主題にする。

再生機能は既に実装され、多数の未コミット変更として作業フォルダに残っている。初めから実装し直す状態ではない。新チャット作成時点では、次に直す具体的な新しい症状はまだ報告されていない。最初は現状を確認して残る実機テストを短く提示し、ユーザーの次の報告から修正対象を決める。

## ユーザーの作業ルール

ユーザーから明示されたAGENTS.mdの指示：

> コードの作成・修正は設計計画書を作成し提示後、okが出てから作業開始すること
>
> リポジトリのコミット、マージ、リセットなど変更を加えるものは、どの様な操作をするのか具体的に分かりやすく説明してから、ok後に作業すること

再生機能の元の設計は承認済みで、これまでの実装・修正はその後に行っている。新たな修正は既存の承認範囲を確認し、必要な計画を提示する。新チャットの作成は、ブランチ変更・コミット・push・PR公開・ゲームDLLの自動置換の承認ではない。未コミットの変更を保持する。説明・設計書・PR原稿は日本語。

## 作業場所と現在の状態

| 対象 | パス・状態 |
| --- | --- |
| ワークスペース | `D:\PROGRAM\github\@Maintenance\CameraSongScript`。アプリの保存済みプロジェクト名はCameraSongScript。複数の独立リポジトリを含むフォルダ |
| MovementRecorder | `D:\PROGRAM\github\@Maintenance\CameraSongScript\MovementRecorder` |
| ブランチ・HEAD | `BS1.29.1` / `cf6c13dcd8f9a6a89d9361ecb82c3ba3947f963d`（整理時に確認） |
| ゲーム | `C:\Program Files (x86)\Steam\steamapps\common\Beat Saber`、1.29.1 |
| 記録 | ゲーム内 `UserData\MovementRecorder`。選択中のカスタム譜面内の `MovementRecorder` も探索対象 |
| ゲームログ | ゲーム内 `Logs\_latest.log`。過去分は日時付き `.log` / `.log.gz` |
| HDT DB | `C:\Users\Ryuichi\AppData\LocalLow\Hyperbolic Magnetism\Beat Saber\HMDDistance.litedb` |
| manifest | `version=0.2.4`、`gameVersion=1.20.0`。ユーザーが互換性確認後に変更するため、そのまま維持する |

整理時のGit状態は、Installers・RecordData・csproj・Plugin・設定タブ・READMEに変更があり、Playback・Tests・Audit・docs・scripts・licenses等が未追跡。既存の再生機能全体がこの差分に含まれる。Git操作は必ずMovementRecorderのリポジトリを対象にする。

## 確定済みの仕様

- 記録済みの3Dモデルの動きを、実際のHMDから第三者視点で鑑賞する。記録した一人称視点の再現は不要。
- 記録姿勢でゲーム本来の左右セイバーを動かす。ノーツ判定・スコア計算はゲームに任せ、記録時のスコアの再現は行わない。
- セイバーのモデルをコピーしない。表示・トレイルの生成や更新はゲームと現在のセイバーMODに任せる。SaberFactory専用処理にしない。観客の実際の手元にセイバーを追加しない。
- アバター等はMODのアセンブリに依存せず、ゲームに展開されたTransform・メッシュ描画・ボーン・LODをコピーする。コピー先でAnimator・IK・揺れもの等のスクリプトを動かさない。
- 記録のworld位置・回転と、ヘッダー `objectScales` の初期localScaleを使用する。ChroMapper用の倍率・原点補正はBeat Saberで適用しない。動的なスケール・表情・材質変化等、未記録の情報は再現対象外。
- MOVEMENT RECORDERタブに専用の「リプレイ」ボタンを置く。記録／再生モード切替にはしない。ファイル選択後にリプレイボタンで開始し、記録のEnabledとは独立させる。
- 一覧は選択中の曲・譜面種別・難易度と一致する記録を表示する。フォルダ内メタデータをキャッシュし、差分更新・再構築・中止に対応する。
- HDTの過去の頭の移動距離を参考値として表示する。HDTのアセンブリを参照せず、DBが読める場合だけ使う。曲・譜面・終了日時±3秒で一意に一致するものだけ採用し、候補不明・DBなし等は空欄。元DBは書き換えない。
- **HeadDistanceTravelled / HDT Counterの生成・計測・保存には干渉しない。リプレイ中のHDT距離記録は許容されている。**
- 成績・履歴の保存抑止はBeatLeader / ScoreSaberのリプレイと同等の範囲にする。MovementRecorder自身の再記録も抑止する。無関係なMODへの過剰な保存抑止は追加しない。
- 操作パネルは再生中に隠し、ポーズ時だけ表示する。準備エラー・再生完了等の停止時にも操作・退出できる。UIの左右コントローラーは実HMDと同じ観客原点で追跡する。
- シークはBeatLeader / ScoreSaberを参考にした同一シーン内の方式。音声・譜面・環境・判定状態を整え、シーク後もポーズを維持する。UIは先頭・前後5秒・スライダー・再開・鑑賞位置・退出。
- 現在の初期対応範囲はSolo / Standard / 両手 / 通常速度。必須拡張のある譜面や別骨格への変換は初期範囲外。

## これまでの修正と確認状況

| 項目 | 状態 |
| --- | --- |
| 一覧が空で選択できない | BSMLコンポーネント型と選択イベント引数を修正。ユーザーが表示・選択できることを確認済み |
| 起動時のCRITICAL大量発生 | HDTのInstallerを止めて必須依存を壊していたフックを削除。HDT非介入へ修正 |
| ゲームシーンで曲が始まらない | モデル準備、未対応描画、破棄済みTransform参照を修正。その後、独自トレイルに標準ResetTrailDataを呼んでいた問題も修正 |
| アバター・セイバー再生 | セイバー非複製・既存セイバー駆動へ変更後、ユーザーが正常な再生を確認済み |
| ポーズパネル・実手元のポインター | 修正実装・Releaseビルド・自動テスト済み。最新DLLは導入済みとハッシュで確認。左右ポインター・鑑賞位置・再ポーズ等の個別の実機結果は未報告 |
| 「ぶらんにゅーでぽじてぃぶ feat. ふたばこみなと」が1件だけ | 以前の実データ調査では8件のうちExpertが1件、Expert+が7件だった。難易度フィルターによる正しい表示。再度報告された場合は現在のファイルを確認する |
| リプレイ2回目等のメニューBGM暴走 | AutoScreenShotが複製したAudioListenerControllerの破棄で全体音声ポーズを残すことが原因。AutoScreenShot修正版でリプレイ2回目・3回目と通常プレイ開始前ポーズの暴走が解消し、修正前へ戻すと再発。MovementRecorderに全体音声ポーズを強制解除する処理を追加しない |

未報告の項目は「まだ個別に確認できていない」という意味で、現在も不具合があると断定しない。

## 最新配布物と検証記録

[ポーズUI修正版ZIP](D:/PROGRAM/github/@Maintenance/CameraSongScript/MovementRecorder/artifacts/MovementRecorder-Replay-BS1.29.1-pause-ui-fix.zip)

- DLL SHA-256：`96701EFAD9113422556735D45AA7CB25A945C970CC3E951AF020ACD6096714D5`
- ZIP SHA-256：`8DBE4D8A7084F1DE1FBBA8E4F0C1A93081583CBB57BC37588C854F27DB4C6BFF`
- 2026-09-13の整理時、導入済み `Plugins\MovementRecorder.dll` は上記DLLハッシュと一致した。
- 既存の検証結果：Releaseビルド成功、自動テスト65件成功、内部API・BSML等197項目照合、ZIP13エントリ検証。既存MSB3277警告あり。
- 整理時に既存のTRX・契約照合・ZIP検証記録とハッシュを確認した。テストやビルドは再実行していない。
- 自動テストはUnity等のテスト用実装を含み、本物のXR・描画・音声・イベント実行順を保証する試験ではない。

証拠はMovementRecorder内の `artifacts\tests\pause-ui-fix.trx`、`contracts-pause-ui-fix.json`、`package-pause-ui-fix-verification.json`、`build-pause-ui-fix.log`。`Replay-Implementation-ja.md` にある「ゲームへの配置未実施」は作成時点の記録であり、現在の導入状況は上記ハッシュ照合を優先する。

## 次に確認する項目

1. 再生中はパネルなし → メニューボタンでポーズ → 実際の左右の手元でポインター操作 → 再開 → 再ポーズ → 曲選択へ戻る。鑑賞位置変更・左右切替・スライダー・終了後の通常メニュー入力も確認する。
2. ポーズ中の前後5秒・スライダー・先頭・末尾から戻る操作。音声、モデル、セイバー、ノーツ、壁、アーク／チェーン、ライト演出の同期と、移動先の余分なMiss・遅延加点を確認する。
3. 通常終了・途中退出・モデル不一致時に、元のRenderer・カメラ・入力が復元されること。次の通常プレイの記録・スコア保存が正常であること。
4. リプレイ前後のゲーム成績・統計・プレイ回数、BL／SSの保存・送信、対応するプレイ履歴、MovementRecorder再記録の抑止を確認する。HDTの保存は許容する。
5. 複数モデル・別階層・同名候補・初期スケール、キャッシュ更新、HDT DBなし等。具体的な症状の報告があればその項目を優先する。

全項目を無条件に改修するのではなく、実機結果とソース・ログの証拠から必要な修正を決める。

## 最初に読む資料とコード

- [承認済み設計書](D:/PROGRAM/github/@Maintenance/CameraSongScript/docs/MovementRecorder_Replay_Design_BS1.29.1.md)
- [使い方・現行仕様](D:/PROGRAM/github/@Maintenance/CameraSongScript/MovementRecorder/docs/Replay-ja.md)
- [実装と検証報告](D:/PROGRAM/github/@Maintenance/CameraSongScript/MovementRecorder/docs/Replay-Implementation-ja.md)
- [直近のポーズUI修正](D:/PROGRAM/github/@Maintenance/CameraSongScript/MovementRecorder/docs/Replay-Pause-UI-Fix-ja.md)
- [既存セイバーを使う方式への修正](D:/PROGRAM/github/@Maintenance/CameraSongScript/MovementRecorder/docs/Replay-Native-Sabers-Fix-ja.md)

主な実装は `MovementRecorder\Playback` 以下。UIは `UI`、記録・キャッシュ・距離DBは `Data`、コピーと対応付けは `Models`、実行・セイバー・観客リグ・入力・シークは `Runtime`、保存抑止は `Compatibility\ReplaySaveGuards.cs`。

参考ソースはワークスペース直下の `beatleader-mod`、`scoresaber-plugin`、`SaberFactory`、`BeatSaberMarkupLanguage`、`ChroMapper-CameraMovement`、`HeadDistanceTravelled`。ゲームの逆コンパイル済みソースは `BeatSaber\SourceCode\1.29.1`。これらはユーザー提供の調査用資料。SaberFactoryを参考にしても、SaberFactory専用の依存・分岐は追加しない。

## 変更後のビルドと確認

MovementRecorderリポジトリを作業ディレクトリにして実行する。実装変更がない引き継ぎ作業だけでは再実行不要。

```powershell
.\scripts\Build-Replay.ps1 -GameDirectory 'C:\Program Files (x86)\Steam\steamapps\common\Beat Saber' -Package
dotnet test .\MovementRecorder.Tests\MovementRecorder.Tests.csproj
.\scripts\Test-ReplayContracts.ps1 -GameDirectory 'C:\Program Files (x86)\Steam\steamapps\common\Beat Saber'
```

Visual StudioのMSBuildと.NET Framework 4.7.2のターゲットパックを使用する。確認済みMSBuildは `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`。NuGetはユーザーのローカルキャッシュ。ビルドスクリプトはゲームへのコピー・Git操作・manifest変更を行わない。ゲーム参照DLLの読み取りやビルドにサンドボックス外実行が必要になった場合は、通常の承認ツールを使用する。

## 脱線していた2つのMODの状態（参照用）

AutoScreenShotの音声ポーズ修正は実機確認済み。詳細は[実機検証報告](D:/PROGRAM/github/@Maintenance/CameraSongScript/docs/AutoScreenShot_AudioPause_Validation_20260913.md)。コード差分は1ファイル、13行追加・1行削除。日本語PR原稿は `diagnostics\AutoScreenShot\PR-draft.md`。Git操作のOKはまだ得ていない。

CustomMenuMusicにも音声全体のポーズ中の自動曲送り抑止を1条件追加済み。Releaseビルド・IL比較済み、実機確認は未実施。日本語PR原稿は `CustomMenuMusic\PR-draft.md`、ZIPは `artifacts\CustomMenuMusic\audio-pause-fix\CustomMenuMusic-5.1.2-BS1.29.1-audio-pause-fix.zip`。診断コードは `diagnostics\CustomMenuMusic` に退避済み。Git操作・PR公開は未実施。

これらのPR作業は元チャットに残し、新しいMovementRecorderのチャットで自動的に再開しない。導入DLLの状態はユーザーが手動で差し替えるため、必要になった時点で再確認する。
