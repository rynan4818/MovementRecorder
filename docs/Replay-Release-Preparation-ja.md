# リプレイ版のリリース準備

2026-09-13。公開前の作業記録です。対応範囲は下表とし、移植した各世代のHMDでの動作確認は公開前に実施してください。

| ブランチ | MOD | Beat Saber | manifest.gameVersion |
| --- | --- | --- | --- |
| BS1.29.1 | 0.3.0 | 1.29.0-1.29.1 | 1.29.0 |
| BS1.37.1 | 0.3.1 | 1.37.1-1.39.1 | 1.37.1 |
| BS1.40.0 | 0.3.2 | 1.40.0-1.40.8 | 1.40.0 |
| main | 0.3.3 | 1.42.0-1.44.1 | 1.42.0 |

1.44.2以上は未対応です。将来その対応を始める際に、現在のmainからBS1.42.0を切り出します。Legatoは導入しません。詳しい判断と依存バージョンは[設計第2版](Replay-Release-Branches-Plan-v2-ja.md)を参照してください。

## Visual Studioと同じRelease ZIPを作る

Visual StudioでMovementRecorderプロジェクトの参照先（BeatSaberDir）を対象ゲームに設定し、Releaseでビルドします。既存のcsproj.userによる設定を使えます。スクリプトからは同じプロジェクトを呼びます。

```powershell
./scripts/Build-Replay.ps1 -GameDirectory 'C:/Program Files (x86)/Steam/steamapps/common/Beat Saber'
./scripts/Test-ReleasePackage.ps1
./scripts/Test-ReplayContracts.ps1 -GameDirectory 'C:/Program Files (x86)/Steam/steamapps/common/Beat Saber'
dotnet test MovementRecorder.Tests/MovementRecorder.Tests.csproj
```

参照先はブランチに合うゲームへ変更してください。0.3.0の通常参照先は動作確認済みの1.29.1です。Visual StudioのMSBuildと.NET Framework 4.7.2のターゲットパックを使用します。スクリプトは既定でローカルNuGetキャッシュから復元し、初回は必要に応じて `-NuGetSource https://api.nuget.org/v3/index.json` を指定します。

ZIPはBeatSaberModdingTools.Tasks 2.0.0-beta1が `MovementRecorder/bin/Release/zip` に自動生成します。ファイル名を変更する処理はありません。例は `MovementRecorder-0.3.0-bs1.29.0-<commit>.zip` です。対応範囲の終端はZIP名には付けません。ビルドは実ゲームへ配置しません。

配布内容はPluginsのDLL、利用説明、ライセンスの7ファイルです。Test-ReleasePackage.ps1は標準名、全エントリー、各ファイルのSHA-256を検証します。ZIP圧縮時刻はビルドごとに変わり得るため、別ビルド間の比較にはZIP全体のハッシュではなく内容を使います。

## このブランチの確認

- BS1.29.1 / 0.3.0：自動テスト160件成功。1.29.1の実DLLによる内部API照合、Releaseビルド、ZIP内容検証に成功。
- ScoreSaber 3.3のPlugin.Instance.ReplayStateと3.4のReplayStateRegistryを別のAPIとして扱います。未知の保存抑止APIでは開始を止めます。3.3の結果保存入口はUploadDaemon.Threeで、ローカル保存・送信前のSolo終了コールバックです。
- 1.29.0のゲームAPIは事前の137項目比較で1.29.1と一致しています。1.29.0のHMDでの動作確認は未実施です。
- 他世代のビルド・照合結果は、そのブランチの本書で記録します。基本リプレイについて得られた1.29.1のユーザーテスト結果を、移植先での動作保証には流用しません。

## 公開前の確認とリリース文

HMDでの一覧選択、開始、ポーズ・再開、前後シーク、音声・モデル同期、床ミラー、表情、コピー元の表示とオフセット保存、Camera2のREPLAY切替を確認します。通常プレイを前後に挟み、成績・外部MODの記録保存の抑止がリプレイ中だけに適用されることも確認します。

原稿は[v0.3.0](release-notes/v0.3.0.md)、[v0.3.1](release-notes/v0.3.1.md)、[v0.3.2](release-notes/v0.3.2.md)、[v0.3.3](release-notes/v0.3.3.md)です。検証完了後、対応ブランチの確定コミットにタグを付け、そのコミットから標準ZIPをビルドして各リリースに1つずつ添付します。他リリースへのリンクは4件公開後に有効になります。

リリース本文に古い設定ファイルの削除案内は引き継ぎません。旧標準の探索設定だけを新世代で移行し、ユーザーが編集した設定やHMDオフセットを保持します。ここでの原稿作成はGitHubへの公開ではありません。
