# SceneBgm

[English](README.md) | **日本語**

**シーンをまたいでBGMを流し続ける処理、毎回書いていませんか？**

SceneBgm は、エディタウィンドウで Build Settings の全シーンに **[継続] / [停止] / 曲（AudioClip）** を割り当てるだけで、
シーン遷移時のBGMを引き受ける Unity パッケージです。シーンに何かを置く必要はありません。

ボタンで [継続] / [停止] を選び、曲をドラッグで入れて、未設定が0件になれば完了です。

![Window > SceneBgm で設定する様子](Documents~/assign.gif)

Play 中はシーンを移るとクロスフェードし、[継続] なら同じ曲が途切れずに流れ、[停止] ならフェードアウトします。
シーン側には BGM のコードを書いていません（[音付き MP4](Documents~/playmode.mp4)）。

![Play 中のシーン遷移](Documents~/playmode.gif)

## 自作するとハマるところ、全部やります

| よくある落とし穴 | SceneBgm では |
|---|---|
| BGM用オブジェクトを `DontDestroyOnLoad` したら、シーンに戻るたびに増殖して二重に鳴る | 起動時に1つだけ自動で作る。シーンには何も置かない |
| 途中のシーンから Play すると無音 | どのシーンから Play しても、そのシーンの設定で鳴る |
| 同じ曲のシーンへ移ったのに頭から流れ直す | 同じ曲なら途切れず、再生位置もそのまま |
| クロスフェードを自前で書くのが面倒 | 全シーン共通のフェード時間で自動クロスフェード |
| ポーズ（`Time.timeScale = 0`）でフェードが止まる / `AudioListener.pause` でBGMまで止まる | unscaled time で進み、`AudioListener.pause` の影響も受けない |
| 「このシーンは継続」の指定が各シーンのスクリプトに散らばる | ウィンドウの一覧で全シーンを見渡して設定 |
| シーンを足したときに BGM の設定を忘れる | 未設定のシーンは黄色で表示し、ビルド時にも警告 |
| シーンを移動・リネームしたら設定が外れた | GUID で覚えているので外れない |
| 音量の保存・復元を毎回書く | `SceneBgm.Volume` に代入するだけで保存され、次回起動時に復元 |

- 対応: Unity 6000.3 以降
- 依存: なし（Unity 標準機能のみ）
- 名前空間: `Kagami.SceneBgm`（エディタ拡張は `Kagami.SceneBgm.Editor`）

AI エージェント向けの要約は [AGENTS.md](AGENTS.md) を参照してください。

---

## 導入

### git URL で入れる（おすすめ）

Unity の **Window > Package Manager** → 左上の **＋** → **Install package from git URL...** に次の URL を入力します。

```
https://github.com/Kagami003d/SceneBgm.git#v1.0.0
```

または `Packages/manifest.json` の `dependencies` に直接追加します。

```json
"com.kagami003d.scenebgm": "https://github.com/Kagami003d/SceneBgm.git#v1.0.0"
```

`#v1.0.0` はタグです。省略すると既定ブランチの最新を取得します。

### フォルダーごと置く（embedded package）

このリポジトリ（または配布アーカイブを展開したフォルダー）を、プロジェクトの `Packages/com.kagami003d.scenebgm/` として置きます。
`Packages/manifest.json` の変更は不要です（Unity が自動で認識し、Package Manager に「SceneBgm」と表示されます）。

ランタイムのアセンブリ（`Kagami.SceneBgm`）は Auto Referenced なので、`Assembly-CSharp` からそのまま `using Kagami.SceneBgm;` で使えます。
自分の asmdef から使う場合は `Kagami.SceneBgm` を参照に追加してください。

---

## 使い方

1. メニュー **Window > SceneBgm** を開きます。
   初めて開いたときに設定アセット `Assets/Resources/SceneBgmSettings.asset` が作られます。
2. 上部で **フェード時間（秒）**（全シーン共通、既定 0.75）、**既定音量**（BGM音量がまだ保存されていないときの値、0〜1、既定 1.0）と、必要なら **出力先（AudioMixerGroup）** を設定します。
3. 一覧（Build Settings のシーンが同じ順番で並びます）の各行で、次のどれか1つを選びます。

![Window > SceneBgm](Documents~/window.png)

