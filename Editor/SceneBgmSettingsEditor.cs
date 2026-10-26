using UnityEditor;
using UnityEngine;

namespace Kagami.SceneBgm.Editor
{
    /// <summary>
    /// 設定アセットの Inspector。エントリを直接編集して矛盾した状態にしないよう、概要とウィンドウを開くボタンだけを出す。
    /// </summary>
    [CustomEditor(typeof(SceneBgmSettings))]
    internal sealed class SceneBgmSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (SceneBgmSettings)target;
            EditorGUILayout.HelpBox("シーンごとのBGMは Window > SceneBgm で編集します。", MessageType.Info);
            if (GUILayout.Button("SceneBgm ウィンドウを開く", GUILayout.Height(28f)))
            {
                SceneBgmWindow.Open();
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("フェード時間（秒）", settings.FadeSeconds);
                EditorGUILayout.FloatField("既定音量", settings.DefaultVolume);
                EditorGUILayout.ObjectField("出力先", settings.OutputMixerGroup, typeof(UnityEngine.Audio.AudioMixerGroup), false);
                EditorGUILayout.IntField("エントリ数", settings.Entries.Count);
            }

            if (AssetDatabase.GetAssetPath(settings) != SceneBgmSettings.AssetPath)
            {
                EditorGUILayout.HelpBox($"実行時は {SceneBgmSettings.AssetPath} だけが読み込まれます。", MessageType.Warning);
            }
        }
    }
}
