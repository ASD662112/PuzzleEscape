using System.Collections.Generic;
using PuzzleEscape.Data;
using PuzzleEscape.Interaction;
using PuzzleEscape.Puzzle;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 两层室内庄园：一层书房/储藏室/展厅/配电间，二层档案室/实验室/卧室/观测室。
    /// 通过电梯连接楼层，最终在西侧封印大门集合四枚徽记通关。
    /// </summary>
    public static class MansionLevelBuilder
    {
        private const float UpperBaseY = 3.6f;
        private const float WallHeight = 3.3f;
        private const float MapHalfWidth = 22f;
        private const float MapHalfDepth = 9f;
        private const float ElevatorDoorX = 33.9f;
        private const float ElevatorPanelX = 33.72f;
        private const float ElevatorArrivalX = 31.8f;
        private const float FinalGateX = -21.9f;

        internal static Material floorMaterial;
        internal static Material wallMaterial;
        internal static Material trimMaterial;
        internal static Material woodMaterial;
        internal static Material woodDarkMaterial;
        internal static Material woodLightMaterial;
        internal static Material metalMaterial;
        internal static Material copperMaterial;
        internal static Material paperMaterial;
        internal static Material goldMaterial;
        internal static Material rugMaterial;
        internal static Material fabricMaterial;
        internal static Material leatherMaterial;
        internal static Material porcelainMaterial;
        internal static Material marbleMaterial;
        internal static Material stoneMaterial;
        internal static Material lampShadeMaterial;
        internal static Material tableclothMaterial;
        internal static Material glassMaterial;
        internal static Material clockFaceMaterial;
        internal static Material flameMaterial;
        internal static Material waxMaterial;
        internal static Material[] bookMaterials;
        internal static Material[] paintingMaterials;

        private static readonly string[] RandomContainerLoot =
        {
            ItemIds.SilverCandlestick,
            ItemIds.PocketWatch,
            ItemIds.VinylRecord,
            ItemIds.OldDiary,
            ItemIds.PorcelainVase,
            ItemIds.BrassCompass,
            ItemIds.OldCoin,
            ItemIds.Candle,
            ItemIds.ClothScrap,
            ItemIds.EmptyBottle
        };

        public static void Build()
        {
            ItemSpawnDirector.ResetRegistry();
            CreateMaterials();
            ConfigureRendering();

            var root = new GameObject("Environment").transform;

            BuildGroundFloor(root);
            BuildEastWing(root);
            BuildSouthWing(root);
            BuildUpperFloor(root);
            BuildExtraRoomContainers(root);
            BuildBasement(root);
            ReplaceCratesWithRealModels(root);
            ReplaceDecorativeProps(root);
            AddDoorsToDoorways(root);
            BuildLighting(root);

            // 吊灯是在 BuildLighting 里才生成的，灯具替换必须放在灯光之后。
            ReplaceLampVisuals(root);
        }

        /// <summary>
        /// 为所有门框补上可推开的实木房门（凸面板 + 真实木纹/黄铜材质）。
        /// 已存在铰链门的门洞（档案室密码门）会自动跳过，避免重复。
        /// </summary>
        public static void AddDoorsToDoorways(Transform root)
        {
            var leftTrims = new List<Transform>();
            var rightTrims = new List<Transform>();

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("DoorTrimLeft"))
                {
                    leftTrims.Add(t);
                }
                else if (t.name.Contains("DoorTrimRight"))
                {
                    rightTrims.Add(t);
                }
            }

            var existingDoors = root.GetComponentsInChildren<HingedDoor>(true);
            int built = 0;
            int existingCount = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("RoomDoor_"))
                {
                    existingCount++;
                }
            }

            foreach (Transform left in leftTrims)
            {
                Vector3 leftSize = left.lossyScale;
                bool alongX = leftSize.x <= leftSize.z;

                Transform right = null;
                float best = float.MaxValue;

                foreach (Transform candidate in rightTrims)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    // 同一 x/z 位置上下楼层会有重叠门框，必须限定在同一楼层，
                    // 否则地下室/二层门会配到一层门框，产生 0.6m 以上的门缝。
                    if (Mathf.Abs(candidate.position.y - left.position.y) > 0.12f)
                    {
                        continue;
                    }

                    Vector3 a = left.position;
                    Vector3 b = candidate.position;

                    if (alongX)
                    {
                        if (b.x <= a.x || Mathf.Abs(b.z - a.z) > 0.05f)
                        {
                            continue;
                        }

                        float distance = b.x - a.x;
                        if (distance < best && distance < 2.2f)
                        {
                            best = distance;
                            right = candidate;
                        }
                    }
                    else
                    {
                        if (b.z <= a.z || Mathf.Abs(b.x - a.x) > 0.05f)
                        {
                            continue;
                        }

                        float distance = b.z - a.z;
                        if (distance < best && distance < 2.2f)
                        {
                            best = distance;
                            right = candidate;
                        }
                    }
                }

                if (right == null)
                {
                    continue;
                }

                float floorY = left.position.y - leftSize.y * 0.5f + 0.03f;
                Vector3 center = (left.position + right.position) * 0.5f;
                center.y = floorY;

                bool occupied = false;
                foreach (HingedDoor door in existingDoors)
                {
                    if (door == null)
                    {
                        continue;
                    }

                    Vector3 doorPosition = door.transform.position;
                    // 只跳过同一楼层、同一门洞上已有的门，避免误伤上下层重叠的门洞。
                    if (Mathf.Abs(doorPosition.y - floorY) > 2f)
                    {
                        continue;
                    }

                    Vector3 offset = doorPosition - center;
                    offset.y = 0f;
                    if (offset.sqrMagnitude < 2.25f)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (occupied)
                {
                    continue;
                }

                // 沿 X 的门洞看 trim 的 x 厚度；沿 Z 的门洞必须看 z 厚度。
                // 之前统一用 leftSize.x，沿 Z 的门会窄 12cm，留下约 15cm 门缝。
                float trimThickness = alongX ? leftSize.x : leftSize.z;
                float clearWidth = best - trimThickness;
                float doorWidth = Mathf.Max(0.9f, clearWidth - 0.06f);
                // 门框上档下沿约在 2.38m；门高 2.32 会留下 6cm 顶部缝隙，
                // 改为 2.36，仅保留 2cm 正常安装余量。
                float doorHeight = 2.36f;

                Vector3 pivotPosition = center;
                if (alongX)
                {
                    pivotPosition.x = left.position.x + leftSize.x * 0.5f + 0.03f;
                }
                else
                {
                    pivotPosition.z = left.position.z + leftSize.z * 0.5f + 0.03f;
                }

                BuildRoomDoor(root, pivotPosition, alongX, doorWidth, doorHeight, existingCount + built);
                built++;
            }

            Debug.Log("[PuzzleEscape] 门框补门完成：" + built + " 扇。");
        }

        private static void BuildRoomDoor(Transform root, Vector3 pivotPosition, bool alongX,
            float width, float height, int index)
        {
            var pivot = new GameObject("RoomDoor_" + index);
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = pivotPosition;
            // 门宽方向统一在本地 +X：沿 Z 的门洞把铰链整体转 90°。
            pivot.transform.localRotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f);

            // 场景烘焙后静态材质字段可能为空（编辑器重开），按名称回退到场景中已有的材质。
            Material slabMaterial = ResolveSceneMaterial(woodDarkMaterial, "MansionWoodDark");
            Material panelMaterial = ResolveSceneMaterial(woodMaterial, "MansionWood");
            Material brassMaterial = ResolveSceneMaterial(goldMaterial, "MansionGold");
            Material hingeMaterial = ResolveSceneMaterial(metalMaterial, "MansionMetal");

            Box(pivot.transform, "DoorLeaf", new Vector3(width * 0.5f, height * 0.5f, 0f),
                new Vector3(width, height, 0.06f), slabMaterial, true);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = -1; row <= 1; row += 2)
                {
                    for (int column = -1; column <= 1; column += 2)
                    {
                        float panelWidth = width * 0.36f;
                        float panelHeight = height * 0.3f;
                        float panelX = width * 0.5f + column * (panelWidth * 0.5f + 0.03f);
                        float panelY = height * 0.5f + row * (panelHeight * 0.5f + 0.06f);

                        // 凸面板略嵌入平面 2 mm，避免与门板共面闪烁。
                        Box(pivot.transform, "DoorPanel", new Vector3(panelX, panelY, side * 0.038f),
                            new Vector3(panelWidth, panelHeight, 0.02f), panelMaterial, false);
                    }
                }
            }

            float handleX = width - 0.16f;
            float handleY = 1.02f;

            for (int side = -1; side <= 1; side += 2)
            {
                Box(pivot.transform, "DoorHandlePlate", new Vector3(handleX, handleY, side * 0.037f),
                    new Vector3(0.05f, 0.22f, 0.014f), brassMaterial, false);
                Cylinder(pivot.transform, "DoorHandleKnob", new Vector3(handleX, handleY, side * 0.062f),
                    new Vector3(0.055f, 0.026f, 0.055f), Quaternion.Euler(90f, 0f, 0f), brassMaterial, false);
            }

            for (int i = 0; i < 3; i++)
            {
                float hingeY = 0.42f + i * 0.75f;
                Cylinder(pivot.transform, "DoorHinge", new Vector3(0.02f, hingeY, 0f),
                    new Vector3(0.034f, 0.05f, 0.034f), Quaternion.identity, hingeMaterial, false);
            }

            var hinge = pivot.AddComponent<HingedDoor>();
            hinge.Pivot = pivot.transform;
            hinge.OpenAngle = -100f;
            hinge.Duration = 0.8f;

            var interactable = pivot.AddComponent<SimpleDoorInteractable>();
            interactable.Door = hinge;
        }

        private static Material ResolveSceneMaterial(Material material, string materialName)
        {
            if (material != null)
            {
                return material;
            }

            foreach (Material candidate in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (candidate != null && candidate.name == materialName)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 批量把剩余的装饰性程序化摆件（植物、花瓶、边桌、梳妆台、凳子、长凳、工具车、木桶）
        /// 替换为真实 CC0 模型。
        /// </summary>
        private static void ReplaceDecorativeProps(Transform root)
        {
#if UNITY_EDITOR
            ReplacePropRoots(root, "PottedPlant", new[] { "potted_plant_04" }, true, false);
            ReplacePropRoots(root, "FlowerVase", new[] { "ceramic_vase_02", "brass_vase_02" }, true, false);
            ReplacePropRoots(root, "ShelfVase", new[] { "ceramic_vase_02" }, true, false);
            ReplacePropRoots(root, "SideTable", new[] { "side_table_01" }, false, true);
            ReplacePropRoots(root, "Dresser", new[] { "GothicCommode_01" }, false, true);
            ReplacePropRoots(root, "BarStool", new[] { "wooden_stool_01" }, false, true);
            ReplacePropRoots(root, "Bench", new[] { "painted_wooden_bench" }, false, true);
            ReplacePropRoots(root, "ToolCart", new[] { "tool_cart" }, false, true);
            ReplacePropRoots(root, "Mirror", new[] { "ornate_mirror_01" }, false, false);
            ReplacePropRoots(root, "Sculpture", new[] { "marble_bust_01", "gothic_statue" }, true, true);
            ReplacePropRoots(root, "GrandfatherClock", new[] { "vintage_grandfather_clock_01" }, false, true);
            ReplaceBarrels(root);
#endif
        }

        /// <summary>
        /// 把剩下的程序化灯具替换成真实扫描模型：
        /// 吊灯/落地灯的灯罩换成铁艺灯笼，壁灯换成黄铜壁灯，台灯换成维多利亚油灯。
        /// 灯光对象会被保留在原位，避免房间变暗。
        /// </summary>
        private static void ReplaceLampVisuals(Transform root)
        {
            ReplaceLanternShades(root, "PendantLamp",
                new[] { "Shade", "ShadeTopRing", "ShadeBottomRing", "Bulb" }, 0.3f);
            ReplaceLanternShades(root, "FloorLamp",
                new[] { "LampShade", "LampShadeTopRing", "LampShadeBottomRing", "LampBulb", "LampFinial" }, 0.34f);
            ReplaceLampRoots(root, "WallSconce", "industrial_wall_sconce", true);
            ReplaceLampRoots(root, "TableLamp", "vintage_oil_lamp", false);
        }

        /// <summary>保留灯杆与吊线，只把灯罩部分换成真实灯笼模型。</summary>
        private static void ReplaceLanternShades(Transform root, string rootName, string[] shadeParts,
            float height)
        {
#if UNITY_EDITOR
            if (!MansionRealModels.Has("Lantern_01"))
            {
                return;
            }

            var lamps = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == rootName)
                {
                    lamps.Add(t);
                }
            }

            int replaced = 0;
            foreach (Transform lamp in lamps)
            {
                Transform shade = lamp.Find(shadeParts[0]);
                Renderer shadeRenderer = shade != null ? shade.GetComponent<Renderer>() : null;
                if (shadeRenderer == null)
                {
                    continue;
                }

                Bounds shadeBounds = shadeRenderer.bounds;
                Vector3 anchor = new Vector3(shadeBounds.center.x, shadeBounds.max.y, shadeBounds.center.z);

                foreach (string part in shadeParts)
                {
                    Transform child = lamp.Find(part);
                    if (child != null)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                GameObject lantern = MansionRealModels.Spawn(lamp, "Lantern_01",
                    new Vector3(anchor.x, anchor.y - height, anchor.z),
                    lamp.eulerAngles.y, height, false, 0f, anchor.y);
                if (lantern != null)
                {
                    replaced++;
                }
            }

            if (replaced > 0)
            {
                Debug.Log($"[PuzzleEscape] 灯笼替换：{rootName} {replaced} 个。");
            }
#endif
        }

        /// <summary>整体替换灯具主体，同时把灯光对象移到父节点保留照明。</summary>
        private static void ReplaceLampRoots(Transform root, string rootName, string modelId, bool topAligned)
        {
#if UNITY_EDITOR
            if (!MansionRealModels.Has(modelId))
            {
                return;
            }

            var lamps = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == rootName)
                {
                    lamps.Add(t);
                }
            }

            int replaced = 0;
            foreach (Transform lamp in lamps)
            {
                var renderers = lamp.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    continue;
                }

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                foreach (Light light in lamp.GetComponentsInChildren<Light>(true))
                {
                    light.transform.SetParent(lamp.parent, true);
                }

                Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                GameObject realLamp = MansionRealModels.Spawn(lamp.parent, modelId, bottom,
                    lamp.eulerAngles.y, bounds.size.y, false, 0f,
                    topAligned ? bounds.max.y : (float?)null, false, bounds.size, null, true);

                if (realLamp != null)
                {
                    replaced++;
                    Object.DestroyImmediate(lamp.gameObject);
                }
            }

            if (replaced > 0)
            {
                Debug.Log($"[PuzzleEscape] {rootName}: 已替换 {replaced} 个真实灯具。");
            }
#endif
        }

        private static void ReplacePropRoots(Transform root, string rootName, string[] modelIds,
            bool fitInside, bool addCollider)
        {
#if UNITY_EDITOR
            if (modelIds == null || modelIds.Length == 0 || !MansionRealModels.Has(modelIds[0]))
            {
                return;
            }

            var props = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == rootName)
                {
                    props.Add(t);
                }
            }

            if (props.Count == 0)
            {
                return;
            }

            int replaced = 0;
            int index = 0;
            foreach (Transform prop in props)
            {
                if (prop == null)
                {
                    continue;
                }

                // 带道具刷新点/可开启容器的根节点保持原样，避免破坏交互。
                if (prop.GetComponentInChildren<ItemSpawnPoint>(true) != null ||
                    prop.GetComponentInChildren<OpenableContainer>(true) != null)
                {
                    continue;
                }

                var renderers = prop.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    continue;
                }

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                if (bounds.size.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                Transform parent = prop.parent;
                Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                float yaw = prop.eulerAngles.y;
                string modelId = modelIds[index % modelIds.Length];
                index++;

                GameObject realProp = MansionRealModels.Spawn(parent, modelId, bottom, yaw,
                    bounds.size.y, addCollider, 0f, null, false, bounds.size, null, fitInside);
                if (realProp != null)
                {
                    replaced++;
                    Object.DestroyImmediate(prop.gameObject);
                }
            }

            if (replaced > 0)
            {
                Debug.Log($"[PuzzleEscape] {rootName}: 已替换 {replaced} 个真实模型。");
            }
#endif
        }

        private static void ReplaceBarrels(Transform root)
        {
#if UNITY_EDITOR
            if (!MansionRealModels.Has("Barrel_02"))
            {
                return;
            }

            var bodies = new List<Transform>();
            var details = new List<GameObject>();

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "BarrelBand" || t.name == "BarrelTopRim" ||
                    t.name == "BarrelBottomRim" || t.name == "BarrelLid" || t.name == "BarrelBung")
                {
                    details.Add(t.gameObject);
                    continue;
                }

                if (t.name.Contains("Barrel") && t.GetComponent<MeshFilter>() != null)
                {
                    bodies.Add(t);
                }
            }

            int replaced = 0;
            foreach (Transform body in bodies)
            {
                if (body == null)
                {
                    continue;
                }

                var renderer = body.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Transform parent = body.parent;
                Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                float yaw = body.eulerAngles.y;

                GameObject realBarrel = MansionRealModels.Spawn(parent, "Barrel_02", bottom, yaw,
                    bounds.size.y, true, 0f, null, false, bounds.size, null, true);
                if (realBarrel != null)
                {
                    replaced++;
                    Object.DestroyImmediate(body.gameObject);
                }
            }

            foreach (GameObject detail in details)
            {
                if (detail != null)
                {
                    Object.DestroyImmediate(detail);
                }
            }

            if (replaced > 0)
            {
                Debug.Log($"[PuzzleEscape] 木桶: 已替换 {replaced} 个真实模型。");
            }
#endif
        }

        /// <summary>
        /// 把全图的程序化木箱替换成真实木箱模型。
        /// 程序化箱体与箱盖/木条/标签是分开的 GameObject，替换时一并清理。
        /// </summary>
        private static void ReplaceCratesWithRealModels(Transform root)
        {
#if UNITY_EDITOR
            if (!MansionRealModels.Has("wooden_crate_01"))
            {
                return;
            }

            var bodies = new List<Transform>();
            var details = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (IsCrateDetailName(t.name))
                {
                    details.Add(t.gameObject);
                    continue;
                }

                if (!t.name.Contains("Crate"))
                {
                    continue;
                }

                if (t.GetComponent<MeshFilter>() == null || t.GetComponent<BoxCollider>() == null)
                {
                    continue;
                }

                if (t.GetComponent<OpenableContainer>() != null || t.GetComponent<ItemSpawnPoint>() != null)
                {
                    continue;
                }

                if (t.lossyScale.y < 0.35f)
                {
                    continue;
                }

                bodies.Add(t);
            }

            int replaced = 0;
            foreach (Transform body in bodies)
            {
                if (body == null)
                {
                    continue;
                }

                Vector3 size = body.lossyScale;
                Vector3 bottom = body.TransformPoint(new Vector3(0f, -0.5f, 0f));
                float yaw = body.eulerAngles.y;
                Transform parent = body.parent;

                string modelId = (size.x >= 1.0f || size.z >= 1.0f) &&
                                 MansionRealModels.Has("wooden_crate_02")
                    ? "wooden_crate_02"
                    : "wooden_crate_01";

                GameObject realCrate = MansionRealModels.Spawn(parent, modelId, bottom, yaw,
                    size.y, true, 0f, null, false, size);
                if (realCrate != null)
                {
                    replaced++;
                }

                Object.DestroyImmediate(body.gameObject);
            }

            foreach (GameObject detail in details)
            {
                if (detail != null)
                {
                    Object.DestroyImmediate(detail);
                }
            }

            Debug.Log($"[PuzzleEscape] 已用真实模型替换 {replaced} 个木箱。");
