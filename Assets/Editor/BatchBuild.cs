#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 命令行/CI 构建入口，避免依赖 MCP 桥接。
    /// 示例：
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod PuzzleEscape.EditorTools.BatchBuild.BuildAndroid
    /// </summary>
    public static class BatchBuild
    {
        /// <summary>兜底场景列表：Build Settings 里没有可用条目时才用。</summary>
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Sanatorium.unity"
        };

        /// <summary>
        /// 构建场景以 Build Settings 为准，只取启用且位于 Assets/Scenes 下的场景，
        /// 避免把编辑器里临时保存到其它目录的 .unity 打进包，也避免漏掉新加的场景。
        /// </summary>
        private static string[] GetScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();

            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || string.IsNullOrEmpty(entry.path))
                {
                    continue;
                }

                if (!entry.path.StartsWith("Assets/Scenes/"))
                {
                    Debug.LogWarning("[PuzzleEscape] 跳过不在 Assets/Scenes 下的场景：" + entry.path);
                    continue;
                }

                if (!File.Exists(entry.path))
                {
                    Debug.LogWarning("[PuzzleEscape] Build Settings 里的场景已丢失：" + entry.path);
                    continue;
                }

                if (!scenes.Contains(entry.path))
                {
                    scenes.Add(entry.path);
                }
            }

            if (scenes.Count == 0)
            {
                foreach (string path in ScenePaths)
                {
                    if (File.Exists(path))
                    {
                        scenes.Add(path);
                    }
                }
            }

            return scenes.ToArray();
        }

        /// <summary>
        /// 删除 Library/PlayerDataCache：Unity 的玩家数据缓存，删掉 Build 输出目录不会动它，
        /// 残留会让下一次构建直接复制旧的 levelN，看起来“场景没有更新”。
        /// </summary>
        private static void PurgePlayerDataCache()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string cachePath = Path.Combine(projectRoot, "Library/PlayerDataCache");

            if (!Directory.Exists(cachePath))
            {
                return;
            }

            try
            {
                Directory.Delete(cachePath, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PuzzleEscape] 清理 PlayerDataCache 失败：" + e.Message);
            }
        }

        /// <summary>仅用于验证当前激活平台，不执行构建。</summary>
        public static void LogActiveTarget()
        {
            Debug.Log("[PuzzleEscape] 当前激活构建平台：" +
                      EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("PuzzleEscape/批处理构建 Windows")]
        public static void BuildWindows()
        {
            Build(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64,
                "Build/Windows/PuzzleEscape.exe", "Windows x64", false);
        }

        /// <summary>
        /// 干净构建：先清掉 Library/PlayerDataCache，再强制重建玩家数据。
        /// 普通构建在“没有变化”时会直接从缓存复制 levelN，删掉 Build/Windows 也不会刷新场景。
        /// </summary>
        [MenuItem("PuzzleEscape/批处理构建 Windows（干净构建）")]
        public static void BuildWindowsClean()
        {
            Build(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64,
                "Build/Windows/PuzzleEscape.exe", "Windows x64", true);
        }

        /// <summary>
        /// 先报告当前场景是否有未保存改动，再保存并做干净构建。
        /// 日志里 isDirty=False 而你刚改过场景，说明改动根本没进 Unity 的内存（常见于在 Play 模式里改场景）。
        /// </summary>
        [MenuItem("PuzzleEscape/保存场景并干净构建 Windows")]
        public static void SaveAndCleanBuildWindows()
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            Debug.Log("[PuzzleEscape] 当前场景：" + scene.path +
                      "，未保存改动(isDirty)：" + scene.isDirty +
                      "，根对象数：" + scene.rootCount);

            BuildWindowsClean();
        }

        [MenuItem("PuzzleEscape/批处理构建 Android")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTargetGroup.Android, BuildTarget.Android,
                "Build/Android/PuzzleEscape.apk", "Android", false);
        }

        private static void Build(BuildTargetGroup group, BuildTarget target,
            string outputPath, string label, bool clean)
        {
            // BuildPipeline 读取的是磁盘上的场景文件：编辑器里没保存的改动不会进构建。
            // 构建前先保存所有打开的场景与资源，保证构建结果和编辑器里看到的一致。
            bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[PuzzleEscape] 构建前保存场景：" + (saved ? "已保存" : "无需保存"));

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                throw new Exception("[PuzzleEscape] 无法切换构建平台：" + target);
            }

            BuildOptions options = BuildOptions.DetailedBuildReport;

            if (clean)
            {
                PurgePlayerDataCache();
                options |= BuildOptions.CleanBuildCache;
                Debug.Log("[PuzzleEscape] 干净构建：已清空 PlayerDataCache 缓存。");
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string[] scenes = GetScenes();
            if (scenes.Length == 0)
            {
                throw new Exception("[PuzzleEscape] 没有可构建的场景，请先在 Build Settings 里加入 Assets/Scenes 下的场景。");
            }

            Debug.Log("[PuzzleEscape] 构建场景（按 Build Settings，位于 Assets/Scenes）：" +
                      string.Join(", ", scenes));

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report == null)
            {
                throw new Exception("[PuzzleEscape] " + label + " 构建失败：没有返回 BuildReport。");
            }

            BuildSummary summary = report.summary;
            Debug.Log(string.Format(
                "[PuzzleEscape] {0} 构建结果：{1}；总大小 {2:F2} MB；错误 {3}，警告 {4}；耗时 {5:F1} 秒。",
                label, summary.result, summary.totalSize / 1048576f,
                summary.totalErrors, summary.totalWarnings, summary.totalTime.TotalSeconds));

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception("[PuzzleEscape] " + label + " 构建失败：" + summary.result);
            }

            string fullPath = Path.GetFullPath(outputPath);
            bool exists = File.Exists(fullPath);
            Debug.Log(string.Format(
                "[PuzzleEscape] {0} 输出文件：{1}（存在 {2}，{3}）",
                label, fullPath, exists,
                exists ? File.GetLastWriteTime(fullPath).ToString("yyyy-MM-dd HH:mm:ss") : "无"));
        }
    }
}
#endif
