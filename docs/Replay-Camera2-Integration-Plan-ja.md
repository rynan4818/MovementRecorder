# Camera2のREPLAYシーン連携：調査・設計計画

日付：2026-09-13。対象：MovementRecorder `BS1.29.1`、Beat Saber 1.29.1。

状態：ユーザー承認後、実装・自動検証を完了。結果は [実装・検証報告](Replay-Camera2-Integration-ja.md) を参照。以下は承認時の設計を保持する。

## 結論

MovementRecorderのリプレイをCamera2のREPLAYシーンに対応させることは可能。**BeatLeaderと同じ `Camera2.SDK.ReplaySources` に、MovementRecorderを独立したリプレイ元として登録する方法を推奨する。**

導入済みCamera2 0.6.108には、登録・解除・再生状態・頭の姿勢を渡す公開APIがある。MovementRecorder側で任意連携として呼び出せるため、Camera2の改造や、ScoreSaber・BeatLeaderの状態フラグの書き換えは必要ない。

**Camera2.dllへのアセンブリ参照は追加しない。** CameraPlusだけが導入されている環境や、カメラMODがない環境でもMovementRecorderが読み込める構成とする。Camera2の読み込み済みプラグインが存在するときだけ、文字列の型名からAPIを取得する。

今回の範囲はREPLAYへのカメラ割り当て対応と、そのために必要な姿勢情報の提供とする。カメラの選択順はCamera2に任せる。

## 調査対象とバージョンの違い

| 対象 | 確認した状態 |
| --- | --- |
| Camera2の作業ツリー | `v0.6.91` / `4096323d5c7a05154b9f330e1aba859e2b80ba22`。この版はScoreSaber専用判定を使用 |
| 導入済みCamera2.dll | 0.6.108。公開 `ReplaySources` APIと、登録済みリプレイ元を調べるシーン判定を確認 |
| Camera2の対応ソース | 同じローカルGitにある `v0.6.108` / `9b596886e45620f3b84520bad5e68274649e91cf` を `git show` で参照。作業ツリーの切り替えはしていない |
| ScoreSaber | 指定ソース `875564d27c0989030c5202267ccf0ea9addcd87a`、導入済みDLL 3.4.2。両方でCamera2互換クラスを確認 |
| BeatLeader | 指定ソース `5ad70db17220ef071507307c0cdb73f09d165dc8`、導入済みDLL 0.9.20。両方で `Cam2Interop` の登録・状態通知を確認 |

Camera2は、古い作業ツリーだけを見ると利用できる連携方法を見落とす。今回の推奨案は導入済み0.6.108のAPIに基づく。各DLLのパス・SHA-256・公開メソッドの照合記録は [API調査記録](../artifacts/inspection/camera2-replay-contracts.json) に保存した。

## REPLAYとして扱われない理由

Camera2 0.6.108の `ScenesManager.LoadGameScene` は、`GameCore` に入ったときに、登録されたリプレイ元のいずれかが `isInReplay == true` ならREPLAYを候補に加える。

MovementRecorderは自身の `MovementReplay.IsActive` を管理しているが、Camera2への登録・通知処理を持っていない。`MovementRecorderReplay` というゲームモード名だけではCamera2のREPLAY判定に入らない。このため、現在の実装では通常のPLAY側の候補が使われる。

根拠：[Camera2 0.6.108のシーン判定](../artifacts/inspection/camera2-0.6.108/Managers.ScenesManager.cs)、[MovementRecorderの状態管理](../MovementRecorder/Playback/ReplaySession.cs)。

## ScoreSaberとBeatLeaderの方式

| MOD | REPLAY状態の認識 | 一人称カメラへの姿勢情報 |
| --- | --- | --- |
| ScoreSaber | Camera2が旧API名 `ScoreSaber.Core.ReplaySystem.HarmonyPatches.PatchHandleHMDUnmounted.Prefix()` を反射で取得。今回のScoreSaber互換クラスは現在のリプレイ状態を旧形式の戻り値へ変換する。Camera2自身が `SSReplaySource` を登録する | Camera2が所定のオブジェクトパスにある `RecorderCamera` を見つけ、そこから位置・回転を読む。ScoreSaber側もカメラ名と階層を維持している |
| BeatLeader | `GenericSource("BeatLeaderReplayer")` を反射で生成して `Register`。リプレイ開始・終了イベントから `SetActive(true/false)` を呼ぶ | 今回のBeatLeaderはGenericSourceの姿勢getterにHarmony処理を追加し、再生中プレイヤーの頭のTransformから値を更新する |

**今回確認したScoreSaberもCamera2のREPLAYシーンに対応している。** ScoreSaber自身がCamera2の登録APIを呼ぶ方式ではなく、Camera2側にScoreSaber用のアダプターが組み込まれている、という連携方向の違いである。MovementRecorderはこのScoreSaber用アダプターの対象に含まれない。

