using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kagami.SceneBgm.Editor
{
    /// <summary>
    /// ビルド前に GUID→パスを更新し、BGMが未設定の有効なシーンを警告する（ビルドは止めない）。
    /// </summary>
    internal sealed class SceneBgmBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Run(SceneBgmSettingsLocator.Find());
        }

        /// <summary>ビルド前の処理本体。例外を外に出さない（ビルドを止めない）。</summary>
        /// <returns>出した警告の行数。</returns>
        internal static int Run(SceneBgmSettings settings)
        {
            try
            {
                if (settings == null)
                {
                    Debug.LogWarning($"{SceneBgmValidator.LogPrefix}設定アセットがありません（{SceneBgmSettings.AssetPath}）。シーンごとのBGM自動切替は行われません。");
                    return 1;
                }

                return SceneBgmValidator.LogResult(SceneBgmValidator.Validate(settings), logWhenOk: false);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{SceneBgmValidator.LogPrefix}ビルド前の検査に失敗しました（ビルドは続行します）: {e}");
                return 1;
            }
        }
    }
}
