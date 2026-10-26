# Changelog

このパッケージの変更履歴です。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準じ、[Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [1.0.0] - 2026-10-26

### Changed

- 初の公開版。機能は 0.1.2 と同じ。
- README を英語版（`README.md`）と日本語版（`README.ja.md`）に分け、操作の様子の GIF / スクリーンショット（`Documents~/`）を追加。
- MIT License（`LICENSE.md`）を追加。

## [0.1.2] - 2026-09-24

### Removed

- メニュー `Tools > SceneBgm > Validate` を削除（ウィンドウの未設定表示・Validate ボタンと重複していたため）。検査はウィンドウの Validate ボタン、ビルド前の自動警告、バッチ用 `SceneBgmValidator.RunBatch` で行う。

## [0.1.1] - 2026-09-24

### Added

- 設定アセットに **既定音量**（`SceneBgmSettings.DefaultVolume`、0〜1、既定 1.0）を追加。`SceneBgm.Volume` が `PlayerPrefs` に保存されていないとき（初回起動時など）はこの値を使う。設定アセットが無いときは従来どおり `SceneBgm.DefaultVolume`（1.0）。`Window > SceneBgm` の上部で編集できる。
- EditMode / PlayMode テストに既定音量のケースを追加。

### Changed

- `SceneBgm.Volume` に現在値と同じ値を代入したとき、まだ保存されていなければ `PlayerPrefs` に保存するようにした（`VolumeChanged` は従来どおり発火しない）。既定音量と同じ値を明示的に設定した場合も、後で既定音量を変えたときに値が変わらない。

## [0.1.0] - 2026-09-24

### Added

- シーンごとのBGM設定（`SceneBgmSettings`、`Assets/Resources/SceneBgmSettings.asset`）。モードは `Unset` / `Continue` / `Stop` / `Clip`。シーンは GUID で識別し、実行時用のパスを自動更新する。
- エディタウィンドウ `Window > SceneBgm`（Build Settings の全シーンに [継続] / [停止] / BGM を排他で設定、一律フェード時間、任意の AudioMixerGroup、Undo 対応、Build Settings に無い設定の保持と削除）。
- 実行時プレイヤー（自動生成・`DontDestroyOnLoad`・2本の `AudioSource` によるクロスフェード・`unscaledDeltaTime`・Additive 読み込みは無視）。
- 公開API `SceneBgm`（`Play` / `Stop` / `Volume` / `VolumeChanged` / `SetVolumeMultiplier` / `CurrentClip` / `IsPlaying` / `Settings`）。
- ビルド前の未設定シーン警告（`IPreprocessBuildWithReport`、ビルドは止めない）、`Tools > SceneBgm > Validate`、バッチ用 `SceneBgmValidator.RunBatch`。
- EditMode / PlayMode テスト。
