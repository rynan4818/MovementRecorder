# リプレイUI英語化：設計計画書・変更前後対照表

作成日：2026-09-13  
対象：MovementRecorder / ブランチ `BS1.29.1`  
調査範囲：`cf6c13dcd8f9a6a89d9361ecb82c3ba3947f963d` より後から、現在の `2171389` までに追加されたリプレイ機能  
状態：**2026-09-13にユーザー了承済み。実装・自動検証完了。英語UIの実機表示は未確認。**

## 方針

リプレイ関連の日本語UIを以下の英語に置き換える。対象はボタン、設定名、補足説明、一覧・選択状態、進捗、完了通知、画面に表示されるアプリ自身のエラー文言。用語は Replay、recording、View Settings、Source Avatar、Model Mapping、Saber Anchor に統一する。

以下は重複をまとめた全147項目の対照表。`{filename}`、`{date}`、`{count}`、`{reason}`、`{path}` などは実際の値が入る箇所を表し、画面に波括弧は出さない。数値の桁数・小数表示、ファイル名・曲名・モデル名・階層パスは現在のデータを使う。状態文を連結するときの改行も維持する。

- `Source Avatar` は普段のアバターMODが表示するコピー元モデル、`View Offset` はHMDの鑑賞位置オフセットを指す。
- `Recorded Root` と `Scene Root` は記録側と現在のシーン側のルート。`Saber Anchor` はセイバーを再生するための固定基準。
- 一覧は日付・記録秒数・対象数・サイズを1行で表示し、選択行と頭の移動距離の水色表示を維持する。
- 英語の長さに合わせ、必要な箇所だけボタン幅・文字サイズ・説明文の折返しを調整する。オフセットの0.1m刻みと設定値の保存・復元は現在の動作を維持する。
- 内部の設定キー、BSMLのバインド名、記録ファイル形式、API名は表示文言と分離する。
- ログだけに出るCamera2連携警告、頭の移動距離DBの診断、表情同期・復元の警告は今回のUI翻訳対象外。UIとログで同じ例外文を共有する箇所は、両方が英語になる。
- OS・ゲーム・他MOD・外部ライブラリが生成する例外の詳細は原文を保ち、その前に付く本MODの案内を英語にする。
- 指定範囲内で追加され、MODタブに表示される記録保存失敗の通知も含める。

## 1. リプレイメニューと鑑賞設定

対象：`MovementRecorder/Views/SettingTabViewController.bsml`、`MovementRecorder/Playback/UI/ReplayFileViewController.bsml`、`MovementRecorder/Playback/UI/ReplayFileViewController.cs`、`MovementRecorder/Playback/UI/ReplayFileFlowCoordinator.cs`

| 変更前 | 変更後 |
| --- | --- |
| リプレイ | Replay |
| MovementRecorder リプレイ | MovementRecorder Replay |
| 一覧を更新 | Refresh List |
| キャッシュ再構築 | Rebuild Cache |
| 詳細 | Details |
| 鑑賞設定 | View Settings |
| 一覧へ | Back to List |
| リプレイ開始 | Start Replay |
| 読込中止 | Cancel Load |
| 記録ファイル未選択 | No recording selected |
| 選択中: {filename} | Selected: {filename} |
| 選択中  {date}  /  {details} | Selected  {date}  /  {details} |
| 頭の移動距離（参考） | Head Travel (Reference) |
| {duration} 秒 / {objects} 対象 / {size} MiB | {duration} s / {objects} objects / {size} MiB |
| {frames} フレーム / {objects} 対象 / {start}–{end} 秒 | {frames} frames / {objects} objects / {start}–{end} s |
| 一覧から記録を選択してください。 | Select a recording from the list. |
| 読込不可: {reason} | Cannot load: {reason} |
| No Fail（最後まで鑑賞） | No Fail (Play to End) |
| コピー元アバターを表示 | Show Source Avatar |
| コピー元アバターにHMDオフセットを適用 | Apply HMD Offset to Source Avatar |
| コピー元を表示する場合だけ適用します。変更は次のリプレイ開始時に反映されます。 | Only applies when the source avatar is shown. Changes take effect when the next replay starts. |
| 鑑賞位置・左右 (m) | View Offset: Left / Right (m) |
| 鑑賞位置・上下 (m) | View Offset: Up / Down (m) |
| 鑑賞位置・前後 (m) | View Offset: Forward / Back (m) |

