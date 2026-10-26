# SceneBgm — AI エージェント向けメモ

人間向けの詳細は [README.ja.md](README.ja.md)（英語版は [README.md](README.md)）。ここは要点だけ。

## API 早見表（`using Kagami.SceneBgm;`、すべて static）

| API | 用途 |
|---|---|
| `SceneBgm.Play(AudioClip clip, float? fadeSeconds = null)` | 手動再生（ループ）。同じ曲なら何もしない。null は Stop |
| `SceneBgm.Stop(float? fadeSeconds = null)` | フェードアウト停止 |
| `SceneBgm.Volume` (get/set, 0〜1) | ユーザー音量。PlayerPrefs `"Kagami.SceneBgm.Volume"`（`SceneBgm.VolumePrefsKey`）に保存。未保存時は設定アセットの既定音量（`SceneBgmSettings.DefaultVolume`、ウィンドウ上部で設定、フィールド既定 1.0）、設定アセットが無ければ 1.0 |
| `SceneBgm.VolumeChanged` (`Action<float>`) | 音量変更通知 |
| `SceneBgm.SetVolumeMultiplier(float)` | 保存しない倍率（ダッキング） |
| `SceneBgm.CurrentClip` / `IsPlaying` / `Settings` | 状態の参照 |

- プレイヤーは `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` で自動生成される。シーンに何も置かない。
- シーン読み込み（Single のみ）で設定に従う。手動 `Play` 後も次のシーンでは設定が優先。
- 最初のシーンで手動再生するなら `Start` 以降で呼ぶ（`Awake` だと設定で上書きされる）。

## 導入手順

1. git URL: `manifest.json` の `dependencies` に `"com.kagami003d.scenebgm": "https://github.com/Kagami003d/SceneBgm.git#v1.0.0"`（パッケージはリポジトリ直下）。embedded: `Packages/com.kagami003d.scenebgm/` に置く（manifest 変更不要）。
2. `Window > SceneBgm` を開く → `Assets/Resources/SceneBgmSettings.asset` が作られる。
3. 全シーンに [継続]/[停止]/曲 を設定する。
4. ウィンドウ上部の未設定件数が0であることを確認する（Validate ボタン、またはエディタ外なら下記の `RunBatch`）。

## シーン設定を追加する手順

- **推奨: ウィンドウ**（`Window > SceneBgm`）。Build Settings にシーンを追加すると一覧に出るので、行のボタンか BGM 欄を操作する。
- **アセットを直接編集する場合**（エディタが使えないとき）: `Assets/Resources/SceneBgmSettings.asset` の `entries` に追加する。

```yaml
  entries:
  - sceneGuid: <シーンの .unity.meta の guid>
    scenePath: Assets/Scenes/Foo.unity   # 実行時の照合キー。Scene.path と完全一致させる
    mode: 3                              # 0=Unset 1=Continue 2=Stop 3=Clip
    clip: {fileID: 8300000, guid: <AudioClip の .meta の guid>, type: 3}   # mode 3 以外は {fileID: 0}
```

  - `mode: 3` でクリップが `{fileID: 0}` だと読み込み時に `Unset` に直される。`mode` が 3 以外ならクリップは外される。
  - 編集後は Unity で `Window > SceneBgm` を開いて Validate ボタンを押すか、`RunBatch` を実行する（GUID から `scenePath` も更新される）。

## 検証コマンド

```
Unity.exe -batchmode -projectPath <project> -executeMethod Kagami.SceneBgm.Editor.SceneBgmValidator.RunBatch -logFile <log>
```

- 終了コード 0 = 設定アセットあり（未設定シーンは `[SceneBgm] BGMが未設定のシーン: <path>` の警告としてログに出る）。
- 終了コード 1 = 設定アセットが無い／読めない。
- テスト: Test Runner（または Unity MCP の `run_tests`）で `Kagami.SceneBgm.Editor.Tests`（EditMode）/ `Kagami.SceneBgm.Tests`（PlayMode）。

## 禁止事項

- `SceneBgmMode` の既存の値（0〜3）を変更・削除・並べ替えしない（int で保存されている。追加のみ）。
- 設定アセットをパッケージ内や `Assets/Resources/` 以外に置かない（実行時は `Resources.Load("SceneBgmSettings")` だけを読む）。設定アセットを2つ作らない。
- パッケージのコードから `Assembly-CSharp` やゲーム固有の名前空間を参照しない（asmdef の `references` を増やさない）。外部パッケージに依存させない。
- シーンに `[SceneBgm]` や `AudioSource` を手で置かない（自動生成される）。
- `scenePath` だけを書き換えて GUID を合わせないまま運用しない（エディタが GUID から上書きする）。

## よくあるエラーと対処

| 症状 | 原因 / 対処 |
|---|---|
| `[SceneBgm] 設定アセットが見つかりません` | `Window > SceneBgm` を開いて作る。パスが `Assets/Resources/SceneBgmSettings.asset` か確認 |
| `設定アセットが <path> にあります` | 別の場所にある。`Assets/Resources/SceneBgmSettings.asset` に移動する |
| `BGMが未設定のシーン: <path>` | そのシーンに [継続]/[停止]/曲 のどれかを設定する（ビルドは止まらない） |
| シーンに入っても曲が変わらない | Additive で読み込んでいる / そのシーンが `Continue`・未設定 / `scenePath` が `Scene.path` と一致していない（Validate で更新） |
| 曲が二重に鳴る | ゲーム側の別の BGM 再生処理と併用している。どちらか一方にする |
| 音が出ない | シーンに `AudioListener` が無い / `SceneBgm.Volume` や倍率が 0 / 出力先の AudioMixerGroup がミュート |
