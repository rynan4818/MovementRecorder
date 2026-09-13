# リプレイ中の表情反映：設計計画書

作成日：2026-09-13

対象：MovementRecorder `BS1.29.1`、HEAD `7549abf8516a7785b169b7cd9278a1b1f6308798`、Beat Saber 1.29.1。

状態：2026-09-13、本チャットで設計承認済み。以下は承認した設計の記録。実装・検証結果は [表情同期の実装・検証報告](Replay-BlendShape-Live-Fix-ja.md) を参照。

承認前の補足：同期処理はAnimatorを必須条件とせず、VRM等のスクリプトが出力するBlendShape値も扱う。材質の色・テクスチャによる表情は今回の同期対象に含めない。

## 1. 調査で確認した原因

- `RenderModelClone.CopyRenderers()` は、元の `SkinnedMeshRenderer` のBlendShape値を複製時に一度だけコピーする。`Apply()` は記録した位置・回転・初期スケールと欠損時の表示状態を更新するが、BlendShape値は更新しない。[MR1]
- 複製側にはAnimator・Event Manager・Every Nth Combo Filter・Custom Key Event等を作らない。元アバターは破棄・非アクティブ化せず、Rendererの `forceRenderingOff` で描画を抑えている。[MR1]
- したがって、元のAnimatorが顔のBlendShape値を更新しても、その変化が鑑賞用モデルへ届かず、表情が複製時の値で止まる。ここで扱うBlendShapeの操作先は、Unityの型としては `SkinnedMeshRenderer` である。
- 現在の `.mvrec` は各フレームの時刻・位置・回転を保存する。BlendShape値、Animatorの状態、当時のキー操作は保存していない。[MR2]

元の承認済み設計では未記録の表情変化は初期対応範囲外だった。本計画では、既存の姿勢再生に、元アバターが現在生成する表情の反映を追加する。元設計書は保持する。

## 2. CustomAvatarsとCustomKeyEventsの経路

CustomAvatarsの `AvatarGameplayEventsPlayer.Initialize()` は、`currentlySpawnedAvatar` のEvent Managerを取得し、ノーツ判定・コンボ・倍率等のゲームイベントを購読する。コンボ増加時は `EventManager.comboIncreased` が呼ばれ、`EveryNthComboFilter` は0以外のコンボが `ComboStep` の倍数になったときに `NthComboReached` を発火する。[CA1][CA2]

Custom Key Eventは実際のコントローラー入力をUpdateで読み、設定されたUnityEventを呼び出す。[CK1] ユーザーが説明したアバターでは、これらのUnityEventの接続先がAnimatorであり、Animation Controllerが顔のBlendShapeを動かす。

今回、導入済みDLLのメタデータ・該当メソッドのILも読み、次を確認した。アバターファイル個別のUnityEvent接続先や、実行中のBlendShape変化そのものはまだ観測していない。

| 対象 | 確認内容 |
| --- | --- |
| `Plugins/CustomAvatar.dll` 5.3.2.0 | 元アバターのEvent Manager取得、コンボイベント購読、EveryNthComboFilterからのUnityEvent呼び出し |
| `Plugins/CustomKeyEvents.dll` 0.4.0.0 | Updateでの入力処理と `InvokeEventsForSourceEvent()` からのUnityEvent呼び出し |
| ゲームの `UnityEngine.AnimationModule.dll` | `Animator.cullingMode`、`runtimeAnimatorController`、`AnimatorCullingMode.AlwaysAnimate` の存在 |

調査に使ったCustomAvatarsソースのHEADは `d99de76c38e759dcb975b99d8af915053f787f78`。今回確認した箇所以外の全ソースと導入DLLの一致を保証するものではない。

## 3. 追加する動作

### 3.1 元のBlendShape出力を反映

