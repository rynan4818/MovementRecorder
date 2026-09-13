# オフセット調整を0.1 m刻みに変更：実装・検証報告

日付：2026-09-13。対象：MovementRecorder `BS1.29.1`、Beat Saber 1.29.1。

ユーザーの追加指定に従い、承認済みの鑑賞位置設定を調整した。

## 変更内容

- リプレイメニューの鑑賞設定と、再生中の操作パネルの左右・上下・前後を、0.5 m刻みから0.1 m刻みに変更した。対象は2画面・各3軸の6項目。
- 調整範囲、設定項目、保存・復元の仕組みは維持した。保存済みの座標は読み込み時に丸めない。
- BSMLの増減ボタンがfloat値へ増分を繰り返し加算するため、調整時のみ、0.1 mの整数倍から0.0001 m未満の差を補正する。表示が0へ戻っても微小値が残り、アバター移動処理が有効なままになることを防ぐ。
- 0.25 mなど、0.1 mの整数倍付近でない値を強制的に丸めることはない。

## 検証結果

- 自動テスト155件成功、失敗・スキップ0件。0.1 mの加減算で各軸を0に戻した際の値・保存値・オフセット無効判定と、保存値の精度維持について5件を追加した。
- Visual Studio Releaseビルド成功。既存のMSB3277（System.Net.Http参照バージョン競合）警告は残る。
- 導入済みDLLと埋め込みBSMLの照合283項目成功。2画面・6項目の増分が0.1、上下限が従来どおりであることもDLL内のリソースから確認した。
- HMDでの表示・操作確認は未実施。

検証記録：`artifacts/tests/avatar-offset-01m.trx`、`artifacts/build-avatar-offset-01m.log`、`artifacts/contracts-avatar-offset-01m.json`。

## 動作確認用ファイル

- ZIP：`artifacts/MovementRecorder-Replay-BS1.29.1-avatar-offset-01m.zip`
- DLL：`MovementRecorder/bin/Release/MovementRecorder.dll`
- DLL SHA-256：`CCBA30FEFF848B8DC335F981F0AE4067031673F696E7B56959B7A991BB3E6A22`

直前のアバターオフセット・ON/OFF・設定保存とCamera2連携の変更も含む。ゲームへの配置、コミット、pushは行っていない。