## 2. リプレイ中の操作

対象：`MovementRecorder/Playback/UI/ReplayControlsViewController.bsml`、`MovementRecorder/Playback/UI/ReplayControlsViewController.cs`、`MovementRecorder/Playback/UI/ReplayMenuService.cs`

| 変更前 | 変更後 |
| --- | --- |
| 一時停止 | Pause |
| 再開 | Resume |
| 先頭へ | Back to Start |
| −5秒 | −5 s |
| ＋5秒 | +5 s |
| 再生位置 | Playback Position |
| 左右 (m) | Left / Right (m) |
| 上下 (m) | Up / Down (m) |
| 前後 (m) | Forward / Back (m) |
| モデルの対応設定 | Model Mapping |
| 曲選択へ戻る | Back to Song Selection |
| 曲選択へ | Song Selection |
| スコア・プレイ履歴は保存しません。HDTの距離記録は通常どおりです。 | Scores and play history are not saved. HDT tracks distance as usual. |

## 3. モデルの対応設定

対象：`MovementRecorder/Playback/UI/ReplayControlsViewController.bsml`、`MovementRecorder/Playback/UI/ReplayControlsViewController.cs`

| 変更前 | 変更後 |
| --- | --- |
| 記録ルート | Recorded Root |
| シーンのルート | Scene Root |
| ルート対応を保存 | Save Root Mapping |
| このルートを省略 | Skip This Root |
| 個別の記録対象 | Recorded Object |
| 対応先のフルパス | Target Full Path |
| 個別の対応を保存 | Save Object Mapping |
| 左セイバー基準 | Left Saber Anchor |
| 右セイバー基準 | Right Saber Anchor |
| 消失した対象を最後の姿勢で表示 | Keep Missing Objects at Last Pose |
| 自動設定に戻す | Reset to Auto |
| 再確認 | Recheck |
| 操作に戻る | Back to Controls |
| 記録と同じモデルを普段のMODで読み込んでください。 | Use your usual mod to load the same model used in the recording. |
| ルート対応またはセイバーの固定基準を選択できます。 | Choose a root mapping or a fixed saber anchor. |
| 対応先のルートを一意に選択してください。 | Select a single matching target root. |
| ルート対応を保存しました。再確認してください。 | Root mapping saved. Select Recheck. |
| 対応先パスはシーン内の一意なTransformを指定してください。 | The target path must identify exactly one Transform in the scene. |
| 個別の対応を保存しました。再確認してください。 | Object mapping saved. Select Recheck. |
| 左セイバーの固定基準を保存しました。 | Left saber anchor saved. |
| 右セイバーの固定基準を保存しました。 | Right saber anchor saved. |
| このルートを省略します。セイバーの記録は省略できません。 | This root will be skipped. Saber recordings cannot be skipped. |
| 対応を自動設定に戻しました。 | Automatic mapping restored. |

## 4. 読込・開始・終了の案内

対象：`MovementRecorder/Playback/UI/ReplayMenuService.cs`、`MovementRecorder/Playback/UI/ReplayFileFlowCoordinator.cs`、`MovementRecorder/Playback/ReplaySession.cs`、`MovementRecorder/Models/RecordData.cs`

