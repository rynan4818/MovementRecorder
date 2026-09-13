# MovementRecorder リプレイ版のリリース・ブランチ設計 第2版

作成日：2026-09-13  
状態：2026-09-13のユーザー回答で指定された変更を反映し、実装を開始する。初版は調査時点の案として保存する。

## 1. 提案するリリース

ユーザー指定に従い、旧世代から順に0.3.0、0.3.1、0.3.2、0.3.3の4リリースを用意する。対応範囲ごとにMODの版番号も分ける。

対応コードは以下の **4ブランチ** に分ける。共通化のためのゲームバージョン判定や、大量の条件付きコンパイルは追加しない。

| ブランチ | MODバージョン | 対応・検証対象 | manifest.gameVersion |
| --- | --- | --- | --- |
| BS1.29.1（既存） | 0.3.0 | 1.29.0～1.29.1 | 1.29.0 |
| BS1.37.1（新設） | 0.3.1 | 1.37.1～1.39.1 | 1.37.1 |
| BS1.40.0（新設） | 0.3.2 | 1.40.0～1.40.8 | 1.40.0 |
| main（既存） | 0.3.3 | 1.42.0～1.44.1 | 1.42.0 |

最新版はmainで管理する。BS1.42.0は今回は作らず、1.44.2以上への対応を決めた時に、その時点のmainから保守ブランチとして切り出す。
上表は移植・検証する範囲であり、全バージョンでのリプレイ動作確認が完了したという意味ではない。追加の互換処理が必要な版は、無理に同じDLLへ取り込まず、公開範囲を狭めるか別途相談する。

ユーザー指定により、**1.44.2以上は今回の対象外**とする。最新版の対応枠は1.42.0～1.44.1までを用意する。実際に1.44.2～1.45.0のDLLでも開始APIの型変更を確認しており、1.44.1までのDLLで無理に対応しない。

## 2. 利用者数を使った対象の絞り込み

