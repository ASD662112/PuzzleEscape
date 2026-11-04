#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 疗养院摆件整理：除了墙壁、地板、天花板、门框、门之外，
    /// 把场景里所有模型与摆件都检查一遍——
    /// 浮空的落到下方物体表面上，嵌进墙/门的沿水平方向推出来。
    ///
    /// 处理单位：
    ///  · 真实模型（Real_*）与可交互柜子（Container_*）整棵子树一起移动；
    ///  · 程序化家具按「同一父节点 + 水平投影相交」合并成一组整体移动，
    ///    这样床架、床垫、枕头、床头板不会被拆散。
    /// 菜单：PuzzleEscape → 整理疗养院摆件
    /// </summary>
    public static class SanatoriumPropAudit
    {
        private const string ScenePath = "Assets/Scenes/Sanatorium.unity";

        private const float GroundTolerance = 0.03f;
        private const float MaxGroundSnap = 3f;
        private const float SeparationGap = 0.03f;
        private const float MaxSeparateTotal = 1.2f;
        private const int SeparationIterations = 4;

        /// <summary>高过这个高度、又没有支撑物的摆件按“挂在墙上/天花板上”处理，不下拉。</summary>
        private const float MountedHeight = 1.15f;

        /// <summary>结构件与刻意吊挂/贴墙的部件：完全不动。</summary>
        private static readonly string[] SkipNameTokens =
        {
            "Wall", "Floor", "Roof", "Ceiling", "Slab",
            "Skirting", "Handrail", "RailBracket", "Threshold", "Jamb",
            "Door",   // 门扇、门套、门框、门槛、合页、执手等全部不参与移动
            "Step", "StairRail", "Stairwell",
            "SignPlaque", "FireCabinet", "CableTray", "BreakerPanel",
            "Sconce", "Chandelier", "SurgeryLamp", "ExamLamp",
            "NurseMonitor", "Monitor", "ReceptionDeskLamp",
            "Record_", "Note_", "Paper", "Hit",
            "Pipe", "FusePanel", "FusePanelDoor", "FuseLever", "FuseLamp",
            "ReceptionBackWall", "ReceptionSign", "ToiletPartition",
            "GarageLine", "GarageShutter", "ShutterPanel", "ShutterRib", "Hoist",
            "FireBalcony", "FireRail", "FireGate", "FireLadderRung",
            // 恐怖陈设：血迹贴地、墙面血痕、散纸、翻倒的家具，位置都是刻意摆的
            "Blood", "Smear", "ScatteredPaper", "Toppled", "BodyBag", "FallenCrate",
            "Wheelchair", "Bag", "Crate", "Wheel", "Chair", "Stand"
        };

        [MenuItem("PuzzleEscape/整理疗养院摆件", false, 43)]
        public static void FixAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleEscape] 请先退出播放模式再执行。");
                return;
            }

            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError("[PuzzleEscape] 找不到场景：" + ScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FixScene(scene);
        }

        /// <summary>对已经打开的疗养院场景做整理（生成场景后可以直接调用）。</summary>
        public static void FixScene(Scene scene)
        {
            Physics.SyncTransforms();

            List<List<Transform>> units = CollectUnits(scene);

            // 从低到高处理：先把桌子落地，桌上的东西随后才会正确落到桌面上
            units.Sort((a, b) =>
            {
                TryGetUnitBounds(a, out Bounds boundsA);
                TryGetUnitBounds(b, out Bounds boundsB);
                return boundsA.min.y.CompareTo(boundsB.min.y);
            });

            int grounded = 0;
            int separated = 0;
            int skippedMounted = 0;

            foreach (List<Transform> unit in units)
            {
                if (!TryGetUnitBounds(unit, out Bounds bounds))
                {
                    continue;
                }

                // 1) 浮空：落到下方最近的地面 / 桌面 / 柜顶。
                //    已经有人托着的不动；离下方表面太远（壁灯、吊灯、标识、管线）当作吊挂件跳过。
                if (!HasSupport(unit, bounds) && TryFindSurfaceDelta(unit, bounds, out float delta))
                {
                    if (delta < -MountedHeight)
                    {
                        skippedMounted++;
                        continue;
                    }

                    if (Mathf.Abs(delta) > GroundTolerance && Mathf.Abs(delta) < MaxGroundSnap)
                    {
                        MoveUnit(unit, Vector3.up * delta);
                        grounded++;
                        Physics.SyncTransforms();

                        if (!TryGetUnitBounds(unit, out bounds))
                        {
                            continue;
                        }
                    }
                }

                // 2) 穿模：只从墙体、门框、门扇、柜子里水平推出
                float movedTotal = 0f;

                for (int iteration = 0; iteration < SeparationIterations; iteration++)
                {
                    if (!TryFindPush(unit, out Vector3 push))
                    {
                        break;
                    }

                    movedTotal += push.magnitude;
                    if (movedTotal > MaxSeparateTotal)
                    {
                        break;
                    }

                    MoveUnit(unit, push);
                    separated++;
                    Physics.SyncTransforms();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[PuzzleEscape] 疗养院摆件整理完成：共 " + units.Count +
                      " 组，落地修正 " + grounded + " 组，水平推开 " + separated +
                      " 次，跳过吊挂类 " + skippedMounted + " 组。");
        }

        // ------------------------------------------------------------------
        // 收集与分组
        // ------------------------------------------------------------------
        private static List<List<Transform>> CollectUnits(Scene scene)
        {
            var units = new List<List<Transform>>();

            // 每个父节点下各自做一次“相交即合并”，家具不会被拆散
            var looseByParent = new Dictionary<Transform, List<Transform>>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    // Real_ / Container_ 子树整体处理，内部零件不再单独收集
                    if (HasPropAncestor(t))
                    {
                        continue;
                    }

                    if (IsPropRoot(t))
                    {
                        if (t.GetComponentInChildren<Renderer>(true) != null)
                        {
                            units.Add(new List<Transform> { t });
                        }

                        continue;
                    }

                    if (t.GetComponent<Renderer>() == null)
                    {
                        continue;   // 只有碰撞体/灯/逻辑节点的对象不参与
                    }

                    if (IsSkipName(t.name))
                    {
                        continue;
                    }

                    Transform parent = t.parent != null ? t.parent : t;
                    if (!looseByParent.TryGetValue(parent, out List<Transform> list))
                    {
                        list = new List<Transform>();
                        looseByParent[parent] = list;
                    }

                    list.Add(t);
                }
            }

            foreach (List<Transform> siblings in looseByParent.Values)
            {
                units.AddRange(ClusterByOverlap(siblings));
            }

            return units;
        }

        /// <summary>同一父节点下，水平投影相交的家具零件合并成一组。</summary>
        private static List<List<Transform>> ClusterByOverlap(List<Transform> items)
        {
            var result = new List<List<Transform>>();
            int count = items.Count;

            if (count == 0)
            {
                return result;
            }

            var bounds = new Bounds[count];
            for (int i = 0; i < count; i++)
            {
                bounds[i] = GetRendererBounds(items[i]);
            }

            int[] parent = new int[count];
            for (int i = 0; i < count; i++)
            {
                parent[i] = i;
            }

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (OverlapsXZ(bounds[i], bounds[j]))
                    {
                        Union(parent, i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<Transform>>();
            for (int i = 0; i < count; i++)
            {
                int rootIndex = Find(parent, i);
                if (!groups.TryGetValue(rootIndex, out List<Transform> list))
                {
                    list = new List<Transform>();
                    groups[rootIndex] = list;
                }

                list.Add(items[i]);
            }

            result.AddRange(groups.Values);
            return result;
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);

            if (rootA != rootB)
            {
                parent[rootB] = rootA;
            }
        }

        private static bool OverlapsXZ(Bounds a, Bounds b)
        {
            const float padding = 0.02f;

            bool overlapX = a.min.x < b.max.x + padding && a.max.x + padding > b.min.x;
            bool overlapZ = a.min.z < b.max.z + padding && a.max.z + padding > b.min.z;
            return overlapX && overlapZ;
        }

        private static bool IsPropRoot(Transform transform)
        {
            string name = transform.name;
            return name.StartsWith("Real_") || name.StartsWith("Container_");
        }

        private static bool HasPropAncestor(Transform transform)
        {
            Transform parent = transform.parent;

            while (parent != null)
            {
                if (IsPropRoot(parent))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static bool IsSkipName(string name)
        {
            foreach (string token in SkipNameTokens)
            {
                if (name.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // 落地
        // ------------------------------------------------------------------
        /// <summary>返回“下方表面高度 − 摆件底部高度”：负数表示浮空，正数表示陷进去了。</summary>
        private static bool TryFindSurfaceDelta(List<Transform> unit, Bounds bounds, out float delta)
        {
            delta = 0f;

            Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down,
                bounds.size.y + MountedHeight + MaxGroundSnap + 1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger)
                {
                    continue;
                }

                if (BelongsTo(hit.collider.transform, unit))
                {
                    continue;
                }

                delta = hit.point.y - bounds.min.y;
                return true;
            }

            return false;
        }

        /// <summary>底部是否已经被地板、桌面、层板等托住。</summary>
        private static bool HasSupport(List<Transform> unit, Bounds bounds)
        {
            Vector3 center = new Vector3(bounds.center.x, bounds.min.y - 0.02f, bounds.center.z);
            Vector3 half = new Vector3(
                Mathf.Max(0.05f, bounds.size.x * 0.45f), 0.04f,
                Mathf.Max(0.05f, bounds.size.z * 0.45f));

            Collider[] hits = Physics.OverlapBox(center, half, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                if (BelongsTo(hit.transform, unit))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // 水平分离
        // ------------------------------------------------------------------
        private static bool TryFindPush(List<Transform> unit, out Vector3 push)
        {
            push = Vector3.zero;

            Vector3 best = Vector3.zero;
            float bestDistance = 0f;

            foreach (Transform member in unit)
            {
                foreach (Collider own in member.GetComponentsInChildren<Collider>(true))
                {
                    if (own == null || own.isTrigger)
                    {
                        continue;
                    }

                    Bounds ownBounds = own.bounds;
                    Collider[] overlaps = Physics.OverlapBox(ownBounds.center, ownBounds.extents,
                        Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

                    foreach (Collider other in overlaps)
                    {
                        if (other == null || other == own || other.isTrigger)
                        {
                            continue;
                        }

                        if (BelongsTo(other.transform, unit))
                        {
                            continue;
                        }

                        if (!IsObstacle(other))
                        {
                            continue;
                        }

                        if (!Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                                other, other.transform.position, other.transform.rotation,
                                out Vector3 direction, out float distance))
                        {
                            continue;
                        }

                        if (distance <= 0.005f)
                        {
                            continue;
                        }

                        direction.y = 0f;

                        if (direction.sqrMagnitude < 0.0001f)
                        {
                            continue;
                        }

                        direction.Normalize();

                        if (distance > bestDistance)
                        {
                            bestDistance = distance;
                            best = direction;
                        }
                    }
                }
            }

            if (bestDistance <= 0f)
            {
                return false;
            }

            push = best * (bestDistance + SeparationGap);
            return true;
        }

        /// <summary>只和建筑结构、门、柜子做分离；家具之间不互相推（椅子本来就该塞在桌子下）。</summary>
        private static bool IsObstacle(Collider collider)
        {
            string name = collider.gameObject.name;

            return name.Contains("Wall") || name.Contains("Jamb") || name.Contains("Door") ||
                   name.Contains("Container");
        }

        private static bool BelongsTo(Transform transform, List<Transform> unit)
        {
            foreach (Transform member in unit)
            {
                if (transform == member || transform.IsChildOf(member))
                {
                    return true;
                }
            }

            return false;
        }

        private static void MoveUnit(List<Transform> unit, Vector3 delta)
        {
            foreach (Transform member in unit)
            {
                member.position += delta;
            }
        }

        private static Bounds GetRendererBounds(Transform root)
        {
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            bool has = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

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

            return bounds;
        }

        private static bool TryGetUnitBounds(List<Transform> unit, out Bounds bounds)
        {
            bounds = new Bounds(unit[0].position, Vector3.zero);
            bool has = false;

            foreach (Transform member in unit)
            {
                foreach (Renderer renderer in member.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                    {
                        continue;
                    }

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
            }

            return has;
        }
    }
}
#endif
