# カメラ複製方式とアバターレイヤー修正：実装計画書

作成日：2026-09-13。対象：MovementRecorder `BS1.29.1`、変更前HEAD `9dbb5c2`、Beat Saber 1.29.1。

実装・自動検証済み。結果と実機確認の範囲は [検証報告](Replay-Camera-Clone-Layer-Fix-ja.md) を参照。

ユーザーの「既存カメラを複製し、不要な制御を取り除く方式にしつつ、レイヤーを修正してください」という指示に基づき、提示済みの方式比較と [レイヤー修正計画](Replay-Layer-Mirror-Fix-Plan-ja.md) を以下の実装へ具体化する。旧計画書は調査記録として保持する。

## カメラ

- 元HMDカメラを、非アクティブな鑑賞用ルートの下へ `Instantiate` する。元カメラは非アクティブ化せず、既存の実HMD追従・AudioListenerを維持する。
- 複製が非アクティブな間に不要な子オブジェクトと制御コンポーネントを除去する。`MainCamera`、入力・追従制御、AudioListener、MODの登録処理を複製側で起動させない。元カメラとして誤検出されないようタグも外す。
- 残すのはCamera・Transformと、導入ゲームDLLで確認した標準の描画用コンポーネント `BloomPrePass`、`MainEffectController`、`CameraDepthTextureMode`。未知のコンポーネントは除去して型名をログに記録する。独自の描画MODまで無条件で互換性を保証するものではない。
- `MainEffectController` の描画コールバックは複製側で作り直す。複製に含まれる `ImageEffectController` と古い実行時参照を除去し、有効化時に複製自身へ接続する。
- `BloomPrePass` は元カメラと共有する実行時描画データを切り離し、自分の視点で描画データを生成する。元カメラのRenderTextureを複製の破棄で解放しない。
- 不要コンポーネントの除去と参照の準備が終わってから有効化する。除去できない場合は有効化せず、理由を報告して片付ける。必要な描画コンポーネントが元にない場合は新しく捏造しない。
- 頭の実測姿勢、鑑賞位置、FPFC切り替え、ポーズ中の実手元ポインターは現在の動作を引き継ぐ。元HMDカメラは描画のenabledだけを切り替え、失敗・終了時に元の値へ戻す。

UnityのInstantiateは付属コンポーネントと子も複製し、アクティブな階層ではAwake・OnEnableが呼ばれる。そのため、作成直後に止めるのではなく、最初から非アクティブな親を指定する。[Unityの仕様](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Object.Instantiate.html)

## レイヤー

- モデルの複製時に、親・ルート・子それぞれの元レイヤーを保持する。0への固定を撤去する。
- 既存のRenderer対応表から元レイヤーの変更を反映し、使用レイヤーのマスクを集計する。ポーズ・シーク中にも反映し、シーン全体の毎フレーム探索は行わない。
- 鑑賞カメラは「元HMDカメラのマスク OR 複製Rendererの使用レイヤー」から一人称専用の6を除外する。元の設定・モデル対応が変わったときも再計算し、不要になったビットを残さない。
- CustomAvatars等の3・10の割り当てを保持し、床ミラーとCamera2等が既存のアバター表示設定を適用できるようにする。外部カメラやミラーのマスクへの新規パッチは追加しない。

## 実装・検証

- `SpectatorCameraClone` に複製・除去・描画参照の準備をまとめ、`SpectatorRig` から使用する。`RenderModelClone` と `PlaybackRuntime` でレイヤーの保持・追従・カメラへの受け渡しを行う。
- 製品コードを使うテストで、複製の準備中に不要な処理が起動しないこと、描画処理の保持と参照の独立、子・音声・入力の除去、失敗時の復元、レイヤー追従と第三者用マスク、再準備・退出を確認する。
- テスト用Unity実装の範囲を明示する。実際のAwake順序・XR・描画・反射は実機で確認する。
- Releaseビルドと導入済みゲームDLLのAPI照合を行い、確認用ZIPを作る。記録形式、表情同期、実セイバー駆動、HDT計測・保存、音声制御、manifestのバージョンは維持する。
- 実機ではHMDの顔・身体、問題の外部カメラ、床ミラー、Bloom等の描画、音声、ポーズ・シーク・FPFC切り替え・退出後の通常プレイを確認する。VRMでは一人称用メッシュの二重表示も確認する。

参照したゲーム実装：`HMRendering/BloomPrePass.cs` の描画データ生成・破棄、`Rendering/MainEffectController.cs` のImageEffectController生成と接続、`HMLib/CameraDepthTextureMode.cs` の深度設定。カメラ複製の比較は導入済みScoreSaber 3.4.2・BeatLeader 0.9.20のDLLで確認した。
