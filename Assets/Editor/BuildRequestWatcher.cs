#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 编辑器内构建请求监听。
    /// Unity 打开项目时，另一个 Unity 进程无法再打开同一项目做批处理构建，
    /// 所以由 PowerShell 脚本写一个请求文件，让「正在运行的编辑器」自己执行构建。
    ///
    /// 请求：Build/build_request.flag
    /// 进度：Build/build_result.txt（BUILDING / OK / FAIL: 原因）
    /// </summary>
    [InitializeOnLoad]
    public static class BuildRequestWatcher
    {
        private const double CheckInterval = 0.5;

        private static readonly string Root = Path.GetDirectoryName(Application.dataPath);
        private static readonly string RequestPath = Path.Combine(Root, "Build/build_request.flag");
        private static readonly string ResultPath = Path.Combine(Root, "Build/build_result.txt");

        private static bool building;
        private static double nextCheck;

        static BuildRequestWatcher()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (building || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextCheck)
            {
                return;
            }

            nextCheck = EditorApplication.timeSinceStartup + CheckInterval;

            if (!File.Exists(RequestPath))
            {
                return;
            }

            RunBuild();
        }

        private static void RunBuild()
        {
            building = true;

            try
            {
                TryDelete(RequestPath);
                Debug.Log("[PuzzleEscape] 收到外部构建请求，开始在编辑器内构建 Windows 版本…");
                WriteResult("BUILDING");

                AssetDatabase.SaveAssets();
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

                try
                {
                    BatchBuild.BuildWindows();
                    WriteResult("OK");
                    Debug.Log("[PuzzleEscape] 编辑器内构建完成。");
                }
                catch (Exception e)
                {
                    Debug.LogError("[PuzzleEscape] 编辑器内构建失败：" + e.Message);
                    WriteResult("FAIL: " + e.Message.Replace("\r", " ").Replace("\n", " "));
                }
            }
            finally
            {
                building = false;
            }
        }

        private static void WriteResult(string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                File.WriteAllText(ResultPath, text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PuzzleEscape] 写入构建结果失败：" + e.Message);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        }
    }
}
#endif
