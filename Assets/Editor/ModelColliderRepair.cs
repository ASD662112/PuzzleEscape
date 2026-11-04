#if UNITY_EDITOR
using System.Collections.Generic;
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 手动移动模型后，把场景中所有模型 BoxCollider 重新对齐到当前渲染包围盒。
    /// 不再只处理 Real_* 容器：墙体、门扇、程序化家具、灯具、便签等只要自身或子级
    /// 带有 MeshRenderer，都会被纳入修复范围。
    /// 菜单：PuzzleEscape → 修复模型碰撞箱 / 校验模型碰撞箱
    /// </summary>
    public static class ModelColliderRepair
    {
        private const float CenterToleranceSqr = 0.0004f; // 2cm
        private const float SizeToleranceSqr = 0.0025f;   // 5cm
        private const float InteractionMinThickness = 0.06f;

        [MenuItem("PuzzleEscape/修复模型碰撞箱")]
        public static void RepairAllModelColliders()
        {
            RepairOrValidate(false);
        }

        [MenuItem("PuzzleEscape/校验模型碰撞箱")]
        public static void ValidateAllModelColliders()
        {
            RepairOrValidate(true);
        }

        /// <summary>
        /// 修复被错误放大的碰撞箱：某些摆件（如落地灯）的 BoxCollider 尺寸会变成
        /// 整张地图大小（44 × 1.93 × 18），玩家一移动就会被这层看不见的盒子顶到半空。
        /// 这里按渲染包围盒重新贴合，处理 Main 与 Sanatorium 两个场景。
        /// </summary>
        [MenuItem("PuzzleEscape/修复异常放大的碰撞箱")]
        public static void FixOversizedColliders()
        {
            string[] scenes = { "Assets/Scenes/Main.unity", "Assets/Scenes/Sanatorium.unity" };
            int total = 0;

            foreach (string path in scenes)
            {
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                total += FixOversizedInScene(path);
            }

            Debug.Log("[PuzzleEscape] 异常放大的碰撞箱修复完成，共修正 " + total + " 个。");
        }

        private static int FixOversizedInScene(string scenePath)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            var samples = new List<string>();
            int fixedCount = 0;

            foreach (BoxCollider box in Object.FindObjectsOfType<BoxCollider>(true))
            {
                if (box == null || box.isTrigger || box.gameObject.scene != scene)
                {
                    continue;
                }

                Bounds target;
                if (!TryComputeLocalBounds(box.transform, out target))
                {
                    continue;
                }

                // 真实模型本身不超过 6 m；某一轴比模型大 3 倍以上就判定为错误尺寸。
                if (target.size.x > 6f || target.size.y > 6f || target.size.z > 6f)
                {
                    continue;
                }

                bool oversized =
                    box.size.x > target.size.x * 3f + 0.5f ||
                    box.size.y > target.size.y * 3f + 0.5f ||
                    box.size.z > target.size.z * 3f + 0.5f;

                if (!oversized)
                {
                    continue;
                }

                samples.Add($"{GetPath(box.transform)}  {box.size} → {target.size}");
                Undo.RecordObject(box, "修复异常放大的碰撞箱");
                box.center = target.center;
                box.size = target.size;
                EditorUtility.SetDirty(box);
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[PuzzleEscape] " + scenePath + " 异常放大碰撞箱修正 " + fixedCount + " 个：" +
                      string.Join("；", samples));
            return fixedCount;
        }

        private static void RepairOrValidate(bool dryRun)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[PuzzleEscape] 当前没有可修复的场景。");
                return;
            }

            BoxCollider[] colliders = Object.FindObjectsOfType<BoxCollider>(true);
            int totalCount = 0;
            int checkedCount = 0;
            int changedCount = 0;
            int skippedTrigger = 0;
            int skippedWithoutRenderer = 0;
            var samples = new List<string>();

            foreach (BoxCollider box in colliders)
            {
                if (box == null || box.gameObject.scene != scene)
                {
                    continue;
                }

                totalCount++;

                // 触发器通常用作灯光/出口区域，尺寸有意大于模型，保持原样。
                if (box.isTrigger)
                {
                    skippedTrigger++;
                    continue;
                }

                if (IsUnderPlayer(box.transform))
                {
                    continue;
                }

                Bounds currentBounds;
                if (!TryComputeLocalBounds(box.transform, out currentBounds))
                {
                    // 只有纯碰撞体、没有可见网格的隐形墙/边界保留手动尺寸。
                    skippedWithoutRenderer++;
                    continue;
                }

                // 便签等可交互薄片的包围盒只有几毫米厚，直接贴合物体会让射线很难命中；
                // 只对 Interactable 保留最小 6cm 厚度，中心仍严格对齐模型。
                bool forceInteractionPadding = false;
                if (box.GetComponentInParent<Interactable>() != null)
                {
                    forceInteractionPadding =
                        box.size.x < InteractionMinThickness ||
                        box.size.y < InteractionMinThickness ||
                        box.size.z < InteractionMinThickness;

                    Vector3 size = currentBounds.size;
                    currentBounds.size = new Vector3(
                        Mathf.Max(size.x, InteractionMinThickness),
                        Mathf.Max(size.y, InteractionMinThickness),
                        Mathf.Max(size.z, InteractionMinThickness));
                }

                checkedCount++;

                Vector3 centerDelta = currentBounds.center - box.center;
                Vector3 sizeDelta = currentBounds.size - box.size;

                if (!forceInteractionPadding &&
                    centerDelta.sqrMagnitude <= CenterToleranceSqr &&
                    sizeDelta.sqrMagnitude <= SizeToleranceSqr)
                {
                    continue;
                }

                if (samples.Count < 12)
                {
                    samples.Add($"{GetPath(box.transform)}  中心 {centerDelta}  尺寸 {sizeDelta}");
                }

                if (!dryRun)
                {
                    Undo.RecordObject(box, "修复模型碰撞箱");
                    box.center = currentBounds.center;
                    box.size = currentBounds.size;
                    EditorUtility.SetDirty(box);
                }

                changedCount++;
            }

            if (!dryRun && changedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            string action = dryRun ? "校验" : "修复";
            Debug.Log($"[PuzzleEscape] 模型碰撞箱{action}完成：" +
                      $"场景 BoxCollider {totalCount} 个，其中可计算模型 {checkedCount} 个，" +
                      $"{action} {changedCount} 个；跳过触发器 {skippedTrigger} 个、无渲染器 {skippedWithoutRenderer} 个。");

            for (int i = 0; i < samples.Count; i++)
            {
                Debug.Log("[PuzzleEscape]   偏移 " + samples[i]);
            }
        }

        private static bool IsUnderPlayer(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.CompareTag("Player"))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string GetPath(Transform transform)
        {
            var parts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        /// <summary>
        /// 计算 root 本地空间下所有子 MeshRenderer / SkinnedMeshRenderer 的联合包围盒。
        /// </summary>
        private static bool TryComputeLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = GetSharedMesh(renderer);
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 localMatrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Vector3 center = mesh.bounds.center;
                Vector3 extents = mesh.bounds.extents;

                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = localMatrix.MultiplyPoint3x4(
                                center + new Vector3(extents.x * x, extents.y * y, extents.z * z));

                            if (!hasBounds)
                            {
                                bounds = new Bounds(corner, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                bounds.Encapsulate(corner);
                            }
                        }
                    }
                }
            }

            return hasBounds &&
                   bounds.size.x > 0.0001f &&
                   bounds.size.y > 0.0001f &&
                   bounds.size.z > 0.0001f;
        }

        private static Mesh GetSharedMesh(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
            {
                return filter.sharedMesh;
            }

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            return skinned != null ? skinned.sharedMesh : null;
        }
    }
}
#endif
