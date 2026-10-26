using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Kagami.SceneBgm.Editor
{
    /// <summary>
    /// 設定の検査。有効な Build Settings シーンのうち未設定（<see cref="SceneBgmMode.Unset"/> またはエントリ無し）のものと、
    /// 見つからないシーンのエントリを報告する。ビルドは止めない。
    /// <para>
    /// ウィンドウの Validate ボタン、ビルド前（<see cref="SceneBgmBuildPreprocessor"/>）、
    /// バッチ（<c>-executeMethod Kagami.SceneBgm.Editor.SceneBgmValidator.RunBatch</c>）から使う。
    /// </para>
    /// </summary>
    public static class SceneBgmValidator
    {
        internal const string LogPrefix = "[SceneBgm] ";

        /// <summary>検査結果。</summary>
        public sealed class Result
        {
            internal Result(List<string> unsetScenePaths, List<string> missingSceneEntries, int checkedSceneCount)
            {
                UnsetScenePaths = unsetScenePaths;
                MissingSceneEntries = missingSceneEntries;
                CheckedSceneCount = checkedSceneCount;
            }

            /// <summary>有効な Build Settings シーンのうち、BGMが未設定のシーンのパス（Build Settings の順）。</summary>
            public IReadOnlyList<string> UnsetScenePaths { get; }

            /// <summary>シーンアセットが見つからないエントリ（「最後のパス (GUID)」形式）。エントリは削除されない。</summary>
            public IReadOnlyList<string> MissingSceneEntries { get; }

            /// <summary>検査した有効な Build Settings シーンの数。</summary>
            public int CheckedSceneCount { get; }

            /// <summary>未設定のシーンがあれば true（ビルド時の警告対象）。</summary>
            public bool HasUnsetScenes => UnsetScenePaths.Count > 0;
        }

        /// <summary>Build Settings のシーン1件（テストで差し替えられるよう EditorBuildSettingsScene から切り離している）。</summary>
        internal readonly struct BuildScene
        {
            public BuildScene(string guid, string path, bool enabled)
            {
                Guid = guid ?? string.Empty;
                Path = path ?? string.Empty;
                Enabled = enabled;
            }

            public string Guid { get; }

            public string Path { get; }

            public bool Enabled { get; }
        }

        /// <summary>GUID→パスの更新をしてから、現在の Build Settings で検査する。</summary>
        public static Result Validate(SceneBgmSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            SceneBgmScenePathSync.Sync(settings);
            return Validate(settings, GetBuildScenes(), SceneBgmScenePathSync.ResolveScenePath);
        }

        internal static Result Validate(SceneBgmSettings settings, IEnumerable<BuildScene> buildScenes, Func<string, string> resolveScenePath)
        {
            var unset = new List<string>();
            int checkedCount = 0;
            foreach (BuildScene scene in buildScenes)
            {
                if (!scene.Enabled)
                {
                    // 無効（チェックなし）のシーンはビルドに含まれないので対象外。
                    continue;
                }

                checkedCount++;
                if (!settings.TryGetEntryByGuid(scene.Guid, out SceneBgmEntry entry) || entry.Mode == SceneBgmMode.Unset)
                {
                    unset.Add(string.IsNullOrEmpty(scene.Path) ? scene.Guid : scene.Path);
                }
            }

            var missing = new List<string>();
            foreach (SceneBgmEntry entry in settings.Entries)
            {
                if (string.IsNullOrEmpty(resolveScenePath(entry.SceneGuid)))
                {
                    string lastPath = string.IsNullOrEmpty(entry.ScenePath) ? "(パス不明)" : entry.ScenePath;
                    missing.Add($"{lastPath} ({entry.SceneGuid})");
                }
            }

            return new Result(unset, missing, checkedCount);
        }

        /// <summary>現在の Build Settings のシーン一覧（順番どおり）。</summary>
        internal static List<BuildScene> GetBuildScenes()
        {
            var list = new List<BuildScene>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                string guid = scene.guid.Empty() ? AssetDatabase.AssetPathToGUID(scene.path) : scene.guid.ToString();
                string path = SceneBgmScenePathSync.ResolveScenePath(guid);
                list.Add(new BuildScene(guid, string.IsNullOrEmpty(path) ? scene.path : path, scene.enabled));
            }

            return list;
        }

        /// <summary>未設定シーンを1シーン1行＋まとめ1行の警告で出す。見つからないシーンは情報ログで出す。</summary>
        /// <returns>出した警告の行数。</returns>
        internal static int LogResult(Result result, bool logWhenOk)
        {
            int warnings = 0;
            foreach (string path in result.UnsetScenePaths)
            {
                Debug.LogWarning($"{LogPrefix}BGMが未設定のシーン: {path}（実行時は直前のBGMを継続します。Window > SceneBgm で[継続]/[停止]/曲のどれかを選んでください）");
                warnings++;
            }

            if (result.HasUnsetScenes)
            {
                Debug.LogWarning($"{LogPrefix}BGMが未設定のシーンが {result.UnsetScenePaths.Count} 件あります（有効な Build Settings シーン {result.CheckedSceneCount} 件中）。");
                warnings++;
            }

            if (result.MissingSceneEntries.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append(LogPrefix).Append("シーンが見つからない設定が ").Append(result.MissingSceneEntries.Count)
                    .Append(" 件あります（設定は残しています。不要なら Window > SceneBgm の「Build Settings に無い設定」から削除できます）:");
                foreach (string item in result.MissingSceneEntries)
                {
                    sb.Append("\n  ").Append(item);
                }

                Debug.Log(sb.ToString());
            }

            if (logWhenOk && !result.HasUnsetScenes)
            {
                Debug.Log($"{LogPrefix}問題ありません（有効な Build Settings シーン {result.CheckedSceneCount} 件すべてにBGMが設定されています）。");
            }

            return warnings;
        }

        /// <summary>
        /// バッチ用: <c>Unity -batchmode -projectPath &lt;path&gt; -executeMethod Kagami.SceneBgm.Editor.SceneBgmValidator.RunBatch -logFile &lt;log&gt;</c>。
        /// 未設定のシーンがあっても警告を出して終了コード0。設定アセットが無い／読めない場合だけ終了コード1。
        /// </summary>
        public static void RunBatch()
        {
            int exitCode;
            try
            {
                SceneBgmSettings settings = SceneBgmSettingsLocator.Find();
                if (settings == null)
                {
                    string reason = SceneBgmSettingsLocator.AssetFileExists() ? "読み込めません（ファイルが壊れている可能性があります）" : "見つかりません";
                    Debug.LogError($"{LogPrefix}設定アセットが{reason}: {SceneBgmSettings.AssetPath}");
                    exitCode = 1;
                }
                else
                {
                    LogResult(Validate(settings), logWhenOk: true);
                    exitCode = 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }
    }
}
