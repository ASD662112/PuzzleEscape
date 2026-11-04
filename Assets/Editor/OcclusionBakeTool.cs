#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 生成遮挡剔除（Occlusion Culling）数据：
    /// 墙壁、天花板、地板、门框、地毯作为遮挡物，其余模型作为被遮挡物，
    /// 被墙挡住或在视野外的模型由引擎直接跳过渲染，降低开销。
    /// 菜单：PuzzleEscape → 生成遮挡剔除数据 / 检查遮挡标记
    /// </summary>
    public static class OcclusionBakeTool
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Sanatorium.unity"
        };

        [MenuItem("PuzzleEscape/生成遮挡剔除数据", false, 30)]
        public static void BakeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            foreach (string path in Scenes)
            {
                if (System.IO.File.Exists(path))
                {
                    BakeScene(path);
                }
            }
        }

        [MenuItem("PuzzleEscape/生成疗养院遮挡剔除", false, 32)]
        public static void BakeSanatorium()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            const string path = "Assets/Scenes/Sanatorium.unity";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[PuzzleEscape] 找不到场景：" + path);
                return;
            }

            BakeScene(path);
        }

        [MenuItem("PuzzleEscape/清除疗养院遮挡剔除", false, 33)]
        public static void ClearSanatoriumOcclusion()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            const string path = "Assets/Scenes/Sanatorium.unity";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[PuzzleEscape] 找不到场景：" + path);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            float size = StaticOcclusionCulling.umbraDataSize;
            StaticOcclusionCulling.Clear();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PuzzleEscape] 已清除疗养院遮挡剔除数据（清除前 " + (size / 1024f).ToString("F0") +
                      " KB）。重新烘焙请用「生成疗养院遮挡剔除」。");
        }

        [MenuItem("PuzzleEscape/检查遮挡标记", false, 31)]
        public static void Inspect()
        {
            foreach (string path in Scenes)
            {
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int occluders = 0;
                int occludees = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        StaticEditorFlags flags =
                            GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                        if ((flags & StaticEditorFlags.OccluderStatic) != 0)
                        {
                            occluders++;
                        }

                        if ((flags & StaticEditorFlags.OccludeeStatic) != 0)
                        {
                            occludees++;
                        }
                    }
                }

                Debug.Log($"[PuzzleEscape] {path}：遮挡物 {occluders} 个，被遮挡物 {occludees} 个。");
            }
        }

        private static void BakeScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int occluders = 0;
            int occludees = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    GameObject go = renderer.gameObject;
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                    flags &= ~(StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                    flags |= StaticEditorFlags.OccludeeStatic;

                    if (IsOccluder(go.name))
                    {
                        flags |= StaticEditorFlags.OccluderStatic;
                        occluders++;
                    }

                    occludees++;
                    GameObjectUtility.SetStaticEditorFlags(go, flags);

                    // 门、怪物、玩家等动态物体也允许被静态遮挡物剔除。
                    renderer.allowOcclusionWhenDynamic = true;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 默认 Smallest Occluder 是 5 m，导致小墙体、门框、家具根本不参与遮挡。
            // 调到 0.5 m 并收紧孔洞阈值，细胞划分更细，剔除更准。
            StaticOcclusionCulling.smallestOccluder = 0.5f;
            StaticOcclusionCulling.smallestHole = 0.1f;

            bool ok = StaticOcclusionCulling.Compute();

            // 烘焙结果会写回场景的 OcclusionCullingData 引用，再存一次。
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[PuzzleEscape] {path} 遮挡剔除烘焙{(ok ? "完成" : "失败")}：" +
                      $"遮挡物 {occluders} 个，被遮挡物 {occludees} 个，" +
                      $"Smallest Occluder 0.5 m / Hole 0.1 m，数据 {StaticOcclusionCulling.umbraDataSize:F0} KB。");
        }

        /// <summary>
        /// 墙壁、天花板、地板、门框、地毯以及大件家具作为遮挡物。
        /// 会旋转的门扇、抽屉和灯具不算遮挡物：烘焙数据是静态的，它们转动后会导致错误剔除。
        /// </summary>
        private static bool IsOccluder(string name)
        {
            string lower = name.ToLowerInvariant();

            // 会动的部件、透明贴片（血迹/贴花/纸张）、灯具都不能当遮挡物：
            // 遮挡数据是静态烘焙的，这些东西一动就会把后面的模型错误剔除。
            if (lower.Contains("lamp") || lower.Contains("light") ||
                lower.Contains("doorleaf") || lower.Contains("doorslab") || lower.Contains("doorpanel") ||
                lower.Contains("doorhandle") || lower.Contains("doorhinge") || lower.Contains("doorbar") ||
                lower.Contains("drawer") || lower.Contains("cabinetdoor") ||
                lower.Contains("blood") || lower.Contains("smear") || lower.Contains("decal") ||
                lower.Contains("handprint") || lower.Contains("paper") || lower.Contains("stain") ||
                lower.Contains("drip") || lower.Contains("spatter"))
            {
                return false;
            }

            // 结构：墙、天花板、屋顶、地板、地毯、门框、门楣、拱形、楼梯、立柱
            if (lower.Contains("wall") || lower.Contains("ceiling") || lower.Contains("floor") ||
                lower.Contains("roof") || lower.Contains("carpet") || lower.Contains("rug") ||
                lower.Contains("trim") || lower.Contains("lintel") || lower.Contains("frame") ||
                lower.Contains("arch") || lower.Contains("step") || lower.Contains("pillar") ||
                lower.Contains("column"))
            {
                return true;
            }

            // 楼梯梯段可以挡视线，但细栏杆不算
            if (lower.Contains("stair") && !lower.Contains("rail"))
            {
                return true;
            }

            // 大件家具：本身体积足够大，可以挡住里面的东西。
            return lower.Contains("bookshelf") || lower.Contains("shelf") || lower.Contains("cabinet") ||
                   lower.Contains("piano") || lower.Contains("wardrobe") || lower.Contains("sofa") ||
                   lower.Contains("counter") || lower.Contains("specimencase") || lower.Contains("winerack") ||
                   lower.Contains("dresser") || lower.Contains("bed") || lower.Contains("table") ||
                   lower.Contains("barrel") || lower.Contains("crate");
        }
    }
}
#endif