#endif
        }

        private static bool IsCrateDetailName(string objectName)
        {
            return objectName == "CratePost" ||
                   objectName == "CrateFrontSlat" ||
                   objectName == "CrateBackSlat" ||
                   objectName == "CrateSideSlat" ||
                   objectName == "CrateLid" ||
                   objectName == "CrateLidPlank" ||
                   objectName == "CrateBrace" ||
                   objectName == "CrateLabel";
        }

        private static void BuildGroundFloor(Transform root)
        {
            BuildShell(root, 0f, false, true, true);
            BuildCorridorWalls(root, 0f);
            BuildElevator(root, 0f, true);

            BuildStudy(root);
            BuildStorage(root);
            BuildGallery(root);
            BuildUtility(root);
            BuildDiningRoom(root);
            BuildKitchen(root);
            BuildSalon(root);
            BuildFoyer(root);
            BuildWestAnnex(root);
        }

        private static void BuildUpperFloor(Transform root)
        {
            BuildShell(root, UpperBaseY, true, false, true);
            BuildCorridorWalls(root, UpperBaseY);
            BuildUpperEastLobby(root);
            BuildElevator(root, UpperBaseY, false);

            KeypadPuzzle archiveKeypad = BuildArchive(root);
            PowerLever breaker = Object.FindObjectOfType<PowerLever>();
            if (breaker != null)
            {
                breaker.Keypad = archiveKeypad;
            }

            BuildLab(root);
            BuildBedroom(root);
            BuildObservatory(root);
            BuildBallroom(root);
            BuildServantsRoom(root);
            BuildMusicRoom(root);
            BuildGuestRoom(root);
            BuildFinalGate(root);
        }

        private static void BuildShell(Transform root, float baseY, bool gateOpening, bool corridorOpening,
            bool eastCorridorOpening)
        {
            float wallCenterY = baseY + WallHeight * 0.5f;
            float width = MapHalfWidth * 2f;
            float depth = MapHalfDepth * 2f;

            Box(root, "Floor", new Vector3(0f, baseY - 0.1f, 0f), new Vector3(width, 0.2f, depth),
                floorMaterial, true);
            Box(root, "Ceiling", new Vector3(0f, baseY + WallHeight, 0f), new Vector3(width, 0.2f, depth),
                wallMaterial, true);

            if (eastCorridorOpening)
            {
                Box(root, "WallEastNorth", new Vector3(MapHalfWidth + 0.05f, wallCenterY, 5.5f),
                    new Vector3(0.1f, WallHeight, 7f), wallMaterial, true);
                Box(root, "WallEastSouth", new Vector3(MapHalfWidth + 0.05f, wallCenterY, -5.5f),
                    new Vector3(0.1f, WallHeight, 7f), wallMaterial, true);
                Box(root, "WallEastLintel", new Vector3(MapHalfWidth + 0.05f, baseY + 2.85f, 0f),
                    new Vector3(0.1f, 0.9f, 4f), wallMaterial, true);
            }
            else
            {
                Box(root, "WallEast", new Vector3(MapHalfWidth + 0.05f, wallCenterY, 0f),
                    new Vector3(0.1f, WallHeight, depth), wallMaterial, true);
            }

            Box(root, "WallNorth", new Vector3(0f, wallCenterY, MapHalfDepth + 0.05f),
                new Vector3(width + 0.1f, WallHeight, 0.1f), wallMaterial, true);
            Box(root, "WallSouth", new Vector3(0f, wallCenterY, -MapHalfDepth - 0.05f),
                new Vector3(width + 0.1f, WallHeight, 0.1f), wallMaterial, true);

            if (gateOpening)
            {
                Box(root, "WallWestNorth", new Vector3(-MapHalfWidth - 0.05f, wallCenterY, 5.25f),
                    new Vector3(0.1f, WallHeight, 7.5f), wallMaterial, true);
                Box(root, "WallWestSouth", new Vector3(-MapHalfWidth - 0.05f, wallCenterY, -5.25f),
                    new Vector3(0.1f, WallHeight, 7.5f), wallMaterial, true);
                Box(root, "WallWestGateLintel", new Vector3(-MapHalfWidth - 0.05f, baseY + 2.95f, 0f),
                    new Vector3(0.1f, 0.7f, 3f), wallMaterial, true);
            }
            else if (corridorOpening)
            {
                Box(root, "WallWestNorth", new Vector3(-MapHalfWidth - 0.05f, wallCenterY, 5.5f),
                    new Vector3(0.1f, WallHeight, 7f), wallMaterial, true);
                Box(root, "WallWestSouth", new Vector3(-MapHalfWidth - 0.05f, wallCenterY, -5.5f),
                    new Vector3(0.1f, WallHeight, 7f), wallMaterial, true);
            }
            else
            {
                Box(root, "WallWest", new Vector3(-MapHalfWidth - 0.05f, wallCenterY, 0f),
                    new Vector3(0.1f, WallHeight, depth), wallMaterial, true);
            }

            foreach (float dividerX in new[] { -14f, 0f, 14f })
            {
                Box(root, "RoomDividerNorth", new Vector3(dividerX, wallCenterY, 5.5f),
                    new Vector3(0.12f, WallHeight, 7f), wallMaterial, true);
                Box(root, "RoomDividerSouth", new Vector3(dividerX, wallCenterY, -5.5f),
                    new Vector3(0.12f, WallHeight, 7f), wallMaterial, true);
            }

            Box(root, "BaseboardNorth", new Vector3(0f, baseY + 0.08f, MapHalfDepth - 0.06f),
                new Vector3(width, 0.16f, 0.06f), woodDarkMaterial, false);
            Box(root, "BaseboardSouth", new Vector3(0f, baseY + 0.08f, -MapHalfDepth + 0.06f),
                new Vector3(width, 0.16f, 0.06f), woodDarkMaterial, false);
        }

        private static void BuildCorridorWalls(Transform root, float baseY)
        {
            BuildCorridorWall(root, baseY, 2f);
            BuildCorridorWall(root, baseY, -2f);
        }

        private static void BuildCorridorWall(Transform root, float baseY, float z)
        {
            float wallCenterY = baseY + WallHeight * 0.5f;
            float[] doorXs = { -18f, -9f, 9f, 18f };
            float cursor = -MapHalfWidth;

            foreach (float doorX in doorXs)
            {
                float wallEnd = doorX - 0.8f;
                if (wallEnd > cursor)
                {
                    Box(root, "CorridorWall", new Vector3((cursor + wallEnd) * 0.5f, wallCenterY, z),
                        new Vector3(wallEnd - cursor, WallHeight, 0.12f), wallMaterial, true);
                }

                Box(root, "DoorLintel", new Vector3(doorX, baseY + 2.85f, z),
                    new Vector3(1.6f, 0.9f, 0.12f), wallMaterial, true);

                // 门框立边向门洞内侧收 2 cm，并向下埋入地面 3 cm：
                // 避免外侧面与墙体断面、底面与地面共面造成闪烁。
                Box(root, "DoorTrimLeft", new Vector3(doorX - 0.82f, baseY + 1.19f, z),
                    new Vector3(0.08f, 2.44f, 0.2f), woodDarkMaterial, false);
                Box(root, "DoorTrimRight", new Vector3(doorX + 0.82f, baseY + 1.19f, z),
                    new Vector3(0.08f, 2.44f, 0.2f), woodDarkMaterial, false);
                Box(root, "DoorTrimTop", new Vector3(doorX, baseY + 2.42f, z),
                    new Vector3(1.76f, 0.08f, 0.2f), woodDarkMaterial, false);

                cursor = doorX + 0.8f;
            }

            if (MapHalfWidth > cursor)
            {
                Box(root, "CorridorWall", new Vector3((cursor + MapHalfWidth) * 0.5f, wallCenterY, z),
                    new Vector3(MapHalfWidth - cursor, WallHeight, 0.12f), wallMaterial, true);
            }

            foreach (float sconceX in new[] { -14f, -5f, 5f, 14f })
            {
                float sconceZ = z > 0f ? z - 0.15f : z + 0.15f;
                float sconceYaw = z > 0f ? 180f : 0f;
                MansionProps.AddWallSconce(root, new Vector3(sconceX, baseY + 1.9f, sconceZ), sconceYaw);
            }

            if (z > 0f)
            {
                MansionProps.AddConsoleTable(root, new Vector3(-13f, baseY, 1.63f), 0f, 1.4f, 0.35f);
                MansionProps.AddVase(root, new Vector3(-13f, baseY + 0.84f, 1.63f), 0);
                MansionProps.AddPlant(root, new Vector3(-21f, baseY, 1.3f), 0.7f);
                MansionProps.AddPainting(root, new Vector3(-12f, baseY + 1.8f, 1.93f), 180f, 1f, 0.7f, 1);
                Box(root, "CorridorRunner", new Vector3(0f, baseY + 0.02f, 0f),
                    new Vector3(36f, 0.012f, 1.2f), rugMaterial, false);
                MansionProps.AddRugBorder(root, new Vector3(0f, baseY + 0.02f, 0f), 36f, 1.2f);
            }
            else
            {
                MansionProps.AddConsoleTable(root, new Vector3(13f, baseY, -1.63f), 0f, 1.4f, 0.35f);
                MansionProps.AddVase(root, new Vector3(13f, baseY + 0.84f, -1.63f), 2);
                MansionProps.AddPlant(root, new Vector3(21f, baseY, -1.3f), 0.7f);
                MansionProps.AddPainting(root, new Vector3(12f, baseY + 1.8f, -1.93f), 0f, 1f, 0.7f, 3);
            }
        }

        private static void BuildElevator(Transform root, float baseY, bool goingUp)
        {
            Box(root, "ElevatorDoorLeft", new Vector3(ElevatorDoorX, baseY + 1.3f, -0.58f),
                new Vector3(0.06f, 2.5f, 0.95f), metalMaterial, false);
            Box(root, "ElevatorDoorRight", new Vector3(ElevatorDoorX, baseY + 1.3f, 0.58f),
                new Vector3(0.06f, 2.5f, 0.95f), metalMaterial, false);
            Box(root, "ElevatorFrame", new Vector3(ElevatorDoorX, baseY + 1.3f, 0f),
                new Vector3(0.08f, 2.7f, 2.4f), woodDarkMaterial, false);
            Box(root, "ElevatorDoorInset", new Vector3(ElevatorDoorX - 0.04f, baseY + 1.3f, 0f),
                new Vector3(0.03f, 2.35f, 2.1f), glassMaterial, false);

            var panelRoot = new GameObject(goingUp ? "ElevatorPanelUpper" : "ElevatorPanelLower");
            panelRoot.transform.SetParent(root, false);
            panelRoot.transform.localPosition = new Vector3(ElevatorPanelX, baseY + 1.35f, -1.25f);

            var collider = panelRoot.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.22f, 0.8f, 0.62f);

            Box(panelRoot.transform, "Panel", Vector3.zero, new Vector3(0.05f, 0.5f, 0.4f),
                metalMaterial, false);
            Box(panelRoot.transform, "UpButton", new Vector3(-0.035f, 0.1f, 0f),
                new Vector3(0.02f, 0.1f, 0.1f), CreateEmissive("ElevatorButtonUp", new Color(0.5f, 0.9f, 0.7f), 0.8f),
                false);
            Box(panelRoot.transform, "DownButton", new Vector3(-0.035f, -0.08f, 0f),
                new Vector3(0.02f, 0.1f, 0.1f), CreateEmissive("ElevatorButtonDown", new Color(0.95f, 0.65f, 0.3f), 0.8f),
                false);

            var elevator = panelRoot.AddComponent<ElevatorInteractable>();
            elevator.GoingUp = goingUp;
            elevator.DisplayName = "电梯";
            elevator.Destination = goingUp
                ? new Vector3(ElevatorArrivalX, UpperBaseY + 0.05f, 0f)
                : new Vector3(ElevatorArrivalX, 0.05f, 0f);
            elevator.DestinationYaw = 270f;
        }

        private static void BuildStudy(Transform root)
        {
            Box(root, "StudyRug", new Vector3(-9f, 0.012f, 5.6f), new Vector3(4.4f, 0.02f, 3f),
                rugMaterial, false);

            bool useRealDesk = false;
#if UNITY_EDITOR
            useRealDesk = MansionRealModels.Spawn(root, "wooden_table_02",
                root.TransformPoint(new Vector3(-10.8f, 0f, 6.1f)), 180f, 0.86f, true,
                0f, null, false, new Vector3(2.3f, 0.86f, 1.1f)) != null;
#endif
            if (!useRealDesk)
            {
                Box(root, "StudyDeskTop", new Vector3(-10.8f, 0.82f, 6.1f), new Vector3(2.3f, 0.08f, 1.1f),
                    woodMaterial, true);
                Box(root, "StudyDeskLegA", new Vector3(-11.8f, 0.39f, 6.55f), new Vector3(0.1f, 0.78f, 0.1f),
                    woodDarkMaterial, true);
                Box(root, "StudyDeskLegB", new Vector3(-9.8f, 0.39f, 6.55f), new Vector3(0.1f, 0.78f, 0.1f),
                    woodDarkMaterial, true);
                Box(root, "StudyDeskLegC", new Vector3(-11.8f, 0.39f, 5.65f), new Vector3(0.1f, 0.78f, 0.1f),
                    woodDarkMaterial, true);
                Box(root, "StudyDeskLegD", new Vector3(-9.8f, 0.39f, 5.65f), new Vector3(0.1f, 0.78f, 0.1f),
                    woodDarkMaterial, true);
            }

            CreateNote(root, "StudyNote", new Vector3(-10.6f, 0.88f, 6.0f), "书房便签",
                "备用钥匙被我放在了书房书桌的左前方。\n挂钟停下的时刻，就是二层档案室的密码。",
                "阅读书房便签", "desk", 8f);

            BuildBookshelf(root, new Vector3(-13.6f, 0f, 5.1f));
            CreateItemSpawnPoint(root, "Spawn_BrassKey", ItemIds.BrassKey, new Vector3(-11.6f, 0.87f, 5.75f), true);

            BuildClock(root, new Vector3(-9.2f, 2.12f, 8.85f));

            Box(root, "StudyPaintingFrame", new Vector3(-13.9f, 2f, 7.7f), new Vector3(0.05f, 1.1f, 1.5f),
                goldMaterial, false);
            Box(root, "StudyPaintingMat", new Vector3(-13.878f, 2f, 7.7f), new Vector3(0.02f, 0.96f, 1.34f),
                paperMaterial, false);
            Box(root, "StudyPainting", new Vector3(-13.86f, 2f, 7.7f), new Vector3(0.015f, 0.84f, 1.2f),
                paintingMaterials[1], false);

            // 书架与书桌旁的书堆
            Box(root, "StudyBookStack", new Vector3(-10.5f, 0.88f, 6.45f), new Vector3(0.4f, 0.16f, 0.3f),
                bookMaterials[1], false);

            MansionProps.AddDeskDetails(root, new Vector3(-10.8f, 0f, 6.1f), 180f, 2.3f);
            MansionProps.AddChair(root, new Vector3(-10.8f, 0f, 6.95f), 180f);
            MansionProps.AddRugBorder(root, new Vector3(-9f, 0.012f, 5.6f), 4.4f, 3f);
            MansionProps.AddPlant(root, new Vector3(-13f, 0f, 3f), 0.9f);
            MansionProps.AddChair(root, new Vector3(-3.6f, 0f, 6.9f), 270f, true);
            MansionProps.AddSideTable(root, new Vector3(-2.3f, 0f, 5.8f));
            MansionProps.AddFloorLamp(root, new Vector3(-2.2f, 0f, 3.2f));
            MansionProps.AddConsoleTable(root, new Vector3(-4.5f, 0f, 2.35f), 0f, 2f);
            MansionProps.AddVase(root, new Vector3(-4.5f, 0.84f, 2.35f), 1);
            MansionProps.AddBookStack(root, new Vector3(-2.3f, 0.74f, 5.8f), 4);
            MansionProps.AddPainting(root, new Vector3(-0.12f, 1.8f, 5f), -90f, 1.4f, 0.9f, 2);
        }

        private static void BuildBookshelf(Transform root, Vector3 origin)
        {
#if UNITY_EDITOR
            GameObject realShelf = MansionRealModels.Spawn(root, "wooden_bookshelf_worn",
                root.TransformPoint(origin), 90f, 3.0f, true, 0f, null, false,
                new Vector3(2.6f, 3.0f, 0.52f));
            if (realShelf != null)
            {
                MansionProps.AddRealShelfBooks(root, origin, 90f, 2.6f, 3.0f, 0.52f, 3, 3, 3);
                return;
            }
#endif

            Box(root, "ShelfBack", origin + new Vector3(-0.3f, 1.5f, 0f), new Vector3(0.06f, 3f, 2.6f),
                woodDarkMaterial, true);
            Box(root, "ShelfSideA", origin + new Vector3(0f, 1.5f, -1.3f), new Vector3(0.5f, 3f, 0.08f),
                woodDarkMaterial, true);
            Box(root, "ShelfSideB", origin + new Vector3(0f, 1.5f, 1.3f), new Vector3(0.5f, 3f, 0.08f),
                woodDarkMaterial, true);

            foreach (float height in new[] { 0.55f, 1.2f, 1.85f, 2.5f })
            {
                Box(root, "ShelfBoard", origin + new Vector3(0f, height, 0f), new Vector3(0.52f, 0.06f, 2.55f),
                    woodMaterial, true);
            }

            for (int i = 0; i < 12; i++)
            {
                float height = 0.18f + (i % 4) * 0.035f;
                float z = -1.1f + i * 0.16f;
                Box(root, "Book", origin + new Vector3(0.06f, 0.6f + height * 0.5f, z),
                    new Vector3(0.26f, height, 0.1f), bookMaterials[i % bookMaterials.Length], false,
                    Quaternion.Euler(0f, 0f, (i % 2 == 0) ? 0f : 4f));
            }
        }

        private static void BuildClock(Transform root, Vector3 position)
        {
            var clockRoot = new GameObject("StudyClock");
            clockRoot.transform.SetParent(root, false);
            clockRoot.transform.localPosition = position;

            var collider = clockRoot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.06f, 1.06f, 0.18f);

            Cylinder(clockRoot.transform, "ClockFrame", Vector3.zero, new Vector3(1f, 0.03f, 1f),
                Quaternion.Euler(90f, 0f, 0f), woodDarkMaterial);
            Cylinder(clockRoot.transform, "ClockFace", new Vector3(0f, 0f, -0.025f),
                new Vector3(0.86f, 0.02f, 0.86f), Quaternion.Euler(90f, 0f, 0f), clockFaceMaterial);

            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                float radians = angle * Mathf.Deg2Rad;
                Box(clockRoot.transform, "Tick", new Vector3(Mathf.Sin(radians) * 0.36f,
                    Mathf.Cos(radians) * 0.36f, -0.045f), new Vector3(0.016f, 0.045f, 0.01f),
                    woodDarkMaterial, false, Quaternion.Euler(0f, 0f, -angle));
            }

            var hourPivot = new GameObject("HourHand");
            hourPivot.transform.SetParent(clockRoot.transform, false);
            hourPivot.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            hourPivot.transform.localRotation = Quaternion.Euler(0f, 0f, -133.5f);
            Box(hourPivot.transform, "HourHandBar", new Vector3(0f, 0.13f, 0f),
                new Vector3(0.04f, 0.26f, 0.014f), woodDarkMaterial, false);

            var minutePivot = new GameObject("MinuteHand");
            minutePivot.transform.SetParent(clockRoot.transform, false);
            minutePivot.transform.localPosition = new Vector3(0f, 0f, -0.07f);
            minutePivot.transform.localRotation = Quaternion.Euler(0f, 0f, -162f);
            Box(minutePivot.transform, "MinuteHandBar", new Vector3(0f, 0.18f, 0f),
                new Vector3(0.028f, 0.36f, 0.012f), woodDarkMaterial, false);

            Sphere(clockRoot.transform, "ClockPin", new Vector3(0f, 0f, -0.075f),
                new Vector3(0.06f, 0.06f, 0.06f), goldMaterial);

            var clue = clockRoot.AddComponent<ClueInteractable>();
            clue.Title = "书房挂钟";
            clue.Body = "挂钟的指针停在 4:27。\n\n四位密码应当是 0427。";
            clue.Label = "查看书房挂钟";
            clue.ClueId = "clock";
        }

        private static void BuildStorage(Transform root)
        {
            Box(root, "StorageRug", new Vector3(-9f, 0.012f, -5.5f), new Vector3(4f, 0.02f, 2.8f),
                rugMaterial, false);

            Box(root, "StorageCabinetBody", new Vector3(-9f, 0.35f, -5.5f),
                new Vector3(1.7f, 0.7f, 1.1f), woodMaterial, true);
            Box(root, "StorageCabinetTop", new Vector3(-9f, 0.74f, -5.5f),
                new Vector3(1.8f, 0.08f, 1.2f), woodDarkMaterial, true);

            var drawerRoot = new GameObject("StorageDrawer");
            drawerRoot.transform.SetParent(root, false);
            drawerRoot.transform.localPosition = new Vector3(-9f, 0.53f, -4.96f);

            var drawerCollider = drawerRoot.AddComponent<BoxCollider>();
            drawerCollider.size = new Vector3(0.95f, 0.34f, 0.14f);

            Box(drawerRoot.transform, "DrawerFront", Vector3.zero, new Vector3(0.9f, 0.3f, 0.06f),
                woodDarkMaterial, false);
            Box(drawerRoot.transform, "DrawerKnob", new Vector3(0f, 0f, 0.05f),
                new Vector3(0.18f, 0.05f, 0.06f), goldMaterial, false);

            var rewardPoint = CreateItemSpawnPoint(drawerRoot.transform, "Spawn_Fuse", ItemIds.Fuse,
                new Vector3(0f, 0.14f, 0.08f), false);

            var drawer = drawerRoot.AddComponent<LockedDrawer>();
            drawer.DrawerRoot = drawerRoot.transform;
            drawer.OpenOffset = new Vector3(0f, 0f, 0.55f);
            drawer.RewardSpawnPoint = rewardPoint;
            drawer.NoteTitle = "抽屉里的便签";
            drawer.NoteBody =
                "「钟停在 4:27，那就是二层档案室的密码。」\n\n" +
                "保险丝留给你。去配电间合上电闸，电梯才会恢复。";

            CreateNote(root, "StorageNote", new Vector3(-11.5f, 0.88f, -4.4f), "储藏室清单",
                "货架上只剩下空箱子。\n抽屉锁着，钥匙在书房。",
                "阅读储藏室清单", "storage", 0f);
            Box(root, "StorageNoteTable", new Vector3(-11.5f, 0.4f, -4.4f),
                new Vector3(1.3f, 0.8f, 0.9f), woodDarkMaterial, true);

            Box(root, "StorageCrateA", new Vector3(-6.4f, 0.4f, -7.3f), new Vector3(1f, 0.8f, 1f),
                woodDarkMaterial, true);
            Box(root, "StorageCrateB", new Vector3(-5.2f, 0.25f, -6.8f), new Vector3(0.7f, 0.5f, 0.7f),
                woodDarkMaterial, true);
            Box(root, "StorageShelf", new Vector3(-12.5f, 1.4f, -7.6f), new Vector3(2.4f, 0.08f, 0.8f),
                woodMaterial, true);

            MansionProps.AddCrateDetails(root, new Vector3(-6.4f, 0.4f, -7.3f), new Vector3(1f, 0.8f, 1f));
            MansionProps.AddCrateDetails(root, new Vector3(-5.2f, 0.25f, -6.8f), new Vector3(0.7f, 0.5f, 0.7f));
            MansionProps.AddRugBorder(root, new Vector3(-9f, 0.012f, -5.5f), 4f, 2.8f);
            MansionProps.AddTableLamp(root, new Vector3(-11.1f, 0.8f, -4.4f), 0.7f);
            MansionProps.AddShelfUnit(root, new Vector3(-0.55f, 0f, -5.5f), 90f, 2.2f, 2.2f, 0.45f, 1);
            Box(root, "StorageExtraCrateA", new Vector3(-3.6f, 0.4f, -7.4f),
                new Vector3(1.1f, 0.8f, 1.1f), woodDarkMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(-3.6f, 0.4f, -7.4f), new Vector3(1.1f, 0.8f, 1.1f));
            Box(root, "StorageExtraCrateB", new Vector3(-2.6f, 0.3f, -4.2f),
                new Vector3(0.8f, 0.6f, 0.8f), woodMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(-2.6f, 0.3f, -4.2f), new Vector3(0.8f, 0.6f, 0.8f));
            Cylinder(root, "StorageBarrel", new Vector3(-3.2f, 0.45f, -8f),
                new Vector3(0.45f, 0.45f, 0.45f), Quaternion.Euler(0f, 0f, 90f), woodDarkMaterial, true);
            MansionProps.AddBarrelDetails(root, new Vector3(-3.2f, 0.45f, -8f));
            MansionProps.AddFloorLamp(root, new Vector3(-12.5f, 0f, -3f));
            MansionProps.AddConsoleTable(root, new Vector3(-11.5f, 0f, -8.4f), 0f, 1.8f);
            MansionProps.AddVase(root, new Vector3(-11.5f, 0.84f, -8.4f), 2);
            MansionProps.AddPainting(root, new Vector3(-13.87f, 1.8f, -4f), 90f, 1.4f, 0.9f, 4);
            Box(root, "StorageCentralCrateA", new Vector3(-9f, 0.4f, -6.8f),
                new Vector3(1.2f, 0.8f, 1.2f), woodDarkMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(-9f, 0.4f, -6.8f), new Vector3(1.2f, 0.8f, 1.2f));
            Box(root, "StorageCentralCrateB", new Vector3(-9f, 0.95f, -6.8f),
                new Vector3(0.85f, 0.6f, 0.85f), woodMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(-9f, 0.95f, -6.8f), new Vector3(0.85f, 0.6f, 0.85f));
            MansionProps.AddWallSign(root, new Vector3(-12.8f, 1.9f, -8.9f), 0f, 5);
        }

        private static void BuildGallery(Transform root)
        {
            Box(root, "GalleryRug", new Vector3(9f, 0.012f, 5.4f), new Vector3(5f, 0.02f, 3.2f),
                rugMaterial, false);

            Cylinder(root, "GalleryPedestalBase", new Vector3(9f, 0.4f, 5.4f), new Vector3(0.8f, 0.4f, 0.8f),
                Quaternion.identity, metalMaterial, true);
            Cylinder(root, "GalleryPedestalTop", new Vector3(9f, 0.82f, 5.4f), new Vector3(1.05f, 0.06f, 1.05f),
                Quaternion.identity, goldMaterial, false);

            CreateItemSpawnPoint(root, "Spawn_SealSun", ItemIds.SealSun, new Vector3(9f, 0.86f, 5.4f), true);

            Box(root, "GalleryBenchA", new Vector3(6.3f, 0.4f, 4.5f), new Vector3(1.8f, 0.12f, 0.55f),
                woodMaterial, true);
            Box(root, "GalleryBenchB", new Vector3(11.8f, 0.4f, 4.5f), new Vector3(1.8f, 0.12f, 0.55f),
                woodMaterial, true);

            Box(root, "GalleryPaintingFrame", new Vector3(13.9f, 2f, 5.5f), new Vector3(0.05f, 1.5f, 2.2f),
                goldMaterial, false);
            Box(root, "GalleryPaintingMat", new Vector3(13.878f, 2f, 5.5f), new Vector3(0.02f, 1.34f, 2.02f),
                paperMaterial, false);
            Box(root, "GalleryPainting", new Vector3(13.86f, 2f, 5.5f), new Vector3(0.015f, 1.18f, 1.86f),
                paintingMaterials[2], false);

            for (int i = -1; i <= 1; i += 2)
            {
                Box(root, "GalleryBenchLeg", new Vector3(6.3f + i * 0.75f, 0.18f, 4.5f),
                    new Vector3(0.08f, 0.36f, 0.08f), woodDarkMaterial);
                Box(root, "GalleryBenchLeg", new Vector3(11.8f + i * 0.75f, 0.18f, 4.5f),
                    new Vector3(0.08f, 0.36f, 0.08f), woodDarkMaterial);
            }

            Box(root, "GalleryBenchCushionA", new Vector3(6.3f, 0.49f, 4.5f),
                new Vector3(1.7f, 0.06f, 0.5f), rugMaterial);
            Box(root, "GalleryBenchCushionB", new Vector3(11.8f, 0.49f, 4.5f),
                new Vector3(1.7f, 0.06f, 0.5f), rugMaterial);
            MansionProps.AddRugBorder(root, new Vector3(9f, 0.012f, 5.4f), 5f, 3.2f);
            MansionProps.AddDisplayCase(root, new Vector3(12.3f, 0f, 6.7f), 0f, 1.2f);
            MansionProps.AddBench(root, new Vector3(8.5f, 0f, 7.8f), 0f, 2f);
            MansionProps.AddFloorLamp(root, new Vector3(1.2f, 0f, 3f));
            MansionProps.AddConsoleTable(root, new Vector3(2.5f, 0f, 8.5f), 0f, 2f);
            MansionProps.AddVase(root, new Vector3(2.5f, 0.84f, 8.5f), 3);
            MansionProps.AddPlant(root, new Vector3(1f, 0f, 7.2f), 0.8f);
            MansionProps.AddPainting(root, new Vector3(0.12f, 1.8f, 5.5f), 90f, 1.2f, 0.8f, 1);
            MansionProps.AddSculpture(root, new Vector3(5.5f, 0f, 7.8f), 0f);

            CreateNote(root, "GalleryNote", new Vector3(9.5f, 0.88f, 6.9f), "展厅说明",
                "「日之徽记」是开启西侧封印大门的第一枚钥匙。\n另外两枚分别在档案室与实验室。",
                "阅读展厅说明", "gallery", 0f);
            Box(root, "GalleryNoteStand", new Vector3(9.5f, 0.4f, 6.9f),
                new Vector3(1.1f, 0.8f, 0.8f), woodMaterial, true);
        }

        private static void BuildUtility(Transform root)
        {
            Box(root, "UtilityWorkbench", new Vector3(11.5f, 0.75f, -6.8f),
                new Vector3(2.2f, 0.1f, 0.9f), woodDarkMaterial, true);
            Box(root, "UtilityWorkbenchLeg", new Vector3(10.6f, 0.32f, -6.8f),
                new Vector3(0.1f, 0.64f, 0.8f), metalMaterial, true);
            Box(root, "UtilityWorkbenchShelf", new Vector3(11.5f, 0.35f, -6.8f),
                new Vector3(2f, 0.05f, 0.8f), woodDarkMaterial);
            Box(root, "UtilityVise", new Vector3(10.7f, 0.88f, -6.65f),
                new Vector3(0.2f, 0.15f, 0.2f), metalMaterial);
            Cylinder(root, "UtilityViseHandle", new Vector3(10.7f, 0.88f, -6.5f),
                new Vector3(0.025f, 0.18f, 0.025f), Quaternion.Euler(0f, 0f, 90f), goldMaterial);
            Box(root, "UtilityToolbox", new Vector3(12.2f, 0.88f, -6.9f),
                new Vector3(0.55f, 0.22f, 0.3f), metalMaterial);
            Box(root, "UtilityPegboard", new Vector3(11.5f, 1.8f, -3.2f),
                new Vector3(2.4f, 1.2f, 0.05f), woodDarkMaterial);
            for (int i = 0; i < 4; i++)
            {
                Cylinder(root, "WallTool", new Vector3(10.6f + i * 0.45f, 1.8f, -3.16f),
                    new Vector3(0.03f, 0.3f, 0.03f), Quaternion.identity, metalMaterial);
            }

            MansionProps.AddShelfUnit(root, new Vector3(0.6f, 0f, -6.5f), 90f, 2f, 2.1f, 0.45f, 2);
            MansionProps.AddDresser(root, new Vector3(5f, 0f, -8.4f), 0f, 1.4f, 0.9f, false);
            MansionProps.AddFloorLamp(root, new Vector3(13.2f, 0f, -3.1f));
            MansionProps.AddConsoleTable(root, new Vector3(5.5f, 0f, -2.5f), 0f, 2f);
            MansionProps.AddVase(root, new Vector3(5.5f, 0.84f, -2.5f), 4);
            Box(root, "UtilityExtraCrate", new Vector3(2f, 0.4f, -8.3f),
                new Vector3(1f, 0.8f, 1f), woodDarkMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(2f, 0.4f, -8.3f), new Vector3(1f, 0.8f, 1f));
            Cylinder(root, "UtilityBarrel", new Vector3(12.5f, 0.45f, -8.2f),
                new Vector3(0.45f, 0.45f, 0.45f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, true);
            MansionProps.AddBarrelDetails(root, new Vector3(12.5f, 0.45f, -8.2f));
            MansionProps.AddPainting(root, new Vector3(13.87f, 1.8f, -6f), -90f, 1.4f, 0.9f, 3);
            Box(root, "UtilityFloorMat", new Vector3(7f, 0.012f, -5.5f),
                new Vector3(6f, 0.02f, 2.8f), rugMaterial, false);
            MansionProps.AddGenerator(root, new Vector3(9f, 0f, -8.2f), 0f);
            MansionProps.AddToolCart(root, new Vector3(5f, 0f, -5.8f), 0f);
            MansionProps.AddWallSign(root, new Vector3(5.2f, 1.9f, -8.9f), 0f, 6);
            MansionProps.AddFireExtinguisher(root, new Vector3(13.4f, 0f, -8.7f));

            var breakerRoot = new GameObject("UtilityBreaker");
            breakerRoot.transform.SetParent(root, false);
            breakerRoot.transform.localPosition = new Vector3(13.35f, 1.4f, -5.2f);

            var collider = breakerRoot.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.24f, 0.82f, 0.72f);

            Box(breakerRoot.transform, "BreakerPanel", Vector3.zero, new Vector3(0.07f, 0.62f, 0.5f),
                metalMaterial, false);

            var indicator = Sphere(breakerRoot.transform, "BreakerLight", new Vector3(-0.05f, 0.2f, 0.12f),
                new Vector3(0.08f, 0.08f, 0.08f), CreateEmissive("BreakerRed", new Color(0.9f, 0.3f, 0.3f), 0.5f));

            var handlePivot = new GameObject("BreakerHandle");
            handlePivot.transform.SetParent(breakerRoot.transform, false);
            handlePivot.transform.localPosition = new Vector3(-0.06f, -0.08f, -0.08f);
            Box(handlePivot.transform, "HandleBar", new Vector3(0f, -0.12f, 0f),
                new Vector3(0.05f, 0.24f, 0.05f), goldMaterial, false);

            var lever = breakerRoot.AddComponent<PowerLever>();
            lever.HandlePivot = handlePivot.transform;
            lever.Indicator = indicator.GetComponent<Renderer>();
            lever.RequiredItemId = ItemIds.Fuse;
            lever.RequiredItemName = "保险丝";

            CreateNote(root, "UtilityNote", new Vector3(10.9f, 0.83f, -7.0f), "配电间记录",
                "电闸缺少一枚保险丝。\n没有电，电梯和二层密码锁都无法工作。",
                "阅读配电间记录", "utility", 0f);

            for (int i = 0; i < 3; i++)
            {
                Cylinder(root, "Pipe", new Vector3(13.7f, 1.9f, -3.2f - i * 0.45f),
                    new Vector3(0.08f, 1.4f, 0.08f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, false);
            }
        }

        private static KeypadPuzzle BuildArchive(Transform root)
        {
            float baseY = UpperBaseY;

            var hinge = new GameObject("ArchiveDoorHinge");
            hinge.transform.SetParent(root, false);
            hinge.transform.localPosition = new Vector3(-9.75f, baseY, 2f);

            Box(hinge.transform, "ArchiveDoor", new Vector3(0.72f, 1.18f, 0f),
                new Vector3(1.42f, 2.36f, 0.08f), woodDarkMaterial, true);
            Box(hinge.transform, "ArchiveDoorInset", new Vector3(0.72f, 1.35f, -0.05f),
                new Vector3(1.05f, 1.1f, 0.02f), woodMaterial, false);
            Box(hinge.transform, "ArchiveDoorLowerPanel", new Vector3(0.72f, 0.55f, -0.05f),
                new Vector3(1.05f, 0.5f, 0.02f), woodMaterial, false);
            Box(hinge.transform, "ArchiveDoorHandle", new Vector3(1.24f, 1.12f, -0.12f),
                new Vector3(0.08f, 0.08f, 0.18f), goldMaterial, false);

            var door = hinge.AddComponent<HingedDoor>();
            door.Pivot = hinge.transform;
            door.OpenAngle = -95f;

            var doorInteractable = hinge.AddComponent<DoorInteractable>();
            doorInteractable.Door = door;

            var keypadRoot = new GameObject("ArchiveKeypad");
            keypadRoot.transform.SetParent(root, false);
            keypadRoot.transform.localPosition = new Vector3(-7.4f, baseY + 1.42f, 1.9f);

            var collider = keypadRoot.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.58f, 0.98f, 0.16f);

            Box(keypadRoot.transform, "KeypadPanel", Vector3.zero, new Vector3(0.46f, 0.88f, 0.07f),
                metalMaterial, false);
            Box(keypadRoot.transform, "KeypadScreen", new Vector3(0f, 0.31f, -0.045f),
                new Vector3(0.34f, 0.12f, 0.02f), glassMaterial, false);

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    Box(keypadRoot.transform, "KeypadButton", new Vector3((column - 1) * 0.13f,
                        0.13f - row * 0.15f, -0.045f), new Vector3(0.09f, 0.09f, 0.02f),
                        metalMaterial, false);
                }
            }

            var indicator = Sphere(keypadRoot.transform, "KeypadLight", new Vector3(0.18f, 0.38f, -0.04f),
                new Vector3(0.07f, 0.07f, 0.07f), CreateEmissive("ArchiveKeypadRed", new Color(0.9f, 0.3f, 0.3f), 0.5f));

            var keypad = keypadRoot.AddComponent<KeypadPuzzle>();
            keypad.Solution = "0427";
            keypad.Indicator = indicator.GetComponent<Renderer>();
            keypad.LinkedDoor = door;
            keypad.UnlocksArchive = true;

            Box(root, "ArchiveShelfBack", new Vector3(-13.85f, baseY + 1.4f, 5.5f),
                new Vector3(0.06f, 2.8f, 2.6f), woodDarkMaterial, true);
            Box(root, "ArchiveShelfBoardA", new Vector3(-13.55f, baseY + 0.7f, 5.5f),
                new Vector3(0.5f, 0.06f, 2.55f), woodMaterial, true);
            Box(root, "ArchiveShelfBoardB", new Vector3(-13.55f, baseY + 1.4f, 5.5f),
                new Vector3(0.5f, 0.06f, 2.55f), woodMaterial, true);
            Box(root, "ArchiveShelfBoardC", new Vector3(-13.55f, baseY + 2.1f, 5.5f),
                new Vector3(0.5f, 0.06f, 2.55f), woodMaterial, true);

            Cylinder(root, "ArchivePedestal", new Vector3(-9f, baseY + 0.4f, 5.2f),
                new Vector3(0.8f, 0.4f, 0.8f), Quaternion.identity, metalMaterial, true);
            CreateItemSpawnPoint(root, "Spawn_SealMoon", ItemIds.SealMoon,
                new Vector3(-9f, baseY + 0.81f, 5.2f), true);

            CreateNote(root, "ArchiveNote", new Vector3(-11.2f, baseY + 0.88f, 6.8f), "档案室便签",
                "四枚徽记：日、月、星、影。\n日、月、星分别在展厅、档案室与实验室，影之徽记在庄园地下。\n把四枚徽记带到西侧封印大门。",
                "阅读档案室便签", "archive", 0f);

            Box(root, "ArchiveDesk", new Vector3(-11.8f, baseY + 0.82f, 6.8f),
                new Vector3(2f, 0.08f, 1f), woodMaterial, true);

            MansionProps.AddDeskDetails(root, new Vector3(-11.8f, baseY, 6.8f), 180f, 2f);
            MansionProps.AddChair(root, new Vector3(-11.8f, baseY, 5.8f), 0f);
            Box(root, "ArchiveRug", new Vector3(-9f, baseY + 0.012f, 5f),
                new Vector3(3.2f, 0.02f, 2.6f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(-9f, baseY + 0.012f, 5f), 3.2f, 2.6f);
            MansionProps.AddShelfUnit(root, new Vector3(-0.8f, baseY, 4.5f), 90f, 2f, 2.2f, 0.4f, 2);
            MansionProps.AddDresser(root, new Vector3(-13.3f, baseY, 3.2f), 90f, 1.4f, 0.95f, false);
            MansionProps.AddFloorLamp(root, new Vector3(-0.4f, baseY, 7f));
            MansionProps.AddConsoleTable(root, new Vector3(-5f, baseY, 8.7f), 0f, 2f);
            MansionProps.AddGlobe(root, new Vector3(-5f, baseY + 0.84f, 8.7f));
            MansionProps.AddVase(root, new Vector3(-4.5f, baseY + 0.84f, 8.7f), 3);
            MansionProps.AddBookStack(root, new Vector3(-10.9f, baseY + 0.9f, 6.3f), 3);
            MansionProps.AddPainting(root, new Vector3(-13.87f, baseY + 1.8f, 5f), 90f, 1.4f, 0.9f, 2);

            return keypad;
        }

        private static void BuildLab(Transform root)
        {
            float baseY = UpperBaseY;

            Box(root, "LabConsoleTop", new Vector3(9f, baseY + 0.82f, 3.15f),
                new Vector3(2.8f, 0.08f, 1f), metalMaterial, true);
            Box(root, "LabConsoleBody", new Vector3(9f, baseY + 0.45f, 3.15f),
                new Vector3(2.6f, 0.7f, 0.8f), woodDarkMaterial, true);

            var puzzleGo = new GameObject("LabSequencePuzzle");
            puzzleGo.transform.SetParent(root, false);
            puzzleGo.transform.localPosition = new Vector3(9f, baseY + 1f, 3.2f);
            var puzzle = puzzleGo.AddComponent<LabSequencePuzzle>();
            puzzle.ExpectedOrder = new[] { 1, 3, 2 };

            var switches = new SequenceSwitch[3];
            Color[] colors =
            {
                new Color(0.9f, 0.3f, 0.3f),
                new Color(0.35f, 0.8f, 0.4f),
                new Color(0.35f, 0.55f, 0.95f)
            };
            string[] labels = { "红色开关", "绿色开关", "蓝色开关" };

            for (int i = 0; i < 3; i++)
            {
                var switchRoot = new GameObject("LabSwitch_" + (i + 1));
                switchRoot.transform.SetParent(root, false);
                switchRoot.transform.localPosition = new Vector3(7.8f + i * 1.2f, baseY + 1.02f, 2.68f);

                var collider = switchRoot.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.3f, 0.56f, 0.2f);

                Box(switchRoot.transform, "SwitchBase", Vector3.zero, new Vector3(0.24f, 0.4f, 0.1f),
                    metalMaterial, false);

                var indicator = Sphere(switchRoot.transform, "SwitchLight", new Vector3(0f, 0.22f, -0.07f),
                    new Vector3(0.08f, 0.08f, 0.08f), CreateEmissive("SwitchColor" + i, colors[i], 0.5f));

                var handlePivot = new GameObject("SwitchHandle");
                handlePivot.transform.SetParent(switchRoot.transform, false);
                handlePivot.transform.localPosition = new Vector3(0f, 0.02f, -0.09f);
                Box(handlePivot.transform, "HandleBar", new Vector3(0f, -0.12f, 0f),
                    new Vector3(0.04f, 0.24f, 0.04f), goldMaterial, false);

                var sequenceSwitch = switchRoot.AddComponent<SequenceSwitch>();
                sequenceSwitch.Puzzle = puzzle;
                sequenceSwitch.SwitchIndex = i + 1;
                sequenceSwitch.SwitchLabel = labels[i];
                sequenceSwitch.HandlePivot = handlePivot.transform;
                sequenceSwitch.Indicator = indicator.GetComponent<Renderer>();
                sequenceSwitch.ActiveColor = new Color(0.4f, 1f, 0.6f, 1f);
                switches[i] = sequenceSwitch;
            }

            puzzle.Switches = switches;

            var caseRoot = new GameObject("LabSealCase");
            caseRoot.transform.SetParent(root, false);
            caseRoot.transform.localPosition = new Vector3(9f, baseY + 0.35f, 6.25f);

            Box(caseRoot.transform, "CaseBase", new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.7f, 0.9f),
                metalMaterial, true);

            var blocker = new GameObject("LabCaseGlass");
            blocker.transform.SetParent(caseRoot.transform, false);
            blocker.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            Box(blocker.transform, "Glass", Vector3.zero, new Vector3(0.82f, 0.72f, 0.82f),
                glassMaterial, true);

            var rewardPoint = CreateItemSpawnPoint(root, "Spawn_SealStar", ItemIds.SealStar,
                new Vector3(9f, baseY + 0.71f, 6.25f), false);

            puzzle.Blockers = new[] { blocker };
            puzzle.RewardSpawnPoint = rewardPoint;

            CreateNote(root, "LabNote", new Vector3(9f, baseY + 0.88f, 3.6f), "实验日志",
                "机关顺序：红 → 蓝 → 绿。\n四枚徽记之一就在陈列柜里。",
                "阅读实验日志", "lab", 0f);

            MansionProps.AddChair(root, new Vector3(9f, baseY, 4.4f), 0f);
            MansionProps.AddShelfUnit(root, new Vector3(0.8f, baseY, 4.5f), 90f, 2f, 2.1f, 0.4f, 3);
            MansionProps.AddConsoleTable(root, new Vector3(13.2f, baseY, 7.8f), 90f, 1.4f);
            MansionProps.AddBarStool(root, new Vector3(7.8f, baseY, 4.9f), 0f);
            MansionProps.AddFloorLamp(root, new Vector3(13.2f, baseY, 3.8f));
            MansionProps.AddVase(root, new Vector3(13.2f, baseY + 0.84f, 7.8f), 4);
            MansionProps.AddBookStack(root, new Vector3(7.2f, baseY + 0.9f, 3.2f), 2);
            MansionProps.AddPainting(root, new Vector3(0.12f, baseY + 1.8f, 6.5f), 90f, 1.2f, 0.8f, 5);
            Box(root, "LabFloorMat", new Vector3(9f, baseY + 0.012f, 6f),
                new Vector3(5f, 0.02f, 2.5f), rugMaterial, false);
            MansionProps.AddFumeHood(root, new Vector3(12.5f, baseY, 6.8f), 0f);
            MansionProps.AddWallSign(root, new Vector3(9f, baseY + 1.9f, 8.9f), 180f, 5);
            MansionProps.AddFireExtinguisher(root, new Vector3(1f, baseY, 8.8f));

            Box(root, "LabTable", new Vector3(5.8f, baseY + 0.78f, 5.4f),
                new Vector3(2.2f, 0.08f, 1f), woodDarkMaterial, true);
            Cylinder(root, "LabFlask", new Vector3(5.4f, baseY + 0.94f, 5.4f),
                new Vector3(0.14f, 0.18f, 0.14f), Quaternion.identity, glassMaterial, false);
        }

        private static void BuildBedroom(Transform root)
        {
            float baseY = UpperBaseY;

            bool useRealBed = false;
#if UNITY_EDITOR
            useRealBed = MansionRealModels.Spawn(root, "GothicBed_01",
                root.TransformPoint(new Vector3(-9f, baseY, -6f)), 180f, 1.5f, true) != null;
#endif
            if (!useRealBed)
            {
                Box(root, "BedFrame", new Vector3(-9f, baseY + 0.28f, -6f), new Vector3(2.2f, 0.35f, 3f),
                    woodDarkMaterial, true);
                Box(root, "BedMattress", new Vector3(-9f, baseY + 0.52f, -6f), new Vector3(2f, 0.24f, 2.8f),
                    paperMaterial, true);
                Box(root, "BedPillow", new Vector3(-9f, baseY + 0.7f, -7.2f), new Vector3(1.3f, 0.16f, 0.5f),
                    paperMaterial, false);
                Box(root, "BedHeadboard", new Vector3(-9f, baseY + 0.9f, -7.55f), new Vector3(2.3f, 1f, 0.12f),
                    woodMaterial, true);
            }

            Box(root, "Nightstand", new Vector3(-6.9f, baseY + 0.4f, -5.1f), new Vector3(0.7f, 0.8f, 0.7f),
                woodMaterial, true);
            CreateNote(root, "BedroomNote", new Vector3(-6.9f, baseY + 0.86f, -5.1f), "卧室里的纸条",
                "「如果你看到这张纸，说明你已经在二层了。\n实验室的机关顺序是：红、蓝、绿。」",
                "阅读卧室里的纸条", "bedroom", 0f);

            Box(root, "BedroomWindow", new Vector3(-13.92f, baseY + 2f, -4.6f),
                new Vector3(0.04f, 1.5f, 1.9f), glassMaterial, false);
            Box(root, "BedroomWindowFrame", new Vector3(-13.9f, baseY + 2f, -4.6f),
                new Vector3(0.06f, 1.7f, 2.1f), woodDarkMaterial, false);
            Box(root, "BedroomWindowGlass", new Vector3(-13.87f, baseY + 2f, -4.6f),
                new Vector3(0.02f, 1.4f, 1.8f), glassMaterial, false);

            Box(root, "BedroomPaintingFrame", new Vector3(-9f, baseY + 2f, -8.88f),
                new Vector3(1.7f, 1f, 0.06f), goldMaterial, false);
            Box(root, "BedroomPaintingMat", new Vector3(-9f, baseY + 2f, -8.84f),
                new Vector3(1.52f, 0.82f, 0.02f), paperMaterial, false);
            Box(root, "BedroomPainting", new Vector3(-9f, baseY + 2f, -8.82f),
                new Vector3(1.4f, 0.7f, 0.015f), paintingMaterials[3], false);

            if (!useRealBed)
            {
                MansionProps.AddBedDetails(root, new Vector3(-9f, baseY, -6f), 0f, 2f, 3f);
            }
            MansionProps.AddTableLamp(root, new Vector3(-6.65f, baseY + 0.8f, -5.3f), 0.62f);
            Box(root, "BedroomRug", new Vector3(-9f, baseY + 0.012f, -5.2f),
                new Vector3(3.4f, 0.02f, 2.4f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(-9f, baseY + 0.012f, -5.2f), 3.4f, 2.4f);
            MansionProps.AddDresser(root, new Vector3(-1.4f, baseY, -5.6f), 270f, 1.4f, 0.95f, true);
            MansionProps.AddShelfUnit(root, new Vector3(-13.4f, baseY, -8.1f), 90f, 1.5f, 2f, 0.4f, 1);
            MansionProps.AddChair(root, new Vector3(-2.4f, baseY, -3.4f), 180f, true);
            MansionProps.AddFloorLamp(root, new Vector3(-13f, baseY, -3.2f));
            MansionProps.AddVase(root, new Vector3(-1.6f, baseY + 1.0f, -5.6f), 1);
            MansionProps.AddCurtains(root, new Vector3(-13.8f, baseY + 1.6f, -4.6f), 90f, 2.2f);
            MansionProps.AddPainting(root, new Vector3(-0.12f, baseY + 1.8f, -6.8f), -90f, 1.2f, 0.8f, 3);
        }

        private static void BuildObservatory(Transform root)
        {
            float baseY = UpperBaseY;

            Cylinder(root, "TelescopeBase", new Vector3(9f, baseY + 0.35f, -5.6f),
                new Vector3(0.9f, 0.35f, 0.9f), Quaternion.identity, metalMaterial, true);
            Cylinder(root, "TelescopeTube", new Vector3(9f, baseY + 1.2f, -5.6f),
                new Vector3(0.25f, 1.2f, 0.25f), Quaternion.Euler(55f, 0f, 0f), goldMaterial, false);
            Cylinder(root, "TelescopeMount", new Vector3(9f, baseY + 0.75f, -5.6f),
                new Vector3(0.18f, 0.8f, 0.18f), Quaternion.identity, metalMaterial, false);

            Box(root, "ObservatoryTable", new Vector3(11.5f, baseY + 0.78f, -5.8f),
                new Vector3(2f, 0.08f, 1f), woodDarkMaterial, true);
            CreateNote(root, "ObservatoryNote", new Vector3(11.5f, baseY + 0.86f, -5.8f),
                "观测室笔记", "「西侧封印大门需要日、月、星、影四枚徽记。\n凑齐之后，它们会自行嵌入大门。」",
                "阅读观测室笔记", "observatory", 0f);

            Box(root, "ObservatoryWindow", new Vector3(13.92f, baseY + 2f, -5.4f),
                new Vector3(0.04f, 1.8f, 2.2f), glassMaterial, false);

            MansionProps.AddTelescopeDetails(root, new Vector3(9f, baseY, -5.6f));
            MansionProps.AddChair(root, new Vector3(10.8f, baseY, -4.7f), 270f);
            MansionProps.AddTableLamp(root, new Vector3(12.2f, baseY + 0.8f, -5.6f), 0.6f);
            MansionProps.AddShelfUnit(root, new Vector3(0.8f, baseY, -4.5f), 90f, 2f, 2.1f, 0.4f, 4);
            MansionProps.AddConsoleTable(root, new Vector3(13.2f, baseY, -7.8f), 90f, 1.6f);
            MansionProps.AddGlobe(root, new Vector3(13.2f, baseY + 0.84f, -7.8f));
            MansionProps.AddFloorLamp(root, new Vector3(1.2f, baseY, -7.8f));
            MansionProps.AddBarStool(root, new Vector3(5.5f, baseY, -3.2f), 0f);
            MansionProps.AddPainting(root, new Vector3(0.12f, baseY + 1.8f, -6.5f), 90f, 1.2f, 0.8f, 2);
            Box(root, "ObservatoryRug", new Vector3(7f, baseY + 0.012f, -5.5f),
                new Vector3(4f, 0.02f, 2.6f), rugMaterial, false);
            MansionProps.AddPainting(root, new Vector3(9f, baseY + 1.8f, -8.9f), 0f, 1.6f, 0.9f, 1);
        }

        private static void BuildFinalGate(Transform root)
        {
            float baseY = UpperBaseY;

            var hinge = new GameObject("SealedGateHinge");
            hinge.transform.SetParent(root, false);
            hinge.transform.localPosition = new Vector3(FinalGateX, baseY, -1.4f);

            Box(hinge.transform, "SealedGate", new Vector3(0f, 1.3f, 1.4f),
                new Vector3(0.1f, 2.6f, 2.8f), woodDarkMaterial, true);

            var door = hinge.AddComponent<HingedDoor>();
            door.Pivot = hinge.transform;
            door.OpenAngle = -105f;

            var indicators = new Renderer[4];
            Color[] sealColors =
            {
                new Color(1f, 0.72f, 0.25f),
                new Color(0.42f, 0.7f, 1f),
                new Color(0.75f, 0.5f, 1f),
                new Color(0.42f, 0.24f, 0.66f)
            };

            for (int i = 0; i < 4; i++)
            {
                var indicator = Sphere(hinge.transform, "SealIndicator", new Vector3(0.1f, 1.75f, 0.8f + i * 0.5f),
                    new Vector3(0.16f, 0.16f, 0.16f),
                    CreateEmissive("SealIndicator" + i, sealColors[i], 0.7f));
                indicators[i] = indicator.GetComponent<Renderer>();
            }

            var gate = hinge.AddComponent<SealGate>();
            gate.Door = door;
            gate.RequiredItemIds = new[]
            {
                ItemIds.SealSun,
                ItemIds.SealMoon,
                ItemIds.SealStar,
                ItemIds.SealShadow
            };
            gate.SealIndicators = indicators;

            Box(root, "ExitFloor", new Vector3(-24.5f, baseY - 0.1f, 0f), new Vector3(3f, 0.2f, 4f),
                floorMaterial, true);
            Box(root, "ExitCeiling", new Vector3(-24.5f, baseY + 3.3f, 0f), new Vector3(3f, 0.2f, 4f),
                wallMaterial, true);
            Box(root, "ExitWallWest", new Vector3(-26.05f, baseY + 1.65f, 0f),
                new Vector3(0.1f, 3.3f, 4f), wallMaterial, true);
            Box(root, "ExitWallNorth", new Vector3(-24.5f, baseY + 1.65f, 2.05f),
                new Vector3(3.1f, 3.3f, 0.1f), wallMaterial, true);
            Box(root, "ExitWallSouth", new Vector3(-24.5f, baseY + 1.65f, -2.05f),
                new Vector3(3.1f, 3.3f, 0.1f), wallMaterial, true);

            Box(root, "ExitGlow", new Vector3(-25.85f, baseY + 1.35f, 0f),
                new Vector3(0.04f, 2.4f, 1.8f),
                CreateEmissive("MansionExitGlow", new Color(0.8f, 0.9f, 1f), 1.3f), false);

            var exitZone = new GameObject("ExitZone");
            exitZone.transform.SetParent(root, false);
            exitZone.transform.localPosition = new Vector3(-24.4f, baseY + 1.1f, 0f);

            var trigger = exitZone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.2f, 2.2f, 2.8f);

            var body = exitZone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            exitZone.AddComponent<ExitZone>();
        }

        private static void BuildDiningRoom(Transform root)
        {
            Box(root, "DiningRug", new Vector3(-18f, 0.012f, 5.5f), new Vector3(6f, 0.02f, 3.2f),
                rugMaterial, false);

            bool useRealDiningTable = false;
#if UNITY_EDITOR
            if (MansionRealModels.Has("wooden_table_02"))
            {
                useRealDiningTable = true;
                for (int i = -1; i <= 1; i++)
                {
                    float tableX = -18f + i * 1.733f;
                    if (MansionRealModels.Spawn(root, "wooden_table_02",
                            root.TransformPoint(new Vector3(tableX, 0f, 5.5f)), 0f, 0.86f, true,
                            0f, null, false, new Vector3(1.733f, 0.86f, 1.0f)) == null)
                    {
                        useRealDiningTable = false;
                        break;
                    }
                }
            }
#endif
            if (!useRealDiningTable)
            {
                Box(root, "DiningTableTop", new Vector3(-18f, 0.82f, 5.5f), new Vector3(5.2f, 0.08f, 1.5f),
                    woodMaterial, true);

                float[] legX = { -20.3f, -15.7f };
                float[] legZ = { 4.9f, 6.1f };
                foreach (float x in legX)
                {
                    foreach (float z in legZ)
                    {
                        Box(root, "DiningTableLeg", new Vector3(x, 0.39f, z), new Vector3(0.12f, 0.78f, 0.12f),
                            woodDarkMaterial, true);
                    }
                }
            }

            // 整幅桌布盖住三张拼桌的接缝，并做出垂边与金色滚边。
            Box(root, "DiningClothTop", new Vector3(-18f, 0.8615f, 5.5f), new Vector3(5.24f, 0.005f, 1.06f),
                tableclothMaterial, false);
            Box(root, "DiningClothDropA", new Vector3(-18f, 0.73f, 6.03f), new Vector3(5.24f, 0.27f, 0.008f),
                tableclothMaterial, false);
            Box(root, "DiningClothDropB", new Vector3(-18f, 0.73f, 4.97f), new Vector3(5.24f, 0.27f, 0.008f),
                tableclothMaterial, false);
            Box(root, "DiningClothEndA", new Vector3(-20.62f, 0.73f, 5.5f), new Vector3(0.008f, 0.27f, 1.05f),
                tableclothMaterial, false);
            Box(root, "DiningClothEndB", new Vector3(-15.38f, 0.73f, 5.5f), new Vector3(0.008f, 0.27f, 1.05f),
                tableclothMaterial, false);
            Box(root, "DiningClothHemA", new Vector3(-18f, 0.8665f, 6.0f), new Vector3(5.2f, 0.004f, 0.012f),
                goldMaterial, false);
            Box(root, "DiningClothHemB", new Vector3(-18f, 0.8665f, 5.0f), new Vector3(5.2f, 0.004f, 0.012f),
                goldMaterial, false);
            Box(root, "DiningClothHemC", new Vector3(-20.6f, 0.8665f, 5.5f), new Vector3(0.012f, 0.004f, 1.0f),
                goldMaterial, false);
            Box(root, "DiningClothHemD", new Vector3(-15.4f, 0.8665f, 5.5f), new Vector3(0.012f, 0.004f, 1.0f),
                goldMaterial, false);

            Box(root, "DiningSideboard", new Vector3(-21.3f, 0.45f, 7.2f),
                new Vector3(0.9f, 0.9f, 1.6f), woodDarkMaterial, true);
            Box(root, "DiningSideboardTop", new Vector3(-21.3f, 0.93f, 7.2f),
                new Vector3(1f, 0.06f, 1.7f), woodMaterial, false);

            CreateNote(root, "DiningNote", new Vector3(-18.2f, 0.88f, 6.15f), "餐厅便签",
                "长桌上的酒已经放了很久。\n仆人每晚都会把餐具摆好，像是在等待一场不会开始的晚宴。",
                "阅读餐厅便签", "dining", 0f);

            MansionProps.AddDiningSet(root, new Vector3(-18f, 0f, 5.5f), 5.2f, 1.0f);
            MansionProps.AddRugBorder(root, new Vector3(-18f, 0.012f, 5.5f), 6f, 3.2f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(root, "DiningSideboardDoor", new Vector3(-20.84f, 0.52f, 7.2f + side * 0.38f),
                    new Vector3(0.03f, 0.6f, 0.55f), woodMaterial);
                Box(root, "DiningSideboardHandle", new Vector3(-20.81f, 0.52f, 7.2f + side * 0.22f),
                    new Vector3(0.03f, 0.04f, 0.12f), goldMaterial);
            }

            Box(root, "DiningPaintingFrame", new Vector3(-18f, 2f, 8.86f), new Vector3(2.3f, 1.3f, 0.06f),
                goldMaterial, false);
            Box(root, "DiningPaintingMat", new Vector3(-18f, 2f, 8.82f), new Vector3(2.1f, 1.12f, 0.02f),
                paperMaterial, false);
            Box(root, "DiningPainting", new Vector3(-18f, 2f, 8.8f), new Vector3(1.94f, 0.96f, 0.015f),
                paintingMaterials[0], false);
            Box(root, "DiningWindow", new Vector3(-21.9f, 2f, 5.5f), new Vector3(0.04f, 1.6f, 2.2f),
                glassMaterial, false);

            MansionProps.AddShelfUnit(root, new Vector3(-21.2f, 0f, 3.4f), 90f, 1.8f, 1.9f, 0.4f, 5);
            MansionProps.AddConsoleTable(root, new Vector3(-14.8f, 0f, 3.4f), 90f, 1.6f);
            MansionProps.AddVase(root, new Vector3(-14.8f, 0.84f, 3.4f), 0);
            MansionProps.AddFloorLamp(root, new Vector3(-14.9f, 0f, 7.7f));
            MansionProps.AddCurtains(root, new Vector3(-21.8f, 1.6f, 5.5f), 90f, 2.2f);
            MansionProps.AddPainting(root, new Vector3(-14.12f, 1.8f, 6.7f), 90f, 1.4f, 0.9f, 3);
            MansionProps.AddVase(root, new Vector3(-21.3f, 0.96f, 6.5f), 1);
        }

        private static void BuildKitchen(Transform root)
        {
            Box(root, "KitchenCounter", new Vector3(-21.2f, 0.45f, -6.4f),
                new Vector3(0.8f, 0.9f, 4.4f), woodDarkMaterial, true);
            Box(root, "KitchenCounterTop", new Vector3(-21.2f, 0.92f, -6.4f),
                new Vector3(0.9f, 0.08f, 4.5f), paperMaterial, false);
            Box(root, "KitchenStove", new Vector3(-21.2f, 0.45f, -3.6f),
                new Vector3(0.9f, 0.9f, 0.9f), metalMaterial, true);
            Box(root, "KitchenSink", new Vector3(-21.2f, 0.9f, -8.0f),
                new Vector3(0.7f, 0.05f, 0.7f), metalMaterial, false);

            for (int i = 0; i < 3; i++)
            {
                Cylinder(root, "KitchenPot", new Vector3(-21.0f, 1.05f, -5.2f - i * 0.45f),
                    new Vector3(0.18f, 0.12f, 0.18f), Quaternion.identity, metalMaterial, false);
            }

            Box(root, "KitchenCrateA", new Vector3(-16.2f, 0.4f, -7.6f),
                new Vector3(1f, 0.8f, 1f), woodDarkMaterial, true);
            Box(root, "KitchenCrateB", new Vector3(-15.2f, 0.3f, -6.9f),
                new Vector3(0.7f, 0.6f, 0.7f), woodMaterial, true);
            Box(root, "KitchenShelf", new Vector3(-15.0f, 1.4f, -8.3f),
                new Vector3(2f, 0.08f, 0.7f), woodMaterial, true);

            CreateNote(root, "KitchenNote", new Vector3(-20.9f, 0.97f, -7.2f), "厨房清单",
                "食材早已腐烂。\n有人把保险丝和工具都收进了储藏室。",
                "阅读厨房清单", "kitchen", 0f);

            MansionProps.AddKitchenDetails(root);
            MansionProps.AddSideTable(root, new Vector3(-17.6f, 0f, -6.5f), 0f, 1.4f, 1f);
            MansionProps.AddChair(root, new Vector3(-18.8f, 0f, -6.5f), 90f);
            MansionProps.AddChair(root, new Vector3(-16.4f, 0f, -6.5f), 270f);
            MansionProps.AddVase(root, new Vector3(-17.6f, 0.78f, -6.5f), 2);
            MansionProps.AddShelfUnit(root, new Vector3(-14.7f, 0f, -7.8f), 90f, 1.8f, 1.9f, 0.4f, 2);
            MansionProps.AddFloorLamp(root, new Vector3(-14.9f, 0f, -3.1f));
            MansionProps.AddHangingPlant(root, new Vector3(-15.3f, 3.05f, -8.6f), 0.8f);
            MansionProps.AddPainting(root, new Vector3(-14.12f, 1.8f, -5f), 90f, 1.2f, 0.8f, 4);
            MansionProps.AddFireExtinguisher(root, new Vector3(-21.7f, 0f, -2.3f));
        }

        private static void BuildSalon(Transform root)
        {
            Box(root, "SalonRug", new Vector3(18f, 0.012f, 5.5f), new Vector3(6f, 0.02f, 3.2f),
                rugMaterial, false);
            bool useRealSofa = false;
#if UNITY_EDITOR
            GameObject sofaA = MansionRealModels.Spawn(root, "Sofa_01",
                root.TransformPoint(new Vector3(15.75f, 0f, 6.9f)), 180f, 0.8f, true);
            GameObject sofaB = MansionRealModels.Spawn(root, "Sofa_01",
                root.TransformPoint(new Vector3(17.25f, 0f, 6.9f)), 180f, 0.8f, true);
            useRealSofa = sofaA != null && sofaB != null;
#endif
            if (!useRealSofa)
            {
                Box(root, "SalonSofa", new Vector3(16.5f, 0.42f, 6.7f), new Vector3(3f, 0.55f, 0.9f),
                    rugMaterial, true);
                Box(root, "SalonSofaBack", new Vector3(16.5f, 0.85f, 7.1f), new Vector3(3f, 0.9f, 0.15f),
                    rugMaterial, true);
            }
            MansionProps.AddChair(root, new Vector3(18.6f, 0f, 6.8f), 180f, true);

            Box(root, "SalonTeaTableTop", new Vector3(19f, 0.78f, 5.2f), new Vector3(1.5f, 0.08f, 0.9f),
                woodMaterial, true);
            Box(root, "SalonTeaTableLeg", new Vector3(19f, 0.37f, 5.2f), new Vector3(0.5f, 0.74f, 0.5f),
                woodDarkMaterial, true);

            CreateItemSpawnPoint(root, "Spawn_SilverTeacup", ItemIds.SilverTeacup,
                new Vector3(19.2f, 0.83f, 4.95f), true);
            CreateNote(root, "SalonNote", new Vector3(18.4f, 0.86f, 5.45f), "会客厅便签",
                "壁炉已经很多年没有点燃了。\n茶桌上的银杯，是主人招待客人时用的。",
                "阅读会客厅便签", "salon", 0f);

            Box(root, "SalonFireplace", new Vector3(18f, 0.9f, 8.85f), new Vector3(2.4f, 1.8f, 0.2f),
                woodDarkMaterial, true);
            Box(root, "SalonFireplaceGlow", new Vector3(18f, 0.62f, 8.74f), new Vector3(1.2f, 0.6f, 0.04f),
                CreateEmissive("SalonFireGlow", new Color(1f, 0.45f, 0.16f), 0.5f), false);
            if (!useRealSofa)
            {
                MansionProps.AddSofaDetails(root, new Vector3(16.5f, 0f, 6.7f), 180f, 3f);
            }
            MansionProps.AddFireplaceDetails(root, new Vector3(18f, 0f, 8.85f));
            MansionProps.AddPlant(root, new Vector3(15.2f, 0f, 4.2f), 1f);
            MansionProps.AddRugBorder(root, new Vector3(18f, 0.012f, 5.5f), 6f, 3.2f);
            MansionProps.AddConsoleTable(root, new Vector3(21.2f, 0f, 4.3f), 90f, 1.8f);
            MansionProps.AddShelfUnit(root, new Vector3(15.2f, 0f, 8.3f), 0f, 1.8f, 2f, 0.45f, 3);
            MansionProps.AddFloorLamp(root, new Vector3(20.8f, 0f, 7.9f));
            MansionProps.AddVase(root, new Vector3(21.2f, 0.84f, 4.3f), 1);
            MansionProps.AddPainting(root, new Vector3(21.88f, 1.8f, 6.8f), 90f, 1.2f, 0.8f, 0);
        }

        private static void BuildFoyer(Transform root)
        {
            Box(root, "FoyerBench", new Vector3(16f, 0.4f, -4.4f), new Vector3(2f, 0.12f, 0.55f),
                woodMaterial, true);
            Box(root, "FoyerBenchLegA", new Vector3(15.2f, 0.2f, -4.4f), new Vector3(0.1f, 0.4f, 0.5f),
                woodDarkMaterial, true);
            Box(root, "FoyerBenchLegB", new Vector3(16.8f, 0.2f, -4.4f), new Vector3(0.1f, 0.4f, 0.5f),
                woodDarkMaterial, true);
            Cylinder(root, "FoyerCoatRack", new Vector3(21f, 1.1f, -4.2f), new Vector3(0.1f, 1.1f, 0.1f),
                Quaternion.identity, woodDarkMaterial, false);
            Cylinder(root, "FoyerUmbrellaStand", new Vector3(20.5f, 0.5f, -6.2f),
                new Vector3(0.25f, 0.5f, 0.25f), Quaternion.identity, metalMaterial, false);

            Box(root, "FoyerCrates", new Vector3(15.4f, 0.4f, -7.8f), new Vector3(1.2f, 0.8f, 1.2f),
                woodDarkMaterial, true);
            Box(root, "FoyerSideTable", new Vector3(16f, 0.4f, -6.4f), new Vector3(1f, 0.8f, 0.8f),
                woodMaterial, true);
            CreateNote(root, "FoyerNote", new Vector3(16f, 0.87f, -6.4f), "门厅便签",
                "西侧走廊尽头有一扇被封死的大门。\n主人说，只有四枚徽记才能解开封印。",
                "阅读门厅便签", "foyer", 0f);

            MansionProps.AddGrandfatherClock(root, new Vector3(20.5f, 0f, -8.2f), 0f);
            MansionProps.AddCrateDetails(root, new Vector3(15.4f, 0.4f, -7.8f), new Vector3(1.2f, 0.8f, 1.2f));
            Box(root, "FoyerRug", new Vector3(18f, 0.012f, -5.5f), new Vector3(4.5f, 0.02f, 2.4f),
                rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(18f, 0.012f, -5.5f), 4.5f, 2.4f);
            Box(root, "FoyerBenchCushion", new Vector3(16f, 0.49f, -4.4f),
                new Vector3(1.9f, 0.06f, 0.5f), rugMaterial);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(root, "FoyerBenchLeg", new Vector3(16f + i * 0.8f, 0.18f, -4.4f),
                    new Vector3(0.08f, 0.36f, 0.08f), woodDarkMaterial);
                Box(root, "CoatHook", new Vector3(21f, 1.85f, -4.2f + i * 0.18f),
                    new Vector3(0.22f, 0.04f, 0.04f), goldMaterial);
            }

            Cylinder(root, "FoyerCoatRackBase", new Vector3(21f, 0.06f, -4.2f),
                new Vector3(0.25f, 0.06f, 0.25f), Quaternion.identity, metalMaterial);
            Box(root, "FoyerHangingCoat", new Vector3(20.98f, 1.45f, -4.08f),
                new Vector3(0.45f, 0.7f, 0.16f), rugMaterial, false);
            Cylinder(root, "FoyerUmbrella", new Vector3(20.52f, 0.95f, -6.15f),
                new Vector3(0.035f, 0.65f, 0.035f), Quaternion.Euler(8f, 0f, 5f), woodDarkMaterial);
            Sphere(root, "FoyerUmbrellaTop", new Vector3(20.55f, 1.35f, -6.13f),
                new Vector3(0.2f, 0.08f, 0.2f), rugMaterial);

            MansionProps.AddConsoleTable(root, new Vector3(18f, 0f, -8.6f), 0f, 2f);
            MansionProps.AddMirror(root, new Vector3(18f, 1.7f, -8.92f), 0f, 1.4f, 0.9f);
            MansionProps.AddVase(root, new Vector3(18f, 0.84f, -8.6f), 2);
            MansionProps.AddLuggage(root, new Vector3(14.6f, 0f, -3.1f), 0f);
            MansionProps.AddPlant(root, new Vector3(21.2f, 0f, -3.2f), 0.8f);
            MansionProps.AddFloorLamp(root, new Vector3(14.2f, 0f, -5.5f));
            MansionProps.AddPainting(root, new Vector3(21.88f, 1.8f, -5.5f), 90f, 1.4f, 0.9f, 4);
        }

        private static void BuildBallroom(Transform root)
        {
            float baseY = UpperBaseY;
            Box(root, "BallroomRug", new Vector3(-18f, baseY + 0.012f, 5.5f),
                new Vector3(6f, 0.02f, 3.5f), rugMaterial, false);

            Box(root, "BallroomPianoBody", new Vector3(-18.5f, baseY + 0.45f, 6.2f),
                new Vector3(2.2f, 0.9f, 1.1f), woodDarkMaterial, true);
            Box(root, "BallroomPianoTop", new Vector3(-18.5f, baseY + 0.93f, 6.2f),
                new Vector3(2.3f, 0.06f, 1.2f), paperMaterial, false);
            Box(root, "BallroomPianoKeys", new Vector3(-18.5f, baseY + 0.97f, 5.75f),
                new Vector3(1.8f, 0.03f, 0.18f), paperMaterial, false);

            MansionProps.AddChandelier(root, new Vector3(-18f, baseY + 2.55f, 5.5f));
            Box(root, "BallroomSofa", new Vector3(-15.5f, baseY + 0.42f, 4.8f),
                new Vector3(0.9f, 0.55f, 2.4f), rugMaterial, true);
            Box(root, "BallroomSideTable", new Vector3(-16.2f, baseY + 0.4f, 6.8f),
                new Vector3(1f, 0.8f, 0.8f), woodMaterial, true);
            CreateNote(root, "BallroomNote", new Vector3(-16.2f, baseY + 0.87f, 6.8f), "宴会厅便签",
                "舞会已经取消了很多年。\n钢琴上的灰尘厚得像一层绒布。",
                "阅读宴会厅便签", "ballroom", 0f);

            MansionProps.AddPianoDetails(root, new Vector3(-18.5f, baseY, 6.2f), 0f);
            MansionProps.AddChair(root, new Vector3(-18.5f, baseY, 4.8f), 0f);
            MansionProps.AddRugBorder(root, new Vector3(-18f, baseY + 0.012f, 5.5f), 6f, 3.5f);
            MansionProps.AddConsoleTable(root, new Vector3(-14.8f, baseY, 3.2f), 90f, 1.8f);
            MansionProps.AddShelfUnit(root, new Vector3(-21.2f, baseY, 4f), 90f, 1.8f, 2f, 0.4f, 5);
            MansionProps.AddFloorLamp(root, new Vector3(-21.2f, baseY, 7.3f));
            MansionProps.AddVase(root, new Vector3(-14.8f, baseY + 0.84f, 3.2f), 2);
            MansionProps.AddMirror(root, new Vector3(-14.12f, baseY + 1.7f, 5.6f), 90f, 1.4f, 0.9f);
            MansionProps.AddCurtains(root, new Vector3(-21.8f, baseY + 1.6f, 5.2f), 90f, 2.4f);

            Box(root, "BallroomWindow", new Vector3(-21.9f, baseY + 2f, 5.2f),
                new Vector3(0.04f, 1.8f, 2.6f), glassMaterial, false);
        }

        private static void BuildServantsRoom(Transform root)
        {
            float baseY = UpperBaseY;
            Box(root, "ServantsShelf", new Vector3(-21.2f, baseY + 1.3f, -6.2f),
                new Vector3(0.7f, 2.2f, 3.6f), woodDarkMaterial, true);
            Box(root, "ServantsCrateA", new Vector3(-17.5f, baseY + 0.4f, -7.8f),
                new Vector3(1.1f, 0.8f, 1.1f), woodDarkMaterial, true);
            Box(root, "ServantsCrateB", new Vector3(-16.2f, baseY + 0.3f, -7.1f),
                new Vector3(0.7f, 0.6f, 0.7f), woodMaterial, true);
            Box(root, "ServantsBed", new Vector3(-18.5f, baseY + 0.35f, -4.2f),
                new Vector3(1.9f, 0.4f, 2.2f), woodDarkMaterial, true);
            Box(root, "ServantsTable", new Vector3(-15.8f, baseY + 0.4f, -5.6f),
                new Vector3(1f, 0.8f, 0.8f), woodMaterial, true);
            CreateNote(root, "ServantsNote", new Vector3(-15.8f, baseY + 0.87f, -5.6f), "仆役房便签",
                "主人把最后的钥匙交给了档案室。\n仆人们只负责守住这层走廊。",
                "阅读仆役房便签", "servants", 0f);
            Cylinder(root, "ServantsLamp", new Vector3(-20.8f, baseY + 0.7f, -4.4f),
                new Vector3(0.16f, 0.5f, 0.16f), Quaternion.identity, goldMaterial, false);

            MansionProps.AddBedDetails(root, new Vector3(-18.5f, baseY, -4.2f), 0f, 1.9f, 2.2f);
            MansionProps.AddTableLamp(root, new Vector3(-16.2f, baseY + 0.8f, -5.2f), 0.6f);
            MansionProps.AddCrateDetails(root, new Vector3(-17.5f, baseY + 0.4f, -7.8f),
                new Vector3(1.1f, 0.8f, 1.1f));
            MansionProps.AddCrateDetails(root, new Vector3(-16.2f, baseY + 0.3f, -7.1f),
                new Vector3(0.7f, 0.6f, 0.7f));
            MansionProps.AddDresser(root, new Vector3(-15f, baseY, -3f), 270f, 1.4f, 0.95f, true);
            MansionProps.AddSideTable(root, new Vector3(-20.5f, baseY, -3f));
            MansionProps.AddFloorLamp(root, new Vector3(-14.6f, baseY, -7.8f));
            MansionProps.AddVase(root, new Vector3(-20.5f, baseY + 0.74f, -3f), 3);
            MansionProps.AddPainting(root, new Vector3(-14.12f, baseY + 1.8f, -6.5f), 90f, 1.2f, 0.8f, 4);
            Box(root, "ServantsRug", new Vector3(-18.5f, baseY + 0.012f, -6.8f),
                new Vector3(3.4f, 0.02f, 2f), rugMaterial, false);
            MansionProps.AddDryingRack(root, new Vector3(-15.4f, baseY, -7.8f), 0f);
        }

        private static void BuildMusicRoom(Transform root)
        {
            float baseY = UpperBaseY;
            Box(root, "MusicRug", new Vector3(18f, baseY + 0.012f, 5.5f),
                new Vector3(5f, 0.02f, 3.2f), rugMaterial, false);
            Box(root, "MusicPianoBody", new Vector3(19.2f, baseY + 0.45f, 5.2f),
                new Vector3(2.2f, 0.9f, 1.1f), woodDarkMaterial, true);
            Box(root, "MusicPianoTop", new Vector3(19.2f, baseY + 0.93f, 5.2f),
                new Vector3(2.3f, 0.06f, 1.2f), paperMaterial, false);
            Box(root, "MusicPianoKeys", new Vector3(19.2f, baseY + 0.97f, 4.75f),
                new Vector3(1.8f, 0.03f, 0.18f), paperMaterial, false);

            CreateItemSpawnPoint(root, "Spawn_MusicBox", ItemIds.MusicBox,
                new Vector3(19.6f, baseY + 0.97f, 5.55f), true);
            CreateNote(root, "MusicNote", new Vector3(18.3f, baseY + 0.98f, 5.55f), "音乐室便签",
                "八音盒的曲子只写了一半。\n剩下的一半，似乎在客房的照片背后。",
                "阅读音乐室便签", "music", 0f);

            Box(root, "MusicStand", new Vector3(16.5f, baseY + 0.8f, 4.4f),
                new Vector3(0.5f, 1.2f, 0.1f), woodMaterial, false);

            MansionProps.AddPianoDetails(root, new Vector3(19.2f, baseY, 5.2f), 0f);
            MansionProps.AddChair(root, new Vector3(19.2f, baseY, 3.9f), 0f);
            MansionProps.AddGramophone(root, new Vector3(15.5f, baseY, 6.8f), 0f);
            MansionProps.AddRugBorder(root, new Vector3(18f, baseY + 0.012f, 5.5f), 5f, 3.2f);
            MansionProps.AddShelfUnit(root, new Vector3(15.5f, baseY, 8.3f), 0f, 2f, 2f, 0.4f, 3);
            MansionProps.AddConsoleTable(root, new Vector3(21.2f, baseY, 3f), 90f, 1.4f);
            MansionProps.AddFloorLamp(root, new Vector3(21f, baseY, 7.8f));
            MansionProps.AddVase(root, new Vector3(21.2f, baseY + 0.84f, 3f), 1);
            MansionProps.AddPainting(root, new Vector3(21.88f, baseY + 1.8f, 6.2f), 90f, 1.2f, 0.8f, 0);
            MansionProps.AddChair(root, new Vector3(15.3f, baseY, 4.2f), 90f, true);
        }

        private static void BuildGuestRoom(Transform root)
        {
            float baseY = UpperBaseY;
            Box(root, "GuestBedFrame", new Vector3(18f, baseY + 0.28f, -5.8f),
                new Vector3(2.2f, 0.35f, 3f), woodDarkMaterial, true);
            Box(root, "GuestBedMattress", new Vector3(18f, baseY + 0.52f, -5.8f),
                new Vector3(2f, 0.24f, 2.8f), paperMaterial, true);
            Box(root, "GuestPillow", new Vector3(18f, baseY + 0.7f, -7.0f),
                new Vector3(1.3f, 0.16f, 0.5f), paperMaterial, false);
            Box(root, "GuestHeadboard", new Vector3(18f, baseY + 0.9f, -7.35f),
                new Vector3(2.3f, 1f, 0.12f), woodMaterial, true);

            Box(root, "GuestNightstand", new Vector3(20f, baseY + 0.4f, -5.0f),
                new Vector3(0.8f, 0.8f, 0.8f), woodMaterial, true);
            CreateItemSpawnPoint(root, "Spawn_OldPhoto", ItemIds.OldPhoto,
                new Vector3(20f, baseY + 0.81f, -5.0f), true);
            CreateNote(root, "GuestNote", new Vector3(16.2f, baseY + 0.87f, -4.6f), "客房便签",
                "照片背后写着一行字：\n「四枚徽记，日、月、星、影。」",
                "阅读客房便签", "guest", 0f);

            Box(root, "GuestDesk", new Vector3(16.2f, baseY + 0.4f, -4.6f),
                new Vector3(1f, 0.8f, 0.8f), woodMaterial, true);
            Box(root, "GuestWardrobe", new Vector3(21f, baseY + 0.9f, -7.2f),
                new Vector3(0.6f, 1.8f, 1.6f), woodDarkMaterial, true);
            Box(root, "GuestWindow", new Vector3(21.9f, baseY + 2f, -5.2f),
                new Vector3(0.04f, 1.6f, 1.8f), glassMaterial, false);

            MansionProps.AddBedDetails(root, new Vector3(18f, baseY, -5.8f), 0f, 2f, 3f);
            MansionProps.AddTableLamp(root, new Vector3(19.55f, baseY + 0.8f, -5.25f), 0.55f);
            Box(root, "GuestRug", new Vector3(18f, baseY + 0.012f, -4.9f),
                new Vector3(3.2f, 0.02f, 2f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(18f, baseY + 0.012f, -4.9f), 3.2f, 2f);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(root, "GuestWardrobeDoor", new Vector3(20.68f, baseY + 0.9f, -7.2f + i * 0.38f),
                    new Vector3(0.03f, 1.55f, 0.68f), woodMaterial);
                Box(root, "GuestWardrobeHandle", new Vector3(20.65f, baseY + 0.9f, -7.2f + i * 0.2f),
                    new Vector3(0.03f, 0.18f, 0.04f), goldMaterial);
            }

            MansionProps.AddDresser(root, new Vector3(14.7f, baseY, -3.2f), 0f, 1.4f, 0.95f, true);
            MansionProps.AddChair(root, new Vector3(21f, baseY, -3.4f), 180f, true);
            MansionProps.AddFloorLamp(root, new Vector3(21.2f, baseY, -7.8f));
            MansionProps.AddLuggage(root, new Vector3(15f, baseY, -4.6f), 0f);
            MansionProps.AddPlant(root, new Vector3(14.6f, baseY, -8f), 0.8f);
            MansionProps.AddVase(root, new Vector3(14.7f, baseY + 1.0f, -3.2f), 2);
            MansionProps.AddPainting(root, new Vector3(21.88f, baseY + 1.8f, -6f), 90f, 1.2f, 0.8f, 3);
        }

        private static void BuildWestAnnex(Transform root)
        {
            BuildAnnexShell(root);
            BuildAnnexCorridorWalls(root);

            BuildWestLibrary(root);
            BuildWestBilliardRoom(root);
            BuildWestWineCellar(root);
            BuildWestBoilerRoom(root);
            BuildWestGreenhouse(root);
            BuildWestSmokingRoom(root);
        }

        private static void BuildAnnexShell(Transform root)
        {
            Box(root, "AnnexFloor", new Vector3(-28f, -0.1f, 0f), new Vector3(12f, 0.2f, 28f),
                floorMaterial, true);
            Box(root, "AnnexCeiling", new Vector3(-28f, WallHeight, 0f), new Vector3(12f, 0.2f, 28f),
                wallMaterial, true);
            Box(root, "AnnexWallWest", new Vector3(-34.05f, 1.65f, 0f),
                new Vector3(0.1f, WallHeight, 28f), wallMaterial, true);
            Box(root, "AnnexWallNorth", new Vector3(-28f, 1.65f, 14.05f),
                new Vector3(12.1f, WallHeight, 0.1f), wallMaterial, true);
            // 南墙统一 0.12 厚，门洞坐标取墙中心线（-14.05），门框两侧对称外露 4 cm。
            Box(root, "AnnexWallSouthA", new Vector3(-31.4f, 1.65f, -14.05f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexWallSouthB", new Vector3(-24.6f, 1.65f, -14.05f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            AddAnnexDoorAlongX(root, -28f, -14.05f);

            // 核心区西墙之外、上下延伸段的东侧边界
            Box(root, "AnnexWallEastNorth", new Vector3(-22.05f, 1.65f, 11.5f),
                new Vector3(0.1f, WallHeight, 5f), wallMaterial, true);
            Box(root, "AnnexWallEastSouth", new Vector3(-22.05f, 1.65f, -11.5f),
                new Vector3(0.1f, WallHeight, 5f), wallMaterial, true);
        }

        private static void BuildAnnexCorridorWalls(Transform root)
        {
            BuildAnnexMainWall(root, 2f);
            BuildAnnexMainWall(root, -2f);

            BuildAnnexVerticalWall(root, -30f);
            BuildAnnexVerticalWall(root, -27f);

            // 北侧横向走廊：房间北墙、走廊北墙与温室入口
            Box(root, "AnnexCrossNorthA", new Vector3(-32f, 1.65f, 8f),
                new Vector3(4f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexCrossNorthB", new Vector3(-24.5f, 1.65f, 8f),
                new Vector3(5f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexCrossNorthDoorA", new Vector3(-31.4f, 1.65f, 11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexCrossNorthDoorB", new Vector3(-24.6f, 1.65f, 11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            AddAnnexDoorAlongX(root, -28f, 11f);

            // 南侧横向走廊
            Box(root, "AnnexCrossSouthA", new Vector3(-32f, 1.65f, -8f),
                new Vector3(4f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexCrossSouthB", new Vector3(-24.5f, 1.65f, -8f),
                new Vector3(5f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexCrossSouthDoorA", new Vector3(-31.4f, 1.65f, -11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexCrossSouthDoorB", new Vector3(-24.6f, 1.65f, -11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            AddAnnexDoorAlongX(root, -28f, -11f);
        }

        private static void BuildAnnexMainWall(Transform root, float z)
        {
            Box(root, "AnnexMainWallA", new Vector3(-33.4f, 1.65f, z),
                new Vector3(1.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexMainWallB", new Vector3(-30.6f, 1.65f, z),
                new Vector3(1.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexMainWallC", new Vector3(-25.9f, 1.65f, z),
                new Vector3(2.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "AnnexMainWallD", new Vector3(-22.6f, 1.65f, z),
                new Vector3(1.2f, WallHeight, 0.12f), wallMaterial, true);

            AddAnnexDoorAlongX(root, -32f, z);
            AddAnnexDoorAlongX(root, -24f, z);
        }

        private static void BuildAnnexVerticalWall(Transform root, float x)
        {
            foreach (float z in new[] { 3.1f, 6.9f, -3.1f, -6.9f })
            {
                Box(root, "AnnexVerticalWall", new Vector3(x, 1.65f, z),
                    new Vector3(0.12f, WallHeight, 2.2f), wallMaterial, true);
            }

            AddAnnexDoorAlongZ(root, x, 5f);
            AddAnnexDoorAlongZ(root, x, -5f);
        }

        private static void AddAnnexDoorAlongX(Transform root, float x, float z)
        {
            Box(root, "AnnexDoorLintel", new Vector3(x, 2.85f, z),
                new Vector3(1.6f, 0.9f, 0.12f), wallMaterial, true);
            Box(root, "AnnexDoorTrimLeft", new Vector3(x - 0.82f, 1.19f, z),
                new Vector3(0.08f, 2.44f, 0.2f), woodDarkMaterial, false);
            Box(root, "AnnexDoorTrimRight", new Vector3(x + 0.82f, 1.19f, z),
                new Vector3(0.08f, 2.44f, 0.2f), woodDarkMaterial, false);
            Box(root, "AnnexDoorTrimTop", new Vector3(x, 2.42f, z),
                new Vector3(1.76f, 0.08f, 0.2f), woodDarkMaterial, false);
        }

        private static void AddAnnexDoorAlongZ(Transform root, float x, float z)
        {
            // 门楣厚度必须与墙同厚（0.12），否则会与 0.2 厚的门框横档侧面共面闪烁。
            Box(root, "AnnexDoorLintel", new Vector3(x, 2.85f, z),
                new Vector3(0.12f, 0.9f, 1.6f), wallMaterial, true);
            Box(root, "AnnexDoorTrimLeft", new Vector3(x, 1.19f, z - 0.82f),
                new Vector3(0.2f, 2.44f, 0.08f), woodDarkMaterial, false);
            Box(root, "AnnexDoorTrimRight", new Vector3(x, 1.19f, z + 0.82f),
                new Vector3(0.2f, 2.44f, 0.08f), woodDarkMaterial, false);
            Box(root, "AnnexDoorTrimTop", new Vector3(x, 2.42f, z),
                new Vector3(0.2f, 0.08f, 1.76f), woodDarkMaterial, false);
        }

        private static void BuildWestLibrary(Transform root)
        {
            Box(root, "WestLibraryRug", new Vector3(-32f, 0.012f, 5f), new Vector3(3.2f, 0.02f, 3.2f),
                rugMaterial, false);
            Box(root, "WestLibraryShelfBack", new Vector3(-33.8f, 1.5f, 5f),
                new Vector3(0.06f, 3f, 2.8f), woodDarkMaterial, true);
            Box(root, "WestLibraryShelfA", new Vector3(-33.5f, 0.7f, 5f),
                new Vector3(0.5f, 0.06f, 2.7f), woodMaterial, true);
            Box(root, "WestLibraryShelfB", new Vector3(-33.5f, 1.5f, 5f),
                new Vector3(0.5f, 0.06f, 2.7f), woodMaterial, true);
            Box(root, "WestLibraryShelfC", new Vector3(-33.5f, 2.3f, 5f),
                new Vector3(0.5f, 0.06f, 2.7f), woodMaterial, true);

            for (int i = 0; i < 10; i++)
            {
                float h = 0.18f + (i % 4) * 0.03f;
                Box(root, "WestLibraryBook", new Vector3(-33.3f, 0.75f + h * 0.5f, 3.7f + i * 0.18f),
                    new Vector3(0.25f, h, 0.1f), bookMaterials[i % bookMaterials.Length], false);
            }

            Box(root, "WestLibraryDesk", new Vector3(-32f, 0.82f, 6.5f),
                new Vector3(1.7f, 0.08f, 1f), woodMaterial, true);
            Box(root, "WestLibraryDeskLeg", new Vector3(-32f, 0.39f, 6.5f),
                new Vector3(0.3f, 0.78f, 0.3f), woodDarkMaterial, true);

            CreateNote(root, "WestLibraryNote", new Vector3(-32f, 0.88f, 6.5f), "藏书室便签",
                "庄园主把建筑图纸锁在了楼上的档案室。\n西翼的走廊像一张打结的网。",
                "阅读藏书室便签", "library", 0f);

            MansionProps.AddDeskDetails(root, new Vector3(-32f, 0f, 6.5f), 180f, 1.7f);
            MansionProps.AddChair(root, new Vector3(-32f, 0f, 5.5f), 0f);
            MansionProps.AddRugBorder(root, new Vector3(-32f, 0.012f, 5f), 3.2f, 3.2f);
            MansionProps.AddPlant(root, new Vector3(-33.2f, 0f, 3.1f), 0.8f);
            MansionProps.AddChair(root, new Vector3(-31.2f, 0f, 4.3f), 270f, true);
            MansionProps.AddSideTable(root, new Vector3(-32.2f, 0f, 3.4f));
            MansionProps.AddFloorLamp(root, new Vector3(-30.4f, 0f, 6.8f));
            MansionProps.AddGlobe(root, new Vector3(-32.7f, 0.9f, 6.0f));
            MansionProps.AddBookStack(root, new Vector3(-31.2f, 0.9f, 6.5f), 3);
            MansionProps.AddVase(root, new Vector3(-32.2f, 0.74f, 3.4f), 3);
            MansionProps.AddPainting(root, new Vector3(-30.12f, 1.8f, 7.1f), 90f, 1.2f, 0.8f, 2);
        }

        private static void BuildWestBilliardRoom(Transform root)
        {
            Box(root, "WestBilliardRug", new Vector3(-24.5f, 0.012f, 5f), new Vector3(3.6f, 0.02f, 3f),
                rugMaterial, false);

            Box(root, "WestBilliardTable", new Vector3(-24.5f, 0.78f, 5f),
                new Vector3(2.4f, 0.1f, 1.2f), bookMaterials[3], true);
            Box(root, "WestBilliardTableBody", new Vector3(-24.5f, 0.4f, 5f),
                new Vector3(2.2f, 0.7f, 1f), woodDarkMaterial, true);

            for (int i = 0; i < 6; i++)
            {
                Sphere(root, "BilliardBall", new Vector3(-25.4f + i * 0.12f, 0.86f, 5f),
                    new Vector3(0.1f, 0.1f, 0.1f), bookMaterials[i % bookMaterials.Length]);
            }

            Cylinder(root, "BilliardCue", new Vector3(-23.2f, 1.2f, 6.6f),
                new Vector3(0.04f, 1f, 0.04f), Quaternion.Euler(0f, 0f, 62f), woodMaterial, false);

            Box(root, "WestBilliardSideTable", new Vector3(-23.2f, 0.4f, 6.8f),
                new Vector3(1f, 0.8f, 0.8f), woodMaterial, true);
            CreateNote(root, "WestBilliardNote", new Vector3(-23.2f, 0.87f, 6.8f), "台球室便签",
                "最后一局棋没有下完。\n球杆被靠在墙边，像是在等谁回来。",
                "阅读台球室便签", "billiard", 0f);

            MansionProps.AddPoolTableDetails(root, new Vector3(-24.5f, 0f, 5f));
            MansionProps.AddChair(root, new Vector3(-24.5f, 0f, 3.4f), 0f);
            MansionProps.AddRugBorder(root, new Vector3(-24.5f, 0.012f, 5f), 3.6f, 3f);
            MansionProps.AddConsoleTable(root, new Vector3(-22.6f, 0f, 7f), 90f, 1.6f);
            MansionProps.AddBarStool(root, new Vector3(-26.2f, 0f, 3f), 0f);
            MansionProps.AddBarStool(root, new Vector3(-22.8f, 0f, 3.1f), 90f);
            MansionProps.AddFloorLamp(root, new Vector3(-26.7f, 0f, 7.4f));
            MansionProps.AddVase(root, new Vector3(-22.6f, 0.84f, 7f), 3);
            MansionProps.AddPainting(root, new Vector3(-22.1f, 1.8f, 4.8f), 90f, 1.2f, 0.8f, 1);
        }

        private static void BuildWestWineCellar(Transform root)
        {
            Box(root, "WestWineRackA", new Vector3(-33.6f, 1f, -5.5f),
                new Vector3(0.5f, 2f, 3.2f), woodDarkMaterial, true);
            Box(root, "WestWineRackB", new Vector3(-33.6f, 1f, -2.5f),
                new Vector3(0.5f, 2f, 2f), woodDarkMaterial, true);

            for (int i = 0; i < 6; i++)
            {
                Vector3 barrelPos = new Vector3(-31.8f + (i % 2) * 0.7f, 0.45f, -7.3f + (i / 2) * 0.8f);
                Cylinder(root, "WineBarrel", barrelPos, new Vector3(0.45f, 0.45f, 0.45f),
                    Quaternion.Euler(0f, 0f, 90f), woodDarkMaterial, true);
                MansionProps.AddBarrelDetails(root, barrelPos);
            }

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    Cylinder(root, "RackBottle", new Vector3(-33.3f, 0.7f + row * 0.5f, -6.5f + col * 0.5f),
                        new Vector3(0.04f, 0.16f, 0.04f), Quaternion.Euler(0f, 0f, 90f), glassMaterial);
                }
            }

            Box(root, "WestWineTable", new Vector3(-32f, 0.8f, -5f),
                new Vector3(1.5f, 0.08f, 0.9f), woodMaterial, true);
            Box(root, "WestWineTableLeg", new Vector3(-32f, 0.39f, -5f),
                new Vector3(0.3f, 0.78f, 0.3f), woodDarkMaterial, true);

            CreateItemSpawnPoint(root, "Spawn_WineBottle", ItemIds.WineBottle,
                new Vector3(-32f, 0.85f, -5f), true);
            CreateNote(root, "WestWineNote", new Vector3(-32.5f, 0.95f, -6.2f), "酒窖便签",
                "年份最好的一瓶酒还留在架子上。\n酒标上画着和封印大门相同的徽记。",
                "阅读酒窖便签", "wine", 0f);

            MansionProps.AddBarStool(root, new Vector3(-31.2f, 0f, -4f), 0f);
            MansionProps.AddBarStool(root, new Vector3(-31.2f, 0f, -6f), 180f);
            MansionProps.AddFloorLamp(root, new Vector3(-30.4f, 0f, -7.8f));
            MansionProps.AddShelfUnit(root, new Vector3(-30.4f, 0f, -3f), 90f, 1.4f, 1.8f, 0.4f, 4);
            Box(root, "WineCrateExtra", new Vector3(-33.5f, 0.35f, -2.6f),
                new Vector3(0.8f, 0.7f, 0.8f), woodDarkMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(-33.5f, 0.35f, -2.6f), new Vector3(0.8f, 0.7f, 0.8f));
            Box(root, "WineFloorMat", new Vector3(-32f, 0.012f, -5.2f),
                new Vector3(3f, 0.02f, 2.2f), rugMaterial, false);
        }

        private static void BuildWestBoilerRoom(Transform root)
        {
            Cylinder(root, "BoilerTankA", new Vector3(-23.5f, 0.9f, -6.2f),
                new Vector3(0.8f, 0.9f, 0.8f), Quaternion.identity, metalMaterial, true);
            Cylinder(root, "BoilerTankB", new Vector3(-23.5f, 0.6f, -4.9f),
                new Vector3(0.45f, 0.6f, 0.45f), Quaternion.identity, metalMaterial, true);

            for (int i = 0; i < 4; i++)
            {
                Cylinder(root, "BoilerPipe", new Vector3(-26.8f + i * 0.1f, 1.4f, -6.8f),
                    new Vector3(0.08f, 1.2f, 0.08f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, false);
            }

            Box(root, "BoilerCrate", new Vector3(-25.2f, 0.4f, -7.4f),
                new Vector3(1f, 0.8f, 1f), woodDarkMaterial, true);
            CreateNote(root, "WestBoilerNote", new Vector3(-25.2f, 0.87f, -7.4f), "锅炉房便签",
                "管线锈死了大半。\n这里是整座庄园最早断电的地方。",
                "阅读锅炉房便签", "boiler", 0f);

            Box(root, "BoilerPressureGauge", new Vector3(-23.4f, 1.8f, -6.2f),
                new Vector3(0.3f, 0.3f, 0.06f), goldMaterial, false);

            MansionProps.AddBoilerDetails(root);
            MansionProps.AddCrateDetails(root, new Vector3(-25.2f, 0.4f, -7.4f), new Vector3(1f, 0.8f, 1f));
            MansionProps.AddConsoleTable(root, new Vector3(-22.6f, 0f, -3f), 90f, 1.4f);
            MansionProps.AddFloorLamp(root, new Vector3(-22.4f, 0f, -7.6f));
            Cylinder(root, "BoilerExtraBarrel", new Vector3(-26.2f, 0.45f, -3.2f),
                new Vector3(0.45f, 0.45f, 0.45f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, true);
            MansionProps.AddBarrelDetails(root, new Vector3(-26.2f, 0.45f, -3.2f));
            MansionProps.AddPainting(root, new Vector3(-22.1f, 1.8f, -6f), 90f, 1.2f, 0.8f, 6);
            Box(root, "BoilerFloorMat", new Vector3(-24.5f, 0.012f, -5f),
                new Vector3(4f, 0.02f, 2.6f), rugMaterial, false);
            MansionProps.AddCoalPile(root, new Vector3(-26.8f, 0f, -6.8f));
            MansionProps.AddWallSign(root, new Vector3(-22.1f, 1.9f, -4.5f), 90f, 7);
            MansionProps.AddFireExtinguisher(root, new Vector3(-22.6f, 0f, -7.6f));
        }

        private static void BuildWestGreenhouse(Transform root)
        {
            Box(root, "GreenhousePlanterA", new Vector3(-31.5f, 0.4f, 12.5f),
                new Vector3(2.6f, 0.8f, 1.2f), woodDarkMaterial, true);
            Box(root, "GreenhousePlanterB", new Vector3(-24.5f, 0.4f, 12.5f),
                new Vector3(2.6f, 0.8f, 1.2f), woodDarkMaterial, true);

            for (int i = 0; i < 6; i++)
            {
                Sphere(root, "GreenhousePlant", new Vector3(-32.2f + i * 0.28f,
                    1.05f + (i % 2) * 0.1f, 12.3f), new Vector3(0.36f, 0.5f, 0.36f),
                    bookMaterials[i % bookMaterials.Length]);
            }

            Box(root, "GreenhouseGlassA", new Vector3(-32.5f, 1.8f, 13.9f),
                new Vector3(4f, 2f, 0.04f), glassMaterial, false);
            Box(root, "GreenhouseGlassB", new Vector3(-23.5f, 1.8f, 13.9f),
                new Vector3(4f, 2f, 0.04f), glassMaterial, false);

            Box(root, "GreenhouseTable", new Vector3(-28f, 0.4f, 12.5f),
                new Vector3(1.2f, 0.8f, 0.8f), woodMaterial, true);
            CreateNote(root, "WestGreenhouseNote", new Vector3(-28f, 0.87f, 12.5f), "温室便签",
                "植物早已枯萎，玻璃上却还留着手写的字：\n「沿走廊向西，尽头是封印。」",
                "阅读温室便签", "greenhouse", 0f);

            MansionProps.AddPlant(root, new Vector3(-33f, 0f, 11.8f), 0.9f);
            MansionProps.AddPlant(root, new Vector3(-23f, 0f, 11.8f), 0.9f);
            MansionProps.AddBench(root, new Vector3(-28f, 0f, 13.2f), 180f, 2f, false);
            MansionProps.AddConsoleTable(root, new Vector3(-33f, 0f, 12.4f), 90f, 1.8f);
            MansionProps.AddVase(root, new Vector3(-33f, 0.84f, 12.4f), 1);
            MansionProps.AddHangingPlant(root, new Vector3(-32.5f, 3.2f, 12.5f), 0.9f);
            MansionProps.AddHangingPlant(root, new Vector3(-23.5f, 3.2f, 12.5f), 0.9f);
            MansionProps.AddFloorLamp(root, new Vector3(-22.8f, 0f, 12.4f));
        }

        private static void BuildWestSmokingRoom(Transform root)
        {
            MansionProps.AddChair(root, new Vector3(-32f, 0f, -12.5f), 90f, true);
            MansionProps.AddChair(root, new Vector3(-24f, 0f, -12.5f), 270f, true);
            Box(root, "SmokingTable", new Vector3(-28f, 0.75f, -12.5f),
                new Vector3(1.2f, 0.08f, 0.9f), woodMaterial, true);
            Box(root, "SmokingTableLeg", new Vector3(-28f, 0.37f, -12.5f),
                new Vector3(0.3f, 0.74f, 0.3f), woodDarkMaterial, true);
            Cylinder(root, "SmokingAshtray", new Vector3(-28f, 0.85f, -12.5f),
                new Vector3(0.3f, 0.05f, 0.3f), Quaternion.identity, metalMaterial, false);

            CreateNote(root, "WestSmokingNote", new Vector3(-26.8f, 0.87f, -12.5f), "吸烟室便签",
                "最后一位客人离开后，这里再没人来过。\n墙上的徽记却一直亮着。",
                "阅读吸烟室便签", "smoking", 0f);

            Box(root, "SmokingMirror", new Vector3(-22.1f, 1.6f, -12.5f),
                new Vector3(0.04f, 1.8f, 2f), glassMaterial, false);
            Box(root, "SmokingRug", new Vector3(-28f, 0.012f, -12.5f),
                new Vector3(6f, 0.02f, 2.2f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(-28f, 0.012f, -12.5f), 6f, 2.2f);
            MansionProps.AddConsoleTable(root, new Vector3(-33.2f, 0f, -13.2f), 90f, 1.5f);
            MansionProps.AddMirror(root, new Vector3(-33.92f, 1.7f, -13.2f), 90f, 1.2f, 0.8f);
            MansionProps.AddFloorLamp(root, new Vector3(-24.8f, 0f, -13.3f));
            MansionProps.AddPlant(root, new Vector3(-22.7f, 0f, -13.2f), 0.8f);
            MansionProps.AddVase(root, new Vector3(-33.2f, 0.84f, -13.2f), 4);
            MansionProps.AddPainting(root, new Vector3(-30f, 1.8f, -11.12f), 0f, 1.2f, 0.8f, 5);
        }

        private static void BuildUpperEastLobby(Transform root)
        {
            float baseY = UpperBaseY;

            Box(root, "UpperElevatorLobbyFloor", new Vector3(28f, baseY - 0.1f, 0f),
                new Vector3(12f, 0.2f, 4.2f), floorMaterial, true);
            Box(root, "UpperElevatorLobbyCeiling", new Vector3(28f, baseY + WallHeight, 0f),
                new Vector3(12f, 0.2f, 4.2f), wallMaterial, true);
            Box(root, "UpperElevatorLobbyWallEast", new Vector3(34.05f, baseY + 1.65f, 0f),
                new Vector3(0.1f, WallHeight, 4.2f), wallMaterial, true);
            Box(root, "UpperElevatorLobbyWallNorth", new Vector3(28f, baseY + 1.65f, 2.05f),
                new Vector3(12.1f, WallHeight, 0.1f), wallMaterial, true);
            Box(root, "UpperElevatorLobbyWallSouth", new Vector3(28f, baseY + 1.65f, -2.05f),
                new Vector3(12.1f, WallHeight, 0.1f), wallMaterial, true);

            Box(root, "UpperElevatorLobbyRug", new Vector3(31.8f, baseY + 0.012f, 0f),
                new Vector3(4f, 0.02f, 2.6f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(31.8f, baseY + 0.012f, 0f), 4f, 2.6f);

            // 踢脚线西端离主楼东墙 6 cm，避免与墙面共面闪烁。
            Box(root, "UpperElevatorLobbyTrimNorth", new Vector3(28.08f, baseY + 0.08f, 1.99f),
                new Vector3(11.84f, 0.16f, 0.06f), woodDarkMaterial, false);
            Box(root, "UpperElevatorLobbyTrimSouth", new Vector3(28.08f, baseY + 0.08f, -1.99f),
                new Vector3(11.84f, 0.16f, 0.06f), woodDarkMaterial, false);
        }

        private static void BuildEastWing(Transform root)
        {
            BuildEastWingShell(root);
            BuildEastWingCorridorWalls(root);

            BuildEastLaundry(root);
            BuildEastPantry(root);
            BuildEastWorkshop(root);
            BuildEastIceRoom(root);
            BuildEastSunRoom(root);
            BuildEastWoodStore(root);
        }

        private static void BuildEastWingShell(Transform root)
        {
            Box(root, "EastAnnexFloor", new Vector3(28f, -0.1f, 0f), new Vector3(12f, 0.2f, 28f),
                floorMaterial, true);
            Box(root, "EastAnnexCeiling", new Vector3(28f, WallHeight, 0f), new Vector3(12f, 0.2f, 28f),
                wallMaterial, true);
            Box(root, "EastAnnexWallEast", new Vector3(34.05f, 1.65f, 0f),
                new Vector3(0.1f, WallHeight, 28f), wallMaterial, true);
            Box(root, "EastAnnexWallNorth", new Vector3(28f, 1.65f, 14.05f),
                new Vector3(12.1f, WallHeight, 0.1f), wallMaterial, true);
            Box(root, "EastAnnexWallSouthA", new Vector3(24.6f, 1.65f, -14.05f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastAnnexWallSouthB", new Vector3(31.4f, 1.65f, -14.05f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            AddAnnexDoorAlongX(root, 28f, -14.05f);

            Box(root, "EastAnnexWallWestNorth", new Vector3(22.05f, 1.65f, 11.5f),
                new Vector3(0.1f, WallHeight, 5f), wallMaterial, true);
            Box(root, "EastAnnexWallWestSouth", new Vector3(22.05f, 1.65f, -11.5f),
                new Vector3(0.1f, WallHeight, 5f), wallMaterial, true);

            MansionProps.AddRugBorder(root, new Vector3(28f, 0.012f, 0f), 4f, 1.2f);
            Box(root, "LowerElevatorLobbyRug", new Vector3(31.8f, 0.012f, 0f),
                new Vector3(4f, 0.02f, 2.6f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(31.8f, 0.012f, 0f), 4f, 2.6f);
            MansionProps.AddConsoleTable(root, new Vector3(28f, 0f, 1.62f), 0f, 1.4f, 0.35f);
            MansionProps.AddVase(root, new Vector3(28f, 0.84f, 1.62f), 1);
        }

        private static void BuildEastWingCorridorWalls(Transform root)
        {
            BuildEastWingMainWall(root, 2f);
            BuildEastWingMainWall(root, -2f);

            BuildEastWingVerticalWall(root, 27f);
            BuildEastWingVerticalWall(root, 30f);

            Box(root, "EastCrossNorthA", new Vector3(32f, 1.65f, 8f),
                new Vector3(4f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastCrossNorthB", new Vector3(24.5f, 1.65f, 8f),
                new Vector3(5f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastCrossNorthDoorA", new Vector3(31.4f, 1.65f, 11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastCrossNorthDoorB", new Vector3(24.6f, 1.65f, 11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            AddAnnexDoorAlongX(root, 28f, 11f);

            Box(root, "EastCrossSouthA", new Vector3(32f, 1.65f, -8f),
                new Vector3(4f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastCrossSouthB", new Vector3(24.5f, 1.65f, -8f),
                new Vector3(5f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastCrossSouthDoorA", new Vector3(31.4f, 1.65f, -11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastCrossSouthDoorB", new Vector3(24.6f, 1.65f, -11f),
                new Vector3(5.2f, WallHeight, 0.12f), wallMaterial, true);
            AddAnnexDoorAlongX(root, 28f, -11f);

            MansionProps.AddPainting(root, new Vector3(22.1f, 1.8f, 4.8f), 90f, 1.2f, 0.8f, 2);
            MansionProps.AddPainting(root, new Vector3(33.9f, 1.8f, 7.8f), -90f, 1.2f, 0.8f, 5);
            MansionProps.AddFloorLamp(root, new Vector3(28.4f, 0f, 0.8f));
        }

        private static void BuildEastWingMainWall(Transform root, float z)
        {
            Box(root, "EastMainWallA", new Vector3(22.6f, 1.65f, z),
                new Vector3(1.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastMainWallB", new Vector3(25.9f, 1.65f, z),
                new Vector3(2.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastMainWallC", new Vector3(30.6f, 1.65f, z),
                new Vector3(1.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "EastMainWallD", new Vector3(33.4f, 1.65f, z),
                new Vector3(1.2f, WallHeight, 0.12f), wallMaterial, true);

            AddAnnexDoorAlongX(root, 24f, z);
            AddAnnexDoorAlongX(root, 32f, z);
        }

        private static void BuildEastWingVerticalWall(Transform root, float x)
        {
            foreach (float z in new[] { 3.1f, 6.9f, -3.1f, -6.9f })
            {
                Box(root, "EastWingVerticalWall", new Vector3(x, 1.65f, z),
                    new Vector3(0.12f, WallHeight, 2.2f), wallMaterial, true);
            }

            AddAnnexDoorAlongZ(root, x, 5f);
            AddAnnexDoorAlongZ(root, x, -5f);
        }

        private static void BuildEastLaundry(Transform root)
        {
            Cylinder(root, "LaundryTubA", new Vector3(32.3f, 0.4f, 3.6f),
                new Vector3(0.5f, 0.4f, 0.5f), Quaternion.identity, metalMaterial, true);
            Cylinder(root, "LaundryTubB", new Vector3(32.3f, 0.4f, 4.6f),
                new Vector3(0.45f, 0.4f, 0.45f), Quaternion.identity, metalMaterial, true);
            Cylinder(root, "LaundryTubRimA", new Vector3(32.3f, 0.8f, 3.6f),
                new Vector3(0.53f, 0.025f, 0.53f), Quaternion.identity, copperMaterial, false);
            Cylinder(root, "LaundryTubRimB", new Vector3(32.3f, 0.8f, 4.6f),
                new Vector3(0.48f, 0.025f, 0.48f), Quaternion.identity, copperMaterial, false);
            Cylinder(root, "LaundryTubWaterA", new Vector3(32.3f, 0.76f, 3.6f),
                new Vector3(0.44f, 0.006f, 0.44f), Quaternion.identity, bookMaterials[1], false);
            Sphere(root, "LaundryTubClothB", new Vector3(32.3f, 0.72f, 4.6f),
                new Vector3(0.4f, 0.08f, 0.4f), fabricMaterial);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(root, "LaundryTubLeg", new Vector3(32.3f + x * 0.32f, 0.18f,
                        3.6f + z * 0.32f), new Vector3(0.06f, 0.36f, 0.06f), metalMaterial, false);
                    Box(root, "LaundryTubLegB", new Vector3(32.3f + x * 0.28f, 0.18f,
                        4.6f + z * 0.28f), new Vector3(0.05f, 0.36f, 0.05f), metalMaterial, false);
                }
            }

            Box(root, "LaundryWashboard", new Vector3(31.1f, 0.45f, 3.0f),
                new Vector3(0.5f, 0.8f, 0.04f), woodLightMaterial, false,
                Quaternion.Euler(0f, 15f, 12f));
            Box(root, "LaundryWashboardFrame", new Vector3(31.1f, 0.45f, 2.97f),
                new Vector3(0.56f, 0.86f, 0.03f), woodDarkMaterial, false,
                Quaternion.Euler(0f, 15f, 12f));
            Cylinder(root, "LaundryBasket", new Vector3(30.9f, 0.28f, 4.2f),
                new Vector3(0.34f, 0.26f, 0.34f), Quaternion.identity, woodLightMaterial, false);
            for (int i = 0; i < 3; i++)
            {
                Cylinder(root, "LaundryBasketBand", new Vector3(30.9f, 0.08f + i * 0.16f, 4.2f),
                    new Vector3(0.355f, 0.012f, 0.355f), Quaternion.identity, woodDarkMaterial, false);
            }
            Box(root, "LaundryBasketHandle", new Vector3(30.9f, 0.56f, 3.92f),
                new Vector3(0.5f, 0.03f, 0.03f), woodDarkMaterial, false);
            Box(root, "LaundryBasketHandleB", new Vector3(30.9f, 0.56f, 4.48f),
                new Vector3(0.5f, 0.03f, 0.03f), woodDarkMaterial, false);
            Sphere(root, "LaundryBasketClothA", new Vector3(30.9f, 0.55f, 4.2f),
                new Vector3(0.4f, 0.15f, 0.4f), fabricMaterial);
            Box(root, "LaundrySoap", new Vector3(31.2f, 0.86f, 6.3f),
                new Vector3(0.18f, 0.08f, 0.12f), porcelainMaterial, false);
            Box(root, "LaundryShelf", new Vector3(31.2f, 1.4f, 7.3f),
                new Vector3(2.2f, 0.08f, 0.5f), woodMaterial, true);
            for (int i = 0; i < 3; i++)
            {
                Box(root, "LaundryCloth", new Vector3(30.6f + i * 0.55f, 1.1f, 7.3f),
                    new Vector3(0.4f, 0.5f, 0.04f), paperMaterial, false);
            }

            Box(root, "LaundryNoteTable", new Vector3(31f, 0.4f, 6.2f),
                new Vector3(1.1f, 0.8f, 0.7f), woodDarkMaterial, true);
            CreateNote(root, "LaundryNote", new Vector3(31f, 0.88f, 6.2f), "洗衣房便签",
                "地下室钥匙在洗衣柜的最下层。\n锅炉房的地窖门后面，就是庄园的地下部分。",
                "阅读洗衣房便签", "laundry", 0f);

            AddOpenableCabinet(root, new Vector3(33.4f, 0f, 5.8f), -90f, "洗衣柜",
                new[] { ItemIds.BasementKey });
        }

        private static void BuildEastPantry(Transform root)
        {
            MansionProps.AddShelfUnit(root, new Vector3(30.2f, 0f, -3.4f), 90f, 2.2f, 1.8f, 0.45f, 3);
            MansionProps.AddShelfUnit(root, new Vector3(33.6f, 0f, -4.0f), 90f, 2.0f, 1.9f, 0.4f, 1);
            Box(root, "PantryCrateA", new Vector3(32.6f, 0.35f, -7.2f),
                new Vector3(0.8f, 0.7f, 0.8f), woodMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(32.6f, 0.35f, -7.2f), new Vector3(0.8f, 0.7f, 0.8f));
            for (int i = 0; i < 5; i++)
            {
                Cylinder(root, "PantryJar", new Vector3(31.4f + i * 0.22f, 0.86f, -3.2f),
                    new Vector3(0.09f, 0.12f, 0.09f), Quaternion.identity, porcelainMaterial, false);
                Cylinder(root, "PantryJarLid", new Vector3(31.4f + i * 0.22f, 0.99f, -3.2f),
                    new Vector3(0.095f, 0.018f, 0.095f), Quaternion.identity, goldMaterial, false);
                Box(root, "PantryJarLabel", new Vector3(31.4f + i * 0.22f, 0.86f, -3.105f),
                    new Vector3(0.1f, 0.1f, 0.012f), paperMaterial, false);
            }

            AddOpenableDrawer(root, new Vector3(33.4f, 0f, -6.3f), -90f, "食品柜抽屉",
                RandomContainerLoot);
        }

        private static void BuildEastWorkshop(Transform root)
        {
            Box(root, "EastWorkshopBench", new Vector3(24.4f, 0.78f, 4.8f),
                new Vector3(2.2f, 0.1f, 0.9f), woodDarkMaterial, true);
            Box(root, "EastWorkshopBenchLegA", new Vector3(23.5f, 0.38f, 4.8f),
                new Vector3(0.1f, 0.76f, 0.8f), metalMaterial, true);
            Box(root, "EastWorkshopBenchLegB", new Vector3(25.3f, 0.38f, 4.8f),
                new Vector3(0.1f, 0.76f, 0.8f), metalMaterial, true);
            Box(root, "WorkshopUnderCabinet", new Vector3(24.4f, 0.32f, 4.8f),
                new Vector3(1.86f, 0.55f, 0.7f), woodDarkMaterial, false);
            for (int i = 0; i < 2; i++)
            {
                Box(root, "WorkshopCabinetDoor", new Vector3(23.9f + i * 1f, 0.3f, 5.17f),
                    new Vector3(0.8f, 0.42f, 0.025f), woodLightMaterial, false);
                Box(root, "WorkshopCabinetHandle", new Vector3(23.9f + i * 1f, 0.32f, 5.2f),
                    new Vector3(0.18f, 0.025f, 0.025f), goldMaterial, false);
            }
            Box(root, "WorkshopVise", new Vector3(25.4f, 0.9f, 4.6f),
                new Vector3(0.2f, 0.2f, 0.3f), metalMaterial, false);
            Cylinder(root, "WorkshopViseHandle", new Vector3(25.4f, 0.95f, 4.4f),
                new Vector3(0.02f, 0.18f, 0.02f), Quaternion.Euler(0f, 0f, 90f), copperMaterial, false);
            Box(root, "WorkshopPegboard", new Vector3(24.4f, 1.85f, 7.82f),
                new Vector3(2f, 1.05f, 0.045f), woodDarkMaterial, false);
            for (int i = 0; i < 5; i++)
            {
                Cylinder(root, "WorkshopHangingTool", new Vector3(23.6f + i * 0.38f, 1.75f, 7.77f),
                    new Vector3(0.022f, 0.2f, 0.022f), Quaternion.identity, metalMaterial, false);
                Box(root, "WorkshopToolHead", new Vector3(23.6f + i * 0.38f, 1.52f, 7.77f),
                    new Vector3(0.1f, 0.06f, 0.05f), copperMaterial, false);
            }
            Box(root, "WorkshopWoodShavings", new Vector3(24.4f, 0.78f, 4.2f),
                new Vector3(0.7f, 0.025f, 0.25f), paperMaterial, false);
            for (int i = 0; i < 4; i++)
            {
                Cylinder(root, "WorkshopTool", new Vector3(23.8f + i * 0.35f, 0.92f, 4.8f),
                    new Vector3(0.03f, 0.18f, 0.03f), Quaternion.identity, metalMaterial, false);
            }
            MansionProps.AddToolCart(root, new Vector3(25.8f, 0f, 3.2f), 0f);
            CreateNote(root, "EastWorkshopNote", new Vector3(24.4f, 0.86f, 4.8f), "工匠便签",
                "电梯的传动齿轮被拆到地下机械间维修。\n没有它，电梯只会发出空转声。",
                "阅读工匠便签", "workshop", 0f);

            AddOpenableCabinet(root, new Vector3(22.6f, 0f, 6.8f), 90f, "工具柜",
                RandomContainerLoot);
        }

        private static void BuildEastIceRoom(Transform root)
        {
            for (int i = 0; i < 4; i++)
            {
                Box(root, "IceBlock", new Vector3(23.4f - (i % 2) * 0.5f, 0.25f + (i / 2) * 0.55f, -5.6f),
                    new Vector3(0.7f, 0.5f, 0.7f), glassMaterial, true);
            }
            Box(root, "IceRoomCrate", new Vector3(26.2f, 0.4f, -7.2f),
                new Vector3(0.9f, 0.8f, 0.9f), woodDarkMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(26.2f, 0.4f, -7.2f), new Vector3(0.9f, 0.8f, 0.9f));
            MansionProps.AddShelfUnit(root, new Vector3(23.8f, 0f, -7.6f), 0f, 2f, 1.9f, 0.4f, 2);

            AddOpenableCabinet(root, new Vector3(22.6f, 0f, -6.5f), 90f, "冰窖木柜",
                RandomContainerLoot);
        }

        private static void BuildEastSunRoom(Transform root)
        {
            MansionProps.AddPlant(root, new Vector3(27.4f, 0f, 9.3f), 0.8f);
            MansionProps.AddPlant(root, new Vector3(28.6f, 0f, 9.4f), 0.9f);
            MansionProps.AddPlant(root, new Vector3(28.2f, 0f, 11.8f), 0.7f);
            Box(root, "SunRoomGlassA", new Vector3(29.9f, 2f, 12f),
                new Vector3(0.04f, 1.7f, 2.4f), glassMaterial, false);
            Box(root, "SunRoomGlassFrameA", new Vector3(29.84f, 2f, 12f),
                new Vector3(0.06f, 1.75f, 0.1f), woodDarkMaterial, false);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(root, "SunRoomMullion", new Vector3(29.86f, 2f, 12f + i * 0.8f),
                    new Vector3(0.05f, 1.7f, 0.05f), woodLightMaterial, false);
            }
            Box(root, "SunRoomMullionHorizontal", new Vector3(29.86f, 2f, 12f),
                new Vector3(0.05f, 0.05f, 2.4f), woodLightMaterial, false);
            Cylinder(root, "WateringCanBody", new Vector3(26.9f, 0.18f, 12.5f),
                new Vector3(0.16f, 0.16f, 0.16f), Quaternion.identity, copperMaterial, false);
            Cylinder(root, "WateringCanSpout", new Vector3(27.12f, 0.22f, 12.5f),
                new Vector3(0.025f, 0.24f, 0.025f), Quaternion.Euler(0f, 0f, 65f), copperMaterial, false);

            AddOpenableDrawer(root, new Vector3(28f, 0f, 13.3f), 180f, "日光房抽屉",
                RandomContainerLoot);
        }

        private static void BuildEastWoodStore(Transform root)
        {
            for (int i = 0; i < 6; i++)
            {
                Cylinder(root, "FirewoodLog", new Vector3(27.4f + (i % 3) * 0.28f,
                    0.25f + (i / 3) * 0.3f, -11.5f),
                    new Vector3(0.14f, 0.5f, 0.14f), Quaternion.Euler(0f, 0f, 90f), woodDarkMaterial, true);
            }
            Box(root, "WoodStoreCrateA", new Vector3(28.4f, 0.4f, -12.4f),
                new Vector3(1f, 0.8f, 1f), woodDarkMaterial, true);
            MansionProps.AddCrateDetails(root, new Vector3(28.4f, 0.4f, -12.4f), new Vector3(1f, 0.8f, 1f));
            CreateNote(root, "EastWoodStoreNote", new Vector3(28.4f, 0.88f, -12.4f), "柴房便签",
                "地下室的空气又冷又潮。\n记得先去锅炉房拿手电筒，否则什么都看不见。",
                "阅读柴房便签", "woodstore", 0f);

            AddOpenableCabinet(root, new Vector3(29.2f, 0f, -13.3f), 0f, "柴房柜子",
                RandomContainerLoot);
        }

        private static void BuildSouthWing(Transform root)
        {
            BuildSouthWingShell(root);
            BuildSouthWingCorridorWalls(root);

            BuildSouthGallery(root);
            BuildSouthSpecimenRoom(root);
            BuildSouthTheater(root);
            BuildSouthTextileHall(root);
        }

        private static void BuildSouthWingShell(Transform root)
        {
            Box(root, "SouthWingFloor", new Vector3(0f, -0.1f, -21f), new Vector3(68f, 0.2f, 14f),
                floorMaterial, true);
            Box(root, "SouthWingCeiling", new Vector3(0f, WallHeight, -21f), new Vector3(68f, 0.2f, 14f),
                wallMaterial, true);
            Box(root, "SouthWingWallWest", new Vector3(-34.05f, 1.65f, -21f),
                new Vector3(0.1f, WallHeight, 14f), wallMaterial, true);
            Box(root, "SouthWingWallEast", new Vector3(34.05f, 1.65f, -21f),
                new Vector3(0.1f, WallHeight, 14f), wallMaterial, true);
            Box(root, "SouthWingWallSouth", new Vector3(0f, 1.65f, -28.05f),
                new Vector3(68.1f, WallHeight, 0.1f), wallMaterial, true);
            Box(root, "SouthWingWallNorth", new Vector3(0f, 1.65f, -14.05f),
                new Vector3(44f, WallHeight, 0.1f), wallMaterial, true);

            Box(root, "SouthWingRunner", new Vector3(0f, 0.02f, -15.8f),
                new Vector3(64f, 0.012f, 1.2f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(0f, 0.02f, -15.8f), 64f, 1.2f);
        }

        private static void BuildSouthWingCorridorWalls(Transform root)
        {
            float z = -17.55f;
            float wallCenterY = 1.65f;

            Box(root, "SouthCorridorWallA", new Vector3(-30.4f, wallCenterY, z),
                new Vector3(7.2f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "SouthCorridorWallB", new Vector3(-18f, wallCenterY, z),
                new Vector3(14.4f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "SouthCorridorWallC", new Vector3(-2f, wallCenterY, z),
                new Vector3(14.4f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "SouthCorridorWallD", new Vector3(15f, wallCenterY, z),
                new Vector3(16.4f, WallHeight, 0.12f), wallMaterial, true);
            Box(root, "SouthCorridorWallE", new Vector3(29.4f, wallCenterY, z),
                new Vector3(9.2f, WallHeight, 0.12f), wallMaterial, true);

            foreach (float doorX in new[] { -26f, -10f, 6f, 24f })
            {
                AddAnnexDoorAlongX(root, doorX, z);
            }

            foreach (float dividerX in new[] { -18f, -2f, 14f })
            {
                Box(root, "SouthRoomDivider", new Vector3(dividerX, wallCenterY, -22.75f),
                    new Vector3(0.12f, WallHeight, 10.5f), wallMaterial, true);
            }

            foreach (float sconceX in new[] { -30f, -14f, 2f, 18f, 32f })
            {
                MansionProps.AddWallSconce(root, new Vector3(sconceX, 1.9f, -17.75f), 180f);
            }
        }

        private static void BuildSouthGallery(Transform root)
        {
            Box(root, "SouthGalleryRug", new Vector3(-26f, 0.012f, -22.5f),
                new Vector3(5f, 0.02f, 3.2f), rugMaterial, false);
            MansionProps.AddRugBorder(root, new Vector3(-26f, 0.012f, -22.5f), 5f, 3.2f);
            for (int i = 0; i < 4; i++)
            {
                MansionProps.AddPainting(root, new Vector3(-33.9f, 1.8f, -19.5f - i * 2f),
                    -90f, 1.2f, 0.9f, i + 1);
            }
            MansionProps.AddBench(root, new Vector3(-30f, 0f, -22.4f), 90f, 2.2f);
            MansionProps.AddBench(root, new Vector3(-22f, 0f, -22.4f), 90f, 2.2f);
            MansionProps.AddSculpture(root, new Vector3(-26f, 0f, -26.2f), 0f);
            MansionProps.AddVase(root, new Vector3(-33.2f, 0.84f, -26.8f), 2);
            AddOpenableCabinet(root, new Vector3(-33.4f, 0f, -26.2f), 90f, "画廊边柜",
                RandomContainerLoot);
        }

        private static void BuildSouthSpecimenRoom(Transform root)
        {
            for (int i = 0; i < 3; i++)
            {
                float caseZ = -19.6f - i * 2f;
                Box(root, "SpecimenCase", new Vector3(-13f, 0.55f, caseZ),
                    new Vector3(1.2f, 1.1f, 0.8f), glassMaterial, true);
                Box(root, "SpecimenCasePlinth", new Vector3(-13f, 0.07f, caseZ),
                    new Vector3(1.32f, 0.14f, 0.9f), woodDarkMaterial, true);
                Box(root, "SpecimenCaseCrown", new Vector3(-13f, 1.14f, caseZ),
                    new Vector3(1.26f, 0.06f, 0.86f), woodLightMaterial, false);
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Box(root, "SpecimenCasePost", new Vector3(-13f + x * 0.57f, 0.6f,
                            caseZ + z * 0.38f), new Vector3(0.05f, 1.05f, 0.05f), woodDarkMaterial, false);
                    }
                }
                Cylinder(root, "SpecimenJar", new Vector3(-13f, 0.7f, caseZ),
                    new Vector3(0.16f, 0.2f, 0.16f), Quaternion.identity, glassMaterial, false);
                Sphere(root, "SpecimenSample", new Vector3(-13f, 0.62f, caseZ),
                    new Vector3(0.14f, 0.22f, 0.14f), stoneMaterial);
                Box(root, "SpecimenLabel", new Vector3(-13f, 0.98f, caseZ + 0.42f),
                    new Vector3(0.38f, 0.13f, 0.02f), paperMaterial, false);
                Box(root, "SpecimenLabelEdge", new Vector3(-13f, 1.055f, caseZ + 0.425f),
                    new Vector3(0.4f, 0.015f, 0.015f), goldMaterial, false);
            }
            MansionProps.AddShelfUnit(root, new Vector3(-17.2f, 0f, -21f), 90f, 2.2f, 2f, 0.45f, 4);
            MansionProps.AddDeskDetails(root, new Vector3(-9.6f, 0f, -26.3f), 0f, 2.2f);
            Box(root, "SpecimenDesk", new Vector3(-9.6f, 0.82f, -26.3f),
                new Vector3(2.2f, 0.08f, 1f), woodMaterial, true);
            CreateNote(root, "SouthSpecimenNote", new Vector3(-9.6f, 0.88f, -26.3f), "标本室便签",
                "这些标本都来自庄园的地下。\n其中有几件，似乎比庄园本身还要古老。",
                "阅读标本室便签", "specimen", 0f);
            AddOpenableDrawer(root, new Vector3(-16.8f, 0f, -26.3f), 0f, "标本柜抽屉",
                RandomContainerLoot);
            MansionProps.AddFloorLamp(root, new Vector3(-6.5f, 0f, -24.5f));
            MansionProps.AddSideTable(root, new Vector3(-16.2f, 0f, -24.2f));
            MansionProps.AddVase(root, new Vector3(-16.2f, 0.74f, -24.2f), 2);
        }

        private static void BuildSouthTheater(Transform root)
        {
            Box(root, "SouthTheaterStage", new Vector3(6f, 0.25f, -26f),
                new Vector3(10f, 0.5f, 2.6f), woodDarkMaterial, true);
            Box(root, "SouthTheaterStageEdge", new Vector3(6f, 0.51f, -24.72f),
                new Vector3(10f, 0.035f, 0.18f), woodLightMaterial, false);
            Box(root, "SouthTheaterCurtainA", new Vector3(1.4f, 1.8f, -24.8f),
                new Vector3(1.2f, 3f, 0.08f), rugMaterial, false);
            Box(root, "SouthTheaterCurtainB", new Vector3(10.6f, 1.8f, -24.8f),
                new Vector3(1.2f, 3f, 0.08f), rugMaterial, false);
            for (int i = 0; i < 2; i++)
            {
                Box(root, "SouthTheaterCurtainFoldA", new Vector3(1.0f + i * 0.35f, 1.75f, -24.72f),
                    new Vector3(0.22f, 2.85f, 0.06f), rugMaterial, false);
                Box(root, "SouthTheaterCurtainFoldB", new Vector3(10.1f + i * 0.35f, 1.75f, -24.72f),
                    new Vector3(0.22f, 2.85f, 0.06f), rugMaterial, false);
            }
            Box(root, "SouthTheaterBackdrop", new Vector3(6f, 1.9f, -27.25f),
                new Vector3(9.5f, 2.8f, 0.08f), fabricMaterial, false);
            for (int x = -1; x <= 1; x += 2)
            {
                Box(root, "SouthTheaterProscenium", new Vector3(6f + x * 5.3f, 1.75f, -24.75f),
                    new Vector3(0.22f, 3.5f, 0.26f), woodDarkMaterial, true);
            }
            Box(root, "SouthTheaterLintel", new Vector3(6f, 3.35f, -24.75f),
                new Vector3(10.9f, 0.28f, 0.3f), goldMaterial, false);
            for (int i = 0; i < 5; i++)
            {
                Sphere(root, "SouthTheaterFootlight", new Vector3(2.4f + i * 1.8f, 0.58f, -24.68f),
                    new Vector3(0.12f, 0.1f, 0.12f), flameMaterial);
            }
            MansionProps.AddChair(root, new Vector3(4f, 0f, -21f), 0f);
            MansionProps.AddChair(root, new Vector3(6f, 0f, -21f), 0f);
            MansionProps.AddChair(root, new Vector3(8f, 0f, -21f), 0f);
            MansionProps.AddChair(root, new Vector3(5f, 0f, -19.5f), 0f);
            MansionProps.AddChair(root, new Vector3(7f, 0f, -19.5f), 0f);
            Cylinder(root, "TheaterMusicStand", new Vector3(6f, 0.85f, -25.4f),
                new Vector3(0.03f, 0.85f, 0.03f), Quaternion.identity, metalMaterial, true);
            Box(root, "TheaterMusicSheet", new Vector3(6f, 1.55f, -25.4f),
                new Vector3(0.5f, 0.35f, 0.02f), paperMaterial, false);
            for (int i = 0; i < 3; i++)
            {
                MansionProps.AddBookStack(root, new Vector3(2.2f + i * 0.5f, 0.9f, -26.2f), i + 1);
            }
            AddOpenableCabinet(root, new Vector3(6f, 0f, -18.3f), 180f, "剧场道具柜",
                RandomContainerLoot);
        }

        private static void BuildSouthTextileHall(Transform root)
        {
            MansionProps.AddShelfUnit(root, new Vector3(16.2f, 0f, -22f), 90f, 2.2f, 2.1f, 0.45f, 2);
            MansionProps.AddShelfUnit(root, new Vector3(32.2f, 0f, -22f), 90f, 2.2f, 2.1f, 0.45f, 3);
            Box(root, "TextileTable", new Vector3(23.5f, 0.82f, -24.8f),
                new Vector3(3f, 0.08f, 1.4f), woodMaterial, true);
            Box(root, "TextileTableLegA", new Vector3(22.4f, 0.4f, -24.8f),
                new Vector3(0.1f, 0.8f, 1.2f), woodDarkMaterial, true);
            Box(root, "TextileTableLegB", new Vector3(24.6f, 0.4f, -24.8f),
                new Vector3(0.1f, 0.8f, 1.2f), woodDarkMaterial, true);
            for (int i = 0; i < 4; i++)
            {
                Box(root, "FabricRoll", new Vector3(21.5f + i * 0.5f, 0.95f, -26f),
                    new Vector3(0.35f, 0.22f, 0.8f), bookMaterials[i % bookMaterials.Length], false);
            }
            Box(root, "SewingMachineBase", new Vector3(22.4f, 0.95f, -24.7f),
                new Vector3(0.8f, 0.12f, 0.5f), metalMaterial, false);
            Box(root, "SewingMachineArm", new Vector3(22.4f, 1.12f, -24.85f),
                new Vector3(0.12f, 0.35f, 0.12f), metalMaterial, false);
            Cylinder(root, "SewingMachineWheel", new Vector3(22.85f, 1.02f, -24.7f),
                new Vector3(0.12f, 0.025f, 0.12f), Quaternion.Euler(90f, 0f, 0f), copperMaterial, false);
            Cylinder(root, "SewingMachineSpoolA", new Vector3(22.2f, 1.05f, -24.6f),
                new Vector3(0.04f, 0.06f, 0.04f), Quaternion.identity, porcelainMaterial, false);
            Cylinder(root, "SewingMachineSpoolB", new Vector3(22.5f, 1.05f, -24.6f),
                new Vector3(0.04f, 0.06f, 0.04f), Quaternion.identity, fabricMaterial, false);
            Box(root, "FoldedClothA", new Vector3(24.7f, 0.9f, -24.2f),
                new Vector3(0.8f, 0.12f, 0.6f), fabricMaterial, false);
            Box(root, "FoldedClothB", new Vector3(24.7f, 1.01f, -24.2f),
                new Vector3(0.72f, 0.1f, 0.55f), rugMaterial, false);
            MansionProps.AddSculpture(root, new Vector3(18f, 0f, -20f), 0f);
            MansionProps.AddSculpture(root, new Vector3(26f, 0f, -20f), 0f);
            CreateNote(root, "SouthTextileNote", new Vector3(23.5f, 0.88f, -24.8f), "织物间便签",
                "旧窗帘、旧礼服和舞台幕布都被堆在这里。\n有人说，地下室里还有一间上锁的封印密室。",
                "阅读织物间便签", "textile", 0f);
            AddOpenableDrawer(root, new Vector3(32.8f, 0f, -26.6f), -90f, "织物间衣柜",
                RandomContainerLoot);
        }

        private static void BuildExtraRoomContainers(Transform root)
        {
            AddOpenableDrawer(root, new Vector3(-2.2f, 0f, 8.3f), 180f, "书房抽屉",
                RandomContainerLoot);
            AddOpenableDrawer(root, new Vector3(-20.8f, 0f, -8.3f), 0f, "厨房抽屉",
                RandomContainerLoot);
            AddOpenableCabinet(root, new Vector3(-20.9f, 0f, 8.3f), 180f, "餐厅边柜",
                RandomContainerLoot);
            AddOpenableCabinet(root, new Vector3(20.8f, 0f, 8.3f), 180f, "会客厅边柜",
                RandomContainerLoot);
            AddOpenableDrawer(root, new Vector3(20.8f, 0f, -8.3f), 0f, "门厅抽屉",
                RandomContainerLoot);
            AddOpenableCabinet(root, new Vector3(12.8f, 0f, 8.3f), 180f, "展厅边柜",
                RandomContainerLoot);

            AddOpenableDrawer(root, new Vector3(-2f, UpperBaseY, 8.3f), 180f, "档案室抽屉",
                RandomContainerLoot);
            AddOpenableCabinet(root, new Vector3(12.8f, UpperBaseY, 8.3f), 180f, "实验室边柜",
                RandomContainerLoot);
            AddOpenableDrawer(root, new Vector3(-12.8f, UpperBaseY, -8.3f), 0f, "卧室抽屉",
                RandomContainerLoot);
            AddOpenableCabinet(root, new Vector3(20.8f, UpperBaseY, -8.3f), 0f, "客房边柜",
                RandomContainerLoot);

            AddOpenableCabinet(root, new Vector3(-33.4f, 0f, 3.2f), 90f, "藏书室边柜",
                RandomContainerLoot);
            AddOpenableDrawer(root, new Vector3(-33.4f, 0f, -7.6f), 90f, "酒窖抽屉",
                RandomContainerLoot);

            Box(root, "BoilerLanternTable", new Vector3(-23.683f, 0.35f, -7.3f),
                new Vector3(0.8f, 0.7f, 0.8f), woodDarkMaterial, true);
            CreateItemSpawnPoint(root, "Spawn_Lantern", ItemIds.Lantern,
                new Vector3(-23.683f, 0.71f, -7.3f), true);
        }

        private static void BuildBasement(Transform root)
        {
            float baseY = -4f;

            Box(root, "BasementFloor", new Vector3(0f, baseY - 0.1f, 0f), new Vector3(68f, 0.2f, 28f),
                floorMaterial, true);
            Box(root, "BasementCeiling", new Vector3(0f, baseY + WallHeight, 0f),
                new Vector3(68f, 0.2f, 28f), wallMaterial, true);
            Box(root, "BasementWallWest", new Vector3(-34.05f, baseY + 1.65f, 0f),
                new Vector3(0.1f, WallHeight, 28f), wallMaterial, true);
            Box(root, "BasementWallEast", new Vector3(34.05f, baseY + 1.65f, 0f),
                new Vector3(0.1f, WallHeight, 28f), wallMaterial, true);
            Box(root, "BasementWallNorth", new Vector3(0f, baseY + 1.65f, 14.05f),
                new Vector3(68.1f, WallHeight, 0.1f), wallMaterial, true);
            Box(root, "BasementWallSouth", new Vector3(0f, baseY + 1.65f, -14.05f),
                new Vector3(68.1f, WallHeight, 0.1f), wallMaterial, true);

            BuildBasementCorridorWall(root, baseY, 2f, new[] { -24f, -8f, 8f, 24f });
            BuildBasementCorridorWall(root, baseY, -2f, new[] { -24f, -8f, 8f, 24f });

            foreach (float dividerX in new[] { -12f, 12f })
            {
                Box(root, "BasementDividerNorth", new Vector3(dividerX, baseY + 1.65f, 8f),
                    new Vector3(0.12f, WallHeight, 12f), wallMaterial, true);
                Box(root, "BasementDividerSouth", new Vector3(dividerX, baseY + 1.65f, -8f),
                    new Vector3(0.12f, WallHeight, 12f), wallMaterial, true);
            }

            var zoneGo = new GameObject("BasementLightingZone");
            zoneGo.transform.SetParent(root, false);
            zoneGo.transform.localPosition = new Vector3(0f, baseY + 1.65f, 0f);
            var zoneTrigger = zoneGo.AddComponent<BoxCollider>();
            zoneTrigger.isTrigger = true;
            zoneTrigger.size = new Vector3(66f, 3.2f, 26f);
            var zone = zoneGo.AddComponent<LightingZone>();

            BuildBasementStorage(root, baseY);
            BuildBasementWineVault(root, baseY);
            BuildBasementMechanical(root, baseY);
            BuildBasementPumpRoom(root, baseY);
            BuildBasementShadowVault(root, baseY);
            BuildBasementServantsHall(root, baseY);
            BuildBasementAccess(root, baseY, zone);
            BuildBasementLights(root, baseY);
        }

        private static void BuildBasementCorridorWall(Transform root, float baseY, float z, float[] doorXs)
        {
            float cursor = -34f;
            float wallCenterY = baseY + WallHeight * 0.5f;

            foreach (float doorX in doorXs)
            {
                float wallEnd = doorX - 0.8f;
                if (wallEnd > cursor)
                {
                    Box(root, "BasementCorridorWall", new Vector3((cursor + wallEnd) * 0.5f, wallCenterY, z),
                        new Vector3(wallEnd - cursor, WallHeight, 0.12f), wallMaterial, true);
                }

                Box(root, "BasementDoorLintel", new Vector3(doorX, baseY + 2.85f, z),
                    new Vector3(1.6f, 0.9f, 0.12f), wallMaterial, true);
                Box(root, "BasementDoorTrimLeft", new Vector3(doorX - 0.82f, baseY + 1.19f, z),
                    new Vector3(0.08f, 2.44f, 0.2f), woodDarkMaterial, false);
                Box(root, "BasementDoorTrimRight", new Vector3(doorX + 0.82f, baseY + 1.19f, z),
                    new Vector3(0.08f, 2.44f, 0.2f), woodDarkMaterial, false);
                Box(root, "BasementDoorTrimTop", new Vector3(doorX, baseY + 2.42f, z),
                    new Vector3(1.76f, 0.08f, 0.2f), woodDarkMaterial, false);

                cursor = doorX + 0.8f;
            }

            if (34f > cursor)
            {
                Box(root, "BasementCorridorWall", new Vector3((cursor + 34f) * 0.5f, wallCenterY, z),
                    new Vector3(34f - cursor, WallHeight, 0.12f), wallMaterial, true);
            }
        }

        private static void BuildBasementStorage(Transform root, float baseY)
        {
            for (int i = 0; i < 4; i++)
            {
                Box(root, "BasementCrate", new Vector3(-21.5f - (i % 2) * 1.4f, baseY + 0.4f,
                    -10f + (i / 2) * 1.4f), new Vector3(1f, 0.8f, 1f), woodDarkMaterial, true);
                MansionProps.AddCrateDetails(root, new Vector3(-21.5f - (i % 2) * 1.4f,
                    baseY + 0.4f, -10f + (i / 2) * 1.4f), new Vector3(1f, 0.8f, 1f));
            }

            MansionProps.AddShelfUnit(root, new Vector3(-33.6f, baseY, -12.2f), 90f, 2.2f, 2f, 0.45f, 3);
            MansionProps.AddBarrelDetails(root, new Vector3(-16f, baseY + 0.45f, -12f));
            Cylinder(root, "BasementStorageBarrel", new Vector3(-16f, baseY + 0.45f, -12f),
                new Vector3(0.45f, 0.45f, 0.45f), Quaternion.Euler(0f, 0f, 90f), woodDarkMaterial, true);
            Box(root, "BasementStorageTable", new Vector3(-28f, baseY + 0.4f, -5f),
                new Vector3(1.4f, 0.8f, 0.8f), woodMaterial, true);
            CreateNote(root, "BasementStorageNote", new Vector3(-28f, baseY + 0.88f, -5f),
                "地下储藏室便签",
                "机械间里有电梯缺的传动齿轮。\n封印密室在走廊东侧，影之徽记就在那里。",
                "阅读地下储藏室便签", "basement_storage", 0f);
            AddOpenableCabinet(root, new Vector3(-33.4f, baseY, -7f), 90f, "地下旧柜",
                RandomContainerLoot);
        }

        private static void BuildBasementWineVault(Transform root, float baseY)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 barrelPos = new Vector3(-30f + (i % 2) * 0.9f, baseY + 0.45f,
                    5f + (i / 2) * 1.2f);
                Cylinder(root, "BasementWineBarrel", barrelPos, new Vector3(0.45f, 0.45f, 0.45f),
                    Quaternion.Euler(0f, 0f, 90f), woodDarkMaterial, true);
                MansionProps.AddBarrelDetails(root, barrelPos);
            }

            MansionProps.AddShelfUnit(root, new Vector3(-33.6f, baseY, 9.5f), 90f, 2.2f, 2f, 0.45f, 4);
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    Cylinder(root, "BasementRackBottle",
                        new Vector3(-33.3f, baseY + 0.7f + row * 0.5f, 8.4f + col * 0.5f),
                        new Vector3(0.04f, 0.16f, 0.04f), Quaternion.Euler(0f, 0f, 90f), glassMaterial);
                }
            }

            AddOpenableDrawer(root, new Vector3(-33.4f, baseY, 6.2f), 90f, "地下酒窖抽屉",
                RandomContainerLoot);
        }

        private static void BuildBasementMechanical(Transform root, float baseY)
        {
            Box(root, "MechanicalBench", new Vector3(0f, baseY + 0.8f, -8f),
                new Vector3(2.6f, 0.1f, 1f), woodDarkMaterial, true);
            Box(root, "MechanicalBenchLegA", new Vector3(-1.1f, baseY + 0.4f, -8f),
                new Vector3(0.1f, 0.8f, 0.9f), metalMaterial, true);
            Box(root, "MechanicalBenchLegB", new Vector3(1.1f, baseY + 0.4f, -8f),
                new Vector3(0.1f, 0.8f, 0.9f), metalMaterial, true);
            Cylinder(root, "MechanicalGear", new Vector3(0.5f, baseY + 0.9f, -8f),
                new Vector3(0.3f, 0.05f, 0.3f), Quaternion.identity, goldMaterial, false);

            for (int i = 0; i < 3; i++)
            {
                Cylinder(root, "BasementPipe", new Vector3(-2.4f + i * 0.55f, baseY + 1.8f, -12.5f),
                    new Vector3(0.08f, 1.1f, 0.08f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, false);
            }

            MansionProps.AddToolCart(root, new Vector3(-5.2f, baseY, -10f), 0f);
            Box(root, "BasementMechanicalNoteTable", new Vector3(-5f, baseY + 0.4f, -5f),
                new Vector3(1.2f, 0.8f, 0.8f), woodMaterial, true);
            CreateItemSpawnPoint(root, "Spawn_ElevatorGear", ItemIds.ElevatorGear,
                new Vector3(0f, baseY + 0.86f, -8f), true);
            CreateNote(root, "BasementMechanicalNote", new Vector3(-5f, baseY + 0.88f, -5f),
                "机械间便签",
                "电梯传动箱的齿轮被拆下来放在工作台上。\n把它装回东翼走廊尽头电梯厅的面板，电梯就能重新运转。",
                "阅读机械间便签", "basement_mechanical", 0f);
            AddOpenableDrawer(root, new Vector3(-9f, baseY, -13.3f), 0f, "机械间抽屉",
                RandomContainerLoot);
        }

        private static void BuildBasementPumpRoom(Transform root, float baseY)
        {
            Cylinder(root, "WaterPumpA", new Vector3(-3f, baseY + 0.8f, 8f),
                new Vector3(0.7f, 0.8f, 0.7f), Quaternion.identity, metalMaterial, true);
            Cylinder(root, "WaterPumpB", new Vector3(2.5f, baseY + 0.65f, 10f),
                new Vector3(0.55f, 0.65f, 0.55f), Quaternion.identity, metalMaterial, true);
            Cylinder(root, "PumpPipeA", new Vector3(-1.8f, baseY + 2f, 5.2f),
                new Vector3(0.08f, 1.2f, 0.08f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, false);
            Cylinder(root, "PumpPipeB", new Vector3(3.8f, baseY + 1.9f, 5.2f),
                new Vector3(0.08f, 1.2f, 0.08f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, false);
            Cylinder(root, "PumpValvePipe", new Vector3(0f, baseY + 0.6f, 5.4f),
                new Vector3(0.08f, 0.6f, 0.08f), Quaternion.identity, metalMaterial, true);
            Cylinder(root, "PumpValveWheel", new Vector3(0f, baseY + 1.2f, 5.4f),
                new Vector3(0.4f, 0.05f, 0.4f), Quaternion.Euler(90f, 0f, 0f), goldMaterial, false);

            MansionProps.AddShelfUnit(root, new Vector3(-11.7f, baseY, 6f), 90f, 2f, 2f, 0.4f, 2);
            Box(root, "BasementPumpNoteTable", new Vector3(-3f, baseY + 0.4f, 6.4f),
                new Vector3(1.2f, 0.8f, 0.8f), woodMaterial, true);
            CreateNote(root, "BasementPumpNote", new Vector3(-3f, baseY + 0.88f, 6.4f),
                "水泵房便签",
                "地下封印密室需要四枚徽记才能开启。\n最后一枚是影之徽记，颜色比夜色还深。",
                "阅读水泵房便签", "basement_pump", 0f);
            AddOpenableCabinet(root, new Vector3(0f, baseY, 13.3f), 180f, "水泵房柜子",
                RandomContainerLoot);
        }

        private static void BuildBasementShadowVault(Transform root, float baseY)
        {
            Cylinder(root, "ShadowVaultPedestal", new Vector3(23f, baseY + 0.4f, -8f),
                new Vector3(0.9f, 0.4f, 0.9f), Quaternion.identity, metalMaterial, true);
            CreateItemSpawnPoint(root, "Spawn_SealShadow", ItemIds.SealShadow,
                new Vector3(23f, baseY + 0.86f, -8f), true);

            for (int i = 0; i < 4; i++)
            {
                Box(root, "ShadowVaultColumn", new Vector3(15f + i * 5f, baseY + 1.5f, -13.2f),
                    new Vector3(0.3f, 3f, 0.3f), wallMaterial, true);
                Cylinder(root, "ShadowVaultCandle", new Vector3(15f + i * 5f, baseY + 0.18f, -12.7f),
                    new Vector3(0.08f, 0.18f, 0.08f), Quaternion.identity, paperMaterial, false);
                Sphere(root, "ShadowVaultFlame", new Vector3(15f + i * 5f, baseY + 0.45f, -12.7f),
                    new Vector3(0.08f, 0.12f, 0.08f), flameMaterial);
            }

            MansionProps.AddSculpture(root, new Vector3(29f, baseY, -12f), 0f);
            Box(root, "BasementShadowNoteTable", new Vector3(29f, baseY + 0.4f, -5.5f),
                new Vector3(1.2f, 0.8f, 0.8f), woodMaterial, true);
            CreateNote(root, "BasementShadowNote", new Vector3(29f, baseY + 0.88f, -5.5f),
                "封印密室便签",
                "影之徽记是封印大门的第四枚徽记。\n把它和日、月、星一起带到二层西端的大门。",
                "阅读封印密室便签", "basement_shadow", 0f);
            AddOpenableDrawer(root, new Vector3(29f, baseY, -13.3f), 0f, "封印室石柜",
                RandomContainerLoot);
        }

        private static void BuildBasementServantsHall(Transform root, float baseY)
        {
            for (int i = 0; i < 4; i++)
            {
                Box(root, "BasementServantCrate", new Vector3(17f + (i % 2) * 1.2f, baseY + 0.35f,
                    6f + (i / 2) * 1.4f), new Vector3(0.8f, 0.7f, 0.8f), woodDarkMaterial, true);
                MansionProps.AddCrateDetails(root, new Vector3(17f + (i % 2) * 1.2f, baseY + 0.35f,
                    6f + (i / 2) * 1.4f), new Vector3(0.8f, 0.7f, 0.8f));
            }

            MansionProps.AddShelfUnit(root, new Vector3(33.4f, baseY, 9f), 90f, 2.2f, 2f, 0.45f, 2);
            MansionProps.AddDryingRack(root, new Vector3(24f, baseY, 12f), 0f);
            Cylinder(root, "ServantHallBarrel", new Vector3(30f, baseY + 0.45f, 5f),
                new Vector3(0.45f, 0.45f, 0.45f), Quaternion.Euler(0f, 0f, 90f), woodDarkMaterial, true);
            MansionProps.AddBarrelDetails(root, new Vector3(30f, baseY + 0.45f, 5f));
            AddOpenableCabinet(root, new Vector3(33.4f, baseY, 6f), -90f, "仆役房旧柜",
                RandomContainerLoot);
        }

        private static void BuildBasementAccess(Transform root, float baseY, LightingZone zone)
        {
            var hatchRoot = new GameObject("BasementHatch");
            hatchRoot.transform.SetParent(root, false);
            hatchRoot.transform.localPosition = new Vector3(-25f, 0.03f, -3.6f);

            var hatchCollider = hatchRoot.AddComponent<BoxCollider>();
            hatchCollider.size = new Vector3(1.5f, 0.16f, 1.5f);

            Box(hatchRoot.transform, "HatchFrame", Vector3.zero, new Vector3(1.7f, 0.12f, 1.7f),
                metalMaterial, false);
            var lidPivot = new GameObject("HatchLidPivot");
            lidPivot.transform.SetParent(hatchRoot.transform, false);
            lidPivot.transform.localPosition = new Vector3(-0.7f, 0.06f, 0f);
            Box(lidPivot.transform, "HatchLid", new Vector3(0.7f, 0f, 0f),
                new Vector3(1.4f, 0.06f, 1.4f), woodDarkMaterial, false);
            Box(lidPivot.transform, "HatchHandle", new Vector3(1.2f, 0.08f, 0f),
                new Vector3(0.18f, 0.06f, 0.06f), goldMaterial, false);

            var hatch = hatchRoot.AddComponent<BasementHatch>();
            hatch.LidPivot = lidPivot.transform;
            hatch.Destination = new Vector3(-25f, baseY + 0.05f, -4.2f);
            hatch.DestinationYaw = 180f;
            hatch.Zone = zone;

            var ladderRoot = new GameObject("BasementLadder");
            ladderRoot.transform.SetParent(root, false);
            ladderRoot.transform.localPosition = new Vector3(-25.6f, baseY + 1.2f, -3.6f);

            var ladderCollider = ladderRoot.AddComponent<BoxCollider>();
            ladderCollider.size = new Vector3(0.4f, 2.4f, 0.5f);

            Box(ladderRoot.transform, "LadderRailA", new Vector3(-0.28f, 0f, 0f),
                new Vector3(0.06f, 2.4f, 0.06f), metalMaterial, false);
            Box(ladderRoot.transform, "LadderRailB", new Vector3(0.28f, 0f, 0f),
                new Vector3(0.06f, 2.4f, 0.06f), metalMaterial, false);
            for (int i = 0; i < 7; i++)
            {
                Box(ladderRoot.transform, "LadderRung", new Vector3(0f, -1.05f + i * 0.32f, 0f),
                    new Vector3(0.62f, 0.05f, 0.05f), metalMaterial, false);
            }

            var ladder = ladderRoot.AddComponent<BasementLadder>();
            ladder.Destination = new Vector3(-25f, 0.05f, -4.2f);
            ladder.DestinationYaw = 180f;
            ladder.Zone = zone;
        }

        private static void BuildBasementLights(Transform root, float baseY)
        {
            AddBasementLight(root, "BasementCorridorLightA", new Vector3(-23f, baseY + 2.5f, 0f),
                new Color(0.55f, 0.68f, 0.8f), 6.5f, 0.8f, true);
            AddBasementLight(root, "BasementCorridorLightB", new Vector3(0f, baseY + 2.5f, 0f),
                new Color(0.55f, 0.68f, 0.8f), 6.5f, 0.7f, false);
            AddBasementLight(root, "BasementCorridorLightC", new Vector3(23f, baseY + 2.5f, 0f),
                new Color(0.55f, 0.68f, 0.8f), 6.5f, 0.8f, true);

            AddBasementLight(root, "BasementStorageLight", new Vector3(-24f, baseY + 2.5f, -8f),
                new Color(0.8f, 0.7f, 0.5f), 7f, 0.9f, true);
            AddBasementLight(root, "BasementWineLight", new Vector3(-24f, baseY + 2.5f, 8f),
                new Color(0.85f, 0.55f, 0.38f), 6f, 0.8f, false);
            AddBasementLight(root, "BasementMechanicalLight", new Vector3(0f, baseY + 2.5f, -8f),
                new Color(0.6f, 0.75f, 0.9f), 7f, 0.9f, true);
            AddBasementLight(root, "BasementPumpLight", new Vector3(0f, baseY + 2.5f, 8f),
                new Color(0.55f, 0.72f, 0.7f), 6f, 0.8f, false);
            AddBasementLight(root, "BasementShadowLight", new Vector3(23f, baseY + 2.5f, -8f),
                new Color(0.7f, 0.5f, 1f), 8f, 1.2f, true);
            AddBasementLight(root, "BasementServantsLight", new Vector3(23f, baseY + 2.5f, 8f),
                new Color(0.8f, 0.68f, 0.5f), 6f, 0.8f, false);
        }

        private static void AddBasementLight(Transform root, string name, Vector3 position, Color color,
            float range, float intensity, bool flicker)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            // 同样把光源移到裸灯泡下方，避免灯泡实体遮挡阴影。
            go.transform.localPosition = position + new Vector3(0f, -0.14f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 118f;
            light.innerSpotAngle = 72f;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;

            Sphere(go.transform, "BareBulb", Vector3.zero, new Vector3(0.12f, 0.16f, 0.12f),
                CreateEmissive("BasementBulb" + name, color, 0.8f));

            if (flicker)
            {
                go.AddComponent<FlickerLight>();
            }
        }

        private static OpenableContainer AddOpenableDrawer(Transform root, Vector3 position, float yaw,
            string displayName, string[] loot)
        {
            var containerRoot = new GameObject(displayName + "Root");
            containerRoot.transform.SetParent(root, false);
            containerRoot.transform.localPosition = position;
            containerRoot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(containerRoot.transform, "DrawerBody", new Vector3(0f, 0.45f, 0f),
                new Vector3(0.95f, 0.9f, 0.55f), woodDarkMaterial, false);
            var rootCollider = containerRoot.AddComponent<BoxCollider>();
            rootCollider.size = new Vector3(0.98f, 0.96f, 0.62f);
            rootCollider.center = new Vector3(0f, 0.47f, 0.03f);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(containerRoot.transform, "DrawerFoot", new Vector3(x * 0.4f, 0.03f, z * 0.21f),
                        new Vector3(0.09f, 0.06f, 0.09f), woodDarkMaterial, false);
                }
            }

            Box(containerRoot.transform, "DrawerTop", new Vector3(0f, 0.92f, 0f),
                new Vector3(1.02f, 0.06f, 0.6f), woodMaterial, false);
            Box(containerRoot.transform, "DrawerTopEdge", new Vector3(0f, 0.955f, 0f),
                new Vector3(1.05f, 0.02f, 0.63f), woodLightMaterial, false);
            Box(containerRoot.transform, "DrawerSideTrimL", new Vector3(-0.48f, 0.47f, 0.3f),
                new Vector3(0.035f, 0.86f, 0.035f), woodLightMaterial, false);
            Box(containerRoot.transform, "DrawerSideTrimR", new Vector3(0.48f, 0.47f, 0.3f),
                new Vector3(0.035f, 0.86f, 0.035f), woodLightMaterial, false);
            Box(containerRoot.transform, "DrawerLowerInset", new Vector3(0f, 0.25f, 0.296f),
                new Vector3(0.68f, 0.16f, 0.012f), woodLightMaterial, false);
            Box(containerRoot.transform, "DrawerFrontStatic", new Vector3(0f, 0.25f, 0.28f),
                new Vector3(0.82f, 0.22f, 0.03f), woodMaterial, false);
            Box(containerRoot.transform, "DrawerKnobStatic", new Vector3(0f, 0.25f, 0.31f),
                new Vector3(0.15f, 0.05f, 0.04f), goldMaterial, false);

            var movingDrawer = new GameObject("MovingDrawer");
            movingDrawer.transform.SetParent(containerRoot.transform, false);
            movingDrawer.transform.localPosition = new Vector3(0f, 0.62f, 0.28f);
            Box(movingDrawer.transform, "DrawerFront", Vector3.zero,
                new Vector3(0.82f, 0.24f, 0.04f), woodMaterial, false);
            Box(movingDrawer.transform, "DrawerFrontInset", new Vector3(0f, 0f, -0.023f),
                new Vector3(0.68f, 0.17f, 0.012f), woodLightMaterial, false);
            Box(movingDrawer.transform, "DrawerHandle", new Vector3(0f, 0f, 0.04f),
                new Vector3(0.16f, 0.05f, 0.05f), goldMaterial, false);
            for (int x = -1; x <= 1; x += 2)
            {
                Box(movingDrawer.transform, "DrawerHandlePost", new Vector3(x * 0.06f, 0f, 0.025f),
                    new Vector3(0.025f, 0.04f, 0.025f), goldMaterial, false);
            }
            Box(movingDrawer.transform, "DrawerCavity", new Vector3(0f, 0f, -0.16f),
                new Vector3(0.82f, 0.22f, 0.28f), woodDarkMaterial, false);

            var lootPoint = CreateItemSpawnPoint(movingDrawer.transform, "LootPoint", ItemIds.OldCoin,
                new Vector3(0f, 0.1f, 0.02f), false);

            var container = containerRoot.AddComponent<OpenableContainer>();
            container.DisplayName = displayName;
            container.Motion = OpenableContainer.MotionKind.Drawer;
            container.MovingPart = movingDrawer.transform;
            container.SlideOffset = new Vector3(0f, 0f, 0.38f);
            container.LootSpawnPoint = lootPoint;
            container.LootItemIds = loot;
            return container;
        }

        private static OpenableContainer AddOpenableCabinet(Transform root, Vector3 position, float yaw,
            string displayName, string[] loot)
        {
            var containerRoot = new GameObject(displayName + "Root");
            containerRoot.transform.SetParent(root, false);
            containerRoot.transform.localPosition = position;
            containerRoot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(containerRoot.transform, "CabinetBody", new Vector3(0f, 0.85f, 0f),
                new Vector3(1f, 1.6f, 0.5f), woodDarkMaterial, false);
            var rootCollider = containerRoot.AddComponent<BoxCollider>();
            rootCollider.size = new Vector3(1.04f, 1.72f, 0.62f);
            rootCollider.center = new Vector3(0f, 0.86f, 0.02f);

            Box(containerRoot.transform, "CabinetPlinth", new Vector3(0f, 0.04f, 0f),
                new Vector3(1.04f, 0.08f, 0.56f), woodDarkMaterial, false);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(containerRoot.transform, "CabinetFoot", new Vector3(x * 0.44f, 0.09f, z * 0.23f),
                        new Vector3(0.08f, 0.1f, 0.08f), woodDarkMaterial, false);
                }
            }

            Box(containerRoot.transform, "CabinetTop", new Vector3(0f, 1.7f, 0f),
                new Vector3(1.08f, 0.08f, 0.58f), woodMaterial, false);
            Box(containerRoot.transform, "CabinetCrown", new Vector3(0f, 1.765f, 0f),
                new Vector3(1.12f, 0.05f, 0.62f), woodLightMaterial, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(containerRoot.transform, "CabinetSideTrim", new Vector3(side * 0.51f, 0.85f, 0.28f),
                    new Vector3(0.03f, 1.5f, 0.03f), woodLightMaterial, false);
                Cylinder(containerRoot.transform, "CabinetHinge", new Vector3(-0.48f, side * 0.55f + 0.85f, 0.27f),
                    new Vector3(0.035f, 0.03f, 0.035f), Quaternion.Euler(0f, 0f, 90f), goldMaterial, false);
            }

            Box(containerRoot.transform, "CabinetShelf", new Vector3(0f, 0.56f, 0.26f),
                new Vector3(0.92f, 0.04f, 0.42f), woodMaterial, false);

            var doorPivot = new GameObject("CabinetDoorPivot");
            doorPivot.transform.SetParent(containerRoot.transform, false);
            doorPivot.transform.localPosition = new Vector3(-0.46f, 0.85f, 0.27f);
            Box(doorPivot.transform, "CabinetDoor", new Vector3(0.46f, 0f, 0f),
                new Vector3(0.92f, 1.45f, 0.04f), woodMaterial, false);
            Box(doorPivot.transform, "CabinetDoorInset", new Vector3(0.46f, 0f, -0.024f),
                new Vector3(0.68f, 1.08f, 0.012f), woodLightMaterial, false);
            Box(doorPivot.transform, "CabinetDoorTopTrim", new Vector3(0.46f, 0.68f, -0.03f),
                new Vector3(0.72f, 0.04f, 0.012f), woodDarkMaterial, false);
            Box(doorPivot.transform, "CabinetDoorBottomTrim", new Vector3(0.46f, -0.68f, -0.03f),
                new Vector3(0.72f, 0.04f, 0.012f), woodDarkMaterial, false);
            Box(doorPivot.transform, "CabinetHandle", new Vector3(0.86f, 0f, 0.04f),
                new Vector3(0.06f, 0.12f, 0.05f), goldMaterial, false);
            Cylinder(doorPivot.transform, "CabinetKnob", new Vector3(0.84f, 0f, 0.08f),
                new Vector3(0.045f, 0.02f, 0.045f), Quaternion.Euler(90f, 0f, 0f), goldMaterial, false);

            var lootPoint = CreateItemSpawnPoint(containerRoot.transform, "LootPoint",
                ItemIds.OldCoin, new Vector3(0f, 0.58f, 0.3f), false);

            var container = containerRoot.AddComponent<OpenableContainer>();
            container.DisplayName = displayName;
            container.Motion = OpenableContainer.MotionKind.Cabinet;
            container.MovingPart = doorPivot.transform;
            container.SwingEuler = new Vector3(0f, -105f, 0f);
            container.LootSpawnPoint = lootPoint;
            container.LootItemIds = loot;
            return container;
        }

        private static void BuildLighting(Transform root)
        {
            var sunGo = new GameObject("DirectionalLight");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.localRotation = Quaternion.Euler(55f, -32f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.7f, 0.78f, 0.95f);
            sun.intensity = 0.5f;
            sun.shadows = LightShadows.Soft;

            AddPointLight(root, "StudyLight", new Vector3(-9f, 2.7f, 5.4f),
                new Color(1f, 0.82f, 0.6f), 7f, 2.6f);
            AddPointLight(root, "StorageLight", new Vector3(-9f, 2.7f, -5.4f),
                new Color(1f, 0.8f, 0.55f), 7f, 2.3f);
            AddPointLight(root, "GalleryLight", new Vector3(9f, 2.7f, 5.4f),
                new Color(0.78f, 0.86f, 1f), 7f, 2.6f);
            AddPointLight(root, "UtilityLight", new Vector3(9f, 2.7f, -5.4f),
                new Color(1f, 0.75f, 0.5f), 7f, 2.3f);
            AddPointLight(root, "DiningLight", new Vector3(-18f, 2.7f, 5.5f),
                new Color(1f, 0.8f, 0.55f), 7f, 2.6f);
            AddPointLight(root, "KitchenLight", new Vector3(-18f, 2.7f, -5.5f),
                new Color(1f, 0.76f, 0.5f), 6f, 2.3f);
            AddPointLight(root, "SalonLight", new Vector3(18f, 2.7f, 5.5f),
                new Color(1f, 0.78f, 0.58f), 7f, 2.6f);
            AddPointLight(root, "FoyerLight", new Vector3(18f, 2.7f, -5.5f),
                new Color(1f, 0.82f, 0.62f), 6f, 2.3f);
            AddPointLight(root, "WestLibraryLight", new Vector3(-32f, 2.7f, 5f),
                new Color(1f, 0.84f, 0.64f), 6f, 2.4f);
            AddPointLight(root, "WestBilliardLight", new Vector3(-24.5f, 2.7f, 5f),
                new Color(0.8f, 0.9f, 1f), 6f, 2.3f);
            AddPointLight(root, "WestWineLight", new Vector3(-32f, 2.7f, -5f),
                new Color(1f, 0.74f, 0.5f), 6f, 2.1f);
            AddPointLight(root, "WestBoilerLight", new Vector3(-24.5f, 2.7f, -5f),
                new Color(1f, 0.66f, 0.42f), 6f, 2.2f);
            AddPointLight(root, "WestGreenhouseLight", new Vector3(-28f, 2.7f, 12.5f),
                new Color(0.72f, 1f, 0.76f), 6f, 2.5f);
            AddPointLight(root, "WestSmokingLight", new Vector3(-28f, 2.7f, -12.5f),
                new Color(1f, 0.78f, 0.56f), 6f, 2f);
            AddPointLight(root, "WestAnnexMainLight", new Vector3(-28f, 2.65f, 0f),
                new Color(1f, 0.84f, 0.66f), 7f, 1.9f);
            AddPointLight(root, "WestAnnexCrossNorthLight", new Vector3(-28f, 2.65f, 9.5f),
                new Color(1f, 0.84f, 0.66f), 6f, 1.7f);
            AddPointLight(root, "WestAnnexCrossSouthLight", new Vector3(-28f, 2.65f, -9.5f),
                new Color(1f, 0.84f, 0.66f), 6f, 1.7f);

            AddPointLight(root, "EastLaundryLight", new Vector3(32f, 2.7f, 5f),
                new Color(0.86f, 0.92f, 1f), 6f, 2.2f);
            AddPointLight(root, "EastPantryLight", new Vector3(32f, 2.7f, -5f),
                new Color(1f, 0.82f, 0.6f), 6f, 2f);
            AddPointLight(root, "EastWorkshopLight", new Vector3(24.5f, 2.7f, 5f),
                new Color(1f, 0.76f, 0.5f), 6f, 2.1f);
            AddPointLight(root, "EastIceLight", new Vector3(24.5f, 2.7f, -5f),
                new Color(0.65f, 0.85f, 1f), 6f, 1.9f);
            AddPointLight(root, "EastSunRoomLight", new Vector3(28f, 2.7f, 12.5f),
                new Color(0.75f, 1f, 0.8f), 6f, 2.4f);
            AddPointLight(root, "EastWoodStoreLight", new Vector3(28f, 2.7f, -12.5f),
                new Color(1f, 0.7f, 0.45f), 6f, 2f);
            AddPointLight(root, "EastAnnexMainLight", new Vector3(28f, 2.65f, 0f),
                new Color(1f, 0.86f, 0.66f), 7f, 1.9f);
            AddPointLight(root, "EastAnnexCrossNorthLight", new Vector3(28f, 2.65f, 9.5f),
                new Color(1f, 0.86f, 0.66f), 6f, 1.7f);
            AddPointLight(root, "EastAnnexCrossSouthLight", new Vector3(28f, 2.65f, -9.5f),
                new Color(1f, 0.86f, 0.66f), 6f, 1.7f);

            AddPointLight(root, "SouthGalleryLight", new Vector3(-26f, 2.7f, -22.5f),
                new Color(0.78f, 0.86f, 1f), 7f, 2.2f);
            AddPointLight(root, "SouthSpecimenLight", new Vector3(-10f, 2.7f, -22.5f),
                new Color(0.72f, 1f, 0.82f), 8f, 3f);
            AddPointLight(root, "SouthTheaterLight", new Vector3(6f, 2.7f, -22.5f),
                new Color(1f, 0.7f, 0.5f), 7f, 2.2f);
            AddPointLight(root, "SouthTheaterFill", new Vector3(6f, 2.6f, -16f),
                new Color(1f, 0.82f, 0.62f), 6.5f, 1.8f);
            AddPointLight(root, "SouthTextileLight", new Vector3(24f, 2.7f, -22.5f),
                new Color(1f, 0.84f, 0.66f), 7f, 2.1f);
            foreach (float corridorX in new[] { -30f, -14f, 2f, 18f, 32f })
            {
                AddPointLight(root, "SouthCorridorLight", new Vector3(corridorX, 2.65f, -15.8f),
                    new Color(1f, 0.86f, 0.68f), 6f, 1.6f);
            }

            AddPointLight(root, "CorridorLightA", new Vector3(-15f, 2.65f, 0f),
                new Color(1f, 0.86f, 0.68f), 7f, 2f);
            AddPointLight(root, "CorridorLightB", new Vector3(-5f, 2.65f, 0f),
                new Color(1f, 0.86f, 0.68f), 7f, 2f);
            AddPointLight(root, "CorridorLightC", new Vector3(5f, 2.65f, 0f),
                new Color(1f, 0.86f, 0.68f), 7f, 2f);
            AddPointLight(root, "CorridorLightD", new Vector3(15f, 2.65f, 0f),
                new Color(1f, 0.86f, 0.68f), 7f, 2f);
            AddPointLight(root, "CorridorLightE", new Vector3(20f, 2.65f, 0f),
                new Color(1f, 0.86f, 0.68f), 6f, 1.8f);

            float baseY = UpperBaseY;
            AddPointLight(root, "ArchiveLight", new Vector3(-9f, baseY + 2.7f, 5.4f),
                new Color(1f, 0.82f, 0.6f), 7f, 2.6f);
            AddPointLight(root, "LabLight", new Vector3(9f, baseY + 2.7f, 5f),
                new Color(0.72f, 0.9f, 1f), 7f, 2.7f);
            AddPointLight(root, "BedroomLight", new Vector3(-9f, baseY + 2.7f, -5.4f),
                new Color(1f, 0.76f, 0.56f), 7f, 2.2f);
            AddPointLight(root, "ObservatoryLight", new Vector3(9f, baseY + 2.7f, -5.4f),
                new Color(0.65f, 0.75f, 1f), 6f, 2f);
            AddPointLight(root, "BallroomLight", new Vector3(-18f, baseY + 2.7f, 5.5f),
                new Color(1f, 0.78f, 0.55f), 7f, 2.7f);
            AddPointLight(root, "ServantsLight", new Vector3(-18f, baseY + 2.7f, -5.5f),
                new Color(1f, 0.76f, 0.52f), 6f, 2.2f);
            AddPointLight(root, "MusicLight", new Vector3(18f, baseY + 2.7f, 5.5f),
                new Color(0.92f, 0.85f, 1f), 7f, 2.6f);
            AddPointLight(root, "GuestLight", new Vector3(18f, baseY + 2.7f, -5.5f),
                new Color(1f, 0.8f, 0.6f), 6f, 2.2f);

            AddPointLight(root, "UpperCorridorLightA", new Vector3(-15f, baseY + 2.65f, 0f),
                new Color(1f, 0.84f, 0.66f), 7f, 1.8f);
            AddPointLight(root, "UpperCorridorLightB", new Vector3(-5f, baseY + 2.65f, 0f),
                new Color(1f, 0.84f, 0.66f), 7f, 1.8f);
            AddPointLight(root, "UpperCorridorLightC", new Vector3(5f, baseY + 2.65f, 0f),
                new Color(1f, 0.84f, 0.66f), 7f, 1.8f);
            AddPointLight(root, "UpperCorridorLightD", new Vector3(15f, baseY + 2.65f, 0f),
                new Color(1f, 0.84f, 0.66f), 7f, 1.8f);
            AddPointLight(root, "UpperCorridorLightE", new Vector3(20f, baseY + 2.65f, 0f),
                new Color(1f, 0.84f, 0.66f), 6f, 1.7f);
            AddPointLight(root, "UpperElevatorLobbyLight", new Vector3(31.5f, baseY + 2.65f, 0f),
                new Color(0.86f, 0.92f, 1f), 7f, 2.2f);
            AddPointLight(root, "GateLight", new Vector3(-21f, baseY + 2.5f, 0f),
                new Color(0.8f, 0.9f, 1f), 6f, 2.4f);
            AddPointLight(root, "ExitLight", new Vector3(-24.5f, baseY + 2f, 0f),
                new Color(0.85f, 0.92f, 1f), 6f, 2.6f);
        }

        private static void AddPointLight(Transform root, string name, Vector3 position, Color color, float range,
            float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            // 光源放在灯罩下方 12 cm，避免灯罩（实体圆柱）在开启阴影时把光完全遮住。
            go.transform.localPosition = position + new Vector3(0f, -0.12f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = go.AddComponent<Light>();
            // 现实中的吊灯主要向下照明：改用聚光灯形成光池，墙角与天花板自然变暗。
            light.type = LightType.Spot;
            light.spotAngle = 118f;
            light.innerSpotAngle = 72f;
            light.color = color;
            light.range = range * 1.12f;
            // 吊灯房间把主光压暗一些，避免天花板出现刺眼的光斑；亮度转移到吊灯自身的灯源。
            light.intensity = intensity * (name.Contains("DiningLight") || name.Contains("BallroomLight") ? 0.6f : 1.2f);
            light.shadows = LightShadows.None;

            if (!name.Contains("DiningLight") && !name.Contains("BallroomLight"))
            {
                MansionProps.AddPendantLamp(root, position);
            }
        }

        private static void CreateMaterials()
        {
            floorMaterial = CreateMaterial("MansionFloor", new Color(0.3f, 0.23f, 0.16f), 0.22f);
            wallMaterial = CreateMaterial("MansionWall", new Color(0.46f, 0.41f, 0.34f), 0.08f);
            trimMaterial = CreateMaterial("MansionTrim", new Color(0.18f, 0.13f, 0.1f), 0.18f);
            woodMaterial = CreateMaterial("MansionWood", new Color(0.42f, 0.27f, 0.15f), 0.22f);
            woodDarkMaterial = CreateMaterial("MansionWoodDark", new Color(0.23f, 0.14f, 0.08f), 0.28f);
            woodLightMaterial = CreateMaterial("MansionWoodLight", new Color(0.62f, 0.46f, 0.26f), 0.25f);
            metalMaterial = CreateMaterial("MansionMetal", new Color(0.34f, 0.36f, 0.38f), 0.4f, 0.25f);
            copperMaterial = CreateMaterial("MansionCopper", new Color(0.62f, 0.36f, 0.2f), 0.38f, 0.3f);
            paperMaterial = CreateMaterial("MansionPaper", new Color(0.82f, 0.79f, 0.68f), 0.04f);
            goldMaterial = CreateMaterial("MansionGold", new Color(0.78f, 0.6f, 0.26f), 0.5f, 0.25f);
            rugMaterial = CreateMaterial("MansionRug", new Color(0.34f, 0.12f, 0.12f), 0.04f);
            fabricMaterial = CreateMaterial("MansionFabric", new Color(0.27f, 0.34f, 0.38f), 0.05f);
            leatherMaterial = CreateMaterial("MansionLeather", new Color(0.3f, 0.18f, 0.12f), 0.18f);
            porcelainMaterial = CreateMaterial("MansionPorcelain", new Color(0.84f, 0.83f, 0.76f), 0.48f);
            marbleMaterial = CreateMaterial("MansionMarble", new Color(0.66f, 0.65f, 0.61f), 0.55f);
            stoneMaterial = CreateMaterial("MansionStone", new Color(0.3f, 0.3f, 0.29f), 0.08f);
            lampShadeMaterial = CreateEmissive("MansionLampShade", new Color(0.9f, 0.81f, 0.63f), 0.25f);
            tableclothMaterial = CreateMaterial("MansionTablecloth", new Color(0.75f, 0.7f, 0.62f), 0.04f);
            clockFaceMaterial = CreateMaterial("MansionClockFace", new Color(0.8f, 0.77f, 0.66f), 0.12f);
            glassMaterial = CreateMaterial("MansionGlass", new Color(0.32f, 0.38f, 0.42f, 0.35f), 0.45f, 0.05f);
            ApplyGlassProperties(glassMaterial);
            flameMaterial = CreateEmissive("MansionFlame", new Color(1f, 0.68f, 0.28f), 1.1f);
            waxMaterial = CreateMaterial("MansionWax", new Color(0.9f, 0.84f, 0.7f), 0.1f);

            string[] paintingNames = { "MansionPaintingA", "MansionPaintingB", "MansionPaintingC", "MansionPaintingD" };
            paintingMaterials = new Material[paintingNames.Length];
            for (int i = 0; i < paintingNames.Length; i++)
            {
                paintingMaterials[i] = CreateMaterial(paintingNames[i], Color.white, 0.32f);
            }

            Color[] bookColors =
            {
                new Color(0.24f, 0.12f, 0.1f),
                new Color(0.14f, 0.18f, 0.21f),
                new Color(0.26f, 0.2f, 0.12f),
                new Color(0.12f, 0.19f, 0.15f),
                new Color(0.19f, 0.16f, 0.22f),
                new Color(0.16f, 0.16f, 0.16f)
            };

            bookMaterials = new Material[bookColors.Length];
            for (int i = 0; i < bookColors.Length; i++)
            {
                bookMaterials[i] = CreateMaterial("MansionBook" + i, bookColors[i], 0.08f);
            }
        }

        private static void ConfigureRendering()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.13f, 0.125f);
            // 室内夜景：去掉明亮的默认天空盒，让窗户透出暗色背景，而不是刺眼的白色。
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.008f, 0.01f, 0.013f);
            RenderSettings.fogDensity = 0.01f;
        }

        private static void CreateNote(Transform parent, string name, Vector3 position, string title, string body,
            string label, string clueId, float yaw)
        {
            var noteRoot = new GameObject(name);
            noteRoot.transform.SetParent(parent, false);
            noteRoot.transform.localPosition = position;

            var collider = noteRoot.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.38f, 0.2f, 0.32f);

            Box(noteRoot.transform, "Paper", Vector3.zero, new Vector3(0.32f, 0.014f, 0.24f),
                paperMaterial, false, Quaternion.Euler(0f, yaw, 0f));

            var clue = noteRoot.AddComponent<ClueInteractable>();
            clue.Title = title;
            clue.Body = body;
            clue.Label = label;
            clue.ClueId = clueId;
        }

        private static ItemSpawnPoint CreateItemSpawnPoint(Transform parent, string name, string itemId,
            Vector3 position, bool spawnOnStart)
        {
            var pointGo = new GameObject(name);
            pointGo.transform.SetParent(parent, false);
            pointGo.transform.localPosition = position;

            var point = pointGo.AddComponent<ItemSpawnPoint>();
            point.ItemId = itemId;
            point.SpawnOnStart = spawnOnStart;
            return point;
        }

        private static Material CreateMaterial(string materialName, Color color, float smoothness, float metallic = 0f)
        {
            Shader shader = FindShader();
            if (shader == null)
            {
                Debug.LogWarning("[Mansion] 找不到可用着色器，将使用默认材质。");
                return null;
            }

#if UNITY_EDITOR
            // 编辑模式下生成场景时，把材质保存为资源，避免场景保存后丢失引用。
            if (!Application.isPlaying)
            {
                return CreateOrUpdateAssetMaterial(materialName, color, smoothness, metallic, shader);
            }
#endif

            var material = new Material(shader);
            material.name = materialName;
            material.color = color;
            ApplyMaterialProperties(material, smoothness, metallic);
            ApplyGeneratedTexture(material, materialName);
            return material;
        }

        private static Shader FindShader()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            return shader;
        }

        private static void ApplyMaterialProperties(Material material, float smoothness, float metallic)
        {
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
        }

        /// <summary>
        /// 把 Standard 材质切换为透明玻璃模式，避免窗户、展柜和酒杯变成发光的蓝色塑料。
        /// </summary>
        private static void ApplyGlassProperties(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;

            // 旧版本曾把玻璃做成自发光材质，这里清理掉残留的自发光设置。
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            material.DisableKeyword("_EMISSION");

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(material);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 优先使用 Poly Haven 的真实 PBR 贴图（albedo + 法线 + 金属/光滑度打包图）。
        /// 贴图缺失时返回 false，由旧的程序化贴图兜底。
        /// </summary>
        private static bool TryApplyRealMaterial(Material material, string materialName)
        {
            string setName;
            Vector2 tiling;
            Color tint;
            float bumpScale;
            float glossScale;

            switch (materialName)
            {
                case "MansionFloor":
                    setName = "Floor_Parquet";
                    tiling = new Vector2(6f, 6f);
                    tint = new Color(0.6f, 0.54f, 0.48f);
                    bumpScale = 0.45f;
                    glossScale = 0.55f;
                    break;
                case "MansionWall":
                    setName = "Wall_Plaster";
                    tiling = new Vector2(3f, 2f);
                    tint = new Color(0.82f, 0.8f, 0.76f);
                    bumpScale = 0.18f;
                    glossScale = 0.3f;
                    break;
                case "MansionTrim":
                    setName = "Wood_Dark";
                    tiling = new Vector2(2f, 2f);
                    tint = new Color(0.55f, 0.5f, 0.45f);
                    bumpScale = 0.6f;
                    glossScale = 0.45f;
                    break;
                case "MansionWood":
                    setName = "Wood_Dark";
                    tiling = new Vector2(2.5f, 2.5f);
                    tint = new Color(0.82f, 0.76f, 0.68f);
                    bumpScale = 0.6f;
                    glossScale = 0.5f;
                    break;
                case "MansionWoodDark":
                    setName = "Wood_Fine";
                    tiling = new Vector2(2.5f, 2.5f);
                    tint = new Color(0.62f, 0.55f, 0.48f);
                    bumpScale = 0.6f;
                    glossScale = 0.4f;
                    break;
                case "MansionWoodLight":
                    setName = "Wood_Dark";
                    tiling = new Vector2(2.5f, 2.5f);
                    tint = new Color(1f, 0.9f, 0.78f);
                    bumpScale = 0.6f;
                    glossScale = 0.5f;
                    break;
                case "MansionFabric":
                    setName = "Fabric_Jacquard";
                    tiling = new Vector2(2f, 2f);
                    tint = Color.white;
                    bumpScale = 0.9f;
                    glossScale = 0.4f;
                    break;
                case "MansionLeather":
                    setName = "Leather_Brown";
                    tiling = new Vector2(2f, 2f);
                    tint = new Color(0.9f, 0.86f, 0.82f);
                    bumpScale = 0.9f;
                    glossScale = 0.5f;
                    break;
                case "MansionMarble":
                    setName = "Marble_Cream";
                    tiling = Vector2.one;
                    tint = new Color(0.92f, 0.92f, 0.9f);
                    bumpScale = 0.5f;
                    glossScale = 0.55f;
                    break;
                default:
                    return false;
            }

            string textureRoot = "Assets/Art/Textures/Real/" + setName + "/" + setName;
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "_Albedo.jpg");
            if (albedo == null)
            {
                return false;
            }

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "_Normal.jpg");
            Texture2D packed = AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "_MetallicSmoothness.png");

            material.color = tint;
            material.mainTexture = albedo;
            material.mainTextureScale = tiling;

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", tiling);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", bumpScale);
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            if (packed != null)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.SetTextureScale("_MetallicGlossMap", tiling);
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Glossiness", glossScale);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else
            {
                material.DisableKeyword("_METALLICGLOSSMAP");
            }

            return true;
        }

        private static void ApplyGeneratedTexture(Material material, string materialName)
        {
            if (material == null)
            {
                return;
            }

            if (TryApplyRealMaterial(material, materialName))
            {
                return;
            }

            string textureName = null;
            Vector2 tiling = Vector2.one;

            switch (materialName)
            {
                case "MansionFloor":
                    textureName = "FloorParquet";
                    tiling = new Vector2(8f, 8f);
                    break;
                case "MansionWall":
                    textureName = "WallPlaster";
                    tiling = new Vector2(4f, 2f);
                    break;
                case "MansionTrim":
                case "MansionWood":
                case "MansionWoodDark":
                case "MansionWoodLight":
                    textureName = "WoodGrain";
                    tiling = new Vector2(1.5f, 1.5f);
                    break;
                case "MansionMetal":
                case "MansionCopper":
                case "MansionGold":
                    textureName = "MetalBrushed";
                    tiling = new Vector2(1f, 1f);
                    break;
                case "MansionRug":
                    textureName = "CarpetWeave";
                    tiling = new Vector2(3f, 3f);
                    break;
                case "MansionFabric":
                    textureName = "FabricWeave";
                    tiling = new Vector2(2f, 2f);
                    break;
                case "MansionLeather":
                    textureName = "LeatherGrain";
                    tiling = new Vector2(2f, 2f);
                    break;
                case "MansionPorcelain":
                    textureName = "PorcelainGlaze";
                    break;
                case "MansionMarble":
                    textureName = "MarbleVeins";
                    break;
                case "MansionStone":
                    textureName = "StoneSpeckle";
                    tiling = new Vector2(2f, 2f);
                    break;
                case "MansionLampShade":
                    textureName = "FabricWeave";
                    tiling = new Vector2(2f, 1f);
                    break;
                case "MansionTablecloth":
                    textureName = "TableclothDamask";
                    tiling = new Vector2(4f, 2f);
                    break;
                case "MansionPaintingA":
                    textureName = "OilPaintingLandscape";
                    break;
                case "MansionPaintingB":
                    textureName = "OilPaintingStillLife";
                    break;
                case "MansionPaintingC":
                    textureName = "OilPaintingPortrait";
                    break;
                case "MansionPaintingD":
                    textureName = "OilPaintingNightHarbor";
                    break;
            }

            if (textureName == null)
            {
                return;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Textures/" + textureName + ".png");

            if (texture == null)
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
        }
#else
        // Player 构建不执行编辑器贴图烘焙，场景材质已作为资源保存。
        private static void ApplyGeneratedTexture(Material material, string materialName)
        {
        }
#endif

#if UNITY_EDITOR
        private static Material CreateOrUpdateAssetMaterial(string materialName, Color color, float smoothness,
            float metallic, Shader shader)
        {
            const string folder = "Assets/Art/Materials/Generated";

            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art"))
                {
                    AssetDatabase.CreateFolder("Assets", "Art");
                }

                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets/Art", "Materials");
                }

                AssetDatabase.CreateFolder("Assets/Art/Materials", "Generated");
            }

            string assetPath = folder + "/" + materialName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            if (material == null)
            {
                material = new Material(shader);
                material.name = materialName;
                material.color = color;
                ApplyMaterialProperties(material, smoothness, metallic);
                ApplyGeneratedTexture(material, materialName);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                if (material.shader != shader)
                {
                    material.shader = shader;
                }

                material.color = color;
                ApplyMaterialProperties(material, smoothness, metallic);
                ApplyGeneratedTexture(material, materialName);
                EditorUtility.SetDirty(material);
            }

            return material;
        }
#endif

        private static Material CreateEmissive(string materialName, Color color, float intensity)
        {
            var material = CreateMaterial(materialName, color, 0.35f);
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * intensity);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(material);
                }
#endif
            }

            return material;
        }

        private static GameObject Box(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale,
            Material material, bool keepCollider = false, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            if (localRotation.HasValue)
            {
                go.transform.localRotation = localRotation.Value;
            }

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            RemoveCollider(go, keepCollider);
            return go;
        }

        private static GameObject Sphere(Transform parent, string objectName, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            RemoveCollider(go, false);
            return go;
        }

        private static GameObject Cylinder(Transform parent, string objectName, Vector3 localPosition,
            Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRotation;

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            RemoveCollider(go, keepCollider);
            return go;
        }

        private static void RemoveCollider(GameObject go, bool keepCollider)
        {
            if (keepCollider)
            {
                return;
            }

            Collider collider = go.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            collider.enabled = false;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(collider);
                return;
            }
#endif
            Object.Destroy(collider);
        }
    }
}
