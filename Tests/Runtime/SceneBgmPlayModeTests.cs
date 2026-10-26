using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Kagami.SceneBgm.Tests
{
    /// <summary>
    /// 実際のシーン読み込みでの再生確認。テスト用の空シーン（Build Settings には登録しない）を
    /// <c>EditorSceneManager.LoadSceneAsyncInPlayMode</c> で読み込む。
    /// </summary>
    public sealed class SceneBgmPlayModeTests
    {
        private const string SceneDir = "Packages/com.kagami003d.scenebgm/Tests/Runtime/Scenes/";
        private const string SceneA = SceneDir + "SceneBgmTest_A.unity";
        private const string SceneB = SceneDir + "SceneBgmTest_B.unity";
        private const string SceneC = SceneDir + "SceneBgmTest_C.unity";

        private const float Fade = 0.4f;

        // フェードが終わるのを待つ実時間（フェード時間 + 余裕）。
        private const float SettleSeconds = Fade + 0.35f;

        private readonly List<Object> created = new List<Object>();
        private SceneBgmPlayer player;
        private SceneBgmSettings originalSettings;
        private SceneBgmSettings settings;
        private AudioClip clip1;
        private AudioClip clip2;
        private AudioClip clip3;
        private bool hadVolumeKey;
        private float savedVolume;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if !UNITY_EDITOR
            Assert.Ignore("エディタ専用（テストシーンを Build Settings に登録していないため）。");
#endif
            hadVolumeKey = PlayerPrefs.HasKey(SceneBgm.VolumePrefsKey);
            savedVolume = PlayerPrefs.GetFloat(SceneBgm.VolumePrefsKey, SceneBgm.DefaultVolume);

            player = SceneBgmPlayer.EnsureInstance();
            Assert.IsNotNull(player, "プレイヤーが生成されていません。");
            originalSettings = player.Settings;

            settings = Track(ScriptableObject.CreateInstance<SceneBgmSettings>());
            settings.SetFadeSeconds(Fade);
            clip1 = CreateClip("SceneBgmTest_Clip1");
            clip2 = CreateClip("SceneBgmTest_Clip2");
            clip3 = CreateClip("SceneBgmTest_Clip3");
            player.SetSettings(settings);

            // テストシーンは空なので、AudioListener が無ければ一時的に置く（"no audio listeners" 警告の抑止）。
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                var listener = new GameObject("SceneBgmTest_Listener", typeof(AudioListener));
                Object.DontDestroyOnLoad(listener);
                Track(listener);
            }

            SceneBgm.Stop(0f);
            SceneBgm.SetVolumeMultiplier(1f);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (player != null)
            {
                SceneBgm.Stop(0f);
                player.SetSettings(originalSettings);
            }

            SceneBgm.SetVolumeMultiplier(1f);
            if (hadVolumeKey)
            {
                PlayerPrefs.SetFloat(SceneBgm.VolumePrefsKey, savedVolume);
            }
            else
            {
                PlayerPrefs.DeleteKey(SceneBgm.VolumePrefsKey);
            }

            PlayerPrefs.Save();
            SceneBgm.ReloadVolumeFromPrefs();

            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.Destroy(o);
                }
            }

            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Clip_ToDifferentClip_CrossfadesAndOnlyNewClipRemains()
        {
            SetClip(SceneA, clip1);
            SetClip(SceneB, clip2);

            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip1, SceneBgm.CurrentClip);
            Assert.IsTrue(SceneBgm.IsPlaying);

            yield return Load(SceneB);
            // フェード途中は両方鳴っている。
            Assert.AreEqual(clip2, SceneBgm.CurrentClip);
            Assert.AreEqual(2, CountPlayingSources(), "クロスフェード中は旧曲と新曲の両方が鳴っているはず");

            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip2, SceneBgm.CurrentClip);
            Assert.AreEqual(1, CountPlayingSources());
            Assert.AreEqual(clip2, PlayingSource().clip);
            Assert.AreEqual(1f, player.GetFadeWeight(IndexOf(PlayingSource())), 1e-4f);
            Assert.IsNull(OtherSource(PlayingSource()).clip, "フェードアウトした側は clip=null（Streaming 解放）");
        }

        [UnityTest]
        public IEnumerator Clip_ToSameClip_ContinuesWithoutRestart()
        {
            SetClip(SceneA, clip1);
            SetClip(SceneB, clip1);

            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            AudioSource source = PlayingSource();
            Assert.IsNotNull(source);
            float timeBefore = source.time;

            yield return Load(SceneB);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreSame(source, PlayingSource(), "同じソースで鳴り続けているはず");
            Assert.IsTrue(source.isPlaying);
            Assert.AreEqual(clip1, source.clip);
            Assert.AreEqual(1, CountPlayingSources());
            Assert.GreaterOrEqual(source.time, timeBefore, "再生位置が戻っていない");
        }

        [UnityTest]
        public IEnumerator Stop_FadesOutAndStops()
        {
            SetClip(SceneA, clip1);
            SetButton(SceneB, SceneBgmMode.Stop);

            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.IsTrue(SceneBgm.IsPlaying);

            yield return Load(SceneB);
            Assert.IsNull(SceneBgm.CurrentClip);
            Assert.AreEqual(1, CountPlayingSources(), "フェードアウト中はまだ鳴っている");

            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.IsFalse(SceneBgm.IsPlaying);
            Assert.AreEqual(0, CountPlayingSources());
        }

        [UnityTest]
        public IEnumerator ContinueUnsetAndNoEntry_KeepPreviousClip([Values(SceneBgmMode.Continue, SceneBgmMode.Unset)] SceneBgmMode modeB)
        {
            SetClip(SceneA, clip1);
            if (modeB == SceneBgmMode.Continue)
            {
                SetButton(SceneB, SceneBgmMode.Continue);
            }
            else
            {
                // エントリはあるが未設定。
                AddEntry(SceneB);
            }

            // C はエントリ無し。
            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            AudioSource source = PlayingSource();
            Assert.IsNotNull(source);

            yield return Load(SceneB);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip1, SceneBgm.CurrentClip);
            Assert.AreSame(source, PlayingSource());

            yield return Load(SceneC);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip1, SceneBgm.CurrentClip);
            Assert.AreSame(source, PlayingSource());
            Assert.AreEqual(1, CountPlayingSources());
        }

        [UnityTest]
        public IEnumerator AdditiveLoad_IsIgnored()
        {
            SetClip(SceneA, clip1);
            SetClip(SceneB, clip2);

            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);

            yield return Load(SceneB, LoadSceneMode.Additive);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip1, SceneBgm.CurrentClip);
            Assert.AreEqual(1, CountPlayingSources());

            Scene additive = SceneManager.GetSceneByPath(SceneB);
            if (additive.IsValid() && additive.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(additive);
            }
        }

        [UnityTest]
        public IEnumerator Fade_ProgressesWhenTimeScaleIsZero()
        {
            SetClip(SceneA, clip1);
            SetClip(SceneB, clip2);

            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);

            Time.timeScale = 0f;
            yield return Load(SceneB);
            yield return new WaitForSecondsRealtime(SettleSeconds);

            Assert.AreEqual(1, CountPlayingSources(), "timeScale=0 でもフェードが終わっているはず");
            AudioSource source = PlayingSource();
            Assert.AreEqual(clip2, source.clip);
            Assert.AreEqual(1f, player.GetFadeWeight(IndexOf(source)), 1e-4f);
            Assert.IsTrue(source.ignoreListenerPause);
        }

        [UnityTest]
        public IEnumerator Volume_ClampsPersistsNotifiesAndApplies()
        {
            SetClip(SceneA, clip1);
            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);

            SceneBgm.Volume = 0.7f;
            var received = new List<float>();
            System.Action<float> handler = v => received.Add(v);
            SceneBgm.VolumeChanged += handler;
            try
            {
                SceneBgm.Volume = 1.5f;
                SceneBgm.Volume = 0.25f;
                SceneBgm.Volume = -1f;
                SceneBgm.Volume = -2f; // 変化なし（0のまま）: イベントは出ない
            }
            finally
            {
                SceneBgm.VolumeChanged -= handler;
            }

            Assert.AreEqual(0f, SceneBgm.Volume);
            Assert.AreEqual(0f, PlayerPrefs.GetFloat(SceneBgm.VolumePrefsKey, -1f));
            CollectionAssert.AreEqual(new List<float> { 1f, 0.25f, 0f }, received);

            SceneBgm.Volume = 0.5f;
            SceneBgm.SetVolumeMultiplier(0.5f);
            Assert.AreEqual(0.5f, PlayerPrefs.GetFloat(SceneBgm.VolumePrefsKey, -1f), 1e-6f);
            Assert.AreEqual(0.25f, PlayingSource().volume, 1e-4f, "最終音量 = Volume × 倍率 × フェード係数");

            SceneBgm.SetVolumeMultiplier(2f);
            Assert.AreEqual(1f, SceneBgm.VolumeMultiplier);
        }

        [UnityTest]
        public IEnumerator Volume_WhenNotSaved_UsesSettingsDefaultVolume()
        {
            settings.SetDefaultVolume(0.35f);
            PlayerPrefs.DeleteKey(SceneBgm.VolumePrefsKey);
            SceneBgm.ReloadVolumeFromPrefs();
            Assert.AreEqual(0.35f, SceneBgm.Volume, 1e-6f, "保存値が無いときは設定アセットの既定音量");
            Assert.IsFalse(PlayerPrefs.HasKey(SceneBgm.VolumePrefsKey), "読むだけでは保存しない");

            SetClip(SceneA, clip1);
            yield return Load(SceneA);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(0.35f, PlayingSource().volume, 1e-4f, "既定音量が実際の音量に反映される");

            // 既定音量と同じ値を代入しても保存される（後で既定音量を変えても値が保たれる）。
            SceneBgm.Volume = 0.35f;
            Assert.AreEqual(0.35f, PlayerPrefs.GetFloat(SceneBgm.VolumePrefsKey, -1f), 1e-6f);
        }

        [Test]
        public void Volume_WhenNotSavedAndNoSettings_IsOne()
        {
            player.SetSettings(null);
            PlayerPrefs.DeleteKey(SceneBgm.VolumePrefsKey);
            SceneBgm.ReloadVolumeFromPrefs();
            Assert.AreEqual(SceneBgm.DefaultVolume, SceneBgm.Volume, "設定アセットが無ければ 1.0");
        }

        [Test]
        public void Volume_WhenSaved_IgnoresSettingsDefaultVolume()
        {
            settings.SetDefaultVolume(0.35f);
            PlayerPrefs.SetFloat(SceneBgm.VolumePrefsKey, 0.8f);
            SceneBgm.ReloadVolumeFromPrefs();
            Assert.AreEqual(0.8f, SceneBgm.Volume, 1e-6f, "保存値が既定音量より優先される");
        }

        [UnityTest]
        public IEnumerator ManualPlay_ThenSceneLoadFollowsSettings()
        {
            SetClip(SceneA, clip1);
            SetClip(SceneB, clip2);
            SetButton(SceneC, SceneBgmMode.Continue);

            yield return Load(SceneA);
            SceneBgm.Play(clip3, 0f);
            Assert.AreEqual(clip3, SceneBgm.CurrentClip);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip3, PlayingSource().clip);

            // Continue のシーンでは手動で流した曲が続く。
            yield return Load(SceneC);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip3, SceneBgm.CurrentClip);

            // Clip のシーンでは設定どおりに切り替わる。
            yield return Load(SceneB);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreEqual(clip2, SceneBgm.CurrentClip);
            Assert.AreEqual(1, CountPlayingSources());

            // null は Stop と同じ。
            SceneBgm.Play(null, 0f);
            Assert.IsNull(SceneBgm.CurrentClip);
            Assert.AreEqual(0, CountPlayingSources());
        }

        private IEnumerator Load(string path, LoadSceneMode mode = LoadSceneMode.Single)
        {
#if UNITY_EDITOR
            AsyncOperation op = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(mode));
            Assert.IsNotNull(op, "テストシーンを読み込めません: " + path);
            while (!op.isDone)
            {
                yield return null;
            }

            // sceneLoaded の後、1フレーム進めてフェードを開始させる。
            yield return null;