参照箇所：

- [Camera2のScoreSaber連携](../artifacts/inspection/camera2-0.6.108/Utils.Scoresaber.cs)：`Reflect`、`UpdateIsInReplay`、`SSReplaySource`。
- [Camera2の登録処理](../artifacts/inspection/camera2-0.6.108/Plugin.cs)：`OnApplicationStart`。
- [ScoreSaber互換クラス](../../scoresaber-plugin/src/Features/Replays/Compatibility/Camera2ReplayCompatibility.cs)。
- [ScoreSaberのカメラ構築](../../scoresaber-plugin/src/Features/Replays/Playback/PosePlayer.cs)：`SetupCameras`。
- [BeatLeaderの連携処理](../../beatleader-mod/Source/7_Utils/Interop/Interops/Cam2Interop.cs)：`Init`、`HandleReplayWasStarted`、`HandleReplayWasFinished`。
- [BeatLeaderの頭の参照切り替え](../../beatleader-mod/Source/2_Core/Replayer/Tweaking/Tweaks/InteropsLoaderTweak.cs)。

## 方法の比較

| 方法 | 評価 |
| --- | --- |
| MovementRecorderから公開ReplaySources APIへ登録 | **採用案。** リプレイ元として自然に扱われ、Camera2本来のシーン選択・設定・フォールバックが使える。任意連携にできる |
| Camera2を改造してMovementRecorderを直接認識 | 実現可能だが、Camera2の独自DLLを維持する必要が生じる。現在の導入版には対応APIがあるため優先しない |
| Camera2内部のシーン判定や `SwitchToScene` にHarmony処理を追加 | 内部実装・呼び出し順への依存が増える。公開APIで対応できるため採用しない |
| ScoreSaber・BeatLeaderのリプレイ状態を代用 | 各MODの再生処理・記録処理にも関係するため採用しない。MovementRecorderの他リプレイ実行チェックとも衝突する |
| カスタムシーンへ手動で切り替える | 別の運用としては可能。ただし標準REPLAYの割り当てに対応する今回の目的には合わない |

Camera2の公開 `SDK.Scenes` はカスタムシーンへの切り替えと通常選択への復帰を提供しており、標準REPLAYを一時的に指定する専用APIではない。シーンを強制するより、ReplaySourcesへの登録が適切。

## 実装計画

### 1. 任意連携クラスを追加

`Playback/Compatibility/Camera2ReplayInterop.cs` を追加し、導入済みCamera2から次の公開メンバーを反射で取得する。

- `ReplaySources.GenericSource(string name)`
- `ReplaySources.Register(ISource source)`
- `GenericSource.SetActive(bool isInReplay)`
- `GenericSource.Update(ref Vector3 localHeadPosition, ref Quaternion localHeadRotation)`
- `ReplaySources.Unregister(ISource source)`

登録名は `MovementRecorderReplay` とする。APIの型・引数を照合し、姿勢更新は取得したメソッドのデリゲートをキャッシュして呼ぶ。BeatLeaderのgetterパッチをそのまま持ち込む必要はない。

Camera2の型をフィールド・引数・戻り値・基底型に使わず、取得したインスタンスは `object`、APIは `MethodInfo` とUnity標準型を使うデリゲートで保持する。csprojの `Reference`、ビルド時のCamera2.dll要求、Camera2.dllの同梱・コピー、manifestの `dependsOn` は追加しない。読み込まれているプラグインだけを調べ、`Assembly.Load` 等でCamera2を強制的に読み込む処理も不要。

manifestの `loadAfter` にはCamera2を追加する。これは両方が存在する場合の順序指定であり、アセンブリ参照や必須依存ではない。CameraPlusのみ／カメラMODなしでは連携処理を省略する。旧版などでAPIが不足する場合も、理由を一度ログに出してMovementRecorder本体のリプレイは利用できるようにする。0.6.91に対する内部パッチの代替実装は加えない。

### 2. セッションの開始・終了に合わせて通知

AppのInstallerで連携クラスを管理し、`MovementReplay.SessionChanged` を購読する。登録・更新・解除はUnityのメインスレッドで行う。

| タイミング | Camera2への処理 |
| --- | --- |
| リプレイメニューの表示・ファイル読込 | 非アクティブのまま |
| `ReplaySession.Begin` | 初期姿勢を設定して登録し、`SetActive(true)` |
| GameCoreへの遷移 | Camera2自身のシーン切り替えでREPLAYを選ぶ |
| モデル準備・再生・ポーズ・シーク・末尾停止 | アクティブを維持 |
| 曲選択へ戻った後の `ReplaySession.Finish` | `SetActive(false)`、登録解除、参照を解放 |
| 開始失敗・通常以外の終了経路・アプリ終了 | 同じ解除処理を安全に実行し、登録やアクティブ状態を残さない |

