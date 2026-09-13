# 表情同期の実装・検証報告

更新日：2026-09-13。対象：MovementRecorder `BS1.29.1`、変更前HEAD `7549abf`、Beat Saber 1.29.1。

状態：承認済みの [設計計画書](Replay-BlendShape-Live-Fix-Plan-ja.md) に従って実装し、Releaseビルド・自動テスト・API照合を実施済み。2026-09-13、ユーザーから修正版のテストで問題は見られないとの報告を受けた。

## 修正した動作

元の処理ではBlendShape値をモデル複製時に一度だけコピーしていたため、鑑賞用アバターの表情がその値で固定されていた。

- 通常再生中、元と複製先のSkinnedMeshRendererを対応付け、元の最新BlendShape値をLateUpdateで反映する。顔のオブジェクト名やAnimatorの存在は条件にしない。
- Animatorを持つモデルでは、対象Rendererの同じオブジェクトと親階層のAnimatorだけ、必要な `cullingMode` を一時的に `AlwaysAnimate` へ変更する。enabled、speed、Controller等は変更せず、複製側にAnimatorやイベント用スクリプトを作らない。
- ポーズ・シーク・再生完了中は鑑賞用モデルの表情を保持し、再開後に元の最新値を反映する。元MOD側の表情制御やキー入力処理自体は止めない。
- 正常な有限値をそのまま反映し、0への復帰や0～100外の値も扱う。非有限値は直前の値を保ち、Rendererごとに一度だけ警告する。
- メッシュの差し替え、BlendShape数の変更、対象の破棄を確認してから値にアクセスする。対応が失われた組は同期を停止して一度だけ警告し、別メッシュの同じ番号へ誤って反映しない。再対応にはモデルの再準備が必要。
- 初期化失敗・実行時エラー・再準備・退出で、一時変更したAnimator設定を復元する。二重破棄や破棄済みAnimatorにも対応する。

記録ファイルの形式、HDTの計測・保存、セイバーの駆動、ゲーム全体の音声制御は変更していない。

## CustomAvatars・VRMとの関係

同期対象は表情制御の結果として得られるUnityのBlendShape値であり、CustomAvatars／NalulunaAvatars／CustomKeyEvents／VRMライブラリへのDLL参照は追加していない。元モデルがその値を更新すれば、Animator経由でもスクリプト経由でも同じ処理を使用できる。

CustomAvatars 5.3.2とCustomKeyEvents 0.4.0の導入DLLでは、前回の調査でゲームイベント・キー入力からUnityEventへの呼び出し経路を確認した。手元のNalulunaAvatarsLite 1.6.0の解析用ソースでも、VRMBlendShapeProxy経由または直接 `SetBlendShapeWeight()` で反映する経路を確認した。今回のテスト報告には使用MOD名が示されていないため、NalulunaAvatars固有の実機結果は未確認。版による違いや非表示時の更新停止を保証するものではない。

マテリアルの色・テクスチャの変化や、未記録の骨の表情制御は今回の同期対象外。既存 `.mvrec` には当時のBlendShape値・Animator状態・キー操作がないため、記録当時の表情を復元する機能ではない。

## 検証結果

| 検証 | 結果 |
| --- | --- |
| 自動テスト | 80件成功、失敗・スキップ0件。既存65件と表情同期15件 |
| Releaseビルド | 成功。.NET Framework 4.7.2、Visual Studio 18 CommunityのMSBuildを使用 |
| ゲームDLL・BSML等の照合 | 206項目成功。使用するAnimator・BlendShape API、アバターMOD等への依存がないこと、複製に動作スクリプトを持ち込まないことを含む |
| manifest | `version=0.2.4`、`gameVersion=1.20.0` を維持 |
| 実機 | 2026-09-13、ユーザーよりテストで問題は見られないとの報告。使用MOD・個別項目ごとの結果は未報告 |

追加テストは製品のRenderModelCloneを実行し、Animatorなしの複数Rendererでの値の反映、記録姿勢と元モデルの保持、ポーズ・シーク・再開、非有限値、メッシュ差し替え・破棄、必要なAnimatorのみの設定変更、失敗時の復元、再準備・二重破棄を確認した。テスト用Unity実装はBlendShape値を保持するように変更したが、本物のAnimator評価・XR・描画・フレーム順は再現していない。

ビルドの初回実行とAPI照合の初回実行は、サンドボックス内からゲーム参照DLLを開く際にアクセス拒否となった。参照読み取りの承認付き再実行で成功した。既存のMSB3277参照バージョン競合警告は残っている。

ローカルの検証記録はGit対象外の `artifacts/tests/blendshape-fix.trx`、`artifacts/build-blendshape-fix.log`、`artifacts/contracts-blendshape-fix.json` に保存した。

## 実機確認用DLL

- DLL SHA-256：`B2E5BE0ADB85A258E3BC948282CA72B2F44AFBAEF8CFA29C606F645B0CFA0D08`
- ZIP：`artifacts/MovementRecorder-Replay-BS1.29.1-blendshape-fix.zip`
- ZIPの内容とハッシュ照合結果：`artifacts/package-blendshape-fix-verification.json`

## 実機確認項目

以下は個別確認用の項目。

1. 同じアバターと記録でリプレイし、瞬きなど通常の表情変化を確認する。
2. コンボ到達・ミス等、アバターに設定されたイベントによる表情を確認する。Custom Key Eventがある場合は現在のコントローラー操作も確認する。
3. ポーズ中とシーク中に表示中の表情を保ち、再開すると表情が動くことを確認する。
4. 退出後の通常プレイと、2回目のリプレイで表情・身体・セイバーに異常がないことを確認する。
5. VRM系MODでも同じ確認を行う。表情が固定された場合は、元のBlendShape値が変化しているかと複製先の値を分けて調べる。
