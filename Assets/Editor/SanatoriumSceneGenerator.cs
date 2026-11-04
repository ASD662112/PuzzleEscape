#if UNITY_EDITOR
using PuzzleEscape.Core;
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 生成并烘焙第二张地图「夜巡疗养院」的场景：
    /// 空场景 + Bootstrap + 程序化生成的 Environment，保存为 Assets/Scenes/Sanatorium.unity 并加入 Build Settings。
    /// </summary>
    public static class SanatoriumSceneGenerator
    {
        private const string ScenePath = "Assets/Scenes/Sanatorium.unity";

        [MenuItem("PuzzleEscape/生成疗养院场景", false, 14)]
        public static void GenerateWithDialog()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("PuzzleEscape", "请先退出 Play Mode，再生成疗养院场景。", "确定");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Generate();
        }

        /// <summary>供批处理调用的无确认版本。</summary>
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<Bootstrap>();

            // 先确保程序化血迹贴图存在，材质才能引用到
            BloodTextureGenerator.Ensure();

            ItemSpawnDirector.ResetRegistry();
            SanatoriumLevelBuilder.Build();

            EnsureCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 生成后立即整理摆件：把浮空的贴回地面、把嵌进墙/门的推出来。
            SanatoriumPropAudit.FixScene(scene);

            // 有手动保存过的布局快照时，按快照把物体摆回你调整过的位置
            if (SceneLayoutSnapshot.HasSnapshot(scene.name))
            {
                int restored = SceneLayoutSnapshot.Restore(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[PuzzleEscape] 已应用场景布局快照：" + restored + " 个物体还原到手动位置。");
            }

            RegisterScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PuzzleEscape] 疗养院场景已生成并保存：" + ScenePath);
        }

        private static void EnsureCamera()
        {
            GameMap map = GameMapCatalog.Get(GameMapCatalog.SanatoriumId);
            Vector3 spawn = map.SpawnPosition;

            var cameraGo = new GameObject("PlayerCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.layer = 2;
            cameraGo.transform.position = new Vector3(spawn.x, 1.63f, spawn.z);
            cameraGo.transform.rotation = Quaternion.Euler(0f, map.SpawnYaw, 0f);

            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.018f, 0.028f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.fieldOfView = 70f;
            cameraGo.AddComponent<AudioListener>();
        }

        private static void RegisterScene()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            foreach (EditorBuildSettingsScene entry in scenes)
            {
                if (entry.path == ScenePath)
                {
                    entry.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
