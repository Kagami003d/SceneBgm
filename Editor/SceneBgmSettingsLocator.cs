using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kagami.SceneBgm.Editor
{
    /// <summary>
    /// 設定アセット（<see cref="SceneBgmSettings.AssetPath"/>）を探す／作る。
    /// </summary>
    public static class SceneBgmSettingsLocator
    {
        private static bool warnedMisplaced;
        private static bool warnedMultiple;

        /// <summary>
        /// 設定アセットを探す（作らない）。まず固定パスを見て、無ければ <c>t:SceneBgmSettings</c> で検索する。
        /// 固定パス以外で見つかった場合（実行時に読めない）や複数見つかった場合は警告して先頭を使う。
        /// </summary>
        public static SceneBgmSettings Find()
        {
            var atPath = AssetDatabase.LoadAssetAtPath<SceneBgmSettings>(SceneBgmSettings.AssetPath);
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(SceneBgmSettings));
            if (atPath != null)
            {
                if (guids.Length > 1 && !warnedMultiple)
                {
                    warnedMultiple = true;
                    Debug.LogWarning($"[SceneBgm] SceneBgmSettings が {guids.Length} 個あります。{SceneBgmSettings.AssetPath} だけを使います（他は実行時に使われません）。", atPath);
                }

                return atPath;
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var found = AssetDatabase.LoadAssetAtPath<SceneBgmSettings>(path);
                if (found == null)
                {
                    continue;
                }

                if (!warnedMisplaced)
                {
                    warnedMisplaced = true;
                    Debug.LogWarning(
                        $"[SceneBgm] 設定アセットが {path} にあります。実行時は Resources.Load(\"{SceneBgmSettings.ResourceName}\") で読むため、"
                        + $"{SceneBgmSettings.AssetPath} に移動してください。", found);
                }

                return found;
            }

            return null;
        }

        /// <summary>設定アセットが <see cref="SceneBgmSettings.AssetPath"/> にファイルとしては存在する（読めるかは問わない）。</summary>
        public static bool AssetFileExists()
        {
            return File.Exists(SceneBgmSettings.AssetPath);
        }

        /// <summary>設定アセットを探し、無ければ <see cref="SceneBgmSettings.AssetPath"/> に作る（ウィンドウ操作などUIをきっかけに呼ぶ）。</summary>
        public static SceneBgmSettings FindOrCreate()
        {
            SceneBgmSettings settings = Find();
            if (settings != null)
            {
                return settings;
            }

            if (AssetFileExists())
            {
                // ファイルはあるが読めない（壊れている等）。上書きしない。
                Debug.LogError($"[SceneBgm] {SceneBgmSettings.AssetPath} を読み込めません。ファイルが壊れていないか確認してください。");
                return null;
            }

            string directory = Path.GetDirectoryName(SceneBgmSettings.AssetPath)?.Replace('\\', '/');
            EnsureFolder(directory);

            settings = ScriptableObject.CreateInstance<SceneBgmSettings>();
            AssetDatabase.CreateAsset(settings, SceneBgmSettings.AssetPath);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"[SceneBgm] 設定アセットを作成しました: {SceneBgmSettings.AssetPath}", settings);
            return settings;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
