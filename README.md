# MovementRecorder
このBeatSaberプラグインは、アバターやセイバー等のオブジェクトの動きを曲の時間に合わせて記録するツールです。

[ChroMapper-CameraMovement](https://github.com/rynan4818/ChroMapper-CameraMovement)で記録したファイルを読み込んで再生することができます。

以下のmodの記録はデフォルトで設定してあります。

- アバター
  - [Custom Avatars](https://github.com/nicoco007/BeatSaberCustomAvatars)
  - [VMCAvatar](https://github.com/nagatsuki/VMCAvatar-BS)
  - [NalulunaAvatars](https://nalulululuna.fanbox.cc/)
- セイバー
  - [Saber Factory](https://github.com/ToniMacaroni/SaberFactory)
  - [CustomSaber](https://nalulululuna.fanbox.cc/)

※設定ファイルを作成すれば、任意のオブジェクトも記録可能です。

# インストール方法
1. [リリースページ](https://github.com/rynan4818/MovementRecorder/releases)から最新のMovementRecorderのリリースをダウンロードします。
2. ダウンロードしたzipファイルを`Beat Saber`フォルダに解凍して、`Plugin`フォルダに`MovementRecorder.dll`ファイルをコピーします。
3. 依存modは`SiraUtil`, `BSML`, `SongCore`の3つです。基本modなので既に入っているはずです。

# 使い方
左のMODSタブに`MOVEMENT RECORDER`が追加されます。

![image](https://github.com/rynan4818/MovementRecorder/assets/14249877/01ac34fa-cbbf-4d89-9123-b14da3415550)

![image](https://github.com/rynan4818/MovementRecorder/assets/14249877/df3e7665-c946-42ed-9d2b-7baea3fb3539)

![image](https://github.com/rynan4818/MovementRecorder/assets/14249877/878399cd-cb14-488c-af60-feca88cbc551)


* `Movement Recorder Enabled` : 本modの機能を有効にします。
* `WIP Map Only` : WIP譜面でのみ記録します。
* `Avatar Movement` : アバター表示で使用しているmodを選択します。
* `Saber Movement` : セイバー表示で使用しているmodを選択します。
* `Other Movement1～3` : その他のオブジェクトを記録します。（要設定作成)
* `Record Frame Rate(fps)` : 動きを記録する間隔を設定します。
* `Movement Research` : 動きのあるオブジェクトを調査します。
* `Research Check Song Sec(s)` : 調査する曲の経過時間を選択します。
* `===Movement Recorder Log===` : 記録した結果のログを表示します。
  * `Initialize Time` : ゲーム開始時の初期化時間です。
  * `Capture Object Count` : 記録するオブジェクトの数です。
  * `Record Inistialize Size` : 記録用に初期化した配列の数です。
  * `Record Count` :  記録したフレーム数です。
  * `Last Song Time` : 最後の記録フレームの曲時間です。
  * `Save File Time` : 保存に要した時間です。
  * `Save File Size` : 保存したファイルのサイズです。
  * `One Movement Recording Time` : 1フレームの記録に要した時間の平均,最大,最小時間です。

## 基本的な使い方

1. AvatarとSaberの設定を使用しているmodに合わせて選択します
2. 主にカメラスクリプト作成を想定しているので、WIPに対象譜面を入れて記録機能を有効にしてプレイします。
3. WIP譜面の場合は譜面フォルダに`MovementRecorder`フォルダが作成されて、そこに記録ファイルが保存されます。

    通常譜面は`UserData/MovementRecorder`に保存されます。
4. CameraMovementで読み込んで再生します。※使用したアバターやセイバーは記録時と同じファイルを読み込んで下さい。

## 注意点
動きデータはプレイ中は予め確保したメモリ(配列)に保存だけして極力プレイに負荷がかからないようになっていますが、プレイ終了後にバックグラウンドで保存するため負荷が高くなります。次のプレイ開始までに保存が完了しないと開始できないため、開始時に画面が暗転した状態で固まる場合があります。特にゲーム中に中断してリプレイした場合は保存する時間が無いため顕著に固まりますので注意して下さい。30MBぐらいのデータでも環境によりますが4～5秒はかかります。

記録ファイルはかなりサイズが大きくなります。アバターやセイバーの構成しているオブジェクトの数や、記録時間、fpsによりますが数十MBから100MB以上になることもあります。なので普段の記録に使用する場合は容量に注意してください。

fpsを落としてもCameraMovementで再生時に中間を補間して表示しますので、表示はなめらか※になります。用途に合わせて設定して下さい。

※線形補間しているためセイバーなど動きの激しい部分は、直線運動以外(円運動・斬り返しなど)に一部破綻が出てきます）

デフォルト設定ではアバターやセイバーを構成するオブジェクトの全てを記録しています。これはモデル毎にオブジェクト構成が違うため汎用的にするための措置です。

実際に記録が必要なオブジェクトはもっと少なくできます。特に揺れものはCameraMovementで再生時にVRMならSpring Boneで、CustomAvatarならDynamicBone※で揺らせますので、記録から除外することが可能です。そのためには、モデルごとにオブジェクトの構成を調査して設定ファイルの記録対象や除外対象を作成する必要があります。

※DynamicBoneをChroMapperで動作させるには、BeatSaberのゲームフォルダから`DynamicBone.dll`をコピーしてPluginフォルダに入れて下さい。

### 記録容量の参考

記録オブジェクト数(Capture Object Count) × 記録秒数(s) × fps × 7(座標3,回転4) × 4(Byte:float) = 必要メモリ数[Byte]

上記が記録に必要なメモリサイズで、ゲームプレイの最初にメモリが確保されます。90秒の曲でオブジェクト数180で30fpsだと13MByteぐらいです。

記録ファイルはJSONなので、数値は12～13文字ぐらい=12～13Byteぐらいになるので、上記の3倍＋αぐらいのサイズになります。

### 揺れものなどのオブジェクトの除外の仕方
1. `Movement Research`を有効にして、動き調査ファイル`Motion_Research_Data.json`を作成します。
2. 調査結果を見ながら不要なオブジェクトを探します。
3. 使用する設定ファイルの`searchSettings`の項目をコピーして、`exclusionStrings`項目を追加して調査結果で不要なオブジェクトを追加していきます。設定は正規表現になるので、()などのメタ文字は[エスケープが必要](https://www-creators.com/archives/3102)です。正規表現がわかる人は、子オブジェクト全てなどは/.+などを指定して記載を省略できます。
4. 作成した設定に変更して記録します。  

## 設定ファイルについて
`UserData/MovementRecorder.json`にmodの設定ファイルが保存されます。
設定ファイルのうち`searchSettings`を独自に作成することで、任意のオブジェクトを記録することができます。

```json
  "searchSettings": [
    {
      "name": "CustomAvatar",
      "type": "Avatar",
      "topObjectStrings": [
        "^.+/Avatar Container/SpawnedAvatar[^/]+/"
      ],
      "rescaleString": "^.+/Avatar Container/SpawnedAvatar[^/]+$",
      "searchStirngs": [
        "^.+/Avatar Container/SpawnedAvatar[^/]+(/.+)?"
      ],
      "exclusionStrings": [
        "^.+/Avatar Container/SpawnedAvatar[^/]+/Head(/.+)?",
        "^.+/Avatar Container/SpawnedAvatar[^/]+/LeftHand(/.+)?",
        "^.+/Avatar Container/SpawnedAvatar[^/]+/LeftLeg(/.+)?",
        "^.+/Avatar Container/SpawnedAvatar[^/]+/Pelvis(/.+)?",
        "^.+/Avatar Container/SpawnedAvatar[^/]+/RightHand(/.+)?",
        "^.+/Avatar Container/SpawnedAvatar[^/]+/RightLeg(/.+)?",
        "^.+/Avatar Container/SpawnedAvatar[^/]+/Body(/.+)?"
      ]
    }
  ]
```
* `name` : 設定の名前です。メニューで選択するときに表示されます
* `type` : 設定の種類です。`Avatar`, `Saber`, `Other`から選択します。
* `topObjectStrings` : オブジェクトのパスのうち不要な先頭部分を正規表現で設定します。読み込み時に、この設定に一致した部分を削除します。
* `rescaleString` : オブジェクトのスケールを設定しているパスを正規表現で指定します。
* `searchStirngs` : 記録するオブジェクトのパスを正規表現で指定します。
* `exclusionStrings` : 除外するオブジェクトのパスを正規表現で指定します。

## 調査ファイルについて
`Movement Research`を有効時にプレイすると`MovementRecorder`フォルダに`Motion_Research_Data.json`が保存されます。

これは、曲開始時間から指定した経過時間(デフォルトは1秒後)で座標や角度が変化したオブジェクトの一覧が記録されます。

内容はワールド座標・角度で変化のあったもの、ローカル座標・角度で変化のあったのも、スケールが1倍以外のオブジェクト一覧です。

記録対象の設定ファイルを作成するときに使えると思います。
