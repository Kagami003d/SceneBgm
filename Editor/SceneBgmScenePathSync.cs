using System;
using System.Collections.Generic;
using UnityEditor;

namespace Kagami.SceneBgm.Editor
{
    /// <summary>
    /// エントリのシーンGUIDから実行時用の <see cref="SceneBgmEntry.ScenePath"/> を更新する。
    /// シーンの移動・リネーム時（<see cref="AssetPostprocessor"/>）、ウィンドウを開いたとき、ビルド前、Validate のときに実行される。
    /// </summary>
    public static class SceneBgmScenePathSync
    {
        /// <summary>設定アセットの全エントリのパスを更新し、変更があれば保存する。</summary>
        /// <returns>パスを更新したエントリ数。</returns>
        public static int Sync(SceneBgmSettings settings)
        {
            if (settings == null)
            {
                return 0;
            }

            int changed = Sync(settings, ResolveScenePath);
            if (changed > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }

            return changed;
        }

        /// <summary>
        /// GUIDからパスを引く関数を指定して更新する（保存はしない）。
        /// <para>
        /// GUIDが解決できない（シーンが削除された）エントリは消さずにパスもそのまま残す。ただし、そのパスを
        /// 別の有効なエントリが使っている場合（同じ場所に新しいシーンが作られた等）は、実行時に誤って照合されないよう空にする。
        /// シーンが復活すれば次の同期でパスも戻る。
        /// </para>
        /// </summary>
        internal static int Sync(SceneBgmSettings settings, Func<string, string> resolveScenePath)
        {
            if (settings == null)
            {
                return 0;
            }

            int changed = 0;
            var livePaths = new HashSet<string>();
            var missing = new List<SceneBgmEntry>();
            foreach (SceneBgmEntry entry in settings.Entries)
            {
                string path = resolveScenePath(entry.SceneGuid);
                if (string.IsNullOrEmpty(path))
                {
                    missing.Add(entry);
                    continue;
                }

                livePaths.Add(path);
                if (entry.ScenePath != path)
                {
                    entry.SetScenePath(path);
                    changed++;
                }
            }

            foreach (SceneBgmEntry entry in missing)
            {
                if (!string.IsNullOrEmpty(entry.ScenePath) && livePaths.Contains(entry.ScenePath))
                {
                    entry.SetScenePath(string.Empty);
                    changed++;
                }
            }

            if (changed > 0)
            {
                settings.InvalidateLookup();
            }

            return changed;
        }

        /// <summary>シーンのGUIDからパスを引く。見つからなければ空文字。</summary>
        internal static string ResolveScenePath(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
            {
                return string.Empty;
            }

            string path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.AssetPathExists(path))
            {
                return string.Empty;
            }

            return path;
        }
    }

    /// <summary>シーンの移動・リネームを検知してパスを更新する。</summary>
    internal sealed class SceneBgmScenePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!ContainsScene(movedAssets))
            {
                return;
            }

            // 設定アセットが無ければ何もしない（ここでは自動作成しない）。
            // EditorApplication.delayCall はエディタが非アクティブの間は実行が遅れるため、ここで直接更新・保存する
            // （保存した設定アセットの再インポートでは movedAssets が空なので、ここは再帰しない）。
            SceneBgmSettings settings = SceneBgmSettingsLocator.Find();
            if (settings != null)
            {
                SceneBgmScenePathSync.Sync(settings);
            }
        }

        private static bool ContainsScene(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