| 変更前 | 変更後 |
| --- | --- |
| 記録ファイルを選択してください。 | Select a recording file. |
| Soloで曲と難易度を選択してください。 | Select a song and difficulty in Solo. |
| 記録ファイルを確認しています… | Scanning recordings… |
| {chart} の記録は見つかりません。 | No recordings found for {chart}. |
| {chart}: {count} 件の記録。スコア・履歴は保存しません。 | {chart}: {count} recordings. Scores and play history are not saved. |
| 一部のフォルダを確認できませんでした。 | Some folders could not be scanned. |
| 一覧を更新できません: {reason} | Cannot refresh the list: {reason} |
| 記録を読み込んでいます… | Loading recording… |
| 初期版は両手セイバーのStandard譜面に対応しています。 | This version supports two-saber Standard maps only. |
| 必須拡張がある譜面は初期版の対象外です: {requirements} | This version does not support maps with required extensions: {requirements} |
| 直前の記録の保存完了を待っています… | Waiting for the previous recording to finish saving… |
| 左右のセイバーを記録したファイルを選択してください。 | Select a recording that includes both sabers. |
| リプレイを開始します… | Starting replay… |
| 選択中の譜面が変わりました。記録を選び直してください。 | The selected map has changed. Select a recording again. |
| 選択した記録が変更されました。一覧を更新して選び直してください。 | The selected recording has changed. Refresh the list and select it again. |
| リプレイが終了しました。スコア・プレイ履歴は保存していません。 | Replay finished. Scores and play history were not saved. |
| リプレイを開始できません: {reason} | Cannot start replay: {reason} |
| 読み込みを中止しました。 | Loading canceled. |
| リプレイメニューを閉じられません。 | Cannot close the replay menu. |
| リプレイは既に実行中です。 | A replay is already running. |
| スコア送信の抑止を準備できません。 | Cannot disable score submission. |
| 記録ファイルの保存に失敗しました。ログを確認してください。 | Failed to save the recording file. Check the log. |

## 5. 再生状態・モデル対応の案内

対象：`MovementRecorder/Playback/UI/ReplayControlsViewController.cs`、`MovementRecorder/Playback/Runtime/PlaybackRuntime.cs`、`MovementRecorder/Playback/Models/ModelBinding.cs`

| 変更前 | 変更後 |
| --- | --- |
| 準備しています… | Preparing… |
| モデルを準備しています… | Preparing models… |
| リプレイ：スコア・履歴は保存しません。 | Replay: Scores and play history are not saved. |
| 軌跡など一部の追加エフェクトを省略しています。 | Some extra effects, such as trails, are omitted. |
| {count} 件の対応を確認しています… | Checking {count} mappings… |
| モデルを自動で対応付けできません。対応設定でルートを選ぶか、同じモデルを読み込んで再確認してください。 | Cannot map models automatically. Select a root in Model Mapping, or load the same model and select Recheck. |
| 曲の準備が完了しませんでした。曲選択へ戻って再試行してください。 | Song setup did not finish. Return to song selection and try again. |
| 記録と音声の再生範囲が重なりません。 | The recording and audio have no overlapping playback range. |
| 再生が終了しました。前へ戻して再生できます。 | Playback finished. Seek back to play again. |
| シーク後のスコアは、この位置からの区間で計算します。 | After seeking, the score is calculated from this position. |
| シーンのモデル数が探索上限を超えています。 | The scene exceeds the model search limit. |
| 保存した対応先の階層が変わりました。ルートを選び直してください。 | The saved target hierarchy has changed. Select the root again. |
| 対応するモデルが見つかりません。 | No matching model found. |
| 対応するモデルが複数あります。 | Multiple matching models found. |
| 複数の記録が同じTransformに対応しています。 | Multiple recorded objects map to the same Transform. |

## 6. 記録ファイルの検証エラー

対象：`MovementRecorder/Playback/Data/MovementFileReader.cs`

