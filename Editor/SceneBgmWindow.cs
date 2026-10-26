using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kagami.SceneBgm.Editor
{
    /// <summary>
    /// <c>Window &gt; SceneBgm</c>: Build Settings のシーンごとに [継続] / [停止] / BGM(AudioClip) を排他で設定する。
    /// </summary>
    public sealed class SceneBgmWindow : EditorWindow
    {
        private const string MenuPath = "Window/SceneBgm";
        private const float ButtonWidth = 52f;
        private const float RowHeight = 22f;

        private static readonly Color UnsetRowColor = new Color(1f, 0.75f, 0.1f, 0.18f);
        private static readonly Color DisabledRowColor = new Color(0.5f, 0.5f, 0.5f, 0.12f);

        private SceneBgmSettings settings;
        private SerializedObject serializedSettings;
        private Vector2 scroll;
        private bool showOrphans;

        private GUIContent continueContent;
        private GUIContent stopContent;
        private GUIContent warnIcon;

        /// <summary>ウィンドウを開く。設定アセットが無ければ作る。</summary>
        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<SceneBgmWindow>();
            window.titleContent = new GUIContent("SceneBgm");
            window.minSize = new Vector2(420f, 240f);
            window.BindSettings(SceneBgmSettingsLocator.FindOrCreate());
            window.Show();
        }

        private void OnEnable()
        {
            continueContent = new GUIContent("継続", "直前のBGMをそのまま継続する（もう一度押すと未設定に戻る）");
            stopContent = new GUIContent("停止", "フェードアウトして停止する（もう一度押すと未設定に戻る）");
            warnIcon = EditorGUIUtility.IconContent("console.warnicon.sml");

            EditorBuildSettings.sceneListChanged += OnSceneListChanged;
            Undo.undoRedoPerformed += OnUndoRedo;

            if (settings == null)
            {
                // ドメインリロード後などは探すだけ（自動作成は UI 操作をきっかけにする）。
                BindSettings(SceneBgmSettingsLocator.Find());
            }
        }

        private void OnDisable()
        {
            EditorBuildSettings.sceneListChanged -= OnSceneListChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnFocus()
        {
            if (settings != null)
            {
                SceneBgmScenePathSync.Sync(settings);
            }
        }

        private void BindSettings(SceneBgmSettings target)
        {
            settings = target;
            serializedSettings = target != null ? new SerializedObject(target) : null;
            if (target != null)
            {
                SceneBgmScenePathSync.Sync(target);
            }

            Repaint();
        }

        private void OnSceneListChanged()
        {
            Repaint();
        }

        private void OnUndoRedo()
        {
            if (settings != null)
            {
                settings.InvalidateLookup();
                if (EditorUtility.IsDirty(settings))
                {
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                // 削除された場合などに備えて探し直す。
                BindSettings(SceneBgmSettingsLocator.Find());
            }

            if (settings == null)
            {
                DrawMissingSettings();
                return;
            }

            if (serializedSettings == null || serializedSettings.targetObject != settings)
            {
                serializedSettings = new SerializedObject(settings);
            }

            List<SceneBgmValidator.BuildScene> buildScenes = SceneBgmValidator.GetBuildScenes();

            DrawHeader(buildScenes);
            EditorGUILayout.Space(4f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSceneRows(buildScenes);
            EditorGUILayout.Space(8f);
            DrawOrphans(buildScenes);
            EditorGUILayout.EndScrollView();
        }

        private void DrawMissingSettings()
        {
            EditorGUILayout.HelpBox($"設定アセットがありません（{SceneBgmSettings.AssetPath}）。", MessageType.Info);
            if (GUILayout.Button("設定アセットを作成"))
            {
                BindSettings(SceneBgmSettingsLocator.FindOrCreate());
            }
        }

        private void DrawHeader(List<SceneBgmValidator.BuildScene> buildScenes)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("設定アセット", settings, typeof(SceneBgmSettings), false);
                }

                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                {
                    EditorGUIUtility.PingObject(settings);
                }
            }

            if (AssetDatabase.GetAssetPath(settings) != SceneBgmSettings.AssetPath)
            {
                EditorGUILayout.HelpBox($"実行時は {SceneBgmSettings.AssetPath} だけが読み込まれます。この設定アセットを移動してください。", MessageType.Warning);
            }

            serializedSettings.Update();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("fadeSeconds"), new GUIContent("フェード時間（秒）", "全シーン共通のクロスフェード／フェードアウトの時間。0なら即座に切り替える。"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultVolume"), new GUIContent("既定音量", "BGM音量（SceneBgm.Volume）がまだ保存されていないとき（初回起動時など）の値。保存済みの値があればそちらが優先される。"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("outputMixerGroup"), new GUIContent("出力先（任意）", "BGMの AudioMixerGroup。未設定なら使わない。"));
            if (serializedSettings.ApplyModifiedProperties())
            {
                settings.Sanitize();
                SaveSettings();
            }

            int unsetCount = CountUnset(buildScenes);
            using (new EditorGUILayout.HorizontalScope())
            {
                string label = unsetCount > 0
                    ? $"未設定のシーン: {unsetCount} 件（ビルド時に警告。実行時は直前のBGMを継続）"
                    : "未設定のシーンはありません";
                if (unsetCount > 0)
                {
                    GUILayout.Label(warnIcon, GUILayout.Width(18f), GUILayout.Height(18f));
                }

                GUILayout.Label(label, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(72f)))
                {
                    SceneBgmValidator.LogResult(SceneBgmValidator.Validate(settings), logWhenOk: true);
                }
            }
        }

        private int CountUnset(List<SceneBgmValidator.BuildScene> buildScenes)
        {
            int count = 0;
            foreach (SceneBgmValidator.BuildScene scene in buildScenes)
            {
                if (scene.Enabled && GetMode(scene.Guid) == SceneBgmMode.Unset)
                {
                    count++;
                }
            }

            return count;
        }

        private SceneBgmMode GetMode(string guid)
        {
            return settings.TryGetEntryByGuid(guid, out SceneBgmEntry entry) ? entry.Mode : SceneBgmMode.Unset;
        }

        private void DrawSceneRows(List<SceneBgmValidator.BuildScene> buildScenes)
        {
            EditorGUILayout.LabelField($"Build Settings のシーン（{buildScenes.Count}）", EditorStyles.miniBoldLabel);
            if (buildScenes.Count == 0)
            {
                EditorGUILayout.HelpBox("Build Settings にシーンがありません（File > Build Profiles でシーンを追加してください）。", MessageType.Info);
                return;
            }

            foreach (SceneBgmValidator.BuildScene scene in buildScenes)
            {
                DrawSceneRow(scene);
            }
        }

        private void DrawSceneRow(SceneBgmValidator.BuildScene scene)
        {
            settings.TryGetEntryByGuid(scene.Guid, out SceneBgmEntry entry);
            SceneBgmMode mode = entry != null ? entry.Mode : SceneBgmMode.Unset;
            AudioClip clip = entry?.Clip;
            bool isUnset = mode == SceneBgmMode.Unset;

            Rect row = EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            if (Event.current.type == EventType.Repaint)
            {
                if (!scene.Enabled)
                {
                    EditorGUI.DrawRect(row, DisabledRowColor);
                }
                else if (isUnset)
                {
                    EditorGUI.DrawRect(row, UnsetRowColor);
                }
            }

            // 警告アイコン（有効なシーンの未設定のみ）。
            if (scene.Enabled && isUnset)
            {
                GUILayout.Label(new GUIContent(warnIcon.image, "BGMが未設定です（ビルド時に警告。実行時は直前のBGMを継続）"), GUILayout.Width(18f), GUILayout.Height(RowHeight));
            }
            else
            {
                GUILayout.Space(22f);
            }

            string sceneName = string.IsNullOrEmpty(scene.Path) ? scene.Guid : Path.GetFileNameWithoutExtension(scene.Path);
            string tooltip = scene.Enabled ? scene.Path : scene.Path + "\n（Build Settings で無効。ビルドには含まれません）";
            Color previousColor = GUI.contentColor;
            if (!scene.Enabled)
            {
                GUI.contentColor = new Color(1f, 1f, 1f, 0.55f);
            }

            GUILayout.Label(new GUIContent(sceneName, tooltip), GUILayout.MinWidth(80f), GUILayout.Height(RowHeight));

            bool newContinue = GUILayout.Toggle(mode == SceneBgmMode.Continue, continueContent, EditorStyles.miniButtonLeft, GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight - 2f));
            if (newContinue != (mode == SceneBgmMode.Continue))
            {
                Modify(scene, "SceneBgm: 継続", e => e.ToggleMode(SceneBgmMode.Continue));
            }

            bool newStop = GUILayout.Toggle(mode == SceneBgmMode.Stop, stopContent, EditorStyles.miniButtonRight, GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight - 2f));
            if (newStop != (mode == SceneBgmMode.Stop))
            {
                Modify(scene, "SceneBgm: 停止", e => e.ToggleMode(SceneBgmMode.Stop));
            }

            EditorGUI.BeginChangeCheck();
            var newClip = (AudioClip)EditorGUILayout.ObjectField(clip, typeof(AudioClip), false, GUILayout.MinWidth(120f), GUILayout.Height(RowHeight - 4f));
            if (EditorGUI.EndChangeCheck())
            {
                Modify(scene, "SceneBgm: BGM", e => e.SetClip(newClip));
            }

            GUI.contentColor = previousColor;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOrphans(List<SceneBgmValidator.BuildScene> buildScenes)
        {
            var inBuild = new HashSet<string>();
            foreach (SceneBgmValidator.BuildScene scene in buildScenes)
            {
                inBuild.Add(scene.Guid);
            }

            var orphans = new List<SceneBgmEntry>();
            foreach (SceneBgmEntry entry in settings.Entries)
            {
                if (!inBuild.Contains(entry.SceneGuid))
                {
                    orphans.Add(entry);
                }
            }

            if (orphans.Count == 0)
            {
                return;
            }

            showOrphans = EditorGUILayout.Foldout(showOrphans, $"Build Settings に無い設定（{orphans.Count}件）", true);
            if (!showOrphans)
            {
                return;
            }

            EditorGUILayout.HelpBox("Build Settings から外されたシーンの設定です。再登録すると元の設定が戻ります。", MessageType.None);
            SceneBgmEntry toRemove = null;
            foreach (SceneBgmEntry entry in orphans)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool exists = !string.IsNullOrEmpty(SceneBgmScenePathSync.ResolveScenePath(entry.SceneGuid));
                    string label = string.IsNullOrEmpty(entry.ScenePath) ? entry.SceneGuid : entry.ScenePath;
                    if (!exists)
                    {
                        label += "（シーンが見つかりません）";
                    }

                    GUILayout.Label(new GUIContent(label, entry.SceneGuid), GUILayout.MinWidth(120f));
                    GUILayout.Label(Describe(entry), GUILayout.Width(140f));
                    if (GUILayout.Button("削除", GUILayout.Width(48f)))
                    {
                        toRemove = entry;
                    }
                }
            }

            if (toRemove != null)
            {
                Undo.RecordObject(settings, "SceneBgm: 設定の削除");
                settings.RemoveEntry(toRemove);
                SaveSettings();
                GUIUtility.ExitGUI();
            }
        }

        private static string Describe(SceneBgmEntry entry)
        {
            switch (entry.Mode)
            {
                case SceneBgmMode.Continue:
                    return "継続";
                case SceneBgmMode.Stop:
                    return "停止";
                case SceneBgmMode.Clip:
                    return entry.Clip != null ? "BGM: " + entry.Clip.name : "BGM";
                default:
                    return "未設定";
            }
        }

        private void Modify(SceneBgmValidator.BuildScene scene, string undoName, Action<SceneBgmEntry> change)
        {
            Undo.RecordObject(settings, undoName);
            SceneBgmEntry entry = settings.GetOrAddEntry(scene.Guid, scene.Path);
            change(entry);
            SaveSettings();
        }

        private void SaveSettings()
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Repaint();
        }
    }
}
