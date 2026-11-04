#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 场景布局快照：把当前场景里每个物体的位置 / 旋转 / 缩放（含碰撞箱所在的子物体）存成 JSON，
    /// 重新生成场景后按层级路径还原，这样手动改过的摆件位置不会被生成器重置。
    ///
    /// 菜单：PuzzleEscape → 保存当前场景布局 / 还原当前场景布局
    /// 文件：Assets/SceneLayouts/&lt;场景名&gt;.json
    /// </summary>
    public static class SceneLayoutSnapshot
    {
        private const string Folder = "Assets/SceneLayouts";

        private static readonly string[] SkipRoots = { "Bootstrap", "PlayerCamera" };

        [Serializable]
        public class Entry
        {
            public string Key;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        [Serializable]
        public class Data
        {
            public string Scene;
            public List<Entry> Entries = new List<Entry>();
        }

        // ------------------------------------------------------------------
        // 菜单
        // ------------------------------------------------------------------
        [MenuItem("PuzzleEscape/保存当前场景布局", false, 45)]
        public static void SaveCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[PuzzleEscape] 当前没有打开的场景。");
                return;
            }

            int count = Capture(scene);
            Debug.Log("[PuzzleEscape] 场景布局已保存：" + PathFor(scene.name) +
                      "（" + count + " 个物体）。重新生成后会按这份快照还原。");
        }

        [MenuItem("PuzzleEscape/还原场景布局", false, 46)]
        public static void RestoreCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[PuzzleEscape] 当前没有打开的场景。");
                return;
            }

            int count = Restore(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PuzzleEscape] 场景布局已还原：" + count + " 个物体。");
        }

        // ------------------------------------------------------------------
        // 接口
        // ------------------------------------------------------------------
        public static string PathFor(string sceneName)
        {
            return Folder + "/" + sceneName + ".json";
        }

        public static bool HasSnapshot(string sceneName)
        {
            return File.Exists(PathFor(sceneName));
        }

        /// <summary>把场景里所有物体的本地变换写入快照文件，返回记录数量。</summary>
        public static int Capture(Scene scene)
        {
            var data = new Data { Scene = scene.name };

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsSkippedRoot(root))
                {
                    continue;
                }

                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    data.Entries.Add(new Entry
                    {
                        Key = BuildKey(item),
                        Position = item.localPosition,
                        Rotation = item.localRotation,
                        Scale = item.localScale
                    });
                }
            }

            if (!AssetDatabase.IsValidFolder("Assets/SceneLayouts"))
            {
                AssetDatabase.CreateFolder("Assets", "SceneLayouts");
            }

            File.WriteAllText(PathFor(scene.name), JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(PathFor(scene.name), ImportAssetOptions.ForceUpdate);

            return data.Entries.Count;
        }

        /// <summary>按快照还原场景里物体的本地变换，返回成功匹配的物体数量。</summary>
        public static int Restore(Scene scene)
        {
            string path = PathFor(scene.name);

            if (!File.Exists(path))
            {
                return 0;
            }

            Data data;

            try
            {
                data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PuzzleEscape] 场景布局快照读取失败：" + e.Message);
                return 0;
            }

            if (data == null || data.Entries == null || data.Entries.Count == 0)
            {
                return 0;
            }

            var map = new Dictionary<string, Entry>(data.Entries.Count);
            foreach (Entry entry in data.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Key))
                {
                    map[entry.Key] = entry;
                }
            }

            int applied = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsSkippedRoot(root))
                {
                    continue;
                }

                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!map.TryGetValue(BuildKey(item), out Entry entry))
                    {
                        continue;
                    }

                    item.localPosition = entry.Position;
                    item.localRotation = entry.Rotation;
                    item.localScale = entry.Scale;
                    applied++;
                }
            }

            return applied;
        }

        // ------------------------------------------------------------------
        // 内部
        // ------------------------------------------------------------------
        private static bool IsSkippedRoot(GameObject root)
        {
            foreach (string name in SkipRoots)
            {
                if (root.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 层级路径作为键：每一段是「名字#同名兄弟序号」，保证同名子物体也能一一对应。
        /// </summary>
        private static string BuildKey(Transform item)
        {
            var parts = new List<string>();
            Transform current = item;

            while (current != null)
            {
                int index = 0;

                if (current.parent != null)
                {
                    int same = 0;

                    foreach (Transform sibling in current.parent)
                    {
                        if (sibling.name != current.name)
                        {
                            continue;
                        }

                        if (sibling == current)
                        {
                            index = same;
                        }

                        same++;
                    }
                }

                parts.Add(current.name + "#" + index);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
#endif
