#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 把全图的墙体、地板、门框、房门收进一个「Structure」组，方便整体查看与调整。
    /// 只移动最外层的匹配对象，子部件（门扇、门板、合页）随父节点一起走。
    /// </summary>
    public static class StructureGroupingTool
    {
        private const string GroupName = "Structure";

        [MenuItem("PuzzleEscape/合并建筑结构分组", false, 19)]
        public static void GroupMenu()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Run(true);
        }

        public static void AuditMainScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Run(false);
        }

        public static void GroupMainScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Run(true);
        }

        private static void Run(bool apply)
        {
            GameObject environment = GameObject.Find("Environment");
            if (environment == null)
            {
                Debug.LogWarning("[PuzzleEscape] 场景里没有 Environment。");
                return;
            }

            var matches = new List<Transform>();
            foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
            {
                if (t == environment.transform || t.name == GroupName)
                {
                    continue;
                }

                if (!IsStructure(t.name))
                {
                    continue;
                }

                // 只收最外层，避免把门扇从房门根节点里拆出去。
                if (HasStructureAncestor(t, environment.transform))
                {
                    continue;
                }

                matches.Add(t);
            }

            Debug.Log("[PuzzleEscape] 建筑结构检查：待合并 " + matches.Count + " 个根对象。");

            if (!apply || matches.Count == 0)
            {
                return;
            }

            Transform group = environment.transform.Find(GroupName);
            if (group == null)
            {
                var go = new GameObject(GroupName);
                go.transform.SetParent(environment.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                group = go.transform;
            }

            int moved = 0;
            foreach (Transform match in matches)
            {
                if (match == null || match.IsChildOf(group))
                {
                    continue;
                }

                match.SetParent(group, true);
                moved++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PuzzleEscape] 建筑结构已合并：" + moved + " 个对象移入 " + GroupName + "，场景已保存。");
        }

        private static bool HasStructureAncestor(Transform t, Transform environment)
        {
            Transform parent = t.parent;
            while (parent != null && parent != environment)
            {
                if (IsStructure(parent.name))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static bool IsStructure(string name)
        {
            // 家具/装饰里带 Wall/Door/Floor 的对象先排除。
            if (name.Contains("Painting") || name.Contains("Sconce") || name.Contains("Sign") ||
                name.Contains("Cabinet") || name.Contains("Drawer") || name.Contains("Wardrobe") ||
                name.Contains("Sideboard") || name.Contains("Commode") || name.Contains("Lamp"))
            {
                return false;
            }

            if (name.Contains("DoorTrim") || name.Contains("DoorLintel"))
            {
                return true;
            }

            if (name.StartsWith("RoomDoor") || name == "DoorLeaf" || name == "DoorPanel" ||
                name == "DoorHinge" || name.StartsWith("DoorHandle"))
            {
                return true;
            }

            if (name == "Floor" || name.EndsWith("Floor") || name.StartsWith("Floor_"))
            {
                return true;
            }

            return name.Contains("Wall") || name.Contains("Ceiling");
        }
    }
}
#endif