[指定の分布ページ](https://rynan4818.github.io/BeatSaberVer.html)が読み込む[history.json](https://github.com/rakkyo150/player_count_by_bs_version/blob/master/data/history.json)を直接集計した。

最新データは **2026-09-11、timestamp=20260911_161527**。PCはsteamとoculuspcを合算し、Questのoculusを除いた。

- PC全体：903人。そのうちゲームバージョンが分かる902人を割合の分母とした。不明は1人。
- 母集団：BeatLeaderに日本で登録し、過去1か月にプレイした、集計可能な利用者。世界全体や全MOD利用者の割合ではない。
- 直近4回は2026-08-21、08-28、09-04、09-11。4回の合計人数を使う割合は推移の確認用であり、4週間の重複しない利用者数ではない。

集計対象の説明：[集計元README](https://github.com/rakkyo150/player_count_by_bs_version)。検索結果のキャッシュではなく、取得時点の生データを使用した。

| 系統 | 最新のPC人数 | 最新割合 | 直近4回の合算割合 | 今回の判断 |
| --- | ---: | ---: | ---: | --- |
| 1.29.0～1.29.1 | 264 | 29.27% | 28.92% | 維持 |
| 1.33.0 | 0 | 0.00% | 0.00% | 専用ブランチを作らない |
| 1.34.2 | 16 | 1.77% | 2.04% | 今回は対象外 |
| 1.35.0～1.37.0 | 6 | 0.67% | 0.69% | 今回は対象外 |
| 1.37.1～1.39.1 | 120 | 13.30% | 13.32% | 維持 |
| 1.40.0～1.40.8 | 433 | 48.00% | 47.67% | 維持 |
| 1.41.1 | 0 | 0.00% | 0.00% | 専用ブランチを作らない |
| 1.42.0～1.44.1 | 3 | 0.33% | 0.47% | 最新対応枠として維持 |

4系統の合計は **820 / 902人、90.91%**。これは候補範囲の人口カバー率であって、動作確認済み人数ではない。1.29.0を公開範囲に含められない場合は805 / 902人、89.25%となる。

判断基準の提案：

1. 旧世代は、最新・直近4回とも利用率が3%未満で、専用コードのブランチが必要なら、初回リリース対象から外す。
2. 同じブランチ・同じDLLで追加の互換分岐なく対応できる版は、少人数でも検証対象に含める。1.29.0、1.37.5、1.40.6などが該当する。
3. **最新版の対応枠は人数に関係なく残す。** 最新版への移行に備えるという今回の指定を優先する。
4. 1.28.0以下や1.31系・1.34系の未確認パッチ版を、近い番号という理由で対応範囲に含めない。
5. 対象外版の既存リリースは残す。「旧版の記録機能が使えなくなる」という扱いにはしない。今後のリリース時に利用状況・要望を再確認する。

最新の主な内訳は1.40.8が386人、1.29.1が249人、1.39.1が84人、1.37.1が30人。1.37.1も1.39.1と同じブランチに収まるため残す。1.34.2は直近8回で32人から16人へ減少しており、専用ブランチの追加効果が小さい。

再計算結果とデータのSHA-256は [集計記録](Replay-Release-Evidence-2026-09-13.json) に保存した。

## 3. 過去のリリースと引き継ぐ変更

| 公開版 | 公開日（UTC） | リリースに記載された対応 | 確認結果 |
| --- | --- | --- | --- |
| [0.2.4](https://github.com/rynan4818/MovementRecorder/releases/tag/v0.2.4) | 2024-09-16 | 1.29.1以前、1.33.0～1.34.2 | 同じソースから参照DLLを変えた2種類のZIP。実機確認の記載は1.29.1、1.33.0、1.34.2 |
| [0.2.5](https://github.com/rynan4818/MovementRecorder/releases/tag/v0.2.5) | 2024-09-16 | 1.35.0～1.37.3 | BeatmapKey対応、CustomAvatarsの探索変更、CustomSabersLite追加 |
| [0.2.6](https://github.com/rynan4818/MovementRecorder/releases/tag/v0.2.6) | 2024-09-28 | 1.35.0～1.39.1 | BSML 1.12対応。リリース説明では1.40.0以降を除外 |
| [0.2.7](https://github.com/rynan4818/MovementRecorder/releases/tag/v0.2.7) | 2025-09-15 | 1.37.1～1.40.8 | SongCore 3.14.8以降への対応。現在の公開最新版 |

旧版の対応範囲は記録機能についての情報であり、ゲーム内部の開始・ポーズ・シークを使う新しいリプレイ機能の互換性を証明するものではない。

現在のBS1.29.1には、リプレイ、表情同期、カメラ複製とレイヤー保持、Camera2のREPLAY連携、コピー元アバターの表示・オフセット設定が入っている。英語UIとキャッシュの旧日本語エラー更新は未コミットなので、これを失わず共通の出発点に含める。

新しいゲーム向けには、0.2.5～0.2.7の記録側の修正も移植する。具体的には譜面情報の取得、WIP保存先、CustomAvatarsの探索、CustomSabersLite設定、GameplaySetupの取得方法。古い記録専用コードで現在のリプレイ付きファイルを上書きしない。

## 4. ブランチ境界の技術的な根拠

提供されたSourceCodeの25版を調査し、追加でSteam配下にあるゲームDLLのメタデータを照合した。DLLは実行していない。

| 境界・共通範囲 | 確認したAPI | 方針 |
| --- | --- | --- |
| 1.29.0 / 1.29.1 | 調査対象137項目の型・メソッド・フィールドの比較で差分なし | BS1.29.1を共用し、1.29.0はMOD導入状態を整えて実機検証 |
| 1.33.0 | StartStandardLevelに譜面側の色設定の引数が増える。VRPointerの旧_vrControllerもなくなる | 旧DLLのまま対象を拡張しない |
| 1.34.2 | StartStandardLevelに省略可能なrecordingToolDataが増える | 省略可能でもバイナリのメソッド署名は変わる。専用対応は今回省略 |
| 1.35.0以降 | IDifficultyBeatmapからBeatmapKey / BeatmapLevelへ。DataModels.dllへの参照も必要 | 新しいブランチで素直に新APIへ移植 |
| 1.37.1 | LoadBeatmapLevelDataAsyncにBeatmapLevelDataVersion引数が増える。SongCoreの保存先取得APIも変更 | 1.35.0～1.37.0と分け、1.37.1を中間世代の基準にする |
| 1.37.1～1.39.1 | IBeatmapLevelDataを明示するStartStandardLevelのオーバーロードは共通 | 譜面データを先に取得してこのAPIを呼ぶ。版ごとのオーバーロード探索はしない |
| BSML 1.11→1.12 | GameplaySetupのSingleton基底型が変わる | DIでGameplaySetupを取得し、共通のAddTab / RemoveTabを使う。1.11でも型のDI登録をDLLで確認 |
| 1.40.0 | 光の色設定のbool引数が増える。PauseController._pausedがboolからPauseState列挙型へ変わる | 専用の開始・ポーズ処理へ変更。boolを書き込む旧処理を流用しない |
| SongCore 3.15系 | RetrieveDifficultyData / GetLoadedSaveDataがObsolete(error=true) | 新しい譜面情報APIを使う。保存先はこの版のCustomLevelLoaderの辞書から取得 |
| 1.41.1 | 開始APIの引数構成とオーバーロード数が変更 | 利用者0人のため専用ブランチは今回省略 |
| 1.42.0～1.44.1 | GameplayAdditionalInformationを使う開始APIが共通 | 最新対応枠を1.42.0基準で用意 |
| 1.44.2～1.45.0 | コールバックの型がStandardLevelScenesTransitionSetupDataSOから末尾SOなしへ変更。recordingToolDataもなくなる | 1.44.1までと同じDLLでは扱わず、今回の対象外とする |

1.37.1～1.39.1の共用は、既存の自然なAPIを選ぶ設計であり、互換性のための反射によるメソッド探索を増やす設計ではない。シーク等で既に必要な非公開フィールド参照は、そのブランチの型・署名に合わせて照合する。

### ScoreSaber / BeatLeader / Camera2

ゲーム版だけでなく導入MODのAPIも確認する。実際に、既存の1.34.2・1.37.3環境のScoreSaber 3.3系は、現在のリプレイが想定する3.4系のReplayStateRegistryを持たず、Plugin.Instance.ReplayStateを使っていた。

保存抑止は、確認できた旧・新ScoreSaberのAPIを小さな連携クラスに分けて扱う。ゲーム版ごとの巨大な分岐にはしない。BeatLeader、SongPlayHistoryも各検証環境の実DLLで照合し、保存抑止を確認できない状態でリプレイ開始を許可しないという現在の動作を維持する。

Camera2の公開ReplaySources APIを使う任意連携は残す。Camera2、CameraPlus、ScoreSaber、BeatLeaderへの必須依存やアセンブリ参照は追加しない。Camera2なし、CameraPlusのみ、対応するCamera2ありの組み合わせを分けて確認する。

### 1.29.0未満を同じDLLに含められるか

**現在のリプレイDLLを変更せずに、1.29.0未満まで対応範囲を広げることはできない。** 記録機能だけの0.2.4とは必要なAPIが異なる。

- 1.26.0と1.28.0の実DLLにはBufferedLightColorGroupEffectがない。現在のEnvironmentReplayStateはこの型を直接参照し、シーク時の照明復元と演出の再計算に使っている。
- 確認した1.25.0以前では、BeatmapCallbacksController._callCallbacksBehaviorとTweeningManager._ownerByTweenもない。環境演出やコールバックの復元処理も別対応になる。
- 1.29.0と1.29.1には必要な型があり、今回比較した137項目は一致した。ただしMODの準備と実機検証は別途必要。

1.26.0～1.28.0へ互換処理を追加して同じブランチに収める余地はあるが、型の参照方法と照明復元の実装・検証が増える。今回の「無理に複数版へ対応するためにコードを複雑にしない」という方針では、**新しいリプレイ版の下限は1.29.0とする**ことを推奨する。古いゲームでの記録には既存0.2.4を残す。
## 5. Legatoの扱い

[Legatoの説明](https://github.com/ScoreSaber/legato#how-it-works)と、バージョン定義・開始APIのアダプターを確認した。

Legatoは対象版に応じたソースをビルド時にMODへ組み込む方式であり、ゲーム・MOD参照の更新、未対応APIの修正、対象環境でのテストも必要となる。今回調べた定義には1.29.0～1.29.1、1.37.1～1.37.2、1.38.0～1.39.1、1.40.0～1.40.8、1.42.0～1.44.1のプロファイルがある。

今回は **差分調査の参考として使い、Legato自体は導入しない**。ユーザー指定の別ブランチ方針と、リプレイ独自の非公開状態の照合を中心に進める。LegatoのプロファイルとMovementRecorderの対応範囲は、使用するAPIが異なるので必ずしも一致させる必要はない。

## 6. manifest.jsonの確定案

以下は各リリースのmanifest全文。検証中も指定どおり0.3.0～0.3.3を使用し、独自のrc・preview接尾辞は付けない。AssemblyVersion / AssemblyFileVersionも各manifestのversionに揃える。

gameVersionには対応開始版を記載し、現在の1.20.0を更新する。0.3.0は1.29.0を記載し、通常のコンパイル参照は動作確認済みの1.29.1を維持する。1.29.0の互換性は別途照合・検証する。対応範囲の終端はリリース本文、対応表、BeatMods登録で示す。ZIP名はBeat Saber Modding Toolsの標準生成に任せる。[公式スキーマ](https://raw.githubusercontent.com/bsmg/BSIPA-MetadataFileSchema/master/Schema.json)

依存値は「過去の最古互換版」ではなく、今回の検証で基準にする承認済み版を下限とする。記法は既存と同じキャレット範囲とし、実際にビルド・確認した各ゲーム向けDLLの版は別途固定して記録する。同じMOD番号でも対象ゲームが異なるDLLを入れ替えてよいという意味ではない。

### BS1.29.1

~~~json
{
  "$schema": "https://raw.githubusercontent.com/bsmg/BSIPA-MetadataFileSchema/master/Schema.json",
  "id": "MovementRecorder",
  "name": "MovementRecorder",
  "author": "Rynan",
  "version": "0.3.0",
  "description": "Record object movement and replay recordings.",
  "gameVersion": "1.29.0",
  "loadAfter": [
    "Camera2"
  ],
  "dependsOn": {
    "BSIPA": "^4.3.6",
    "SiraUtil": "^3.1.2",
    "BeatSaberMarkupLanguage": "^1.6.10",
    "SongCore": "^3.11.1"
  },
  "links": {
    "project-home": "https://github.com/rynan4818/MovementRecorder",
    "project-source": "https://github.com/rynan4818/MovementRecorder"
  }
}
~~~

### BS1.37.1

~~~json
{
  "$schema": "https://raw.githubusercontent.com/bsmg/BSIPA-MetadataFileSchema/master/Schema.json",
  "id": "MovementRecorder",
  "name": "MovementRecorder",
  "author": "Rynan",
  "version": "0.3.1",
  "description": "Record object movement and replay recordings.",
  "gameVersion": "1.37.1",
  "loadAfter": [
    "Camera2"
  ],
  "dependsOn": {
    "BSIPA": "^4.3.6",
    "SiraUtil": "^3.1.11",
    "BeatSaberMarkupLanguage": "^1.11.4",
    "SongCore": "^3.14.11"
  },
  "links": {
    "project-home": "https://github.com/rynan4818/MovementRecorder",
    "project-source": "https://github.com/rynan4818/MovementRecorder"
  }
}
~~~

### BS1.40.0

~~~json
{
  "$schema": "https://raw.githubusercontent.com/bsmg/BSIPA-MetadataFileSchema/master/Schema.json",
  "id": "MovementRecorder",
  "name": "MovementRecorder",
  "author": "Rynan",
  "version": "0.3.2",
  "description": "Record object movement and replay recordings.",
  "gameVersion": "1.40.0",
  "loadAfter": [
    "Camera2"
  ],
  "dependsOn": {
    "BSIPA": "^4.3.6",
    "SiraUtil": "^3.2.1",
    "BeatSaberMarkupLanguage": "^1.12.5",
    "SongCore": "^3.15.2"
  },
  "links": {
    "project-home": "https://github.com/rynan4818/MovementRecorder",
    "project-source": "https://github.com/rynan4818/MovementRecorder"
  }
}
~~~

### main

~~~json
{
  "$schema": "https://raw.githubusercontent.com/bsmg/BSIPA-MetadataFileSchema/master/Schema.json",
  "id": "MovementRecorder",
  "name": "MovementRecorder",
  "author": "Rynan",
  "version": "0.3.3",
  "description": "Record object movement and replay recordings.",
  "gameVersion": "1.42.0",
  "loadAfter": [
    "Camera2"
  ],
  "dependsOn": {
    "BSIPA": "^4.3.7",
    "SiraUtil": "^3.3.1",
    "BeatSaberMarkupLanguage": "^1.14.1",
    "SongCore": "^3.16.0"
  },
  "links": {
    "project-home": "https://github.com/rynan4818/MovementRecorder",
    "project-source": "https://github.com/rynan4818/MovementRecorder"
  }
}
~~~

確認元はBeatModsの[1.29.1](https://beatmods.com/api/v1/mod?status=approved&gameVersion=1.29.1)、[1.37.1](https://beatmods.com/api/v1/mod?status=approved&gameVersion=1.37.1)、[1.40.0](https://beatmods.com/api/v1/mod?status=approved&gameVersion=1.40.0)、[1.40.8](https://beatmods.com/api/v1/mod?status=approved&gameVersion=1.40.8)、[1.42.0](https://beatmods.com/api/v1/mod?status=approved&gameVersion=1.42.0)等。0.3.0を公開する直前にも再取得する。

1.37.4～1.39.1ではBSML 1.12.2～1.12.4、SongCore 3.14.14～3.14.15も検証する。1.40.8ではSongCore 3.15.3を使う。1.42.0～1.44.1のAPI掲載基準は同じ主要依存版だが、実環境1.43.0にはSiraUtil 3.4.0も入っているため、これは追加の連携確認対象とする。

SongCoreが必要とするBS Utils等の間接依存はSongCore側のmanifestに任せる。MovementRecorderへ直接参照を追加しない限り、依存宣言を重複させない。組み込み済みLiteDB / System.Buffersは従来の隔離方法とライセンス表記を維持する。

## 7. 実施する作業・Git操作

1. 既存BS1.29.1の英語UI・キャッシュ修正・テスト・説明書を、日本語の簡潔な箇条書きでコミットする。
2. 第2版の計画、リリース文の原稿、通常ビルドと共通のZIP設定を用意し、BS1.29.1を0.3.0にする。
3. BS1.29.1の完成版からBS1.37.1を作成し、専用作業ツリーで1.37系へ移植して0.3.1としてコミットする。
4. BS1.37.1の完成版からBS1.40.0を作成し、専用作業ツリーで1.40系へ移植して0.3.2としてコミットする。
5. 既存mainを専用作業ツリーで開き、BS1.40.0の完成版をno-fast-forwardでマージする。既存mainの0.2.7の履歴と記録側の機能を保ち、競合は移植済みコードに合わせて解決する。その上で1.42系へ移植し0.3.3としてコミットする。
6. BS1.42.0は作成しない。既存のPlayerブランチ・旧タグは保持する。リセットや履歴の書き換えは行わない。
7. GitHub公開用に0.3.0～0.3.3それぞれのリリース本文を作成する。push、タグ作成、GitHubリリース公開、実ゲームへのDLL配置はこの作業では行わない。

この第2版は、提示済みの設計に対するユーザーの承認・変更指定を反映した実施計画である。
## 8. ビルド・動作確認

ビルド参照先は、指定された C:/Program Files (x86)/Steam/steamapps/common 以下の各Beat Saberフォルダーを使用できる。調査時に必要なゲーム本体DLLの存在を確認した。

1.29.1の通常環境は現在の検証環境を継続し、他はBeat Saber_1.37.1、Beat Saber_1.40.0、Beat Saber_1.42.0を基準にする。1.29.0、1.40.5～1.40.7、1.44.0～1.44.1等はゲーム本体のみで必要MODが不足しているので、参照用フォルダーに承認済み依存を揃える。調査や通常ビルドでは実ゲーム側のファイルを書き換えない。

### 自動検証

- 現在のデータ読取、選択、シーク、カメラ、モデル、表示切替の回帰テストを各ブランチで実行する。
- ゲームDLLの照合はメンバーの存在だけでなく、引数・戻り値・フィールド型・呼び出すオーバーロードまで確認する。PauseStateの型変更も検査対象とする。
- 各DLLの埋め込みmanifest、AssemblyVersion、パッケージ名、対象ゲーム参照が一致することを確認する。
- Camera2 / CameraPlusを必須参照していないこと、保存抑止API、異常な記録ファイルの扱いを確認する。
- Releaseビルドは既存のVisual Studio/MSBuild方式を使う。DataModelsや必要なUnityモジュールの参照をブランチごとに調整する。

### 実機確認の最低組み合わせ

| 系統 | 重点確認版 |
| --- | --- |
| BS1.29.1 | 1.29.1、および追加候補の1.29.0 |
| BS1.37.1 | 1.37.1、BSML変更境界の1.37.4、1.39.1 |
| BS1.40.0 | 1.40.0、1.40.8 |
| main | 1.42.0、1.42.2、1.44.1 |

英語UIの文字切れ・選択色、開始・終了・連続再生、ポーズ・再開・前後シーク、音声同期、表情同期、レイヤーと床ミラー、コピー元表示・オフセットON/OFF・非表示時の処理省略・設定保存、Camera2のREPLAY切替を確認する。

通常プレイとリプレイを続けて行い、ゲーム・ScoreSaber・BeatLeader・対応履歴MODの保存抑止がリプレイだけに適用されることを確認する。DLLのビルド成功やAPI照合成功を、これらの実機確認の代わりにはしない。

## 9. 配布と既存データ

ZIPはプロジェクトに導入済みのBeatSaberModdingTools.Tasks 2.0.0-beta1に生成させる。標準のBSMT_GetProjectInfoとBSMT_ZipReleaseを使用し、ArtifactNameやファイル名を独自に組み立てたり、生成後にリネームしたりしない。

標準生成形式はAssemblyName-PluginVersion-bsGameVersion-CommitHash.zip。出力先はMovementRecorder/bin/Release/zip。以下は標準名の例で、コミットIDはビルド時にModding Toolsが決める。

- MovementRecorder-0.3.0-bs1.29.0-<commit>.zip
- MovementRecorder-0.3.1-bs1.37.1-<commit>.zip
- MovementRecorder-0.3.2-bs1.40.0-<commit>.zip
- MovementRecorder-0.3.3-bs1.42.0-<commit>.zip

scripts/Build-Replay.ps1の独自Compress-Archive処理を廃止し、同じcsprojのReleaseビルドを呼ぶ。Visual Studioの手動ビルドも同じターゲットとOutputCopy項目を通す。利用説明書とライセンスの同梱もcsprojへ置き、どちらからビルドしてもZIPの内容が同じになるようにする。

4つのGitHubリリース（v0.3.0、v0.3.1、v0.3.2、v0.3.3）に各1つのZIPを添付する。各タグは対応ブランチのリリースコミットを指す。原稿はdocs/release-notes/v0.3.x.mdに置き、既存リリースの「Support for Beat Saber」「日本語の変更箇条書き」「対応版への案内」「README」「Full Changelog」の形式を引き継ぐ。原稿作成と公開は区別する。

同梱するのはPlugins/MovementRecorder.dll、利用者向け説明書、必要なライセンス。調査資料、ゲームDLL、依存MODのDLL、実際の記録・DB・ユーザー設定は含めない。同一ゲームに4つのDLLを同時に配置する構成にはしない。
既存のmvrec形式は維持し、0.2.4～0.2.7の記録を読めることを確認する。ただし記録にはモデル・表情データや当時の譜面設定が全て保存されているわけではなく、別世代のモデルの骨格へ変換する機能は追加しない。

ユーザー設定を一括削除させない。0.2.5以降の探索設定変更は、旧標準値に一致する場合だけ移行し、編集済みの探索設定と保存済みオフセットは保持する。リプレイの初期範囲（Solo / Standard / 両手セイバー / 必須拡張のない譜面）は現在の仕様を引き継ぐ。

## 調査資料

- SourceCode：D:/PROGRAM/github/@Maintenance/CameraSongScript/BeatSaber/SourceCode
- ゲーム本体：C:/Program Files (x86)/Steam/steamapps/common
- BSML：D:/PROGRAM/github/@Maintenance/CameraSongScript/BeatSaberMarkupLanguage。現在の作業ツリーはv1.6.10BS1.29.1。新しいタグ・origin/masterは作業ツリーを切り替えず参照した。
- SongCore：D:/PROGRAM/github/@Maintenance/CameraSongScript/SongCore。現在のmanifestは3.15.3。承認済み3.16.0のDLLも別途確認した。
- 承認済み依存27パッケージを調査用に取得し、BeatModsのMD5と照合した。
- 詳細なAPI、配布manifest、ゲームDLL照合、導入環境一覧はartifacts/release-analysis内に保存。これらは調査用であり配布物には含めない。
