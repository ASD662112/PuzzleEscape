#if UNITY_EDITOR
using System.IO;
using PuzzleEscape.Core;
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 首次打开项目时自动生成 Main 场景并加入 Build Settings，
    /// 也提供菜单手动重建。场景内容只有一个 Bootstrap，关卡在运行时生成。
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += EnsureSceneExists;
        }

        [MenuItem("PuzzleEscape/打开主场景", false, 10)]
        private static void OpenMainScene()
        {
            if (!File.Exists(ScenePath))
            {
                BuildScene();
                return;
            }

            EnsureBuildSettings();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("PuzzleEscape/重新生成主场景", false, 20)]
        private static void RebuildScene()
        {
            if (File.Exists(ScenePath))
            {
                AssetDatabase.DeleteAsset(ScenePath);
            }

            BuildScene();
        }

        /// <summary>
        /// 供命令行 -executeMethod 调用，生成空场景并挂载 Bootstrap。
        /// </summary>
        public static void BuildScene()
        {
            EnsureSceneFolder();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<Bootstrap>();

            // 新场景同时生成可见的庄园几何，而不是只保留一个运行时入口。
            ItemSpawnDirector.ResetRegistry();
            MansionLevelBuilder.Build();
            SceneCameraSetup.Ensure();
            AssetDatabase.SaveAssets();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PuzzleEscape] 已生成主场景：" + ScenePath);
        }

        private static void EnsureSceneExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(ScenePath))
            {
                EnsureBuildSettings();
                return;
            }

            BuildScene();
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        private static void EnsureBuildSettings()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath)
                {
                    return;
                }
            }

            var scenes = EditorBuildSettings.scenes;
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
#endif