| 選択 | シーンに入ったときの動作 |
|---|---|
| **[継続]** | 直前のBGMをそのまま流し続ける |
| **[停止]** | フェードアウトして止める |
| **BGM欄に AudioClip** | その曲へクロスフェードする（同じ曲が流れていれば何もしない＝途切れず、再生位置も戻らない） |
| どれも選ばない（未設定） | 直前のBGMを継続する。**ビルド時に警告**が出る |

### 排他の仕様

- 3つのうち選べるのは1つだけです。
- [継続] / [停止] を押すと、そのモードになり、BGM欄は **None に戻ります**。
- BGM欄にクリップを入れると、[継続] / [停止] の選択は外れます。
- BGM欄を None に戻すと **未設定** になります。
- 選択中の [継続] / [停止] をもう一度押すと、選択が外れて **未設定** に戻ります。

### 画面の見かた

- 未設定の行は警告アイコンと黄色の背景で表示され、上部に未設定の件数が出ます。
- Build Settings で無効（チェックなし）のシーンはグレーで表示されます。設定はできますが、ビルドに含まれないので警告の対象外です。
- シーン名にマウスを乗せるとパスが表示されます。
- Build Settings から外したシーンの設定は消えずに残り、再登録すると元に戻ります。
  一覧の下の「**Build Settings に無い設定（n件）**」で確認・削除できます（削除は Undo できます）。
- 変更はすぐ設定アセットに保存されます（Undo / Redo 可）。

### シーンの移動・リネーム

シーンは GUID で覚えているので、Project ウィンドウでシーンを移動・リネームしても設定は外れません
（実行時に使うパスは、移動・リネームのたびに自動で更新されます。ウィンドウを開いたとき・ビルド前・Validate のときにも更新されます）。
シーンを削除しても設定は残り、Validate で「シーンが見つからない設定」として報告されます。

---

## 実行時の仕様

- Play 開始時（最初のシーンを読み込む前）に、GameObject `[SceneBgm]`（`AudioSource` 2本）が自動で作られ、`DontDestroyOnLoad` されます。
  シーンに何かを置く必要はありません。どのシーンから Play しても最初のシーンの設定が適用されます。
- シーンを `LoadSceneMode.Single` で読み込んだときに、そのシーンの設定を適用します。**Additive 読み込みは無視します。**
- 実行時はシーンの `Scene.path` で設定を探します。
- フェードは `Time.unscaledDeltaTime` で進むので、`Time.timeScale = 0`（ポーズ等）でも止まりません。
  BGM の `AudioSource` は `ignoreListenerPause = true`（`AudioListener.pause` の影響を受けない）、ループ再生です。
- 停止し終わった `AudioSource` は `clip = null` にして Streaming のメモリを解放します。
- `AudioListener` は追加しません（ゲーム側のものを使ってください）。
- 設定アセットが無い場合は、警告を1回出して、シーンに応じた自動切替だけを行いません（例外は出ません）。
  手動の API（`Play` / `Stop` / `Volume`）はそのまま使えます（フェード時間は既定の 0.75 秒）。
- ドメインリロードを無効にした Enter Play Mode でも、前回の状態は持ち越しません。

---

## 公開API

```csharp
using Kagami.SceneBgm;

SceneBgm.Play(clip);            // 設定の一律フェード時間でクロスフェード。同じ曲なら何もしない。null なら Stop と同じ
SceneBgm.Play(clip, 0f);        // フェード時間を指定（0 で即切替）
SceneBgm.Stop();                // フェードアウトして停止
SceneBgm.Stop(1.5f);

SceneBgm.Volume = 0.5f;         // 0〜1 にクランプ。即時反映・PlayerPrefs に保存・VolumeChanged を発火
SceneBgm.VolumeChanged += v => slider.SetValueWithoutNotify(v);
SceneBgm.SetVolumeMultiplier(0.3f); // 保存しない内部倍率（ダッキング等）。0〜1

AudioClip current = SceneBgm.CurrentClip; // 今「鳴らすべき」曲（フェードアウト中の曲は含まない）。停止中は null
bool playing = SceneBgm.IsPlaying;
SceneBgmSettings settings = SceneBgm.Settings; // 読み込まれた設定（無ければ null）
```

