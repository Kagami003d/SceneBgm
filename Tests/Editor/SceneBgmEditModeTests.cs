using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kagami.SceneBgm.Editor.Tests
{
    public sealed class SceneBgmEditModeTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            created.Clear();
        }

        // ---------------- 排他ロジック ----------------

        [Test]
        public void ToggleMode_SelectsAndClearsClip_AndSecondPressReturnsToUnset()
        {
            SceneBgmSettings settings = CreateSettings();
            SceneBgmEntry entry = settings.GetOrAddEntry("g1", "Assets/A.unity");
            AudioClip clip = CreateClip();

            entry.SetClip(clip);
            Assert.AreEqual(SceneBgmMode.Clip, entry.Mode);
            Assert.AreEqual(clip, entry.Clip);

            entry.ToggleMode(SceneBgmMode.Continue);
            Assert.AreEqual(SceneBgmMode.Continue, entry.Mode);
            Assert.IsNull(entry.Clip, "継続を選ぶとBGM欄は None に戻る");

            entry.ToggleMode(SceneBgmMode.Stop);
            Assert.AreEqual(SceneBgmMode.Stop, entry.Mode, "別のボタンを押すと選択が移る");

            entry.ToggleMode(SceneBgmMode.Stop);
            Assert.AreEqual(SceneBgmMode.Unset, entry.Mode, "選択中のボタンをもう一度押すと未設定に戻る");
        }

        [Test]
        public void SetClip_SelectsClipMode_AndNoneReturnsToUnset()
        {
            SceneBgmSettings settings = CreateSettings();
            SceneBgmEntry entry = settings.GetOrAddEntry("g1", "Assets/A.unity");
            entry.ToggleMode(SceneBgmMode.Stop);

            entry.SetClip(CreateClip());
            Assert.AreEqual(SceneBgmMode.Clip, entry.Mode, "クリップを入れるとボタンの選択は外れる");

            entry.SetClip(null);
            Assert.AreEqual(SceneBgmMode.Unset, entry.Mode);
            Assert.IsNull(entry.Clip);
        }

        [Test]
        public void ToggleMode_RejectsNonButtonModes()
        {
            SceneBgmEntry entry = CreateSettings().GetOrAddEntry("g1", "Assets/A.unity");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => entry.ToggleMode(SceneBgmMode.Clip));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => entry.ToggleMode(SceneBgmMode.Unset));
        }

        [Test]
        public void Sanitize_FixesInconsistentSerializedState()
        {
            SceneBgmSettings settings = CreateSettings();
            settings.GetOrAddEntry("g1", "Assets/A.unity");
            settings.GetOrAddEntry("g2", "Assets/B.unity");
            AudioClip clip = CreateClip();

            // Inspector やファイル直接編集で矛盾した状態を作る。
            var so = new SerializedObject(settings);
            so.FindProperty("fadeSeconds").floatValue = -1f;
            SerializedProperty entries = so.FindProperty("entries");
            SerializedProperty e0 = entries.GetArrayElementAtIndex(0);
            e0.FindPropertyRelative("mode").enumValueIndex = (int)SceneBgmMode.Stop;
            e0.FindPropertyRelative("clip").objectReferenceValue = clip;
            SerializedProperty e1 = entries.GetArrayElementAtIndex(1);
            e1.FindPropertyRelative("mode").enumValueIndex = (int)SceneBgmMode.Clip;
            e1.FindPropertyRelative("clip").objectReferenceValue = null;
            // 適用時に OnValidate が呼ばれて整合が取られる。
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0f, settings.FadeSeconds);
            so.Update();
            Assert.IsNull(so.FindProperty("entries").GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue, "Clip 以外ならクリップを外す");
            Assert.AreEqual(SceneBgmMode.Stop, settings.Entries[0].Mode);
            Assert.AreEqual(SceneBgmMode.Unset, settings.Entries[1].Mode, "Clip なのにクリップが無ければ Unset");
            Assert.IsFalse(settings.Sanitize(), "整合済みなので変更なし");
        }

        [Test]
        public void TryGetEntryByPath_FollowsPathChanges()
        {
            SceneBgmSettings settings = CreateSettings();
            SceneBgmEntry entry = settings.GetOrAddEntry("g1", "Assets/A.unity");
            Assert.IsTrue(settings.TryGetEntryByPath("Assets/A.unity", out SceneBgmEntry found));
            Assert.AreSame(entry, found);

            entry.SetScenePath("Assets/Moved/A2.unity");
            Assert.IsFalse(settings.TryGetEntryByPath("Assets/A.unity", out _), "古いパスでは見つからない");
            Assert.IsTrue(settings.TryGetEntryByPath("Assets/Moved/A2.unity", out found));
            Assert.AreSame(entry, found);
        }

        // ---------------- 既定音量 ----------------

        [Test]
        public void DefaultVolume_DefaultsToOne_AndSetterClamps()
        {
            SceneBgmSettings settings = CreateSettings();
            Assert.AreEqual(SceneBgm.DefaultVolume, settings.DefaultVolume, "フィールド既定値は 1.0（既存の挙動を変えない）");

            settings.SetDefaultVolume(0.6f);
            Assert.AreEqual(0.6f, settings.DefaultVolume, 1e-6f);
            settings.SetDefaultVolume(1.5f);
            Assert.AreEqual(1f, settings.DefaultVolume);
            settings.SetDefaultVolume(-1f);
            Assert.AreEqual(0f, settings.DefaultVolume);
        }

        [Test]
        public void ResolveDefaultVolume_UsesSettingsOrFallsBackToOne()
        {
            SceneBgmSettings settings = CreateSettings();
            settings.SetDefaultVolume(0.35f);
            Assert.AreEqual(0.35f, SceneBgm.ResolveDefaultVolume(settings), 1e-6f);
            Assert.AreEqual(SceneBgm.DefaultVolume, SceneBgm.ResolveDefaultVolume(null), "設定アセットが無ければ 1.0");
        }

        [Test]
        public void Sanitize_FixesOutOfRangeDefaultVolume()
        {
            SceneBgmSettings settings = CreateSettings();
            var so = new SerializedObject(settings);
            so.FindProperty("defaultVolume").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            settings.Sanitize(); // OnValidate で既に正されている場合もある

            so.Update();
            Assert.AreEqual(1f, so.FindProperty("defaultVolume").floatValue, "シリアライズ値も 0〜1 に正される");
            Assert.AreEqual(1f, settings.DefaultVolume);
            Assert.IsFalse(settings.Sanitize(), "整合済みなので変更なし");
        }

        // ---------------- GUID→パスの更新 ----------------

        [Test]
        public void Sync_UpdatesPathsFromGuid_AndKeepsMissingEntries()
        {
            SceneBgmSettings settings = CreateSettings();
            SceneBgmEntry moved = settings.GetOrAddEntry("moved", "Assets/Old.unity");
            SceneBgmEntry same = settings.GetOrAddEntry("same", "Assets/Same.unity");
            SceneBgmEntry deleted = settings.GetOrAddEntry("deleted", "Assets/Deleted.unity");
            moved.SetClip(CreateClip());

            var map = new Dictionary<string, string>
            {
                { "moved", "Assets/Scenes/New.unity" },
                { "same", "Assets/Same.unity" },
            };

            int changed = SceneBgmScenePathSync.Sync(settings, g => map.TryGetValue(g, out string p) ? p : string.Empty);
            Assert.AreEqual(1, changed);
            Assert.AreEqual("Assets/Scenes/New.unity", moved.ScenePath);
            Assert.AreEqual(SceneBgmMode.Clip, moved.Mode, "移動してもBGM設定は外れない");
            Assert.AreEqual("Assets/Same.unity", same.ScenePath);
            Assert.AreEqual(3, settings.Entries.Count, "削除されたシーンのエントリは消さない");
            Assert.AreEqual("Assets/Deleted.unity", deleted.ScenePath, "削除されたシーンのパスはそのまま残す");
            Assert.IsTrue(settings.TryGetEntryByPath("Assets/Scenes/New.unity", out SceneBgmEntry found));
            Assert.AreSame(moved, found);
        }

        [Test]
        public void Sync_ClearsStalePathWhenAnotherSceneNowUsesIt()
        {
            SceneBgmSettings settings = CreateSettings();
            SceneBgmEntry deleted = settings.GetOrAddEntry("deleted", "Assets/A.unity");
            deleted.ToggleMode(SceneBgmMode.Stop);
            SceneBgmEntry recreated = settings.GetOrAddEntry("recreated", "Assets/A.unity");
            recreated.ToggleMode(SceneBgmMode.Continue);

            SceneBgmScenePathSync.Sync(settings, g => g == "recreated" ? "Assets/A.unity" : string.Empty);
            Assert.AreEqual(string.Empty, deleted.ScenePath);
            Assert.IsTrue(settings.TryGetEntryByPath("Assets/A.unity", out SceneBgmEntry found));
            Assert.AreSame(recreated, found, "実行時は今あるシーンの設定が使われる");
        }

        [Test]
        public void Sync_WithRealAssetDatabase_ResolvesExistingSceneAndIgnoresUnknownGuid()
        {
            const string path = "Packages/com.kagami003d.scenebgm/Tests/Runtime/Scenes/SceneBgmTest_A.unity";
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.IsFalse(string.IsNullOrEmpty(guid), "テストシーンが見つかりません");

            SceneBgmSettings settings = CreateSettings();
            SceneBgmEntry entry = settings.GetOrAddEntry(guid, "Assets/WrongPath.unity");
            SceneBgmEntry unknown = settings.GetOrAddEntry("00000000000000000000000000000001", "Assets/Gone.unity");

            Assert.AreEqual(1, SceneBgmScenePathSync.Sync(settings, SceneBgmScenePathSync.ResolveScenePath));
            Assert.AreEqual(path, entry.ScenePath);
            Assert.AreEqual("Assets/Gone.unity", unknown.ScenePath);
        }

        // ---------------- Validator / ビルド前 ----------------

        [Test]
        public void Validate_ReportsUnsetAndMissingEntries_ExcludingDisabledScenes()
        {
            SceneBgmSettings settings = CreateSettings();
            settings.GetOrAddEntry("a", "Assets/A.unity"); // Unset
            settings.GetOrAddEntry("b", "Assets/B.unity").SetClip(CreateClip());
            settings.GetOrAddEntry("c", "Assets/C.unity"); // Unset だが無効シーン
            settings.GetOrAddEntry("e", "Assets/E.unity").ToggleMode(SceneBgmMode.Continue);
            settings.GetOrAddEntry("gone", "Assets/Gone.unity").ToggleMode(SceneBgmMode.Stop);

            var scenes = new[]
            {
                new SceneBgmValidator.BuildScene("a", "Assets/A.unity", true),
                new SceneBgmValidator.BuildScene("b", "Assets/B.unity", true),
                new SceneBgmValidator.BuildScene("c", "Assets/C.unity", false),
                new SceneBgmValidator.BuildScene("d", "Assets/D.unity", true), // エントリ無し
                new SceneBgmValidator.BuildScene("e", "Assets/E.unity", true),
            };

            SceneBgmValidator.Result result = SceneBgmValidator.Validate(settings, scenes, g => g == "gone" ? string.Empty : "Assets/" + g + ".unity");

            CollectionAssert.AreEqual(new[] { "Assets/A.unity", "Assets/D.unity" }, result.UnsetScenePaths);
            Assert.AreEqual(4, result.CheckedSceneCount);
            Assert.IsTrue(result.HasUnsetScenes);
            Assert.AreEqual(1, result.MissingSceneEntries.Count);
            StringAssert.Contains("Assets/Gone.unity", result.MissingSceneEntries[0]);
        }

        [Test]
        public void LogResult_WarnsPerSceneWithPath_AndSummary()
        {
            SceneBgmSettings settings = CreateSettings();
            var scenes = new[]
            {
                new SceneBgmValidator.BuildScene("a", "Assets/Scenes/Title.unity", true),
                new SceneBgmValidator.BuildScene("b", "Assets/Scenes/Game.unity", true),
            };

            SceneBgmValidator.Result result = SceneBgmValidator.Validate(settings, scenes, g => "Assets/" + g + ".unity");
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Assets/Scenes/Title.unity")));
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Assets/Scenes/Game.unity")));
            LogAssert.Expect(LogType.Warning, new Regex("2 件"));
            Assert.AreEqual(3, SceneBgmValidator.LogResult(result, logWhenOk: false));
        }

        [Test]
        public void BuildPreprocessor_WithoutSettings_WarnsOnceAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(SceneBgmSettings.AssetPath)));
            int warnings = 0;
            Assert.DoesNotThrow(() => warnings = SceneBgmBuildPreprocessor.Run(null));
            Assert.AreEqual(1, warnings);
        }

        [Test]
        public void BuildPreprocessor_WithUnsetScenes_OnlyWarns()
        {
            // 実際の Build Settings を使う。未設定のシーンがあれば警告になるだけで例外は出ない（ビルドを止めない）。
            SceneBgmSettings settings = CreateSettings();
            LogAssert.ignoreFailingMessages = true;
            try
            {
                int warnings = SceneBgmBuildPreprocessor.Run(settings);
                int enabledScenes = 0;
                foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                {
                    if (s.enabled)
                    {
                        enabledScenes++;
                    }
                }

                Assert.AreEqual(enabledScenes > 0 ? enabledScenes + 1 : 0, warnings, "空の設定では有効なシーン全件＋まとめ1行が警告になる");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private SceneBgmSettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<SceneBgmSettings>();
            created.Add(settings);
            return settings;
        }

        private AudioClip CreateClip()
        {
            AudioClip clip = AudioClip.Create("SceneBgmEditTest", 800, 1, 8000, false);
            created.Add(clip);
            return clip;
        }
    }
}
