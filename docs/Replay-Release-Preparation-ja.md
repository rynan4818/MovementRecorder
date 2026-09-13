# リプレイ版のリリース準備

2026-09-13。公開前の作業記録です。対応範囲は下表とし、移植した各世代のHMDでの動作確認は公開前に実施してください。

| ブランチ | MOD | Beat Saber | manifest.gameVersion |
| --- | --- | --- | --- |
| BS1.29.1 | 0.3.0 | 1.29.0-1.29.1 | 1.29.0 |
| BS1.37.1 | 0.3.1 | 1.37.1-1.37.3 | 1.37.1 |
| BS1.37.4 | 0.3.1 | 1.37.4-1.39.1 | 1.37.4 |
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

配布内容はPluginsのDLL、利用説明、ライセンスの8ファイルです。Test-ReleasePackage.ps1は標準名、全エントリー、各ファイルのSHA-256を検証します。ZIP圧縮時刻はビルドごとに変わり得るため、別ビルド間の比較にはZIP全体のハッシュではなく内容を使います。

## このブランチの確認

- BS1.37.1 / 0.3.1：164件の自動テスト成功。Releaseビルド成功。
- 1.37.1：内部API 273項目、コンパイル済みの呼び出し370件の署名を照合。
- 1.37.3：内部API 275項目、同じDLLの呼び出し370件を照合。旧ScoreSaberの保存抑止APIも確認。
- 1.37.4以降ではメニューの定義DLLが変わるためBS1.37.4に分離。追加設計は[1.37.4の境界](Replay-Release-1374-Boundary-Plan-ja.md)を参照。
- Main / HMUIの非公開APIはBSIPA.AssemblyPublicizer.MSBuild 0.5.0でビルド用参照を生成。実ゲームのDLLは変更しません。生成したアクセス属性が組み込まれることも確認済みです。
- 1.37系のHMDでの動作確認は未実施です。1.29.1で得られたユーザーテスト結果は移植先の動作保証には流用しません。

## 公開前の確認とリリース文

HMDでの一覧選択、開始、ポーズ・再開、前後シーク、音声・モデル同期、床ミラー、表情、コピー元の表示とオフセット保存、Camera2のREPLAY切替を確認します。通常プレイを前後に挟み、成績・外部MODの記録保存の抑止がリプレイ中だけに適用されることも確認します。

原稿は[v0.3.0](release-notes/v0.3.0.md)、[v0.3.1](release-notes/v0.3.1.md)、[v0.3.2](release-notes/v0.3.2.md)、[v0.3.3](release-notes/v0.3.3.md)です。検証完了後、対応ブランチの確定コミットにタグを付け、そのコミットから標準ZIPをビルドして0.3.1はbs1.37.1とbs1.37.4の2つ、他は各1つを添付します。0.3.1のタグはBS1.37.4を指し、bs1.37.1の対応コミットはZIP名とリリース作業記録で明記します。他リリースへのリンクは4件公開後に有効になります。

リリース本文に古い設定ファイルの削除案内は引き継ぎません。旧標準の探索設定だけを新世代で移行し、ユーザーが編集した設定やHMDオフセットを保持します。ここでの原稿作成はGitHubへの公開ではありません。
