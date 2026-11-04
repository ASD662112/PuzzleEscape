#if UNITY_EDITOR
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 在当前打开的场景里放一个「可躲藏的衣柜」。
    /// 放好后可以像普通物体一样移动、旋转，并 Ctrl+D 复制到其他房间。
    ///
    /// 结构：
    ///   HidingCabinet            根节点：BoxCollider + HidingSpot
    ///     ├─ CabinetBody…        柜体各块板子
    ///     ├─ DoorPivot           可旋转的柜门
    ///     └─ HidePoint           躲进去后玩家站的位置
    ///
    /// 菜单：PuzzleEscape → 在场景中放置躲藏柜
    /// </summary>
    public static class HidingCabinetBuilder
    {
        private const float Width = 0.95f;
        private const float Height = 1.95f;
        private const float Depth = 0.62f;
        private const float Panel = 0.04f;

        [MenuItem("PuzzleEscape/在场景中放置躲藏柜", false, 47)]
        public static void CreateHidingCabinet()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("PuzzleEscape", "请先退出播放模式再放置躲藏柜。", "确定");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[PuzzleEscape] 当前没有打开的场景。");
                return;
            }

            Material wood = LoadMaterial("SanatoriumWood");
            Material woodDark = LoadMaterial("SanatoriumWoodDark");
            Material metal = LoadMaterial("SanatoriumMetal");

            Vector3 origin = GetSpawnPosition();

            var root = new GameObject("HidingCabinet");
            Undo.RegisterCreatedObjectUndo(root, "创建躲藏柜");
            root.transform.position = origin;

            Transform parent = FindEnvironment(scene);
            if (parent != null)
            {
                root.transform.SetParent(parent, true);
            }

            BuildBody(root.transform, wood, woodDark);

            // 柜体碰撞：关上时挡住玩家和怪物，躲进去时由 HidingSpot 临时关闭。
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(Width, Height, Depth);
            collider.center = new Vector3(0f, Height * 0.5f, 0f);

            Transform doorPivot = BuildDoor(root.transform, wood, metal);

            var hidePoint = new GameObject("HidePoint");
            hidePoint.transform.SetParent(root.transform, false);
            hidePoint.transform.localPosition = new Vector3(0f, 0.05f, -0.08f);

            var spot = root.AddComponent<HidingSpot>();
            spot.DisplayName = "衣柜";
            spot.Door = doorPivot;
            spot.HidePoint = hidePoint.transform;

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[PuzzleEscape] 已在 " + origin + " 放置躲藏柜。可直接移动、旋转，或 Ctrl+D 复制。" +
                      "记得 Ctrl+S 保存场景后再构建。");
        }

        private static Vector3 GetSpawnPosition()
        {
            // 优先放在 Scene 视图中心，落在地面上。
            SceneView view = SceneView.lastActiveSceneView;

            if (view != null)
            {
                Vector3 pivot = view.pivot;
                if (Physics.Raycast(pivot + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 20f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    return hit.point;
                }

                return new Vector3(pivot.x, 0f, pivot.z);
            }

            return Vector3.zero;
        }

        private static Transform FindEnvironment(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Environment")
                {
                    return root.transform;
                }
            }

            return null;
        }

        [MenuItem("PuzzleEscape/检查场景中的躲藏柜", false, 48)]
        public static void AuditHidingCabinets()
        {
            HidingSpot[] spots = Object.FindObjectsOfType<HidingSpot>(true);

            if (spots.Length == 0)
            {
                Debug.LogWarning("[PuzzleEscape] 当前场景没有躲藏柜。用菜单「在场景中放置躲藏柜」创建一个。");
                return;
            }

            int missingCollider = 0;
            int missingDoor = 0;
            int missingPoint = 0;

            foreach (HidingSpot spot in spots)
            {
                if (spot.GetComponent<Collider>() == null)
                {
                    missingCollider++;
                    Debug.LogWarning("[PuzzleEscape] 躲藏柜缺少碰撞体（无法交互）：" + PathOf(spot.transform));
                }

                if (spot.Door == null)
                {
                    missingDoor++;
                }

                if (spot.HidePoint == null)
                {
                    missingPoint++;
                }
            }

            Debug.Log("[PuzzleEscape] 场景内躲藏柜 " + spots.Length + " 个；缺碰撞体 " +
                      missingCollider + "，未指定柜门 " + missingDoor + "，未指定躲藏点 " + missingPoint + "。");
        }

        private static string PathOf(Transform item)
        {
            string path = item.name;
            Transform parent = item.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static Material LoadMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Materials/Sanatorium/" + name + ".mat");
        }

        private static void BuildBody(Transform root, Material wood, Material woodDark)
        {
            float halfW = Width * 0.5f;
            float halfD = Depth * 0.5f;

            // 踢脚 + 左右侧板 + 顶板 + 底板 + 背板 + 顶冠线脚
            Part(root, "CabinetPlinth", new Vector3(0f, 0.05f, 0f),
                new Vector3(Width - 0.12f, 0.1f, Depth - 0.1f), woodDark);
            Part(root, "CabinetSide", new Vector3(-halfW + Panel * 0.5f, Height * 0.5f, 0f),
                new Vector3(Panel, Height, Depth), wood);
            Part(root, "CabinetSide", new Vector3(halfW - Panel * 0.5f, Height * 0.5f, 0f),
                new Vector3(Panel, Height, Depth), wood);
            Part(root, "CabinetTop", new Vector3(0f, Height - Panel * 0.5f, 0f),
                new Vector3(Width, Panel, Depth), wood);
            Part(root, "CabinetBottom", new Vector3(0f, 0.1f + Panel * 0.5f, 0f),
                new Vector3(Width - Panel * 2f, Panel, Depth), wood);
            Part(root, "CabinetBack", new Vector3(0f, Height * 0.5f, -halfD + 0.02f),
                new Vector3(Width - Panel * 2f, Height - 0.12f, 0.04f), woodDark);
            Part(root, "CabinetCornice", new Vector3(0f, Height + 0.03f, 0f),
                new Vector3(Width + 0.08f, 0.06f, Depth + 0.06f), woodDark);

            // 顶部一层隔板，让里面看起来像真的衣柜
            Part(root, "CabinetShelf", new Vector3(0f, Height - 0.34f, -0.01f),
                new Vector3(Width - Panel * 2f, 0.03f, Depth - 0.08f), wood);
        }

        private static Transform BuildDoor(Transform root, Material wood, Material metal)
        {
            float doorW = Width - 0.1f;
            float doorH = Height - 0.14f;
            float frontZ = Depth * 0.5f;

            var pivot = new GameObject("DoorPivot");
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = new Vector3(-doorW * 0.5f + 0.02f, Height * 0.5f, frontZ + 0.02f);

            Part(pivot.transform, "DoorPanel", new Vector3(doorW * 0.5f, 0f, 0f),
                new Vector3(doorW, doorH, 0.035f), wood);
            Part(pivot.transform, "DoorInset", new Vector3(doorW * 0.5f, 0f, 0.024f),
                new Vector3(doorW - 0.16f, doorH - 0.2f, 0.012f), wood);
            Part(pivot.transform, "DoorHandle", new Vector3(doorW - 0.09f, 0f, 0.05f),
                new Vector3(0.03f, 0.16f, 0.03f), metal);
            Part(pivot.transform, "DoorHinge", new Vector3(0.04f, doorH * 0.32f, 0.01f),
                new Vector3(0.05f, 0.1f, 0.035f), metal);
            Part(pivot.transform, "DoorHinge", new Vector3(0.04f, -doorH * 0.32f, 0.01f),
                new Vector3(0.05f, 0.1f, 0.035f), metal);

            return pivot.transform;
        }

        private static void Part(Transform parent, string name, Vector3 localPosition,
            Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            Collider own = go.GetComponent<Collider>();
            if (own != null)
            {
                Object.DestroyImmediate(own);
            }

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            Undo.RegisterCreatedObjectUndo(go, "创建躲藏柜部件");
        }
    }
}
#endif