現在の `ReplayMenuService` は `Begin` の後に `StartStandardLevel` を呼んでいるため、シーン判定に間に合う位置で通知できる。Camera2の `ActiveSceneChanged` も次フレームにシーン選択を行う。再生ボタンの読み込み開始時や、モデル準備完了後だけに通知する設計にはしない。

`SetActive` 自体はシーン再読み込みを行わない。今回の通常遷移ではCamera2側の自動選択を使い、`ShowNormalScene` や内部 `LoadGameScene` を無条件に呼んでカスタムシーン設定を上書きしない。

### 3. 一人称カメラへの副作用を防ぐ

ReplaySourcesはシーン判定だけでなく、Camera2の一人称カメラで「Follow replay position」がONの場合の追従先にも使われる。GenericSourceを登録して姿勢を未設定のままにすると、初期値の位置・回転が利用されるため、状態通知と姿勢通知を一緒に実装する。

**今回の姿勢通知は、現在のCamera2の通常一人称処理に相当する、実HMD側の頭の位置・回転を使用する。** HMDのローカル姿勢へRoomSettingsの位置・回転を一度適用する形で、Camera2側の通常処理と合わせる。MovementRecorderの鑑賞位置オフセットは専用HMDカメラ側で引き続き扱う。

開始時は遷移前の有効なHMD姿勢で初期化し、GameCoreの頭・カメラを取得したら参照を切り替える。`SpectatorRig` が保持する元カメラの参照を利用し、描画を無効にした後の `Camera.main` 再検索に依存しない。有効な参照へ切り替わるまでは直前の有効姿勢を保持する。更新位置はカメラの描画順と照合する。

現在の `.mvrec` は選択されたアバター・セイバー等のTransformを記録し、録画時のHMD姿勢を必須データとして持つ仕様ではない。アバターの頭ボーンもHMDと同じ位置・向きとは限らないため、今回の連携で録画時の一人称視点を再現するとは扱わない。一覧の「頭の移動距離」は別DBの集計値であり、姿勢の時系列ではない。

固定カメラ・オブジェクトへの追従カメラはCamera2自身の設定を利用する。FPFC有効時もCamera2自身の処理が優先される。

根拠：[Camera2のSmoothfollow](../artifacts/inspection/camera2-0.6.108/Middlewares.Smoothfollow.cs)、[SpectatorRig](../MovementRecorder/Playback/Runtime/SpectatorRig.cs)、[記録対象設定](../MovementRecorder/Configuration/PluginConfig.cs)。

### 4. Camera2本来の選択ルールを維持

- REPLAYに存在するカメラが割り当てられていれば、その設定を利用する。
- REPLAYに有効なカメラがなければ、Camera2がPLAY等の次の候補を選ぶ。
- FPFC有効時は、FPFCに有効な割り当てがあるとそちらが優先される。
- カスタムシーンを選択中で自動復帰を無効にしている場合は、その選択を尊重する。
- 曲選択へ戻ればCamera2がメニュー用の設定を選ぶ。通常プレイや他MODのリプレイではMovementRecorderの登録を残さない。

## 変更予定と検証

変更予定は連携クラス、App Installer、HMD姿勢を受け渡すRuntime／SpectatorRig、manifest、連携テスト、利用説明。既存のリプレイ状態イベントを利用する。

自動検証では、任意依存の欠如・API不一致、重複登録防止、開始前の状態通知、ポーズ・シーク・末尾停止での維持、開始失敗・終了・次の通常プレイへの復帰、姿勢の初期化と座標変換を確認する。導入済みCamera2へのAPI照合とReleaseビルドも行う。完成したMovementRecorder.dllのAssemblyRefにCamera2が含まれないこと、manifestにCamera2の必須依存がないこと、配布ZIPへCamera2.dllが混入していないことも確認する。

実機では次を確認する。

1. REPLAYだけに割り当てた固定カメラがMovementRecorderのリプレイで表示される。
2. ポーズ・シーク・末尾停止でもREPLAYの表示が続く。
3. メニュー復帰後と次の通常プレイで、MENU／PLAYの設定へ戻る。
4. 連続再生、開始失敗、CameraPlusのみ／カメラMODなしの場合も起動とリプレイが利用できる。
5. 一人称の追従設定ON／OFF、ルーム位置・回転、鑑賞位置の変更を組み合わせて、今回のシーン連携で外部カメラの視点がずれない。
6. FPFCとカスタムシーンの優先設定、ScoreSaber・BeatLeaderの通常リプレイが引き続き動く。

設計書作成時点では調査のみ。承認後の実装では、元HMDの親Transformに反映済みのルーム位置・回転を利用し、そのワールド姿勢を通知する形で座標変換を一度だけ適用した。ScoreSaberに同梱されたRoomSettingsクラスなど、他MODの設定クラスは参照していない。ゲーム内での動作確認は未実施。
