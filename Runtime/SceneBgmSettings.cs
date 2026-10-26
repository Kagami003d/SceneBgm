using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Kagami.SceneBgm
{
    /// <summary>
    /// シーンごとのBGM設定（プロジェクトに1つ）。置き場所は <see cref="AssetPath"/> に固定で、実行時は
    /// <c>Resources.Load&lt;SceneBgmSettings&gt;(<see cref="ResourceName"/>)</c> で読み込む。
    /// <para>
    /// 編集はエディタウィンドウ（<c>Window &gt; SceneBgm</c>）から行う。Inspector で entries を直接いじらないこと
    /// （排他の整合は OnValidate でも正すが、GUID とパスの対応はウィンドウが管理する）。
    /// </para>
    /// </summary>
    public sealed class SceneBgmSettings : ScriptableObject
    {
        /// <summary><c>Resources.Load</c> に渡すリソース名。</summary>
        public const string ResourceName = "SceneBgmSettings";

        /// <summary>設定アセットのパス（ゲーム側の <c>Assets/</c> 配下。パッケージ内には置かない）。</summary>
        public const string AssetPath = "Assets/Resources/" + ResourceName + ".asset";

        /// <summary>一律のフェード時間の既定値（秒）。設定アセットが無いときもこの値を使う。</summary>
        public const float DefaultFadeSeconds = 0.75f;

        [Tooltip("シーン切替時のクロスフェード／フェードアウトにかける時間（秒、全シーン共通）。0なら即座に切り替える。")]
        [Min(0f)]
        [SerializeField] private float fadeSeconds = DefaultFadeSeconds;

        [Tooltip("BGM音量（SceneBgm.Volume）がまだ一度も保存されていないときの値（0〜1）。保存済みの値があればそちらが優先される。")]
        [Range(0f, 1f)]
        [SerializeField] private float defaultVolume = SceneBgm.DefaultVolume;

        [Tooltip("BGMの出力先（任意）。未設定なら AudioMixer を通さない。")]
        [SerializeField] private AudioMixerGroup outputMixerGroup;

        [SerializeField] private List<SceneBgmEntry> entries = new List<SceneBgmEntry>();

        // scenePath -> entries のインデックス。参照時に中身を検証するので、entries が変わっても誤った結果は返さない。
        private Dictionary<string, int> pathLookup;

        /// <summary>一律のフェード時間（秒、0以上）。</summary>
        public float FadeSeconds => Mathf.Max(0f, fadeSeconds);

        /// <summary>
        /// BGM音量（<see cref="SceneBgm.Volume"/>）が <see cref="PlayerPrefs"/> に保存されていないときの値（0〜1）。
        /// 既定は <see cref="SceneBgm.DefaultVolume"/>（1.0）。
        /// </summary>
        public float DefaultVolume => Mathf.Clamp01(defaultVolume);

        /// <summary>BGMの出力先（未設定なら null）。</summary>
        public AudioMixerGroup OutputMixerGroup => outputMixerGroup;

        /// <summary>全エントリ（Build Settings から外されたシーンの分も含む）。</summary>
        public IReadOnlyList<SceneBgmEntry> Entries => entries;

        /// <summary>
        /// 設定アセットを読み込む（実行時の唯一の入口）。見つからなければ null。
        /// </summary>
        internal static SceneBgmSettings Load()
        {
            return Resources.Load<SceneBgmSettings>(ResourceName);
        }

        /// <summary>シーンのパス（<c>Scene.path</c>）からエントリを探す。</summary>
        public bool TryGetEntryByPath(string scenePath, out SceneBgmEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(scenePath) || entries == null)
            {
                return false;
            }

            if (pathLookup != null && pathLookup.TryGetValue(scenePath, out int index)
                && index >= 0 && index < entries.Count && entries[index] != null && entries[index].ScenePath == scenePath)
            {
                entry = entries[index];
                return true;
            }

            RebuildPathLookup();
            if (pathLookup.TryGetValue(scenePath, out index))
            {
                entry = entries[index];
                return true;
            }

            return false;
        }

        /// <summary>シーンのGUIDからエントリを探す（エディタ用）。</summary>
        public bool TryGetEntryByGuid(string sceneGuid, out SceneBgmEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(sceneGuid) || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].SceneGuid == sceneGuid)
                {
                    entry = entries[i];
                    return true;
                }
            }

            return false;
        }

        internal SceneBgmEntry GetOrAddEntry(string sceneGuid, string scenePath)
        {
            if (TryGetEntryByGuid(sceneGuid, out SceneBgmEntry entry))
            {
                return entry;
            }

            entries ??= new List<SceneBgmEntry>();
            entry = new SceneBgmEntry(sceneGuid, scenePath);
            entries.Add(entry);
            pathLookup = null;
            return entry;
        }

        internal bool RemoveEntry(SceneBgmEntry entry)
        {
            if (entries == null || !entries.Remove(entry))
            {
                return false;
            }

            pathLookup = null;
            return true;
        }

        internal void SetFadeSeconds(float seconds)
        {
            fadeSeconds = Mathf.Max(0f, seconds);
        }

        internal void SetDefaultVolume(float value)
        {
            defaultVolume = Mathf.Clamp01(value);
        }

        internal void SetOutputMixerGroup(AudioMixerGroup group)
        {
            outputMixerGroup = group;
        }

        /// <summary>シーンのパスが変わったことを検索キャッシュに伝える。</summary>
        internal void InvalidateLookup()
        {
            pathLookup = null;
        }

        /// <summary>矛盾した状態を正す。</summary>
        /// <returns>変更があれば true。</returns>
        internal bool Sanitize()
        {
            bool changed = false;
            if (!(fadeSeconds >= 0f))
            {
                // 負の値と NaN を 0 にする。
                fadeSeconds = 0f;
                changed = true;
            }

            if (!(defaultVolume >= 0f && defaultVolume <= 1f))
            {
                // 範囲外と NaN を正す（NaN は既定値に戻す）。
                defaultVolume = float.IsNaN(defaultVolume) ? SceneBgm.DefaultVolume : Mathf.Clamp01(defaultVolume);
                changed = true;
            }

            if (entries == null)
            {
                entries = new List<SceneBgmEntry>();
                changed = true;
            }

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] == null)
                {
                    entries.RemoveAt(i);
                    changed = true;
                    continue;
                }

                changed |= entries[i].Sanitize();
            }

            pathLookup = null;
            return changed;
        }

        private void RebuildPathLookup()
        {
            pathLookup ??= new Dictionary<string, int>();
            pathLookup.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                SceneBgmEntry e = entries[i];
                // 同じパスが重複した場合は先に登録されたものを使う（エディタの同期処理で重複は解消される）。
                if (e != null && !string.IsNullOrEmpty(e.ScenePath) && !pathLookup.ContainsKey(e.ScenePath))
                {
                    pathLookup.Add(e.ScenePath, i);
                }
            }
        }

        private void OnEnable()
        {
            pathLookup = null;
        }

        private void OnValidate()
        {
            Sanitize();
        }
    }
}
