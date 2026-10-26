using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kagami.SceneBgm
{
    /// <summary>
    /// BGMを鳴らす常駐コンポーネント（内部用。ゲーム側は <see cref="SceneBgm"/> の静的APIを使う）。
    /// <para>
    /// <c>AudioSource</c> を2本持ち、クロスフェードする。フェードは <c>Time.unscaledDeltaTime</c> で進むので <c>timeScale=0</c> でも止まらない。
    /// <c>AudioListener</c> は追加しない（ゲーム側のものを使う）。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class SceneBgmPlayer : MonoBehaviour
    {
        internal const string GameObjectName = "[SceneBgm]";

        /// <summary>音量変更後、<see cref="PlayerPrefs.Save"/> するまでの待ち時間。</summary>
        private const float PrefsSaveDelaySeconds = 0.5f;

        /// <summary>1フレームで進めるフェードの最大時間（ヒッチで一気に切り替わらないようにする）。</summary>
        private const float MaxFadeDeltaSeconds = 0.1f;

        private const int SourceCount = 2;

        private static bool warnedMissingSettings;

        private readonly AudioSource[] sources = new AudioSource[SourceCount];
        private readonly float[] fadeWeights = new float[SourceCount];
        private readonly float[] fadeTargets = new float[SourceCount];
        private readonly float[] fadeSpeeds = new float[SourceCount];

        private SceneBgmSettings settings;
        private int activeIndex;
        private AudioClip currentClip;
        private int lastAppliedSceneHandle;
        private bool hasAppliedScene;
        private bool prefsDirty;
        private float prefsSaveAt;
        private bool isQuitting;

        internal static SceneBgmPlayer Instance { get; private set; }

        internal SceneBgmSettings Settings => settings;

        internal AudioClip CurrentClip => currentClip;

        internal bool IsPlaying => currentClip != null && sources[activeIndex] != null && sources[activeIndex].isPlaying;

        /// <summary>テスト用: 指定したソース（0/1）。</summary>
        internal AudioSource GetSource(int index) => sources[index];

        /// <summary>テスト用: 指定したソースのフェード係数（0〜1）。</summary>
        internal float GetFadeWeight(int index) => fadeWeights[index];

        internal static void ResetStatics()
        {
            Instance = null;
            warnedMissingSettings = false;
        }

        /// <summary>プレイヤーが無ければ作る（設定アセットもここで読み込む）。</summary>
        internal static SceneBgmPlayer EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            if (!Application.isPlaying)
            {
                return null;
            }

            SceneBgmSettings loaded = SceneBgmSettings.Load();
            if (loaded == null && !warnedMissingSettings)
            {
                warnedMissingSettings = true;
                Debug.LogWarning(
                    $"[SceneBgm] 設定アセットが見つかりません（{SceneBgmSettings.AssetPath}）。シーンごとのBGM自動切替は行いません"
                    + "（SceneBgm.Play / Stop / Volume は使えます）。設定するには Window > SceneBgm を開いてください。");
            }

            var go = new GameObject(GameObjectName);
            DontDestroyOnLoad(go);
            SceneBgmPlayer player = go.AddComponent<SceneBgmPlayer>();
            player.SetSettings(loaded);
            return player;
        }

        /// <summary>設定を差し替える（テスト用の差し込み口。公開APIにはしない）。</summary>
        internal void SetSettings(SceneBgmSettings newSettings)
        {
            settings = newSettings;
            AudioMixerGroupApply();
        }

        internal float ResolveFadeSeconds(float? fadeSeconds)
        {
            if (fadeSeconds.HasValue)
            {
                return Mathf.Max(0f, fadeSeconds.Value);
            }

            return settings != null ? settings.FadeSeconds : SceneBgmSettings.DefaultFadeSeconds;
        }

        /// <summary>シーンの設定を適用する（<see cref="LoadSceneMode.Single"/> 相当の読み込み時に呼ぶ）。</summary>
        internal void ApplyScene(Scene scene)
        {
            hasAppliedScene = true;
            lastAppliedSceneHandle = scene.handle;

            if (settings == null || !settings.TryGetEntryByPath(scene.path, out SceneBgmEntry entry))
            {
                // エントリ無し: 直前のBGMを継続する。
                return;
            }

            switch (entry.Mode)
            {
                case SceneBgmMode.Clip:
                    if (entry.Clip != null)
                    {
                        PlayClip(entry.Clip, settings.FadeSeconds);
                    }

                    break;
                case SceneBgmMode.Stop:
                    StopClip(settings.FadeSeconds);
                    break;
                default:
                    // Continue / Unset: 直前のBGMを継続する。
                    break;
            }
        }

        internal void PlayClip(AudioClip clip, float fadeSeconds)
        {
            if (isQuitting || sources[0] == null)
            {
                return;
            }

            if (clip == null)
            {
                StopClip(fadeSeconds);
                return;
            }

            if (clip == currentClip)
            {
                // 同じ曲（フェードイン中を含む）の再要求は無視する。再生位置は戻らない。
                return;
            }

            // A→B→A や Stop 直後の A のように、フェードアウト中の同じ曲へ戻る場合は再生位置を保ったままフェードインし直す。
            int nextIndex = -1;
            for (int i = 0; i < SourceCount; i++)
            {
                if (sources[i].clip == clip && sources[i].isPlaying)
                {
                    nextIndex = i;
                    break;
                }
            }

            bool resume = nextIndex >= 0;
            if (!resume)
            {
                nextIndex = 1 - activeIndex;
            }

            int previousIndex = 1 - nextIndex;
            AudioSource next = sources[nextIndex];
            if (!resume)
            {
                next.Stop();
                next.clip = clip;
                fadeWeights[nextIndex] = 0f;
            }

            activeIndex = nextIndex;
            currentClip = clip;

            SetFade(previousIndex, 0f, fadeSeconds);
            SetFade(nextIndex, 1f, fadeSeconds);

            if (!resume)
            {
                next.Play();
            }
        }

        internal void StopClip(float fadeSeconds)
        {
            currentClip = null;
            if (sources[0] == null)
            {
                return;
            }

            for (int i = 0; i < SourceCount; i++)
            {
                SetFade(i, 0f, fadeSeconds);
            }
        }

        internal void ApplyVolumes()
        {
            if (sources[0] == null)
            {
                return;
            }

            for (int i = 0; i < SourceCount; i++)
            {
                ApplyVolume(i);
            }
        }

        internal void MarkPrefsDirty()
        {
            prefsDirty = true;
            prefsSaveAt = Time.unscaledTime + PrefsSaveDelaySeconds;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            for (int i = 0; i < SourceCount; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                // BGMは AudioListener.pause の影響を受けない。優先度は最高（0）にしてSEの多発で仮想化されないようにする。
                source.ignoreListenerPause = true;
                source.priority = 0;
                source.volume = 0f;
                sources[i] = source;
            }

            AudioMixerGroupApply();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            if (Instance != this)
            {
                return;
            }

            // 最初のシーンでも確実に鳴らす保険。sceneLoaded で処理済みのシーンは二度処理しない
            // （最初のシーンの Start でゲームが手動 Play した曲を上書きしないため）。
            Scene active = SceneManager.GetActiveScene();
            if (!hasAppliedScene || lastAppliedSceneHandle != active.handle)
            {
                ApplyScene(active);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != this || mode != LoadSceneMode.Single)
            {
                // Additive 読み込みは無視する。
                return;
            }

            ApplyScene(scene);
        }

        private void Update()
        {
            UpdateFades();

            if (prefsDirty && Time.unscaledTime >= prefsSaveAt)
            {
                SavePrefsNow();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SavePrefsNow();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                SavePrefsNow();
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
            SavePrefsNow();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            SavePrefsNow();
            Instance = null;
        }

        private void AudioMixerGroupApply()
        {
            for (int i = 0; i < SourceCount; i++)
            {
                if (sources[i] != null)
                {
                    sources[i].outputAudioMixerGroup = settings != null ? settings.OutputMixerGroup : null;
                }
            }
        }

        private void SetFade(int index, float target, float fadeSeconds)
        {
            fadeTargets[index] = target;
            if (fadeSeconds <= 0f)
            {
                fadeWeights[index] = target;
                fadeSpeeds[index] = 0f;
            }
            else
            {
                fadeSpeeds[index] = 1f / fadeSeconds;
            }

            ApplyVolume(index);
            StopSourceIfSilent(index);
        }

        private void UpdateFades()
        {
            if (sources[0] == null)
            {
                return;
            }

            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, MaxFadeDeltaSeconds);
            for (int i = 0; i < SourceCount; i++)
            {
                if (fadeWeights[i] == fadeTargets[i])
                {
                    continue;
                }

                fadeWeights[i] = fadeSpeeds[i] > 0f
                    ? Mathf.MoveTowards(fadeWeights[i], fadeTargets[i], fadeSpeeds[i] * deltaTime)
                    : fadeTargets[i];

                ApplyVolume(i);
                StopSourceIfSilent(i);
            }
        }

        private void StopSourceIfSilent(int index)
        {
            if (fadeTargets[index] > 0f || fadeWeights[index] > 0f)
            {
                return;
            }

            AudioSource source = sources[index];
            if (source.clip != null)
            {
                source.Stop();
                // Streaming のクリップを解放するため参照も外す。
                source.clip = null;
            }
        }

        private void ApplyVolume(int index)
        {
            sources[index].volume = SceneBgm.Gain * fadeWeights[index];
        }

        private void SavePrefsNow()
        {
            if (!prefsDirty)
            {
                return;
            }

            prefsDirty = false;
            PlayerPrefs.Save();
        }
    }
}