1. モデル複製時に、元と複製先の `SkinnedMeshRenderer` の組を保持する。`Face` 等の名前やCustomAvatarsの型には限定せず、コピー範囲内でBlendShapeを持つRendererを対象にする。既存セイバーは現在どおりコピー対象から外す。
2. 通常再生中、既存の `ReplayLatePoseWriter` から、姿勢適用後にBlendShape値を元から複製先へ反映する。処理位置は現在のLateUpdate・実行順30000を使用する。[MR3]
3. 対応表・配列・前回値は準備時に確保し、毎フレームの階層探索や配列生成を避ける。値が変わった項目を更新し、0への復帰も反映する。
4. 正常な有限値はそのまま扱い、0～100へ一律に制限しない。非有限値は反映せず直前の値を保つ。[U1]
5. 元と複製先が準備時と同じメッシュで、対応するBlendShape数が維持されていることを確認する。メッシュ差し替えや破棄で対応が失われた組は更新を停止し、理由を一度だけログへ出す。別メッシュの同じ番号へ誤って書き込まない。元モデル自体の消失は既存の再生停止処理に従う。

### 3.2 非表示になった元Animatorの更新

Animatorの設定によっては、Rendererが見えないとアニメーション更新が省略される。Unityには画面外でも評価する `AlwaysAnimate` がある。[U2] 現在の実アバターがこの省略で止まっていることまで確認したわけではなく、固定の直接原因は3.1の更新欠落である。

- 対象Rendererを制御し得る、同じオブジェクトと親階層のAnimatorを準備時に収集し、重複を除く。
- 元の `cullingMode` を保存し、必要なものだけ再生セッション中に `AlwaysAnimate` へ変更する。無関係なアバターや全シーンのAnimatorは操作しない。
- 元の `enabled`、Animator Controller、speed、パラメーター、ステート、root motionは変更しない。無効だったAnimatorを強制的に有効化せず、手動の `Animator.Update()` も追加しない。
- 初期化失敗、再準備、実行時エラー、退出で変更した設定を復元する。復元は二重呼び出しと破棄済みオブジェクトに対応させる。
- 複製側のAnimator・スクリプトは引き続き作らない。イベントの付け替え・再発火やCustomAvatars／CustomKeyEventsのDLL参照も追加しない。

### 3.3 ポーズ・シーク時の扱い

| 状態 | 表情の扱い |
| --- | --- |
| 準備完了・通常再生 | 元アバターの現在のBlendShape値を反映する |
| ポーズ・再生完了 | 鑑賞用モデルは最後に反映した表情を保持する |
| シーク | 身体の姿勢を移動し、表情は保持する。移動先の過去の表情は生成しない |
| 再開 | 元アバターの現在の表情の反映を再開する |
| 退出・エラー | 変更したAnimatorの設定を復元する |

ポーズ中も元アバター側のAnimatorやCustom Key Event自体は止めないため、元の表情が変化した場合、再開時にその最新値へ切り替わる。ポーズ中のキー操作による表情を即座に鑑賞用モデルにも表示したい場合は、この表の仕様を変更してから実装する。

通常再生の表情イベントは、ゲームが現在計算する判定・コンボに基づく。シーク時の既存のコンボ初期化通知等が元アバターへ届いた場合も、その結果を採用する。CustomAvatars固有のイベント履歴やアーク／チェーン判定の内部キャッシュを、本変更で巻き戻したり再構築したりしない。曲終了イベントの追加発火も行わない。

## 4. 変更予定ファイル

| ファイル | 変更内容 |
| --- | --- |
| `MovementRecorder/Playback/Models/RenderModelClone.cs` | BlendShapeの対応表・更新、対象Animator設定の保存・復元、片付け |
| `MovementRecorder/Playback/Runtime/PlaybackRuntime.cs` | 再生状態に応じた表情更新と、エラー・再準備時の設定復元 |
| `MovementRecorder/MovementRecorder.csproj` | ゲームの `UnityEngine.AnimationModule.dll` 参照を追加し、コピーを無効化 |
| `MovementRecorder.Tests/ModelPlaybackTests.cs` とテスト用Unity実装 | 複数フレームの表情変化、停止・再開、対応喪失、設定復元の検証 |
| `scripts/Test-ReplayContracts.ps1` | 使用するAnimator APIと、複製側でAnimatorを作らないことの照合 |
| `docs/Replay-ja.md`、README、検証報告 | 表情反映の範囲と、当時の表情再現との違いを明記 |