| 変更前 | 変更後 |
| --- | --- |
| 記録ファイルが更新されました。一覧を更新して選び直してください。 | The recording file has changed. Refresh the list and select it again. |
| 記録フレームがありません。 | The recording contains no frames. |
| 記録データが再生用メモリの上限を超えています。 | The recording exceeds the playback memory limit. |
| 記録の曲時刻が不正、または逆行しています。 | Recorded song timestamps are invalid or out of order. |
| 姿勢データが不正です（フレーム {frame}、対象 {track}）。 | Invalid pose data (frame {frame}, object {track}). |
| 欠損イベントの時刻が記録範囲外です。 | A missing-object event is outside the recording's time range. |
| 記録範囲の曲時刻が不正です。 | The recording's song time range is invalid. |
| メタデータが途中で切れています。 | The metadata is truncated. |
| メタデータの末尾に余分な内容があります。 | The metadata has unexpected trailing content. |
| 記録の対象件数・スケール・フレーム数が不正です。 | The recording's object count, scales, or frame count are invalid. |
| 記録ファイルの長さとヘッダーが一致しません。保存途中または未対応の形式です。 | The file size does not match the header. The recording is incomplete or uses an unsupported format. |
| 記録対象のパスが不正です。 | A recorded object's path is invalid. |
| 記録の初期スケールが不正です。 | A recorded initial scale is invalid. |
| 記録のモデル設定が不正です。 | The recorded model settings are invalid. |
| 空のモデル設定があります。 | A model settings entry is empty. |
| モデルの検索設定が上限を超えています。 | The model search settings exceed the limit. |
| ルート検索設定が長すぎます。 | The root search pattern is too long. |
| 対象の欠損イベントが不正です。 | A missing-object event is invalid. |
| 記録の難易度表記が一致しません。 | The recording's difficulty labels do not match. |
| メタデータの長さが不正です。 | The metadata length is invalid. |
| メタデータのサイズが上限を超えています。 | The metadata exceeds the size limit. |

## 7. セイバー・カメラ・アバターの実行時エラー

対象：`MovementRecorder/Playback/Runtime/PlaybackSeekController.cs`、`MovementRecorder/Playback/Runtime/RecordedSaberDriver.cs`、`MovementRecorder/Playback/Runtime/SpectatorRig.cs`、`MovementRecorder/Playback/Runtime/SpectatorInput.cs`、`MovementRecorder/Playback/Runtime/SpectatorCameraClone.cs`、`MovementRecorder/Playback/Models/RenderModelClone.cs`、`MovementRecorder/Playback/Models/SourceAvatarOffset.cs`

| 変更前 | 変更後 |
| --- | --- |
| シークが重複しています。 | A seek is already in progress. |
| 曲の時刻を変更できません。 | Cannot change the song position. |
| 未対応の譜面オブジェクトが残っています。 | Unsupported map objects remain in the scene. |
| 左右のセイバーには別々の記録が必要です。 | The left and right sabers need separate recorded tracks. |
| セイバーの先頭姿勢がありません。 | The recording has no initial saber pose. |
| 両手セイバーのStandard譜面を選択してください。 | Select a two-saber Standard map. |
| {saber} の固定ルートを一意に決められません。対応設定でセイバーの基準を指定してください。 | Cannot identify a unique fixed root for {saber}. Select a saber anchor in Model Mapping. |
| セイバー基準が固定されていません。動く骨ではなくモデルの固定ルートを選択してください。 | The saber anchor is not fixed. Select the model's fixed root instead of a moving bone. |
| セイバーの記録がこの時刻で途切れています。 | Saber data is missing at this time. |
| 実HMDの描画カメラを取得できません。 | Cannot find the live HMD rendering camera. |
| HMDカメラが消失しました。 | The HMD camera is no longer available. |
| ゲームのメニュー入力を取得できません。 | Cannot access the game's menu input. |
| セイバーを含まないメニュー用コントローラーが必要です。 | A menu controller without a saber is required. |
| 鑑賞カメラの複製先は非アクティブである必要があります。 | The target for the spectator camera clone must be inactive. |
| 鑑賞カメラの不要コンポーネントに循環依存があります。 | Unneeded spectator camera components have circular dependencies. |
| 鑑賞カメラの不要コンポーネントを除去できません。 | Cannot remove unneeded spectator camera components. |
| 再生できるメッシュがありません。未対応の描画だけで構成されたモデルです: {path} | No replayable mesh found. The model contains only unsupported renderers: {path} |
| MeshFilterがありません: {path} | Missing MeshFilter: {path} |
| {purpose} がコピー範囲の外にあります。モデル全体のルートを指定してください: {path} | {purpose} is outside the cloned hierarchy. Select the root of the entire model: {path} |
| LOD参照がモデルの外にあります。 | An LOD reference is outside the model. |
| 再生元のモデルがシーンから消失しました。 | The source model is no longer in the scene. |
| コピー元アバターのボーンが移動範囲の外にあります。モデル全体のルートを指定してください: {path} | A source avatar bone is outside the offset hierarchy. Select the root of the entire model: {path} |
| アバターのオフセット値が不正です。 | The avatar offset is invalid. |
| コピー元アバターの移動対象が消失しました。 | A source avatar object to offset is no longer available. |
| コピー元アバターの階層が変わりました。モデルを再準備してください。 | The source avatar hierarchy has changed. Set up the model again. |

