# リプレイ版のリリース準備

2026-09-13。現在のブランチは **main / MovementRecorder 0.3.4 / Beat Saber 1.42.0-1.44.1** です。採番の変更を反映した[設計第3版](Replay-Release-Branches-Plan-v3-ja.md)を基準にしています。

| Beat Saber | MovementRecorder | ブランチ |
| --- | --- | --- |
| 1.29.0-1.29.1 | 0.3.0 | BS1.29.1 |
| 1.37.1-1.37.3 | 0.3.1 | BS1.37.1 |
| 1.37.4-1.39.1 | 0.3.2 | BS1.37.4 |
| 1.40.0-1.40.8 | 0.3.3 | BS1.40.0 |
| 1.42.0-1.44.1 | 0.3.4 | main |

5つのGitHubリリースに標準ZIPを1つずつ添付します。1.44.2以上は未対応です。将来その対応を始める際に、現在のmainからBS1.42.0を切り出します。Legatoは導入しません。manifestの依存バージョンは設計第3版に記載しています。

## 確認結果

- このブランチは166件の自動テスト、対象世代の実DLLを使ったReleaseビルドに成功しました。
- 対応範囲内の手元の26バージョンすべてで、型・メソッドの定義アセンブリと呼び出し署名が解決することを確認しました。[範囲別の結果](Replay-Release-Validation-2026-09-13.json)を参照してください。
- 各系統の両端では、リプレイに使う内部API、埋め込みmanifest、任意MODの保存抑止APIも照合します。実ゲームにMODがない場合は作業フォルダーの参照専用コピーを使い、本体は変更しません。
- ZIPは標準名、全エントリー、元ファイルとのSHA-256一致を検査します。このブランチの配布物は8ファイルです。
- **今回の移植版をHMD上で動作確認する工程は残っています。** 自動テストやDLLの静的照合だけで、ゲーム上の表示・操作まで検証済みとはしません。1.29.1で開発中に得た動作確認報告は、他世代の実機確認とは区別します。

## Visual Studioと同じRelease ZIPを作る

各作業ツリーのMovementRecorder.slnを開き、Releaseでビルドします。今回作成したローカル作業ツリーには、対応するSteam内のゲームを指すMovementRecorder.csproj.userを設定します。この個人用ファイルはGitへ含めません。他のPCではBeat Saber Modding Toolsまたはcsproj.userのBeatSaberDir / ReferencePathで参照先を指定してください。

スクリプトは同じcsprojをVisual StudioのMSBuildで呼び出します。このブランチの基準となるゲームは1.42.0です。

```powershell
./scripts/Build-Replay.ps1 -GameDirectory 'C:/Program Files (x86)/Steam/steamapps/common/Beat Saber_1.42.0'
./scripts/Test-ReleasePackage.ps1
./scripts/Test-ReplayContracts.ps1 -GameDirectory 'C:/Program Files (x86)/Steam/steamapps/common/Beat Saber_1.42.0'
./scripts/Test-AssemblyReferences.ps1 -GameDirectory 'C:/Program Files (x86)/Steam/steamapps/common/Beat Saber_1.42.0'
dotnet test MovementRecorder.Tests/MovementRecorder.Tests.csproj
```

.NET Framework 4.7.2のターゲットパックを使用します。スクリプトの既定のNuGet復元元はローカルキャッシュです。初回は必要に応じて `-NuGetSource https://api.nuget.org/v3/index.json` を指定します。テストには.NET 10 SDKを使います。

ZIPはBeatSaberModdingTools.Tasks 2.0.0-beta1がMovementRecorder/bin/Release/zipへ自動生成します。このブランチの名前は `MovementRecorder-0.3.4-bs1.42.0-<commit>.zip` です。対応範囲の終端は付けず、名前変更や独自のZIP作成は行いません。ビルド時のゲームへのコピーは無効です。

配布内容はPlugins/MovementRecorder.dll、利用説明書、ライセンスです。依存MODやゲーム本体のDLL、調査資料、ユーザーの記録・DB・設定は含めません。圧縮時刻とコンパイラー生成情報はビルドごとに変わるため、別ビルド間のZIP全体のハッシュ一致は条件にしません。標準ファイル名・内容・DLLの版番号を確認します。

Test-ReplayContracts.ps1とTest-AssemblyReferences.ps1の `-DependencyDirectory` は、MODが不足するゲームを照合するための任意のDLL参照フォルダーです。対象のゲーム本体を優先し、足りない依存MODだけを補います。別バージョンのMain.dllやHMUI.dll等は置かないでください。

## 公開前の実機確認

重点確認版は1.29.0 / 1.29.1、1.37.1 / 1.37.3、1.37.4 / 1.39.1、1.40.0 / 1.40.8、1.42.0 / 1.44.1です。

- 一覧の表示・選択色、ファイル変更、開始・終了・連続再生。
- ポーズ・再開・前後シーク、音声・セイバー・アバターの同期。1.40系以降はNJS変更を含む譜面でも確認。
- 表情、元レイヤー、床ミラー、コピー元表示・オフセットON/OFF、非表示時の省略、0.1m調整と再起動後の設定復元。
- Camera2のREPLAYシーンだけに割り当てたカメラの表示、CameraPlus利用時とカメラMODなしの表示。
- 通常プレイを前後に挟み、成績・ScoreSaber・BeatLeader・対応履歴MODの保存抑止がリプレイだけに適用されること。
- 通常の記録、WIP譜面への記録保存、旧標準の探索設定の移行と編集済み設定の保持。

## リリース文とGitHub公開

本文原稿は[v0.3.0](release-notes/v0.3.0.md)、[v0.3.1](release-notes/v0.3.1.md)、[v0.3.2](release-notes/v0.3.2.md)、[v0.3.3](release-notes/v0.3.3.md)、[v0.3.4](release-notes/v0.3.4.md)です。既存リリースの形式を引き継ぎ、古い設定ファイルの一括削除案内は追加していません。各リリースの冒頭に対象ゲーム範囲を明記します。

実機確認後、各タグv0.3.0～v0.3.4が対応ブランチの確定コミットを指すようにして、そのコミットから生成した標準ZIPを1つずつ添付します。0.3.1に2つのZIPを添付する案は採用しません。リリース本文の相互リンクは5件の公開後に有効になるため、公開順に応じて確認します。

ローカルでは、各移植と本書・本文原稿をコミットして配布ZIPを用意します。既存mainの履歴はno-fast-forwardのマージで残しています。push、タグ作成、GitHub公開、実ゲームへのインストールは行っていません。