記録形式・記録処理、セイバーの駆動方式、HDTの計測・保存、音声全体の制御は変更対象に含めない。manifestの `gameVersion=1.20.0` は維持する。設計承認はコミット・pushの承認とは分ける。

## 5. 検証

- テスト用のBlendShape実装は、現在の「常に0を返す・設定を保持しない」状態から値を保持する形へ更新し、元が変化した後の複製先を検証する。
- 複数Renderer・複数BlendShape、0への復帰、100を超える有限値、ポーズ中の保持、再開後の反映、メッシュ差し替え・破棄を確認する。
- Animatorがないモデル、無効なAnimator、同じAnimatorを複数Rendererが共有する場合、初期化失敗・二重破棄の設定復元を確認する。
- 既存のモデル・セイバー・観客入力・記録データのテスト、Releaseビルド、導入ゲームDLLとのAPI照合、ZIP内容確認を行う。
- 実機では、瞬き等の通常アニメーション、Every Nth Combo Filter、ミス等のEvent Manager、Custom Key Eventを順に確認する。ポーズ・シーク・退出後の通常プレイと、リプレイ2回目の表情・身体・セイバーも確認する。
- 自動テストとAPI照合だけでは、本物のAnimator評価順・非表示時の更新・HMD描画を検証したことにはしない。実機で動かない場合は、元のBlendShape値が変化しているかと複製先の値を分けて観測する。

## 6. 記録当時の表情を再現したい場合

既存の `.mvrec` にはその情報がないため、当時の表情やキー操作は復元できない。将来の記録へBlendShape値と時刻・対象を保存し、補間・シークできる形式を追加する別設計が必要になる。既存のMovementRecorder／ChroMapper向けファイル互換性を確認したうえで設計し、本計画に暗黙では追加しない。

## 参照

- [MR1] [RenderModelClone.cs](../MovementRecorder/Playback/Models/RenderModelClone.cs)：複製時のBlendShapeコピー、描画抑止、Apply、Dispose。
- [MR2] [RecordData.cs](../MovementRecorder/Models/RecordData.cs) と [MovementJson.cs](../MovementRecorder/Models/MovementJson.cs)：記録内容とファイル形式。
- [MR3] [PlaybackRuntime.cs](../MovementRecorder/Playback/Runtime/PlaybackRuntime.cs)：WriteLatePoses、ReplayLatePoseWriter、再生状態と後片付け。
- [CA1] [AvatarGameplayEventsPlayer.cs](../../BeatSaberCustomAvatars/Source/CustomAvatar/Avatar/AvatarGameplayEventsPlayer.cs)：元アバターへのイベント転送。
- [CA2] [EveryNthComboFilter.cs](../../BeatSaberCustomAvatars/Source/CustomAvatar/Scripts/EveryNthComboFilter.cs)：コンボの倍数条件。
- [CK1] [CustemKeyEvent.cs](../../BSCustomKeyEvents/CustomKeyEvents/AvatarScriptPack/CustemKeyEvent.cs)：UpdateとInvokeEventsForSourceEvent。
- [U1] [Unity 2019.4：SkinnedMeshRenderer.SetBlendShapeWeight](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/SkinnedMeshRenderer.SetBlendShapeWeight.html)：BlendShapeの添字とウェイト範囲。
- [U2] [Unity 2019.4：AnimatorCullingMode](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/AnimatorCullingMode.html)：非表示時のAnimator更新方針。
