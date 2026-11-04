#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 混合光照烘焙：灯改成 Mixed + Shadowmask，静态物体参与 GI 烘焙。
    /// 直射光和实时阴影仍然可用（开灯/关灯、闪烁照旧），间接光与远距离阴影烘进 lightmap。
    /// 菜单：PuzzleEscape → 配置混合光照烘焙 / 开始烘焙光影（当前场景）/ 烘焙全部场景
    /// 疗养院专用：配置疗养院光影烘焙 / 开始烘焙疗养院光影 / 清除疗养院光影数据 / 检查疗养院光影
    /// </summary>
    public static class LightingBakeTool
    {
        private const string SettingsPath = "Assets/New Lighting Settings.lighting";
        private const string SanatoriumScenePath = "Assets/Scenes/Sanatorium.unity";

        /// <summary>每单位 40 texel，单张 lightmap 最大 2048。</summary>
        private const float LightmapResolution = 40f;
        private const int LightmapMaxSize = 2048;

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Sanatorium.unity"
        };

        [MenuItem("PuzzleEscape/配置混合光照烘焙", false, 32)]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            LightingSettings settings = EnsureSettings();

            foreach (string path in Scenes)
            {
                if (System.IO.File.Exists(path))
                {
                    ConfigureScene(path, settings);
                }
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("PuzzleEscape/开始烘焙光影（当前场景）", false, 33)]
        public static void BakeActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            if (Lightmapping.isRunning)
            {
                Debug.LogWarning("[PuzzleEscape] 已经有一个烘焙任务在运行。");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            EnsureSceneSettings(scene);

            Debug.Log("[PuzzleEscape] 开始烘焙光影：" + scene.path + "（耗时较长，请勿中途关闭编辑器）。");
            bool ok = Lightmapping.Bake();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PuzzleEscape] 光影烘焙" + (ok ? "完成：" : "失败：") + scene.path);
        }

        [MenuItem("PuzzleEscape/烘焙全部场景", false, 34)]
        public static void BakeAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            foreach (string path in Scenes)
            {
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                EnsureSceneSettings(scene);

                Debug.Log("[PuzzleEscape] 开始烘焙光影：" + path + "（耗时较长，请勿中途关闭编辑器）。");
                bool ok = Lightmapping.Bake();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[PuzzleEscape] 光影烘焙" + (ok ? "完成：" : "失败：") + path);
            }
        }

        private static void ConfigureScene(string path, LightingSettings settings)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Lightmapping.SetLightingSettingsForScene(scene, settings);

            int giRenderers;
            int mixedLights;
            ApplyBakeFlags(scene, out giRenderers, out mixedLights);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[PuzzleEscape] {path} 混合光照配置完成：参与烘焙的渲染器 {giRenderers} 个，" +
                      $"Mixed 灯 {mixedLights} 盏。");
        }

        // ------------------------------------------------------------ 疗养院专用

        [MenuItem("PuzzleEscape/配置疗养院光影烘焙", false, 35)]
        public static void ConfigureSanatorium()
        {
            if (!CanEditScene())
            {
                return;
            }

            if (!System.IO.File.Exists(SanatoriumScenePath))
            {
                Debug.LogError("[PuzzleEscape] 找不到场景：" + SanatoriumScenePath);
                return;
            }

            ConfigureScene(SanatoriumScenePath, EnsureSettings());
            Debug.Log("[PuzzleEscape] 疗养院光影配置完成，可以执行「开始烘焙疗养院光影」。");
        }

        [MenuItem("PuzzleEscape/开始烘焙疗养院光影", false, 36)]
        public static void BakeSanatorium()
        {
            if (!CanEditScene())
            {
                return;
            }

            if (Lightmapping.isRunning)
            {
                Debug.LogWarning("[PuzzleEscape] 已经有一个烘焙任务在运行。");
                return;
            }

            if (!System.IO.File.Exists(SanatoriumScenePath))
            {
                Debug.LogError("[PuzzleEscape] 找不到场景：" + SanatoriumScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SanatoriumScenePath, OpenSceneMode.Single);
            PrepareSanatoriumForBake(scene);

            Debug.Log("[PuzzleEscape] 开始烘焙疗养院光影（" + EnsureSettings().lightmapper + "）。" +
                      "整个疗养院有 5 层、上百个房间，耗时可能很长，请勿中途关闭编辑器。");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool ok = Lightmapping.Bake();
            watch.Stop();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string dataPath = Lightmapping.lightingDataAsset != null
                ? AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)
                : "（无）";
            Debug.Log($"[PuzzleEscape] 疗养院光影烘焙{(ok ? "完成" : "失败")}：用时 " +
                      $"{watch.Elapsed.TotalMinutes:F1} 分钟，lightmap {LightmapSettings.lightmaps.Length} 张，" +
                      $"烘焙数据 {dataPath}。");
        }

        [MenuItem("PuzzleEscape/清除疗养院光影数据", false, 37)]
        public static void ClearSanatoriumLighting()
        {
            if (!CanEditScene())
            {
                return;
            }

            if (!System.IO.File.Exists(SanatoriumScenePath))
            {
                Debug.LogError("[PuzzleEscape] 找不到场景：" + SanatoriumScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SanatoriumScenePath, OpenSceneMode.Single);
            Lightmapping.Clear();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 顺手删掉场景目录下自动生成的 lightmap 与烘焙数据，避免体积越滚越大。
            string folder = System.IO.Path.GetDirectoryName(SanatoriumScenePath);
            int deleted = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(assetPath).StartsWith("Lightmap-"))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    deleted++;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:LightingDataAsset", new[] { folder }))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                deleted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PuzzleEscape] 已清除疗养院光影数据：删除 " + deleted +
                      " 个烘焙资源。重新烘焙请用「开始烘焙疗养院光影」。");
        }

        [MenuItem("PuzzleEscape/检查疗养院光影", false, 38)]
        public static void InspectSanatorium()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            if (!System.IO.File.Exists(SanatoriumScenePath))
            {
                Debug.LogError("[PuzzleEscape] 找不到场景：" + SanatoriumScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SanatoriumScenePath, OpenSceneMode.Single);

            int giRenderers = 0;
            int totalRenderers = 0;
            foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>(true))
            {
                if (renderer == null || renderer.gameObject.scene != scene)
                {
                    continue;
                }

                totalRenderers++;
                if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) &
                     StaticEditorFlags.ContributeGI) != 0)
                {
                    giRenderers++;
                }
            }

            int mixed = 0;
            int baked = 0;
            int realtime = 0;
            foreach (Light light in Object.FindObjectsOfType<Light>(true))
            {
                if (light == null || light.gameObject.scene != scene)
                {
                    continue;
                }

                switch (light.lightmapBakeType)
                {
                    case LightmapBakeType.Mixed:
                        mixed++;
                        break;
                    case LightmapBakeType.Baked:
                        baked++;
                        break;
                    default:
                        realtime++;
                        break;
                }
            }

            LightingSettings settings = Lightmapping.lightingSettings;
            string settingsPath = settings != null ? AssetDatabase.GetAssetPath(settings) : "未指定";
            string backend = settings != null ? settings.lightmapper.ToString() : "未知";
            string dataPath = Lightmapping.lightingDataAsset != null
                ? AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)
                : "无（还没烘焙）";

            Debug.Log($"[PuzzleEscape] 疗养院光影检查：渲染器 {totalRenderers} 个（ContributeGI {giRenderers}）；" +
                      $"灯 Mixed {mixed} / Baked {baked} / Realtime {realtime}；" +
                      $"烘焙后端 {backend}；光照设置 {settingsPath}；烘焙数据 {dataPath}；" +
                      $"lightmap {LightmapSettings.lightmaps.Length} 张。");
        }

        /// <summary>疗养院的烘焙前准备：套用设置、标记静态 GI、把灯切成 Mixed。</summary>
        private static void PrepareSanatoriumForBake(Scene scene)
        {
            LightingSettings settings = EnsureSettings();
            Lightmapping.SetLightingSettingsForScene(scene, settings);

            int giRenderers;
            int mixedLights;
            ApplyBakeFlags(scene, out giRenderers, out mixedLights);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[PuzzleEscape] 疗养院烘焙前准备：参与烘焙的渲染器 {giRenderers} 个，" +
                      $"Mixed 灯 {mixedLights} 盏，光源模式 Shadowmask。");
        }

        [MenuItem("PuzzleEscape/光影烘焙后端：Progressive GPU", false, 39)]
        public static void UseGpuBackend()
        {
            SetBackend(LightingSettings.Lightmapper.ProgressiveGPU);
        }

        [MenuItem("PuzzleEscape/光影烘焙后端：Progressive CPU", false, 40)]
        public static void UseCpuBackend()
        {
            SetBackend(LightingSettings.Lightmapper.ProgressiveCPU);
        }

        private static void SetBackend(LightingSettings.Lightmapper backend)
        {
            if (!CanEditScene())
            {
                return;
            }

            LightingSettings settings = EnsureSettings();
            settings.lightmapper = backend;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log("[PuzzleEscape] 光影烘焙后端已切换到 " + backend + "。");

            if (backend == LightingSettings.Lightmapper.ProgressiveGPU)
            {
                Debug.LogWarning("[PuzzleEscape] GPU 烘焙依赖显卡与显存：显卡不支持 OpenCL 或场景超出显存时，" +
                                 "Unity 会自动回退到 CPU，或直接报错。疗养院规模较大，建议先降分辨率试烘一次。");
            }
        }

        private static bool CanEditScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 标记参与 GI 烘焙的渲染器，并把灯改成 Mixed。
        /// 会转动的门扇、抽屉、灯具不参与烘焙：它们动起来会带着错误的烘焙结果走。
        /// </summary>
        private static void ApplyBakeFlags(Scene scene, out int giRenderers, out int mixedLights)
        {
            giRenderers = 0;
            mixedLights = 0;

            foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>(true))
            {
                if (renderer == null || renderer.gameObject.scene != scene)
                {
                    continue;
                }

                GameObject go = renderer.gameObject;
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);

                if (IsBakeable(go.name))
                {
                    flags |= StaticEditorFlags.ContributeGI;
                    giRenderers++;
                }
                else
                {
                    flags &= ~StaticEditorFlags.ContributeGI;
                }

                GameObjectUtility.SetStaticEditorFlags(go, flags);
            }

            foreach (Light light in Object.FindObjectsOfType<Light>(true))
            {
                if (light == null || light.gameObject.scene != scene)
                {
                    continue;
                }

                if (light.type == LightType.Directional || light.type == LightType.Point ||
                    light.type == LightType.Spot)
                {
                    light.lightmapBakeType = LightmapBakeType.Mixed;
                    mixedLights++;
                }
            }
        }

        /// <summary>
        /// 固定结构与大型家具参与烘焙；门、抽屉、灯具、血迹贴花等动态或透明物体不参与。
        /// </summary>
        private static bool IsBakeable(string name)
        {
            string lower = name.ToLowerInvariant();

            return !(lower.Contains("doorleaf") || lower.Contains("doorslab") ||
                     lower.Contains("doorpanel") || lower.Contains("doorhandle") ||
                     lower.Contains("doorhinge") || lower.Contains("doorbar") ||
                     lower.Contains("drawer") || lower.Contains("cabinetdoor") ||
                     lower.Contains("lamp") || lower.Contains("light") ||
                     lower.Contains("blood") || lower.Contains("smear") ||
                     lower.Contains("decal") || lower.Contains("handprint") ||
                     lower.Contains("paper") || lower.Contains("stain") ||
                     lower.Contains("drip") || lower.Contains("spatter"));
        }

        private static void EnsureSceneSettings(Scene scene)
        {
            if (Lightmapping.lightingSettings != null)
            {
                return;
            }

            Lightmapping.SetLightingSettingsForScene(scene, EnsureSettings());
        }

        private static LightingSettings EnsureSettings()
        {
            LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "New Lighting Settings" };
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var serialized = new SerializedObject(settings);
            SetInt(serialized, "m_GIWorkflowMode", 1);              // 手动烘焙，不自动生成
            SetBool(serialized, "m_EnableBakedLightmaps", true);
            SetBool(serialized, "m_EnableRealtimeLightmaps", false); // 不再烘焙实时 GI（Enlighten 已废弃）
            SetBool(serialized, "m_UsingShadowmask", true);
            SetInt(serialized, "m_MixedBakeMode", 2);               // Shadowmask
            // 烘焙后端（CPU / GPU）由「光影烘焙后端」菜单选择，这里不覆盖。
            SetFloat(serialized, "m_BakeResolution", LightmapResolution);
            SetInt(serialized, "m_LightmapMaxSize", LightmapMaxSize);
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void SetInt(SerializedObject serialized, string property, int value)
        {
            SerializedProperty found = serialized.FindProperty(property);
            if (found != null)
            {
                found.intValue = value;
            }
        }

        private static void SetBool(SerializedObject serialized, string property, bool value)
        {
            SerializedProperty found = serialized.FindProperty(property);
            if (found != null)
            {
                found.boolValue = value;
            }
        }

        private static void SetFloat(SerializedObject serialized, string property, float value)
        {
            SerializedProperty found = serialized.FindProperty(property);
            if (found != null)
            {
                found.floatValue = value;
            }
        }
    }
}
#endif
