#if UNITY_EDITOR
using PuzzleEscape.Core;
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 把运行时程序化生成的庄园几何直接烘焙进当前场景，方便在 Scene 视图里查看和调整。
    /// 生成后材质会保存为 Assets/Art/Materials/Generated 下的资源，场景重开不会丢引用。
    /// </summary>
    public static class MansionSceneGenerator
    {
        [MenuItem("PuzzleEscape/生成场景模型", false, 12)]
        public static void GenerateSceneModels()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("PuzzleEscape", "请先退出 Play Mode，再生成场景模型。", "确定");
                return;
            }

            Generate();
        }

        /// <summary>
        /// 批处理入口：先打开主场景再重新烘焙，避免在空白场景里生成。
        /// </summary>
        public static void RegenerateMainScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Generate();
        }

        /// <summary>
        /// 批处理入口：先打开主场景再修复碰撞箱，避免在空白场景里统计。
        /// </summary>
        public static void RepairMainSceneColliders()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            ModelColliderRepair.RepairAllModelColliders();
        }

        /// <summary>
        /// 供 MCP / 批处理调用的无确认版本。
        /// </summary>
        public static void Generate()
        {
            GameObject environment = GameObject.Find("Environment");
            if (environment != null)
            {
                Object.DestroyImmediate(environment);
            }

            ItemSpawnDirector.ResetRegistry();
            MansionLevelBuilder.Build();
            SceneCameraSetup.Ensure();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[PuzzleEscape] 场景模型已生成并保存：" + scene.path);
        }
    }
}
#endif