#else
            yield break;
#endif
        }

        private void SetClip(string scenePath, AudioClip clip)
        {
            AddEntry(scenePath).SetClip(clip);
        }

        private void SetButton(string scenePath, SceneBgmMode mode)
        {
            AddEntry(scenePath).ToggleMode(mode);
        }

        private SceneBgmEntry AddEntry(string scenePath)
        {
            return settings.GetOrAddEntry("test-guid:" + scenePath, scenePath);
        }

        private AudioClip CreateClip(string name)
        {
            const int frequency = 8000;
            var clip = AudioClip.Create(name, frequency * 2, 1, frequency, false);
            var data = new float[frequency * 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Mathf.Sin(i * 0.05f) * 0.01f;
            }

            clip.SetData(data, 0);
            return Track(clip);
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private int CountPlayingSources()
        {
            int count = 0;
            for (int i = 0; i < 2; i++)
            {
                if (player.GetSource(i).isPlaying)
                {
                    count++;
                }
            }

            return count;
        }

        private AudioSource PlayingSource()
        {
            AudioSource found = null;
            for (int i = 0; i < 2; i++)
            {
                AudioSource s = player.GetSource(i);
                if (s.isPlaying && (found == null || player.GetFadeWeight(i) > player.GetFadeWeight(IndexOf(found))))
                {
                    found = s;
                }
            }

            return found;
        }

        private AudioSource OtherSource(AudioSource source)
        {
            return player.GetSource(1 - IndexOf(source));
        }

        private int IndexOf(AudioSource source)
        {
            return player.GetSource(0) == source ? 0 : 1;
        }
    }
}
