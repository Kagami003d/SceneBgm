using System;
using UnityEngine;

namespace Kagami.SceneBgm
{
    /// <summary>
    /// SceneBgm の公開API（ゲーム側はこのクラスの静的メンバーだけを使う）。
    /// <para>
    /// プレイヤー（GameObject <c>[SceneBgm]</c>）は <c>RuntimeInitializeOnLoadMethod(BeforeSceneLoad)</c> で自動生成されるので、
    /// シーンに何かを置く必要はない。シーン読み込み時のBGM切替は設定アセット（<see cref="SceneBgmSettings"/>）に従う。
    /// </para>
    /// <para>
    /// 最終音量 = <see cref="Volume"/> × <see cref="VolumeMultiplier"/> × フェード係数。
    /// </para>
    /// </summary>
    public static class SceneBgm
    {
        /// <summary><see cref="Volume"/> を保存する <see cref="PlayerPrefs"/> のキー。</summary>
        public const string VolumePrefsKey = "Kagami.SceneBgm.Volume";

        /// <summary>
        /// <see cref="PlayerPrefs"/> に値が無く、設定アセットも無いときの <see cref="Volume"/>。
        /// 設定アセットがあれば <see cref="SceneBgmSettings.DefaultVolume"/> を使う。
        /// </summary>
        public const float DefaultVolume = 1f;

        private static float volume = DefaultVolume;
        private static bool volumeLoaded;
        private static float volumeMultiplier = 1f;

        /// <summary><see cref="Volume"/> が変わったとき（クランプ後の値）。</summary>
        public static event Action<float> VolumeChanged;

        /// <summary>
        /// BGMのユーザー音量（0〜1）。代入でクランプ・即時反映・<see cref="PlayerPrefs"/> への保存（<see cref="VolumePrefsKey"/>）・<see cref="VolumeChanged"/> の発火を行う。
        /// 保存値が無いときは設定アセットの <see cref="SceneBgmSettings.DefaultVolume"/>（設定アセットが無ければ <see cref="DefaultVolume"/>）。
        /// 現在値と同じ値の代入ではイベントを発火しないが、まだ保存されていなければ保存する。
        /// </summary>
        public static float Volume
        {
            get
            {
                EnsureVolumeLoaded();
                return volume;
            }
            set
            {
                EnsureVolumeLoaded();
                float clamped = Mathf.Clamp01(value);
                if (clamped == volume)
                {
                    if (!PlayerPrefs.HasKey(VolumePrefsKey))
                    {
                        // 既定音量と同じ値を明示的に代入した場合も保存する（後で既定音量が変わっても値を保つ）。
                        PlayerPrefs.SetFloat(VolumePrefsKey, clamped);
                        SaveOrMarkDirty();
                    }

                    return;
                }

                volume = clamped;
                PlayerPrefs.SetFloat(VolumePrefsKey, clamped);
                SceneBgmPlayer player = SceneBgmPlayer.Instance;
                if (player != null)
                {
                    // 保存は最後の変更から少し待ってまとめて行う（スライダー操作中の書き込みを間引く）。
                    player.MarkPrefsDirty();
                    player.ApplyVolumes();
                }
                else
                {
                    PlayerPrefs.Save();
                }

                VolumeChanged?.Invoke(clamped);
            }
        }

        /// <summary>保存しない内部倍率（0〜1、既定1）。ダッキングなどに使う。</summary>
        public static float VolumeMultiplier => volumeMultiplier;

        /// <summary>現在「鳴らすべき」クリップ（フェードアウト中のものは含まない）。停止中は null。</summary>
        public static AudioClip CurrentClip
        {
            get
            {
                SceneBgmPlayer player = SceneBgmPlayer.Instance;
                return player != null ? player.CurrentClip : null;
            }
        }

        /// <summary><see cref="CurrentClip"/> が再生中なら true。</summary>
        public static bool IsPlaying
        {
            get
            {
                SceneBgmPlayer player = SceneBgmPlayer.Instance;
                return player != null && player.IsPlaying;
            }
        }

        /// <summary>読み込まれた設定アセット（無ければ null）。</summary>
        public static SceneBgmSettings Settings
        {
            get
            {
                SceneBgmPlayer player = SceneBgmPlayer.Instance;
                return player != null ? player.Settings : null;
            }
        }

