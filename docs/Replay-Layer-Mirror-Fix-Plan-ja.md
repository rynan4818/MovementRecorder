# リプレイのレイヤー保持・床ミラー表示：設計計画書

作成日：2026-09-13

対象：MovementRecorder `BS1.29.1`、HEAD `9dbb5c22f356cd36fb30cf1efc8e7985107024b0`、Beat Saber 1.29.1。

状態：調査・設計のみ。コード修正は本計画の承認後に実施する。

## 1. 調査結果

- `RenderModelClone.CopyTransforms()` は、複製した全GameObjectの `layer` を0（Default）に変更している。`CopyAncestor()` も元レイヤーをコピーしていない。これは複製を通常のHMDカメラから見せるための処理だったが、元アバター用の描画設定を失う原因になる。[MR1]
- `SpectatorRig` は元HMDカメラの設定をコピーしてDefaultだけを追加している。モデルのレイヤーを戻すだけでは、一人称表示から除外されている顔や身体が観客カメラから見えなくなる可能性がある。[MR2]
- 導入済み `CustomAvatar.dll` 5.3.2.0のILを読み、アバターにレイヤー10（常時表示）と3（第三者表示専用）を使用すること、ミラーカメラ生成後にこの2レイヤーを追加することを確認した。保存済み `CustomAvatars.json` の `showAvatarInMirrors` は `true` だった。[CA1][CA2]
- 導入済み `Rendering.dll` のILでは、床ミラーの基本マスクは `~(1 << 4) & reflectLayers & currentCamera.cullingMask`。その後、CustomAvatarsが3・10を追加する。複製が0へ変わると、このアバター用の追加処理が効かない。[BS1]
- Camera2の1.29.1用解析ソースでも、アバター表示は3・6・10、その他の表示は別に選択する。Defaultへ移されたアバターが「アバター表示」設定で扱われないことは、表示されないカメラがあるという報告と整合する。[CAM1]
- セイバーはゲームの実オブジェクトを駆動し、複製・Defaultへの変更の対象から除外している。セイバーだけ反射するという違いとも整合する。[MR1][MR3]

レイヤーを変更している不具合はコード上で確定。床ミラーも同じ原因である可能性が高いが、報告時の実行中ミラーカメラの最終マスクと描画方式は未取得のため、反射が戻ることは修正後の同条件テストで確認する。Unityでは、対象のレイヤーがカメラのcullingMaskに含まれなければ描画されない。[U1]

## 2. 修正方針

### 元モデルのレイヤーを保持する

- 複製時はルート・子階層・補助的に複製する親階層へ、対応する元GameObjectのレイヤーをコピーする。
- 再生中に元MODがRendererのレイヤーを変更した場合も、既存のRenderer対応表を使って複製へ反映する。ポーズ・シーク中も表示設定には追従する。表情の停止規則は従来どおりとする。
- 対応済みRendererから使用レイヤーのビットマスクを集計する。毎フレームのシーン全体探索や階層検索は行わない。
- 元アバターのレイヤー自体は変更しない。元Rendererの非表示と終了時の復元は既存処理を使用する。

### リプレイ専用HMDカメラを第三者表示へ合わせる

- Defaultを一律に追加する処理を、モデルの使用レイヤーを反映する処理へ置き換える。
- 専用カメラのマスクは、元HMDカメラのマスクと複製Rendererの使用レイヤーを合わせ、第三者鑑賞では一人称専用の6を除外する。モデル再準備やレイヤー変更後は再計算し、古い追加ビットを残さない。
- 3（第三者専用）・6（一人称専用）・10（共通）は、Camera2・CameraPlus・手元のNalulunaAvatarsLiteの実装で使われる共通の取り決めとして扱う。VRMで一人称用と第三者用の両メッシュが重なることを避ける。[CAM1][CAM2][VRM1]
- Camera2等の外部カメラとミラーは、元レイヤーを保った複製を各カメラの既存設定で描画する。アバター非表示の設定もそのまま有効にする。
- 専用HMDカメラはセッション終了時に破棄されるため、元カメラへマスクを書き戻す処理は不要。

### 対応範囲

- 主な変更対象は `RenderModelClone`、`SpectatorRig`、両者を接続する `PlaybackRuntime`。
- CustomAvatars・NalulunaAvatars・Camera2へのアセンブリ依存やミラーへの新規パッチは追加しない。
- 記録形式、BlendShape同期、セイバー駆動、HDT計測・保存、音声制御、manifestのバージョンは維持する。
- 元アバターが使う独自シェーダーやカメラ別コールバックが別途必要な場合は、レイヤー修正後の症状から切り分ける。

## 3. 検証

- 自動確認：複数レイヤーの親子・Rendererの保持、元MODによるレイヤー変更への追従、カメラマスクの3・6・10の扱い、再準備時のマスク更新、元オブジェクトとセイバーの保持、既存の表情・姿勢テスト。
- Releaseビルドと導入ゲームDLLのAPI照合を実施する。Unityの実描画や反射表示は自動テストの成功とは分けて報告する。
- 実機確認：元と同じアバター・曲・カメラ設定で、問題の外部カメラと床ミラーに複製アバターが表示されること。HMDでは顔・身体が欠けず、VRMの一人称用メッシュが二重表示されないこと。
- 外部カメラのアバター非表示設定、ミラー表示設定、ポーズ・シーク・退出・再リプレイも確認する。
- 反射が戻らない場合は、元／複製Rendererのレイヤー、ミラーカメラの最終マスク、実際の反射描画方式を優先して調べる。

## 4. 根拠

- [MR1] [RenderModelClone.cs](../MovementRecorder/Playback/Models/RenderModelClone.cs)：レイヤー0固定、元モデルの非表示、セイバー除外。
- [MR2] [SpectatorRig.cs](../MovementRecorder/Playback/Runtime/SpectatorRig.cs)：専用HMDカメラの生成とマスク設定。
- [MR3] [RecordedSaberDriver.cs](../MovementRecorder/Playback/Runtime/RecordedSaberDriver.cs)：ゲームのセイバーを使用。
- [CA1] [AvatarLayers.cs](../../BeatSaberCustomAvatars/Source/CustomAvatar/Avatar/AvatarLayers.cs)：3・10の定義。導入DLLの静的初期化処理も照合済み。
- [CA2] [MirrorRendererSOパッチ](../../BeatSaberCustomAvatars/Source/CustomAvatar/Patches/MirrorRendererSO.cs)：ミラーのアバターレイヤー追加。導入DLLの対象メソッドも照合済み。
- [BS1] [Beat Saber 1.29.1 MirrorRendererSO](../../BeatSaber/SourceCode/1.29.1/Rendering/MirrorRendererSO.cs)：ミラーマスクの生成。導入DLLの対象メソッドも照合済み。
- [CAM1] [Camera2 CameraSettings](../../BeatSaber/DLL/1.29.1/Camera2/Configuration/CameraSettings.cs)、[VisibilityLayers](../../BeatSaber/DLL/1.29.1/Camera2/Utils/VisibilityLayers.cs)：外部カメラの表示選択。
- [CAM2] [CameraPlus CameraConfig](../../CameraPlus/CameraPlus/Configuration/CameraConfig.cs)：第三者カメラで3を含め6を除外する処理。
- [VRM1] [NalulunaAvatarsLite Layers](../../NalulunaAvatars/NalulunaAvatars/Layers.cs)：解析用ソースの一人称／第三者レイヤー定義。今回の導入環境にはNalulunaAvatarsのDLLはなく、実機互換性は未確認。
- [U1] [Unity 2019.4 Camera.cullingMask](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Camera-cullingMask.html)。