## 8. 他MODとの互換性エラー

対象：`MovementRecorder/Playback/Compatibility/ReplaySaveGuards.cs`

| 変更前 | 変更後 |
| --- | --- |
| リプレイ状態 | replay state |
| 履歴の開始 | history initialization |
| 他のMODのリプレイが実行中です。終了してから選び直してください。 | Another mod's replay is running. End it and select a recording again. |
| {mod} の保存抑止に必要な処理を確認できません: {member} | Cannot find the API required to prevent {mod} from saving replay results: {member} |

## 日本語エラーのキャッシュ残留への対応

一覧キャッシュ `file-metadata-v1.json` は、読込不可ファイルのエラーメッセージも保存している。文言だけを変更すると古い日本語が再表示されるため、`MovementRecorder/Playback/Data/MovementFileCatalog.cs` の `ReaderVersion` と受入判定を1から2に更新する。

英語版で最初に一覧を開くと記録のヘッダーを再読込し、英語の結果でキャッシュを保存する。記録データ、対応設定、保存済みオフセット値はそのまま使用する。初回の一覧更新だけ再読込の時間が必要になる。

## 承認後の作業と検証

1. 対照表に沿ってC#とBSMLの表示文言を変更し、一覧キャッシュのバージョンを更新する。
2. 既存のテストで日本語文言を確認している期待値を更新する。旧キャッシュの日本語エラーが再利用されないことを、キャッシュの読み直しで検証する。
3. メインの日本語操作ガイド `docs/Replay-ja.md` は説明を日本語のまま、引用するUIラベルを実際の英語表記に合わせる。
4. 残存する日本語文字列を画面への表示経路と照合し、置換漏れを確認する。BSMLの構文、既存テスト、ReleaseビルドとAPI照合を確認する。
5. 実機では一覧の1行表示・選択色、各設定とボタン、補足説明・エラーの折返しを確認対象にする。ビルドや自動テストの結果と、実機表示の確認結果は区別して報告する。

2026-09-13にこの計画書と英訳案の了承を受けて、実装を開始した。

## 実装・検証結果（2026-09-13）

承認された147項目を実装した。固定文言129項目と値を含む18項目を対照表と照合し、対象ソースに反映されていることを確認した。対象外のログ専用文言と、旧キャッシュを再現するテストデータの日本語は維持している。

- 一覧の1行表示に使う文字サイズを3から2.7へ調整した。選択色、表示項目、0.1m刻み、設定の保存・復元は維持している。
- 一覧キャッシュの `ReaderVersion` を2へ更新した。破損した記録の `InvalidDataException` が個別ファイル用の例外処理から漏れていたため、捕捉対象に追加した。該当ファイルの理由を英語で表示し、他の記録も一覧に出せるようにした。
- 旧バージョンのキャッシュから日本語エラーを読み直して英語へ更新し、次回の一覧表示では更新済みキャッシュを再利用するテストを追加した。
- 自動テスト：**156件成功、失敗0件**。`artifacts/tests/english-ui-final.trx` に記録。
- インストール済みゲームDLLとのAPI・埋込BSML照合：**283項目成功**。`artifacts/contracts-english-ui.json` に記録。
- 3画面分のBSML構文と、ソースの翻訳漏れを確認した。
- Releaseビルド成功。`artifacts/build-english-ui-final.log` に記録。既存の `MSB3277 / System.Net.Http` の参照競合警告は残る。
- DLL：`MovementRecorder/bin/Release/MovementRecorder.dll`。
- DLL SHA-256：`855EAD227247394FB3F9E9C1AC1569E3A88A5D269862734A7D54C710C15A1018`。

今回の英語UIについて、HMDでの文字切れ・折返し・操作感は実機未確認。