        /// <summary>
        /// 手動でBGMを再生する。同じクリップが再生中なら何もしない（再生位置は戻らない）。
        /// <paramref name="clip"/> が null なら <see cref="Stop"/> と同じ。次のシーン読み込み時は設定に従う。
        /// </summary>
        /// <param name="clip">再生するクリップ（ループ再生）。</param>
        /// <param name="fadeSeconds">クロスフェード時間。null なら設定の一律フェード時間（設定が無ければ <see cref="SceneBgmSettings.DefaultFadeSeconds"/>）。</param>
        public static void Play(AudioClip clip, float? fadeSeconds = null)
        {
            SceneBgmPlayer player = SceneBgmPlayer.Instance;
            if (player != null)
            {
                player.PlayClip(clip, player.ResolveFadeSeconds(fadeSeconds));
            }
        }

        /// <summary>BGMをフェードアウトして停止する（停止中なら何もしない）。</summary>
        /// <param name="fadeSeconds">フェード時間。null なら設定の一律フェード時間。</param>
        public static void Stop(float? fadeSeconds = null)
        {
            SceneBgmPlayer player = SceneBgmPlayer.Instance;
            if (player != null)
            {
                player.StopClip(player.ResolveFadeSeconds(fadeSeconds));
            }
        }

        /// <summary>保存しない内部倍率（0〜1にクランプ）を設定する。即座に反映される。</summary>
        public static void SetVolumeMultiplier(float multiplier)
        {
            float clamped = Mathf.Clamp01(multiplier);
            if (clamped == volumeMultiplier)
            {
                return;
            }

            volumeMultiplier = clamped;
            SceneBgmPlayer player = SceneBgmPlayer.Instance;
            if (player != null)
            {
                player.ApplyVolumes();
            }
        }

        /// <summary>
        /// 保存値が無いときの音量。設定アセットがあればその <see cref="SceneBgmSettings.DefaultVolume"/>、無ければ <see cref="DefaultVolume"/>。
        /// </summary>
        internal static float ResolveDefaultVolume(SceneBgmSettings settings)
        {
            return settings != null ? settings.DefaultVolume : DefaultVolume;
        }

        /// <summary>フェード係数を掛ける前の音量（<see cref="Volume"/> × <see cref="VolumeMultiplier"/>）。</summary>
        internal static float Gain => Volume * volumeMultiplier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            // ドメインリロード無効でも前回のPlayの状態を持ち越さない。
            volume = DefaultVolume;
            volumeLoaded = false;
            volumeMultiplier = 1f;
            VolumeChanged = null;
            SceneBgmPlayer.ResetStatics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneBgmPlayer.EnsureInstance();
        }

        /// <summary><see cref="PlayerPrefs"/> から音量を読み直して反映する（テストの後始末用。イベントは発火しない）。</summary>
        internal static void ReloadVolumeFromPrefs()
        {
            volumeLoaded = false;
            EnsureVolumeLoaded();
            SceneBgmPlayer player = SceneBgmPlayer.Instance;
            if (player != null)
            {
                player.ApplyVolumes();
            }
        }

        private static void EnsureVolumeLoaded()
        {
            if (volumeLoaded)
            {
                return;
            }

            volumeLoaded = true;
            if (PlayerPrefs.HasKey(VolumePrefsKey))
            {
                volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefsKey, DefaultVolume));
                return;
            }

            // プレイヤーがあればその設定（テストで差し替えた設定を含む）、まだ無ければ設定アセットを直接読む
            // （BeforeSceneLoad でプレイヤー生成より先に Volume を参照された場合も同じ既定値になる）。
            SceneBgmPlayer player = SceneBgmPlayer.Instance;
            SceneBgmSettings settings = player != null ? player.Settings : SceneBgmSettings.Load();
            volume = ResolveDefaultVolume(settings);
        }

        private static void SaveOrMarkDirty()
        {
            SceneBgmPlayer player = SceneBgmPlayer.Instance;
            if (player != null)
            {
                player.MarkPrefsDirty();
            }
            else
            {
                PlayerPrefs.Save();
            }
        }
    }
}
