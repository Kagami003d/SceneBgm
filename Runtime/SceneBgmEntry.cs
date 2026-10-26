using System;
using UnityEngine;

namespace Kagami.SceneBgm
{
    /// <summary>
    /// 1シーン分のBGM設定。シーンはエディタでは <see cref="SceneGuid"/> で識別し、実行時は <see cref="ScenePath"/>（<c>Scene.path</c>）で照合する。
    /// <para>
    /// 変更はエディタウィンドウ（<c>Window &gt; SceneBgm</c>）から行う。[継続]/[停止]/クリップ は排他で、
    /// [継続]/[停止] を選ぶとクリップは None に戻り、クリップを入れると <see cref="SceneBgmMode.Clip"/> になる。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBgmEntry
    {
        [Tooltip("シーンアセットのGUID（エディタでの識別子。移動・リネームしても変わらない）。")]
        [SerializeField] private string sceneGuid = string.Empty;

        [Tooltip("実行時の照合に使うシーンのパス（例: Assets/Scenes/Title.unity）。エディタがGUIDから自動更新する。")]
        [SerializeField] private string scenePath = string.Empty;

        [SerializeField] private SceneBgmMode mode = SceneBgmMode.Unset;

        [Tooltip("mode が Clip のときだけ有効。")]
        [SerializeField] private AudioClip clip;

        // Unityのシリアライザー用。
        private SceneBgmEntry()
        {
        }

        internal SceneBgmEntry(string sceneGuid, string scenePath)
        {
            this.sceneGuid = sceneGuid ?? string.Empty;
            this.scenePath = scenePath ?? string.Empty;
        }

        /// <summary>シーンアセットのGUID。</summary>
        public string SceneGuid => sceneGuid ?? string.Empty;

        /// <summary>シーンのパス（<c>Scene.path</c> と同じ形式）。</summary>
        public string ScenePath => scenePath ?? string.Empty;

        /// <summary>このシーンでのBGMの扱い。</summary>
        public SceneBgmMode Mode => mode;

        /// <summary><see cref="Mode"/> が <see cref="SceneBgmMode.Clip"/> のときのクリップ。それ以外は常に null。</summary>
        public AudioClip Clip => mode == SceneBgmMode.Clip ? clip : null;

        internal void SetScenePath(string path)
        {
            scenePath = path ?? string.Empty;
        }

        /// <summary>
        /// [継続]/[停止] ボタンの動作。未選択なら <paramref name="buttonMode"/> にし（クリップは None に戻す）、
        /// 選択中のボタンをもう一度押した場合は <see cref="SceneBgmMode.Unset"/> に戻す（トグル）。
        /// </summary>
        internal void ToggleMode(SceneBgmMode buttonMode)
        {
            if (buttonMode != SceneBgmMode.Continue && buttonMode != SceneBgmMode.Stop)
            {
                throw new ArgumentOutOfRangeException(nameof(buttonMode), buttonMode, "ToggleMode は Continue / Stop のみ受け付けます。");
            }

            mode = mode == buttonMode ? SceneBgmMode.Unset : buttonMode;
            clip = null;
        }

        /// <summary>
        /// BGM欄の動作。クリップを入れると <see cref="SceneBgmMode.Clip"/>（ボタンの選択は外れる）、None に戻すと <see cref="SceneBgmMode.Unset"/>。
        /// </summary>
        internal void SetClip(AudioClip newClip)
        {
            clip = newClip;
            mode = newClip != null ? SceneBgmMode.Clip : SceneBgmMode.Unset;
        }

        /// <summary>
        /// 矛盾した状態を正す（Clip 以外ならクリップを外す、Clip なのにクリップが無ければ Unset、未定義の値は Unset）。
        /// </summary>
        /// <returns>変更があれば true。</returns>
        internal bool Sanitize()
        {
            bool changed = false;
            if (!Enum.IsDefined(typeof(SceneBgmMode), mode))
            {
                mode = SceneBgmMode.Unset;
                changed = true;
            }

            if (mode != SceneBgmMode.Clip && clip != null)
            {
                clip = null;
                changed = true;
            }
            else if (mode == SceneBgmMode.Clip && clip == null)
            {
                mode = SceneBgmMode.Unset;
                changed = true;
            }

            if (sceneGuid == null)
            {
                sceneGuid = string.Empty;
                changed = true;
            }

            if (scenePath == null)
            {
                scenePath = string.Empty;
                changed = true;
            }

            return changed;
        }
    }
}
