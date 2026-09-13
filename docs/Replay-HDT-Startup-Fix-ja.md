# リプレイ開始時のHDT依存解決エラーの修正

更新日：2026-09-12

## 原因

`Logs/2026.09.12.20.00.10.log` の20:01:21、4104行目で、ゲームシーンの初期化中に次の例外が発生していた。

```text
ZenjectException: Unable to resolve 'IHeadDistanceTravelledController'
while building object with type 'HDTCounter'.
Object graph: CounterEventBroadcaster -> HDTCounter
```

MovementRecorderの保存抑止処理が、リプレイ中に `HDTGameInstaller.InstallBindings()` 全体を止めていた。これによりHeadDistanceTravelledの計測Controllerの登録がなくなり、Counters+から起動するHDT Counterの必須依存が満たされなくなった。シーンの依存注入が中断され、その後のVRControllerやSongProgressUIControllerなどでNullReferenceExceptionが繰り返された。

導入済みのHDT／HDT CounterのDLLと指定されたソースコードを照合し、Controllerが必要であることを確認した。原因はMovementRecorder側の過剰な保存抑止である。

## 修正

- HDTのInstallerを止めるフックを削除した。
- ユーザーの指定により、HDT／HDT Counterの生成・表示・計測・保存には介入しない。距離が記録されることを許容する。
- HDTのController、Update、OnDestroy、DB保存に対する代替フックも設けない。HDTの内部APIをリプレイの起動条件にしない。
- ゲーム本体の成績・統計、BeatLeader／ScoreSaberの保存・送信、対応するプレイ履歴の除外は、既存リプレイ相当の範囲で維持する。

HDT／HDT Counterへのコンパイル参照や必須依存は追加していない。一覧の過去の距離をDBから読む機能は従来どおり独立している。manifestの `gameVersion` は `1.20.0` のまま。

## 検証

DLL契約検査に、保存互換処理にHDT／HDT Counter向けのフックが含まれないことの確認を追加した。HDTの私有APIの有無を調べる旧検査は除去した。この検査が修正前DLLを拒否し、修正版DLLを受け入れることを確認する。

ビルドと既存データテストも実施し、結果は [実装・検証報告](Replay-Implementation-ja.md) に記載する。Unity／HMD上でのリプレイ起動とモデル描画は、この自動検証の対象ではない。ユーザーからは、この修正前の版で記録一覧の表示と選択ができたことを確認済み。

## 実機での確認

ゲーム終了中に、この修正版の `Plugins/MovementRecorder.dll` を配置する。既存版はバックアップする。HDTやHDT Counterなど他のDLLを置き換える必要はない。

1. 記録を選んでリプレイを開始し、上記の依存解決エラーがなく、モデル準備へ進むことを確認する。
2. HDT Counterの表示・計測・保存が通常どおり動くことを確認する。HDTの距離記録が増えることは許容する。
3. ゲーム本体の成績・統計やBeatLeader／ScoreSaberのリプレイ保存・送信は増えず、次の通常プレイが正常に動くことを確認する。
