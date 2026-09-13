# セイバー表示とリプレイ開始の修正方針

更新日：2026-09-12

## 調査結果

`Logs/2026.09.12.20.59.56.log` の21:01:07では、178トラック・3ルートのモデル準備が完了した直後、`RecordedSaberDriver.PrepareHistory()` から呼んだ `SaberTrail.ResetTrailData()` がNullReferenceExceptionになった。

SaberFactoryの `SFTrail` は `SaberTrail` を継承するが、独自の `Init()`／`LateUpdate()` でモデルの刃元・刃先を読み取る。標準の `_movementData` を使わず、標準の `ResetTrailData()` も上書きしない。そのため、派生クラスを含めて標準リセットを呼ぶ現在の処理は成立しない。初期化待ちだけでは直らない。

ScoreSaberの `PosePlayer` は実際の左右 `Saber` に `OverridePositionAndRotation()` で姿勢を渡し、モデルやトレイルは通常の描画機構を使用する。BeatLeaderの `VRControllersInstantiator` も最初のプレイヤーには既存のコントローラーを使用し、`VirtualPlayer` が姿勢を反映する。両者とも、MovementRecorderのようにセイバーのRendererだけを組み直して表示する方式ではない。

同ログの再試行ではSiraUtilのFPFCが有効になり、21:01:45にカメラ取得にも失敗している。導入済みSiraUtil 3.1.2の `GameTransformFPFCListener` は `PlayerTransforms._headTransform` をカメラのない代理オブジェクトへ差し替える。描画カメラと論理上の頭を同一と仮定した探索が原因である。

## 修正内容

1. 記録とシーンの対応付けは維持し、左右の固定アンカーから実際の `Saber` のworld姿勢を求める。実コントローラーによるセイバー姿勢の更新を再生中だけ止め、ゲームの `SaberManager` 更新前に適用する。ノーツ判定・スコア計算はゲームへ渡す。
2. 左右 `Saber` の階層を描画コピー・元Rendererの非表示化から除外する。アバター／Otherのコピー元にセイバーが含まれる場合も、その枝を除外する。既存のセイバーモデル、材質、アニメーション、軌跡、パーティクルはゲームと導入MODに任せる。
3. 開始・シーク・再開時にトレイルのリセット、初期化、複製、停止・再生成を行わない。判定用のセイバー移動履歴のみを維持する。シーク直後のトレイルは各MODの更新に従い、記録されていない過去の軌跡の厳密な復元は行わない。
4. 鑑賞カメラはシーンの `MainCamera` から取得する。実HMDの姿勢＋鑑賞位置で第三者視点を維持し、記録された頭の姿勢で視点を動かさない。FPFC中はそのカメラ姿勢をXRで上書きせず、表示モードとUIポインターも追従させる。
5. 起動時の履歴準備完了・音声再開・最初の時刻進行をログに残す。終了・対応再試行時は取得した実コントローラーの状態を戻す。

SaberFactoryを含む特定のセイバーMODの型への参照やフックは追加しない。現在のゲームが通常表示するセイバーを使う。HMD側の実際の手にはセイバーを追加せず、操作用ポインターだけを用意する。HDT／HDT Counterの動作・記録には介入せず、manifestの `gameVersion = 1.20.0` を維持する。

## 検証

製品のコピー処理・セイバー駆動・鑑賞リグをテスト用Unityオブジェクトで実行する。セイバーが複製・非表示化されないこと、独自トレイルに標準リセットを呼ばないこと、開始・シーク・再開で判定履歴を保つこと、実コントローラーの復元、FPFCの代理ヘッドからでも描画カメラを取得できることを確認する。Releaseビルドと導入済みDLLとのAPI照合も行う。

この自動試験はUnity描画・音声・HMDの実動作を再現しない。実機では同じ記録の曲開始、標準および各MODのセイバー本体・トレイル、HMDの見回し、一時停止・シーク・退出後の通常プレイを確認する。結果は `Replay-Implementation-ja.md` に記載する。

## 参照した実装

- ScoreSaber：`src/Features/Replays/Playback/PosePlayer.cs` の姿勢適用・HMD観客カメラ・実コントローラーの停止。
- BeatLeader：`Source/2_Core/Replayer/Emulation/Controllers/VRControllersInstantiator.cs`、`VirtualPlayer.cs`、`Camera/ReplayerCameraController.cs` の既存コントローラー使用と第三者カメラ。
- SaberFactory：`SaberFactory/Game/SFSaberModelController.cs`、`Instances/Trail/SFTrail.cs`、`TrailHandler.cs` のモデル・トレイル管理。これは不具合の理由を調べた例であり、製品の依存先にはしない。
- Beat Saber 1.29.1：`Main/Saber.cs`、`SaberManager.cs`、`SaberTrail.cs`、`MainCamera.cs`、`HMLib/VRController.cs`。
- 導入済みSiraUtil 3.1.2：`GameTransformFPFCListener`、`FPFCToggle` のDLL解析。別バージョンのソースにあるAPIをそのまま仮定しない。
