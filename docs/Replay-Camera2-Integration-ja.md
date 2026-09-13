# Camera2のREPLAYシーン連携：実装・検証報告

日付：2026-09-13。対象：MovementRecorder `BS1.29.1`、Beat Saber 1.29.1、Camera2 0.6.108。

## 変更内容

- MovementRecorderをCamera2の公開リプレイAPIへ登録し、REPLAYシーンへのカメラ割り当てに対応した。
- 初期HMD姿勢を設定し、GameCoreへ移る前のセッション開始イベントで有効化する。準備・再生・ポーズ・シーク・末尾停止中は有効状態を維持し、退出完了時に無効化・登録解除する。
- Camera2.dllへのアセンブリ参照・必須依存・同梱は追加していない。読み込み済みCamera2が存在する場合だけリフレクションで公開APIを取得する。manifestの `loadAfter` は両方が存在する場合の順序指定のみ。
- CameraPlusのみ／カメラMODなしでは連携を省略する。公開APIの不足や呼び出し失敗は一度ログへ出し、リプレイ本体を継続する。登録処理が途中で失敗した場合にも、無効化と解除をそれぞれ試みる。
- 一人称カメラへ現在のHMDの姿勢を渡す。元HMDの親に適用済みのルーム補正を使い、鑑賞カメラの移動量は含めない。録画時の一人称視点の再現は追加していない。

Camera2のシーン選択を強制するパッチは加えていない。REPLAYが空の場合のPLAY等へのフォールバック、FPFC、カスタムシーンを維持する設定はCamera2本来の処理を利用する。ScoreSaber・BeatLeaderの状態フラグも書き換えない。

## 実装箇所と更新順

`Camera2ReplayInterop` をAppのInstallerで一度だけ生成し、`MovementReplay.SessionChanged` に接続した。リプレイごとに `GenericSource("MovementRecorderReplay")` を作り、公開 `Register`、`SetActive`、`Update`、`Unregister` を利用する。姿勢更新と有効化は型を照合して取得したデリゲートで呼ぶ。

`ReplaySession.Begin` が `StartStandardLevel` より前に通知する既存の順序を利用する。終了通知のセッションIDと姿勢更新元のIDを照合し、以前のリプレイからの通知で現在の登録を解除・更新しない。

姿勢は `SpectatorRig` の元HMDカメラから受け取る。元カメラの描画を無効にした後も、その参照を維持する。ルーム位置・回転は親Transformに既に反映されているため、ワールド姿勢をそのまま使う。非有限値・無効なQuaternionは通知せず、直前の有効姿勢を保持する。

Camera2 0.6.108は通常の `LateUpdate` でカメラの追従先を計算する。その前に、実行順-1000の `PlaybackRuntime.LateUpdate` でHMD姿勢を更新する。既存のHMD更新時と `Application.onBeforeRender` でも姿勢を渡す。カメラが有効化された直後の描画には、開始時に設定した有効な姿勢が使われる。

## 検証結果

- 自動テスト126件成功、失敗・スキップ0件。連携と姿勢に関する17件を追加した。
- Visual StudioのReleaseビルド成功。既存のMSB3277参照バージョン競合警告は残る。
- 導入済みDLLとのAPI照合261項目成功。Camera2の5つの公開APIの引数・戻り値・公開範囲と、IPAのプラグイン取得APIを含む。
- 生成したMovementRecorder.dllのAssemblyRefにCamera2・CameraPlusが含まれないことを確認した。
- manifestにCamera2・CameraPlusの必須依存がなく、`version=0.2.4`、`gameVersion=1.20.0` が維持されることを確認した。

追加テストは、開始時の初期姿勢、状態の維持、重複登録防止、連続再生・古い通知の無視、起動失敗相当の終了、他MODの登録の保持、破棄時の購読解除、Camera2不在、CameraPlusのみのプラグイン検出、旧API・不正なシグネチャ、初期HMD不在、非有限な姿勢、SDK呼び出し中の例外、ルーム補正と鑑賞位置の分離を検証する。

テストではUnityとCamera2の代替APIを用い、実際の製品クラスを実行している。実ゲームの描画・XRの実行順・CameraPlusそのものの描画動作を再現したものではない。API照合も実機表示の確認とは区別する。

検証記録：`artifacts/tests/camera2-replay.trx`、`artifacts/build-camera2-replay.log`、`artifacts/contracts-camera2-replay.json`。

## 動作確認用ファイル

- ZIP：`artifacts/MovementRecorder-Replay-BS1.29.1-camera2.zip`
- DLL：`MovementRecorder/bin/Release/MovementRecorder.dll`
- DLL SHA-256：`26D4212310E5B249C692D5460EB0791B9F99BB9332C3B85492AFF6C554EB5D94`

ZIPには、直前のリプレイメニュー・コピー元表示設定・選択行の1行表示の修正も含む。

実機では、REPLAYだけに割り当てた固定カメラの表示、ポーズ・シーク時の維持、メニュー復帰と次の通常プレイでの切り替え、一人称カメラの視点、CameraPlusのみの環境、ScoreSaber・BeatLeaderのリプレイを確認する。現在、ゲーム内での表示確認は未実施。

ゲームへの配置、コミット、pushは行っていない。
