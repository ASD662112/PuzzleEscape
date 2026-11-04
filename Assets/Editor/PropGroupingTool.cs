#if UNITY_EDITOR
using System.Collections.Generic;
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 摆件分组整理：把散落在房间根节点下的碰撞体、零件收回各自摆件的根节点，
    /// 让「模型 + 碰撞箱 + 可动部件」都在一个组里，方便整体改位置和缩放。
    /// </summary>
    public static class PropGroupingTool
    {
        [MenuItem("PuzzleEscape/整理摆件分组", false, 18)]
        public static void GroupMenu()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Run(true);
        }

        /// <summary>批处理：只统计不改场景。</summary>
        public static void AuditMainScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Run(false);
        }

        /// <summary>批处理：整理并保存。</summary>
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

            List<Transform> props = CollectPropRoots(environment.transform);
            int candidates = 0;
            int moved = 0;

            foreach (Transform prop in props)
            {
                if (prop.parent == null)
                {
                    continue;
                }

                Bounds propBounds = ComputeBounds(prop);
                var orphans = new List<Transform>();

                foreach (Transform sibling in prop.parent)
                {
                    if (sibling == prop || sibling.childCount > 0)
                    {
                        continue;
                    }

                    if (sibling.name.StartsWith("Real_") || sibling.GetComponent<Interactable>() != null)
                    {
                        continue;
                    }

                    bool colliderOnly = sibling.GetComponent<Renderer>() == null &&
                                        sibling.GetComponent<Collider>() != null;
                    bool samePrefix = sibling.name.StartsWith(prop.name) ||
                                      prop.name.StartsWith(sibling.name);

                    if (!colliderOnly && !samePrefix)
                    {
                        continue;
                    }

                    Bounds siblingBounds = ComputeBounds(sibling);
                    if (!propBounds.Intersects(siblingBounds) &&
                        Vector3.Distance(propBounds.center, siblingBounds.center) > 1.5f)
                    {
                        continue;
                    }

                    orphans.Add(sibling);
                }

                if (orphans.Count == 0)
                {
                    continue;
                }

                candidates += orphans.Count;
                if (!apply)
                {
                    continue;
                }

                foreach (Transform orphan in orphans)
                {
                    orphan.SetParent(prop, true);
                    moved++;
                }
            }

            Debug.Log(string.Format(
                "[PuzzleEscape] 摆件分组检查：摆件根节点 {0} 个；可收回的散落部件 {1} 个{2}。",
                props.Count, candidates, apply ? "，已收回 " + moved + " 个" : string.Empty));

            if (apply && moved > 0)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[PuzzleEscape] 摆件分组整理完成并已保存：" + scene.path);
            }
        }

        private static List<Transform> CollectPropRoots(Transform environment)
        {
            var props = new List<Transform>();
            foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null)
                {
                    continue;
                }

                bool isRealModel = t.name.StartsWith("Real_");
                bool isInteractable = t.GetComponent<Interactable>() != null;
                bool isPropRoot = t.name == "Candelabra" || t.name == "SofaDetails" ||
                                  t.name == "PendantLamp" || t.name == "FloorLamp" ||
                                  t.name == "WallSconce" || t.name == "TableLamp" ||
                                  t.name == "SpecimenCase" || t.name == "Piano";

                if (!isRealModel && !isInteractable && !isPropRoot)
                {
                    continue;
                }

                if (t.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                props.Add(t);
            }

            return props;
        }

        private static Bounds ComputeBounds(Transform root)
        {
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            bool has = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (!has)
                {
                    bounds = collider.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return bounds;
        }
    }
}
#endif