| メンバー | 説明 |
|---|---|
| `Play(AudioClip clip, float? fadeSeconds = null)` | 手動で再生（ループ）。`fadeSeconds` が null なら設定の一律フェード時間 |
| `Stop(float? fadeSeconds = null)` | フェードアウトして停止 |
| `Volume` | ユーザー音量 0〜1。`PlayerPrefs` キー `SceneBgm.VolumePrefsKey`（`"Kagami.SceneBgm.Volume"`）に保存。値が無いときは設定アセットの **既定音量**（`SceneBgmSettings.DefaultVolume`）、設定アセットも無ければ `SceneBgm.DefaultVolume`（1.0）。同じ値の代入でも未保存なら保存する |
| `VolumeChanged` | `Volume` が変わったとき（`Action<float>`、クランプ後の値） |
| `SetVolumeMultiplier(float)` / `VolumeMultiplier` | 保存しない内部倍率（既定 1） |
| `CurrentClip` / `IsPlaying` | 現在の曲 / 再生中か |
| `Settings` | 読み込まれた `SceneBgmSettings`（無ければ null） |

- 最終音量 = `Volume` × `VolumeMultiplier` × フェード係数
- `PlayerPrefs.Save()` は変更のたびではなく、最後の変更から 0.5 秒後にまとめて行います。
  アプリのポーズ・フォーカス喪失・終了時には必ず保存します。
- **手動で `Play` した後でも、次のシーン読み込み時は設定に従います**（[継続] / 未設定のシーンなら手動の曲が続きます）。
  最初のシーンで手動再生したい場合は、`Awake` ではなく `Start` 以降で呼んでください（`Awake` はシーンの設定適用より前に動くため、設定で上書きされます）。

---

## ビルド時の警告と Validate

![Validate ボタンと Console の警告](Documents~/validate.gif)

- ビルド前に、**有効な Build Settings シーンのうち未設定のもの**をシーンのパス付きで `Debug.LogWarning` します（1シーン1行＋まとめ1行）。
  **ビルドは止めません。** 設定アセットが無い場合は警告1行だけです。
- 同じ検査を `Window > SceneBgm` の **Validate ボタン**で実行できます。問題が無ければ Log を出します（未設定の行はウィンドウ上でも黄色で表示されます）。
- バッチ実行（CI や AI の検証用）:

```
Unity.exe -batchmode -projectPath <project> -executeMethod Kagami.SceneBgm.Editor.SceneBgmValidator.RunBatch -logFile <log>
```

  未設定のシーンがあっても警告を出して **終了コード 0**。設定アセットが無い／読めない場合だけ **終了コード 1** です。

---

## 設定アセット

- 置き場所は `Assets/Resources/SceneBgmSettings.asset` 固定です（実行時に `Resources.Load` で読みます）。
  **パッケージ内には置かないでください**（パッケージ更新で消えます）。別の場所に置くとウィンドウに警告が出ます。
- Inspector では概要と「ウィンドウを開く」ボタンだけを表示します。編集はウィンドウから行ってください。
- 不整合（[継続]/[停止] なのにクリップが入っている等）は読み込み時・編集時に自動で直します。

---

## 制限事項

- BGM は1系統（2本の `AudioSource` によるクロスフェード）だけです。SE 機能、プレイリスト／シャッフル、イントロ付きループはありません。
- 曲ごとの音量・ループ有無の設定はありません（常にループ、音量は `Volume` × 倍率）。
- Additive で読み込んだシーンの設定は使われません。
- 設定アセットは1つ（`Assets/Resources/SceneBgmSettings.asset`）だけです。
- 実行時の照合はシーンのパスで行います。ビルド後にパスが変わることはありませんが、エディタ外（エクスプローラー等）でシーンを移動した場合は、
  次に Unity がインポートしたとき（またはウィンドウを開いたとき・ビルド前）に更新されます。

## テスト

`Tests/Editor`（EditMode）と `Tests/Runtime`（PlayMode、テスト用の空シーン3つ）に Unity Test Framework のテストがあります。
Test Runner の `Kagami.SceneBgm.Editor.Tests` / `Kagami.SceneBgm.Tests` から実行できます。

git URL で入れた場合、パッケージのテストは既定では Test Runner に出ません。`Packages/manifest.json` に次を追加してください（embedded の場合は不要です）。

```json
"testables": [ "com.kagami003d.scenebgm" ]
```

## 変更履歴

[CHANGELOG.md](CHANGELOG.md) を参照してください。

## ライセンス

[MIT License](LICENSE.md)
