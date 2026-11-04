#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 给墙体、地板、天花板、门框、房门加上顺序前缀（如 CorridorWall → 001_CorridorWall），
    /// 方便在 Hierarchy 里按顺序查找与定位。已带数字前缀的对象会跳过，可重复执行。
    /// </summary>
    public static class StructureNamingTool
    {
        [MenuItem("PuzzleEscape/给建筑结构加编号前缀", false, 20)]
        public static void PrefixMenu()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Run();
        }

        public static void PrefixMainScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Run();
        }

        private static void Run()
        {
            Transform environment = GameObject.Find("Environment")?.transform;
            if (environment == null)
            {
                Debug.LogWarning("[PuzzleEscape] 场景里没有 Environment。");
                return;
            }

            var targets = new List<Transform>();
            foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
            {
                if (t == environment || t.name == "Structure")
                {
                    continue;
                }

                if (!IsStructure(t.name) || Regex.IsMatch(t.name, @"^\d+_"))
                {
                    continue;
                }

                // 只给最外层命名，门扇、门板等子部件保持原名。
                if (HasStructureAncestor(t, environment))
                {
                    continue;
                }

                targets.Add(t);
            }

            // 按世界坐标排序，编号在场景里从左到右、从前到后。
            targets.Sort((a, b) =>
            {
                int z = a.position.z.CompareTo(b.position.z);
                return z != 0 ? z : a.position.x.CompareTo(b.position.x);
            });

            int index = 1;
            foreach (Transform target in targets)
            {
                target.name = index.ToString("000") + "_" + target.name;
                index++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PuzzleEscape] 建筑结构重命名完成：共 " + targets.Count + " 个对象，已保存 " + scene.path);
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
