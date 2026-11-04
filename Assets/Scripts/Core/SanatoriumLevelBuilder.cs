using System.Collections.Generic;
using PuzzleEscape.Data;
using PuzzleEscape.Interaction;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 第二张地图「夜巡疗养院」：地下室 + 四层主楼，共 5 层。
    /// 每层中央大厅（1~4 层带护士站柜台）、四周三十间功能房，两个楼梯间贯通上下。
    /// 每个房间都有一份记录文本，交代「异变」的来龙去脉。
    /// </summary>
    public static class SanatoriumLevelBuilder
    {
        private const float FloorHeight = 4.2f;
        private const float WallHeight = 3.9f;
        private const float SlabThickness = 0.3f;
        private const float WallThickness = 0.25f;
        private const float DoorWidth = 1.8f;
        private const float DoorHeight = 2.3f;

        // 平面：矩形病区（84 x 33 m），两条平行走廊 + 中间医护核心
        //   z 21 ~ 14   北侧病房/后勤房
        //   z 14 ~ 10.4 后勤走廊
        //   z 10.4 ~ 1.6 中间核心：办公室/检查室/手术室/药房 + 楼梯间 + 中央通道
        //   z 1.6 ~ -2   主走廊（病人走廊）
        //   z -2 ~ -12   南侧病房/治疗室
        private const float HalfWidth = 42f;
        private const float SouthZ = -12f;
        private const float CorridorA0 = -2f;
        private const float CorridorA1 = 1.6f;
        private const float CorridorB0 = 10.4f;
        private const float CorridorB1 = 14f;
        private const float NorthZ = 21f;
        private const float RoomWidth = 8.4f;

        private const int BasementLevel = 0;
        private const int TopLevel = 4;
        private const int Levels = 5;

        private const int BandRooms = 10;
        private const int MiddleRooms = 6;
        private const int RoomsPerFloor = BandRooms * 2 + MiddleRooms;

        // 中间核心：两个楼梯间夹着中央通道
        private const float StairLeftA = -8.4f;
        private const float StairLeftB = -2.4f;
        private const float StairRightA = 2.4f;
        private const float StairRightB = 8.4f;
        private const float PassageA = -2.4f;
        private const float PassageB = 2.4f;
        private const float StairFlightStartZ = 2.8f;
        private const float StairFlightEndZ = 8.2f;
        private const float StairWidth = 3.2f;
        private const int StairSteps = 20;

        // 备用逃生口：4F 东外墙的消防连廊（车库出口在南外墙，位置由房间算出）
        private const float FireExitZ = 17.5f;

        internal static Material floorMaterial;
        internal static Material wallMaterial;
        internal static Material ceilingMaterial;
        internal static Material trimMaterial;
        internal static Material metalMaterial;
        internal static Material counterMaterial;
        internal static Material fabricMaterial;
        internal static Material paperMaterial;
        internal static Material glassMaterial;
        internal static Material panelMaterial;
        internal static Material woodMaterial;
        internal static Material woodDarkMaterial;
        internal static Material bloodMaterial;
        internal static Material bloodDarkMaterial;
        internal static Material bloodFreshMaterial;
        internal static Material bloodDecalMaterial;
        internal static Material bloodSplatterMaterial;
        internal static Material rustMaterial;
        internal static Material bodyBagMaterial;

        private enum RoomType
        {
            Ward,
            Office,
            Exam,
            Treatment,
            Surgery,
            Pharmacy,
            Duty,
            Storage,
            Lounge,
            Archive,
            Utility,
            Lobby
        }

        private class Room
        {
            public string Name;
            public RoomType Type;
            public float X0;
            public float X1;
            public float Z0;
            public float Z1;
            public char DoorSide;
            public float DoorCenter;
            public int Index;
            public int Level;
            public Transform Group;

            public Vector3 Center => new Vector3((X0 + X1) * 0.5f, 0f, (Z0 + Z1) * 0.5f);
            public float Width => X1 - X0;
            public float Depth => Z1 - Z0;
        }

        public static void Build()
        {
            CreateMaterials();
            ConfigureRendering();
            SanatoriumProps.ResetCount();

            var root = new GameObject("Environment").transform;

            for (int level = BasementLevel; level <= TopLevel; level++)
            {
                BuildLevel(root, level);
            }

            Debug.Log("[PuzzleEscape] 疗养院生成完成：5 层（每层结构不同），真实模型摆件 " +
                      SanatoriumProps.Placed + " 件。");
        }

        // ------------------------------------------------------------------
        // 楼层
        // ------------------------------------------------------------------
        private static void BuildLevel(Transform root, int level)
        {
            float baseY = LevelY(level);
            Transform floorRoot = new GameObject("Floor_" + LevelName(level)).transform;
            floorRoot.SetParent(root, false);

            BuildSlabs(floorRoot, level, baseY);

            List<Room> rooms = CreateRooms(level);
            BuildWalls(floorRoot, level, baseY, rooms);
            BuildCorridorFurniture(floorRoot, level, baseY, rooms);
            foreach (Room room in rooms)
            {
                BuildRoomContents(floorRoot, room, baseY);
                AddCeilingLight(floorRoot, room, baseY);
            }

            if (level < TopLevel)
            {
                BuildStairs(floorRoot, baseY);
            }

            if (level == 1)
            {
                BuildExit(floorRoot, rooms, baseY);
            }

            if (level == BasementLevel)
            {
                BuildGarage(floorRoot, baseY, rooms);
            }

            if (level == TopLevel)
            {
                BuildFireEscape(floorRoot, baseY);
            }

            Debug.Log("[PuzzleEscape] 疗养院 " + LevelName(level) + " 完成：房间 " + rooms.Count + " 间。");
        }

        private static float LevelY(int level)
        {
            return (level - 1) * FloorHeight;
        }

        private static string LevelName(int level)
        {
            return level == 0 ? "B1" : level + "F";
        }

        // ------------------------------------------------------------------
        // 楼板
        // ------------------------------------------------------------------
        private static void BuildSlabs(Transform root, int level, float baseY)
        {
            var holes = new List<Rect>();

            // 上一层楼板要为楼梯留洞（楼梯在本层从 baseY 升到 baseY + FloorHeight）
            if (level > BasementLevel)
            {
                holes.Add(StairHole(StairLeftA, StairLeftB));
                holes.Add(StairHole(StairRightA, StairRightB));
            }

            SlabWithHoles(root, "Floor", baseY, -HalfWidth, HalfWidth, SouthZ, NorthZ, holes);

            if (level == TopLevel)
            {
                Slab(root, "Roof", baseY + WallHeight + SlabThickness * 0.5f,
                    -HalfWidth, HalfWidth, SouthZ, NorthZ, true);
            }
        }

        private static Rect StairHole(float x0, float x1)
        {
            // 洞口只覆盖楼梯本身（左右各留 15cm 余量），
            // 楼梯间两侧剩下的地面保持实心，玩家不会从缝隙掉下去。
            float center = (x0 + x1) * 0.5f;
            float half = StairWidth * 0.5f + 0.15f;
            return new Rect(center - half, StairFlightStartZ, half * 2f,
                StairFlightEndZ - StairFlightStartZ);
        }

        private static void Slab(Transform root, string name, float y, float x0, float x1,
            float z0, float z1, bool roof = false)
        {
            if (x1 - x0 <= 0.01f || z1 - z0 <= 0.01f)
            {
                return;
            }

            Box(root, name, new Vector3((x0 + x1) * 0.5f, y - SlabThickness * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0, SlabThickness, z1 - z0),
                roof ? ceilingMaterial : floorMaterial, true);
        }

        /// <summary>铺一整块楼板，并在指定矩形处留洞（用于楼梯）。</summary>
        private static void SlabWithHoles(Transform root, string name, float y,
            float x0, float x1, float z0, float z1, List<Rect> holes)
        {
            if (holes == null || holes.Count == 0)
            {
                Slab(root, name, y, x0, x1, z0, z1);
                return;
            }

            // 先裁剪到楼板范围内
            var clipped = new List<Rect>();
            foreach (Rect hole in holes)
            {
                float hz0 = Mathf.Max(hole.yMin, z0);
                float hz1 = Mathf.Min(hole.yMax, z1);
                float hx0 = Mathf.Max(hole.xMin, x0);
                float hx1 = Mathf.Min(hole.xMax, x1);

                if (hz1 > hz0 && hx1 > hx0)
                {
                    clipped.Add(new Rect(hx0, hz0, hx1 - hx0, hz1 - hz0));
                }
            }

            if (clipped.Count == 0)
            {
                Slab(root, name, y, x0, x1, z0, z1);
                return;
            }

            // 按所有洞口的 z 边界切带，每条带内只处理与该带相交的洞口，
            // 这样两个楼梯洞处在同一 z 带时也不会互相把对方盖住。
            var zs = new List<float> { z0, z1 };
            foreach (Rect hole in clipped)
            {
                zs.Add(hole.yMin);
                zs.Add(hole.yMax);
            }

            zs.Sort();

            for (int i = 0; i < zs.Count - 1; i++)
            {
                float za = zs[i];
                float zb = zs[i + 1];

                if (zb - za < 0.01f)
                {
                    continue;
                }

                float zc = (za + zb) * 0.5f;
                var holeSpans = new List<Vector2>();

                foreach (Rect hole in clipped)
                {
                    if (zc > hole.yMin && zc < hole.yMax)
                    {
                        holeSpans.Add(new Vector2(hole.xMin, hole.xMax));
                    }
                }

                if (holeSpans.Count == 0)
                {
                    Slab(root, name, y, x0, x1, za, zb);
                    continue;
                }

                holeSpans.Sort((a, b) => a.x.CompareTo(b.x));
                float cursor = x0;

                foreach (Vector2 span in holeSpans)
                {
                    if (span.x > cursor + 0.01f)
                    {
                        Slab(root, name, y, cursor, span.x, za, zb);
                    }

                    cursor = Mathf.Max(cursor, span.y);
                }

                if (x1 > cursor + 0.01f)
                {
                    Slab(root, name, y, cursor, x1, za, zb);
                }
            }
        }

        // ------------------------------------------------------------------
        // 走廊与医护核心
        // ------------------------------------------------------------------
        private static void BuildCorridorFurniture(Transform root, int level, float baseY,
            List<Room> rooms)
        {
            var group = new GameObject("Corridors").transform;
            group.SetParent(root, false);

            // 主走廊两侧的门洞位置：长椅、平车、绿植必须避开，不能挡在门口
            var southDoorXs = new List<float>();
            var northDoorXs = new List<float>();

            foreach (Room room in rooms)
            {
                if (Mathf.Abs(room.Z1 - CorridorA0) < 0.01f || Mathf.Abs(room.Z0 - CorridorA0) < 0.01f)
                {
                    southDoorXs.Add(room.DoorCenter);
                }

                if (Mathf.Abs(room.Z1 - CorridorA1) < 0.01f || Mathf.Abs(room.Z0 - CorridorA1) < 0.01f)
                {
                    northDoorXs.Add(room.DoorCenter);
                }
            }

            northDoorXs.Add((StairLeftA + StairLeftB) * 0.5f);
            northDoorXs.Add((StairRightA + StairRightB) * 0.5f);

            // 主走廊（病人走廊）灯带 + 吸顶灯盘
            float corridorAMidZ = (CorridorA0 + CorridorA1) * 0.5f;
            for (float x = -HalfWidth + 7f; x <= HalfWidth - 7f; x += 8f)
            {
                PointLight(group, new Vector3(x, baseY + WallHeight - 0.35f, corridorAMidZ),
                    8f, 0.7f, new Color(0.86f, 0.9f, 0.92f));
                Box(group, "CeilingPanel", new Vector3(x, baseY + WallHeight - 0.06f, corridorAMidZ),
                    new Vector3(0.65f, 0.04f, 0.65f), panelMaterial);
            }

            // 后勤走廊灯带
            float corridorBMidZ = (CorridorB0 + CorridorB1) * 0.5f;
            for (float x = -HalfWidth + 9f; x <= HalfWidth - 9f; x += 10f)
            {
                PointLight(group, new Vector3(x, baseY + WallHeight - 0.35f, corridorBMidZ),
                    8f, 0.55f, new Color(0.75f, 0.82f, 0.86f));
                Box(group, "CeilingPanel", new Vector3(x, baseY + WallHeight - 0.06f, corridorBMidZ),
                    new Vector3(0.6f, 0.04f, 0.6f), panelMaterial);
            }

            // 走廊标识牌（门楣上方）
            for (float x = -36f; x <= 36f; x += 12f)
            {
                Box(group, "SignPlaque",
                    new Vector3(x, baseY + 2.65f, CorridorA1 - WallThickness * 0.5f - 0.03f),
                    new Vector3(0.9f, 0.32f, 0.04f), trimMaterial);
            }

            // 消防柜（挂在扶手上方，避免和 0.95m 的走廊扶手穿模）
            foreach (float x in new[] { -30f, -10f, 10f, 30f })
            {
                Box(group, "FireCabinet",
                    new Vector3(x, baseY + 1.78f, CorridorA0 + WallThickness * 0.5f + 0.14f),
                    new Vector3(0.5f, 0.7f, 0.26f), metalMaterial, true);
                Box(group, "FireCabinetGlass",
                    new Vector3(x, baseY + 1.9f, CorridorA0 + WallThickness * 0.5f + 0.28f),
                    new Vector3(0.36f, 0.28f, 0.03f), glassMaterial);
            }

            // 地下室：天花板上的管道与线槽
            if (level == BasementLevel)
            {
                for (int i = 0; i < 3; i++)
                {
                    Box(group, "Pipe",
                        new Vector3(0f, baseY + WallHeight - 0.22f - i * 0.14f,
                            CorridorA0 + 0.45f + i * 0.12f),
                        new Vector3(HalfWidth * 2f - 1f, 0.12f, 0.12f), metalMaterial);
                }

                Box(group, "CableTray",
                    new Vector3(0f, baseY + WallHeight - 0.3f, CorridorB0 + 0.5f),
                    new Vector3(HalfWidth * 2f - 2f, 0.08f, 0.5f), trimMaterial);
            }

            // 走廊里的恐怖痕迹（所有楼层都有）
            AddCorridorHorror(group, baseY);

            // 中央通道 + 护士站
            if (level == BasementLevel)
            {
                BuildBasementStore(group, baseY);
                return;
            }

            if (level == 1)
            {
                BuildGrandReception(group, baseY);
            }
            else
            {
            float passageZ = (CorridorA1 + CorridorB0) * 0.5f;

            // 柜台靠西侧，东侧留出 1.4m 通行宽度
            Box(group, "NurseCounter", new Vector3(-1.1f, baseY + 0.55f, passageZ - 1.2f),
                new Vector3(2.2f, 1.1f, 0.8f), counterMaterial, true);
            Box(group, "NurseCounterTop", new Vector3(-1.1f, baseY + 1.13f, passageZ - 1.2f),
                new Vector3(2.4f, 0.08f, 0.95f), trimMaterial);
            Box(group, "NurseCabinet", new Vector3(-1.1f, baseY + 1.05f, passageZ + 1.5f),
                new Vector3(2.2f, 2.1f, 0.5f), counterMaterial, true);

            for (int i = -1; i <= 1; i++)
            {
                Box(group, "NurseMonitor", new Vector3(-1.1f + i * 0.65f, baseY + 1.36f, passageZ - 1.2f),
                    new Vector3(0.5f, 0.38f, 0.08f), metalMaterial);
            }

            PointLight(group, new Vector3(0f, baseY + WallHeight - 0.35f, passageZ),
                7f, 0.9f, new Color(0.9f, 0.93f, 0.95f));
            }

            // 走廊里的长椅：用真实木椅模型，位置避开所有门洞与楼梯口
            float[] benchX = { -33f, -25f, -16.8f, 16.8f, 25f, 33f };
            foreach (float x in benchX)
            {
                if (BlocksDoor(northDoorXs, x, 1.1f))
                {
                    continue;
                }

                float z = CorridorA1 - 0.85f;
                if (SanatoriumProps.Place(group, "painted_wooden_bench",
                        new Vector3(x, baseY, z), 180f, 0.55f))
                {
                    continue;
                }

                Box(group, "WaitingBench", new Vector3(x, baseY + 0.24f, z),
                    new Vector3(2.2f, 0.12f, 0.55f), fabricMaterial, true);
                Box(group, "WaitingBenchLeg", new Vector3(x, baseY + 0.11f, z),
                    new Vector3(2f, 0.22f, 0.45f), metalMaterial);
            }

            // 平车：靠南墙摆放，整体留在走廊里，也不能堵在门口
            for (int i = 0; i < 3; i++)
            {
                float x = -32f + i * 28f;
                if (BlocksDoor(southDoorXs, x, 0.5f))
                {
                    continue;
                }

                Box(group, "Gurney", new Vector3(x, baseY + 0.56f, CorridorA0 + 1.2f),
                    new Vector3(1f, 0.12f, 2f), fabricMaterial, true);
                Box(group, "GurneyFrame", new Vector3(x, baseY + 0.25f, CorridorA0 + 1.2f),
                    new Vector3(0.92f, 0.5f, 1.9f), metalMaterial);
            }

            // 主走廊靠南墙的绿植（都放在两扇门之间）
            foreach (float x in new[] { -33f, -8.4f, 8.4f, 33f })
            {
                if (BlocksDoor(southDoorXs, x, 0.3f))
                {
                    continue;
                }

                SanatoriumProps.Place(group, "potted_plant_04",
                    new Vector3(x, baseY, CorridorA0 + 0.6f), 0f, 1.15f);
            }

            // 东西连廊里的落地钟与长椅：只有一层（接待大厅）是通开的，
            // 二到四层中段是封闭房间，摆在这里会穿进房间里。
            if (level == 1)
            {
                foreach (float x in new[] { -37.8f, 37.8f })
                {
                    SanatoriumProps.Place(group, "vintage_grandfather_clock_01",
                        new Vector3(x, baseY, 6f), 90f, 2f);
                    SanatoriumProps.Place(group, "painted_wooden_bench",
                        new Vector3(x, baseY, 8.6f), 180f, 0.55f);
                }
            }

            // 墙面壁灯：两条走廊的墙上各挂一排，位置都避开门洞（门洞处没有墙可挂）
            foreach (float x in new[] { -36f, -27f, -18f, -9f, 9f, 18f, 27f, 36f })
            {
                if (!BlocksDoor(southDoorXs, x, 0.12f))
                {
                    SanatoriumProps.PlaceSized(group, "industrial_wall_sconce",
                        new Vector3(x, baseY + 2.15f, CorridorA0 + 0.14f), 0f,
                        new Vector3(0.24f, 0.42f, 0.18f), false);
                }

                if (!BlocksDoor(northDoorXs, x, 0.12f))
                {
                    SanatoriumProps.PlaceSized(group, "industrial_wall_sconce",
                        new Vector3(x, baseY + 2.15f, CorridorA1 - 0.14f), 180f,
                        new Vector3(0.24f, 0.42f, 0.18f), false);
                }
            }
        }

        /// <summary>沿走廊摆东西时检查是否压到门洞（门洞宽度 + 摆件半宽 + 余量）。</summary>
        private static bool BlocksDoor(List<float> doorCenters, float x, float halfWidth)
        {
            if (doorCenters == null)
            {
                return false;
            }

            foreach (float center in doorCenters)
            {
                if (Mathf.Abs(x - center) < halfWidth + DoorWidth * 0.5f + 0.25f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>一层接待大厅：L 形接待台、背景标识墙、候诊沙发与绿植。</summary>
        private static void BuildGrandReception(Transform group, float baseY)
        {
            float cx = -14f;
            float cz = (CorridorA1 + CorridorB0) * 0.5f;

            // L 形接待台
            Box(group, "ReceptionFront", new Vector3(cx, baseY + 0.55f, cz),
                new Vector3(8f, 1.1f, 0.9f), counterMaterial, true);
            Box(group, "ReceptionTop", new Vector3(cx, baseY + 1.13f, cz),
                new Vector3(8.2f, 0.08f, 1.1f), trimMaterial);
            Box(group, "ReceptionSide", new Vector3(cx - 4.4f, baseY + 0.55f, cz + 1.6f),
                new Vector3(0.9f, 1.1f, 3.4f), counterMaterial, true);
            Box(group, "ReceptionSideTop", new Vector3(cx - 4.4f, baseY + 1.13f, cz + 1.6f),
                new Vector3(1.1f, 0.08f, 3.5f), trimMaterial);

            // 背景标识墙
            Box(group, "ReceptionBackWall", new Vector3(cx, baseY + 1.7f, cz - 2.8f),
                new Vector3(9.4f, 3.4f, 0.3f), trimMaterial, true);
            Box(group, "ReceptionSign", new Vector3(cx, baseY + 2.5f, cz - 2.62f),
                new Vector3(5.2f, 0.8f, 0.08f), panelMaterial);
            Box(group, "ReceptionDeskLamp", new Vector3(cx + 2.6f, baseY + 1.32f, cz),
                new Vector3(0.3f, 0.3f, 0.3f), glassMaterial);

            // 护士长留言：交代整条通关顺序，玩家一进大厅就能读到
            AddStoryNote(group, new Vector3(cx - 2.4f, baseY + 1.5f, cz - 2.62f), 0f,
                "护士长留言",
                "异变那晚，楼里所有电子锁同时落下，人也一个接一个不见了。\n" +
                "想活着出去，只有三条路：\n" +
                "· 一层门厅正门——要主门钥匙，还得先让总闸通电。\n" +
                "· 地下车库卷帘门——要车库钥匙和卷帘门曲柄。\n" +
                "· 四层东侧消防连廊——要消防连廊钥匙和消防斧。\n\n" +
                "总闸缺三枚保险丝：地下库房、地下档案室、地下储藏室各一枚。\n" +
                "各层南侧 2 号值班室都留着下一把钥匙；二层药房、三层手术室、四层档案室与钥匙一一对应。\n" +
                "通行卡从 1F 南侧 1 号病房开始，一层层往上去。\n\n" +
                "如果你还能读到这里——别回头。",
                "sanatorium_lobby_note");

            // 候诊沙发与茶几（全部收在接待大厅内，不压到后勤走廊的隔墙）
            SanatoriumProps.Place(group, "Sofa_01", new Vector3(cx - 3f, baseY, cz + 2.6f), 180f, 0.85f);
            SanatoriumProps.Place(group, "Sofa_01", new Vector3(cx + 3f, baseY, cz + 2.6f), 180f, 0.85f);
            SanatoriumProps.PlaceSized(group, "small_wooden_table_01",
                new Vector3(cx, baseY, cz + 1.4f), 0f, new Vector3(1.4f, 0.62f, 0.7f), false);

            // 大厅两侧绿植
            foreach (float x in new[] { -34f, -24f, 24f, 34f })
            {
                SanatoriumProps.Place(group, "potted_plant_04",
                    new Vector3(x, baseY, cz + 4.6f), 0f, 1.25f);
                SanatoriumProps.Place(group, "potted_plant_04",
                    new Vector3(x, baseY, cz - 4.6f), 0f, 1.25f);
            }

            // 大厅顶部灯带
            for (float x = -30f; x <= 30f; x += 10f)
            {
                SanatoriumProps.PlaceSized(group, "Chandelier_01",
                    new Vector3(x, baseY + WallHeight - 0.9f, cz), 0f,
                    new Vector3(1.2f, 0.9f, 1.2f), false);
            }
        }

        private static void BuildBasementStore(Transform group, float baseY)
        {
            // 地下室走廊两侧堆货箱，中央通道留空
            for (int i = 0; i < 8; i++)
            {
                float x = -36f + i * 10f;
                if (Mathf.Abs(x) < 12f)
                {
                    continue;
                }

                Box(group, "CrateStack", new Vector3(x, baseY + 0.6f, CorridorA0 + 0.7f),
                    new Vector3(1.6f, 1.2f, 1.6f), counterMaterial, true);
                Box(group, "CrateLid", new Vector3(x, baseY + 1.22f, CorridorA0 + 0.7f),
                    new Vector3(1.66f, 0.06f, 1.66f), trimMaterial);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = -30f + i * 18f;
                Box(group, "Shelf", new Vector3(x, baseY + 1f, CorridorB0 + 0.7f),
                    new Vector3(2f, 2f, 0.6f), counterMaterial, true);
            }
        }

        // ------------------------------------------------------------------
        // 房间划分
        // ------------------------------------------------------------------
        /// <summary>每层每侧的病房数量，让各楼层结构明显不同。</summary>
        private static int BandRoomCount(int level)
        {
            switch (level)
            {
                case 0: return 6;    // B1 仓储层：大空间
                case 1: return 10;   // 1F 门诊层
                case 2: return 12;   // 2F 住院层：房间最密
                case 3: return 8;    // 3F 手术层：大房间
                default: return 9;   // 4F 行政区
            }
        }

        /// <summary>中间核心的房间数（0 表示地下室不设中间房间）。</summary>
        private static int MiddleRoomCount(int level)
        {
            switch (level)
            {
                case 0: return 0;
                case 1: return 0;    // 1F 中间完全打开，做成接待大厅
                case 2: return 6;
                case 3: return 4;
                default: return 2;
            }
        }

        private static List<Room> CreateRooms(int level)
        {
            var rooms = new List<Room>();
            int bandCount = BandRoomCount(level);
            float roomWidth = (HalfWidth * 2f) / bandCount;

            RoomType[] south = level == BasementLevel
                ? BasementPattern(bandCount)
                : SouthPattern(level, bandCount);
            RoomType[] north = level == BasementLevel
                ? BasementPattern(bandCount)
                : NorthPattern(level, bandCount);

            // 保证主线与两条备用路线的关键房型一定存在（各层房型顺序会轮换）
            switch (level)
            {
                case BasementLevel:
                    south[0] = RoomType.Storage;    // 保险丝 · 丙
                    south[1] = RoomType.Utility;    // 配电间总闸
                    south[3] = RoomType.Duty;       // 职员钥匙
                    south[4] = RoomType.Archive;    // 保险丝 · 乙（门锁职员钥匙）
                    south[5] = RoomType.Utility;    // 车库（卷帘门出口）
                    north[0] = RoomType.Utility;    // 洗衣房 → 库房钥匙
                    north[1] = RoomType.Storage;    // 库房（门锁库房钥匙）→ 保险丝 · 甲
                    north[2] = RoomType.Storage;    // 车库钥匙
                    break;

                case 1:
                    south[0] = RoomType.Ward;       // 二层通行卡
                    south[1] = RoomType.Duty;       // 病房钥匙
                    south[3] = RoomType.Treatment;  // 病人物品柜钥匙（门锁病房钥匙）
                    break;

                case 2:
                    south[1] = RoomType.Duty;       // 药库钥匙
                    break;

                case 3:
                    south[1] = RoomType.Duty;       // 手术区钥匙
                    break;

                case 4:
                    south[1] = RoomType.Duty;       // 院长室钥匙
                    south[2] = RoomType.Ward;       // 值班钥匙（门锁病人物品柜钥匙）
                    south[3] = RoomType.Treatment;  // 消防斧
                    north[0] = RoomType.Archive;    // 主门钥匙
                    north[1] = RoomType.Storage;    // 消防连廊钥匙
                    break;
            }

            for (int i = 0; i < bandCount; i++)
            {
                float x0 = -HalfWidth + i * roomWidth;
                float center = x0 + roomWidth * 0.5f;

                rooms.Add(new Room
                {
                    Name = LevelName(level) + "_南" + (i + 1) + "_" + TypeName(south[i]),
                    Type = south[i],
                    X0 = x0,
                    X1 = x0 + roomWidth,
                    Z0 = SouthZ,
                    Z1 = CorridorA0,
                    DoorSide = 'N',
                    DoorCenter = center,
                    Level = level,
                    Index = i
                });

                rooms.Add(new Room
                {
                    Name = LevelName(level) + "_北" + (i + 1) + "_" + TypeName(north[i]),
                    Type = north[i],
                    X0 = x0,
                    X1 = x0 + roomWidth,
                    Z0 = CorridorB1,
                    Z1 = NorthZ,
                    DoorSide = 'S',
                    DoorCenter = center,
                    Level = level,
                    Index = i
                });
            }

            int middleCount = MiddleRoomCount(level);
            if (middleCount > 0)
            {
                int perSide = Mathf.Max(1, middleCount / 2);
                RoomType[] middle = MiddlePattern(level, perSide * 2);

                if (level == 2)
                {
                    middle[0] = RoomType.Pharmacy;     // 三层通行卡
                }
                else if (level == 3)
                {
                    middle[0] = RoomType.Surgery;      // 四层通行卡
                }

                float westSpan = StairLeftA + HalfWidth;
                float eastSpan = HalfWidth - StairRightB;
                float westWidth = westSpan / perSide;
                float eastWidth = eastSpan / perSide;

                for (int i = 0; i < perSide; i++)
                {
                    float x0 = -HalfWidth + i * westWidth;
                    rooms.Add(new Room
                    {
                        Name = LevelName(level) + "_西核心" + (i + 1) + "_" + TypeName(middle[i]),
                        Type = middle[i],
                        X0 = x0,
                        X1 = x0 + westWidth,
                        Z0 = CorridorA1,
                        Z1 = CorridorB0,
                        DoorSide = 'S',
                        DoorCenter = x0 + westWidth * 0.5f,
                        Level = level,
                        Index = i
                    });
                }

                for (int i = 0; i < perSide; i++)
                {
                    float x0 = StairRightB + i * eastWidth;
                    int typeIndex = perSide + i;
                    rooms.Add(new Room
                    {
                        Name = LevelName(level) + "_东核心" + (i + 1) + "_" + TypeName(middle[typeIndex]),
                        Type = middle[typeIndex],
                        X0 = x0,
                        X1 = x0 + eastWidth,
                        Z0 = CorridorA1,
                        Z1 = CorridorB0,
                        DoorSide = 'S',
                        DoorCenter = x0 + eastWidth * 0.5f,
                        Level = level,
                        Index = i
                    });
                }
            }

            // 一层南侧第 5 间作为门厅，放主门出口
            if (level == 1)
            {
                foreach (Room room in rooms)
                {
                    if (room.Name.Contains("_南5_"))
                    {
                        room.Type = RoomType.Lobby;
                        room.Name = "1F_南5_门厅";
                        break;
                    }
                }
            }

            return rooms;
        }

        /// <summary>南侧：以病房与治疗为主，每层顺序不同。</summary>
        private static RoomType[] SouthPattern(int level, int count)
        {
            RoomType[] baseList =
            {
                RoomType.Ward, RoomType.Ward, RoomType.Treatment, RoomType.Ward, RoomType.Ward,
                RoomType.Ward, RoomType.Lounge, RoomType.Duty, RoomType.Treatment, RoomType.Ward,
                RoomType.Exam, RoomType.Ward
            };

            return Rotate(baseList, count, (level - 1) * 3);
        }

        /// <summary>北侧：病房 + 后勤辅助用房。</summary>
        private static RoomType[] NorthPattern(int level, int count)
        {
            RoomType[] baseList =
            {
                RoomType.Ward, RoomType.Storage, RoomType.Archive, RoomType.Ward, RoomType.Ward,
                RoomType.Utility, RoomType.Ward, RoomType.Archive, RoomType.Lounge, RoomType.Storage,
                RoomType.Ward, RoomType.Exam
            };

            return Rotate(baseList, count, (level - 1) * 5);
        }

        /// <summary>中间核心：办公室、检查室、手术室、药房。</summary>
        private static RoomType[] MiddlePattern(int level, int count)
        {
            RoomType[] baseList =
            {
                RoomType.Office, RoomType.Exam, RoomType.Surgery,
                RoomType.Pharmacy, RoomType.Storage, RoomType.Exam,
                RoomType.Office, RoomType.Lounge
            };

            return Rotate(baseList, count, (level - 1) * 2);
        }

        private static RoomType[] Rotate(RoomType[] source, int count, int offset)
        {
            var result = new RoomType[count];
            int start = ((offset % source.Length) + source.Length) % source.Length;

            for (int i = 0; i < count; i++)
            {
                result[i] = source[(start + i) % source.Length];
            }

            return result;
        }

        private static RoomType[] BasementPattern(int count)
        {
            RoomType[] layout =
            {
                RoomType.Storage, RoomType.Pharmacy, RoomType.Archive, RoomType.Utility,
                RoomType.Storage, RoomType.Utility
            };

            var result = new RoomType[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = layout[i % layout.Length];
            }

            return result;
        }

        private static string TypeName(RoomType type)
        {
            switch (type)
            {
                case RoomType.Ward: return "病房";
                case RoomType.Office: return "办公室";
                case RoomType.Exam: return "检查室";
                case RoomType.Treatment: return "治疗室";
                case RoomType.Surgery: return "手术室";
                case RoomType.Pharmacy: return "药房";
                case RoomType.Duty: return "值班室";
                case RoomType.Storage: return "储藏室";
                case RoomType.Lounge: return "休息室";
                case RoomType.Archive: return "档案室";
                case RoomType.Utility: return "设备间";
                default: return "门厅";
            }
        }

        /// <summary>门锁表：键为「墙坐标|门洞中心」，值为所需道具 Id。</summary>
        private static readonly Dictionary<string, string> DoorLocks =
            new Dictionary<string, string>();

        private static string DoorKey(float wallCoord, float gapCenter)
        {
            return wallCoord.ToString("F2") + "|" + gapCenter.ToString("F2");
        }

        /// <summary>地下车库（B1 南侧 6 号设备间）中心的 X 坐标。</summary>
        private static float GarageCenter(List<Room> rooms)
        {
            foreach (Room room in rooms)
            {
                if (room.Level == BasementLevel && room.Type == RoomType.Utility &&
                    room.Index == 5 && room.Z0 < -5f)
                {
                    return (room.X0 + room.X1) * 0.5f;
                }
            }

            return HalfWidth - 7f;
        }

        /// <summary>本层楼梯间需要哪张通行卡（null 表示不上锁）。</summary>
        private static string StairCardForLevel(int level)
        {
            switch (level)
            {
                case 1:
                    return ItemIds.Card2F;   // 1F 楼梯间：上楼去 2F，下楼进 B1
                case 3:
                    return ItemIds.Card3F;
                case 4:
                    return ItemIds.Card4F;
                default:
                    return null;             // B1 与 2F 的楼梯间不上锁
            }
        }

        /// <summary>需要钥匙才能进入的关键房间；钥匙都在同层或更低的楼层。</summary>
        private static string RoomDoorLock(Room room)
        {
            if (room.Level == BasementLevel)
            {
                if (room.Type == RoomType.Archive && room.Index == 4 && room.Z0 < -5f)
                {
                    return ItemIds.ClerkKey;    // 地下档案室 ← 地下值班室的职员钥匙
                }

                if (room.Type == RoomType.Storage && room.Index == 1 &&
                    room.Z1 >= NorthZ - 0.01f)
                {
                    return ItemIds.LockerKey;   // 地下库房 ← 洗衣房抽屉的库房钥匙
                }

                if (room.Type == RoomType.Utility && room.Index == 5 && room.Z0 < -5f)
                {
                    return ItemIds.GarageKey;   // 车库 ← 库房柜里的车库钥匙
                }

                return null;
            }

            if (room.Level == 1)
            {
                if (room.Type == RoomType.Treatment && room.Index == 3 && room.Z0 < -5f)
                {
                    return ItemIds.WardKey;     // 1F 4 号治疗室 ← 1F 值班室的病房钥匙
                }

                return null;
            }

            if (room.Level == 2 && room.Type == RoomType.Pharmacy &&
                room.Index == 0 && room.X0 < 0f)
            {
                return ItemIds.StorageKey;      // 2F 药房 ← 2F 值班室的药库钥匙
            }

            if (room.Level == 3 && room.Type == RoomType.Surgery &&
                room.Index == 0 && room.X0 < 0f)
            {
                return ItemIds.SurgeryKey;      // 3F 手术室 ← 3F 值班室的手术区钥匙
            }

            if (room.Level == 4)
            {
                if (room.Type == RoomType.Ward && room.Index == 2 && room.Z0 < -5f)
                {
                    return ItemIds.PatientKey;  // 4F 3 号病房 ← 1F 治疗室的病人物品柜钥匙
                }

                if (room.Type == RoomType.Duty && room.Index == 1 && room.Z0 < -5f)
                {
                    return ItemIds.StaffKey;    // 4F 值班室 ← 4F 病房的值班钥匙
                }

                if (room.Type == RoomType.Archive && room.Index == 0 &&
                    room.Z1 >= NorthZ - 0.01f)
                {
                    return ItemIds.DirectorKey; // 4F 档案室 ← 4F 值班室的院长室钥匙
                }
            }

            return null;
        }

        // ------------------------------------------------------------------
        // 墙体：外墙 + 两条走廊的隔墙（带门洞）+ 房间分隔墙
        // ------------------------------------------------------------------
        private static void BuildWalls(Transform root, int level, float baseY, List<Room> rooms)
        {
            var group = new GameObject("Walls").transform;
            group.SetParent(root, false);

            DoorLocks.Clear();

            var southGaps = new List<Vector2>();   // z = CorridorA0：南侧房间门
            var middleGaps = new List<Vector2>();  // z = CorridorA1：中间核心门
            var northGaps = new List<Vector2>();   // z = CorridorB1：北侧房间门
            var serviceGaps = new List<Vector2>(); // z = CorridorB0：楼梯后门 + 中央通道

            var southXs = new List<float>();
            var northXs = new List<float>();
            var middleXs = new List<float>();

            foreach (Room room in rooms)
            {
                if (Mathf.Abs(room.Z0 - SouthZ) < 0.01f)
                {
                    southGaps.Add(new Vector2(room.DoorCenter, DoorWidth));
                    southXs.Add(room.X0);
                    southXs.Add(room.X1);
                }
                else if (Mathf.Abs(room.Z1 - NorthZ) < 0.01f)
                {
                    northGaps.Add(new Vector2(room.DoorCenter, DoorWidth));
                    northXs.Add(room.X0);
                    northXs.Add(room.X1);
                }
                else
                {
                    middleGaps.Add(new Vector2(room.DoorCenter, DoorWidth));
                    middleXs.Add(room.X0);
                    middleXs.Add(room.X1);
                }
            }

            float stairDoorLeft = (StairLeftA + StairLeftB) * 0.5f;
            float stairDoorRight = (StairRightA + StairRightB) * 0.5f;

            middleGaps.Add(new Vector2(stairDoorLeft, DoorWidth));
            middleGaps.Add(new Vector2(stairDoorRight, DoorWidth));
            serviceGaps.Add(new Vector2(stairDoorLeft, DoorWidth));
            serviceGaps.Add(new Vector2(stairDoorRight, DoorWidth));

            middleGaps.Add(new Vector2(0f, PassageB - PassageA));
            serviceGaps.Add(new Vector2(0f, PassageB - PassageA));

            // 楼梯间门锁：本层楼梯间两侧的门统一用一张卡，避免“刷开南门却锁死北门”。
            // 顺序：1F 用二层卡（拿到才能上楼/下 B1）；2F 不上锁；3F 用三层卡；4F 用四层卡。
            string stairCard = StairCardForLevel(level);
            if (!string.IsNullOrEmpty(stairCard))
            {
                DoorLocks[DoorKey(CorridorA1, stairDoorLeft)] = stairCard;
                DoorLocks[DoorKey(CorridorA1, stairDoorRight)] = stairCard;
                DoorLocks[DoorKey(CorridorB0, stairDoorLeft)] = stairCard;
                DoorLocks[DoorKey(CorridorB0, stairDoorRight)] = stairCard;
            }

            // 关键房间的门锁：钥匙都在本层或更低的楼层，先拿钥匙再开门取下一张通行卡
            foreach (Room room in rooms)
            {
                string roomLock = RoomDoorLock(room);
                if (string.IsNullOrEmpty(roomLock))
                {
                    continue;
                }

                float wallZ = Mathf.Abs(room.Z1 - CorridorA0) < 0.01f
                    ? CorridorA0
                    : (Mathf.Abs(room.Z1 - NorthZ) < 0.01f ? CorridorB1 : CorridorA1);

                DoorLocks[DoorKey(wallZ, room.DoorCenter)] = roomLock;
            }

            // 外墙开口：一层门厅正门、地下车库卷帘门、四层东侧消防连廊
            List<Vector2> southOuterGaps = null;
            if (level == 1)
            {
                float lobbyCenter = 0f;
                foreach (Room room in rooms)
                {
                    if (room.Type == RoomType.Lobby)
                    {
                        lobbyCenter = (room.X0 + room.X1) * 0.5f;
                        break;
                    }
                }

                southOuterGaps = new List<Vector2> { new Vector2(lobbyCenter, 2.5f) };
                DoorLocks[DoorKey(SouthZ, lobbyCenter)] = "SKIP";
            }
            else if (level == BasementLevel)
            {
                // 车库卷帘门：门洞里由 BuildGarage 单独放卷帘，不放普通门扇
                float garageCenter = GarageCenter(rooms);
                southOuterGaps = new List<Vector2> { new Vector2(garageCenter, 3f) };
                DoorLocks[DoorKey(SouthZ, garageCenter)] = "SKIP";
            }

            List<Vector2> eastGaps = null;
            if (level == TopLevel)
            {
                eastGaps = new List<Vector2> { new Vector2(FireExitZ, DoorWidth) };
                DoorLocks[DoorKey(HalfWidth, FireExitZ)] = ItemIds.FireKey;
            }

            WallAlongX(group, SouthZ, baseY, -HalfWidth, HalfWidth, southOuterGaps);
            WallAlongX(group, NorthZ, baseY, -HalfWidth, HalfWidth, null);
            WallAlongZ(group, -HalfWidth, baseY, SouthZ, NorthZ, null);
            WallAlongZ(group, HalfWidth, baseY, SouthZ, NorthZ, eastGaps);

            WallAlongX(group, CorridorA0, baseY, -HalfWidth, HalfWidth, southGaps, 1);

            if (level == 1)
            {
                // 一层接待大厅：中段完全打通，只在两个楼梯间前后保留带门的短墙
                var leftStairGap = new List<Vector2> { new Vector2(stairDoorLeft, DoorWidth) };
                var rightStairGap = new List<Vector2> { new Vector2(stairDoorRight, DoorWidth) };

                WallAlongX(group, CorridorA1, baseY, StairLeftA, StairLeftB, leftStairGap, -1);
                WallAlongX(group, CorridorA1, baseY, StairRightA, StairRightB, rightStairGap, -1);
                WallAlongX(group, CorridorB0, baseY, StairLeftA, StairLeftB, leftStairGap, 1);
                WallAlongX(group, CorridorB0, baseY, StairRightA, StairRightB, rightStairGap, 1);
            }
            else
            {
                WallAlongX(group, CorridorA1, baseY, -HalfWidth, HalfWidth, middleGaps, -1);
                WallAlongX(group, CorridorB0, baseY, -HalfWidth, HalfWidth, serviceGaps, 1);
            }

            WallAlongX(group, CorridorB1, baseY, -HalfWidth, HalfWidth, northGaps, -1);

            BuildDividers(group, baseY, southXs, SouthZ, CorridorA0);
            BuildDividers(group, baseY, northXs, CorridorB1, NorthZ);

            middleXs.Add(StairLeftA);
            middleXs.Add(StairLeftB);
            middleXs.Add(StairRightA);
            middleXs.Add(StairRightB);
            BuildDividers(group, baseY, middleXs, CorridorA1, CorridorB0);
        }

        /// <summary>按房间边界生成竖向分隔墙：自动去重并跳过外墙位置。</summary>
        private static void BuildDividers(Transform group, float baseY, List<float> xs,
            float z0, float z1)
        {
            var unique = new List<float>();

            foreach (float x in xs)
            {
                if (Mathf.Abs(x + HalfWidth) < 0.05f || Mathf.Abs(x - HalfWidth) < 0.05f)
                {
                    continue;
                }

                bool exists = false;
                foreach (float existing in unique)
                {
                    if (Mathf.Abs(existing - x) < 0.05f)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    unique.Add(x);
                }
            }

            unique.Sort();

            foreach (float x in unique)
            {
                WallAlongZ(group, x, baseY, z0, z1, null);
            }
        }

        private static void WallAlongX(Transform parent, float z, float baseY,
            float from, float to, List<Vector2> gaps, int railSide = 0)
        {
            if (gaps == null || gaps.Count == 0)
            {
                Box(parent, "Wall", new Vector3((from + to) * 0.5f, baseY + WallHeight * 0.5f, z),
                    new Vector3(to - from, WallHeight, WallThickness), wallMaterial, true);
                AddWallDetailX(parent, z, baseY, from, to, railSide);
                return;
            }

            gaps.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = from;

            foreach (Vector2 gap in gaps)
            {
                float gapStart = Mathf.Clamp(gap.x - gap.y * 0.5f, from, to);
                float gapEnd = Mathf.Clamp(gap.x + gap.y * 0.5f, from, to);

                if (gapStart > cursor + 0.01f)
                {
                    Box(parent, "Wall", new Vector3((cursor + gapStart) * 0.5f,
                            baseY + WallHeight * 0.5f, z),
                        new Vector3(gapStart - cursor, WallHeight, WallThickness), wallMaterial, true);
                    AddWallDetailX(parent, z, baseY, cursor, gapStart, railSide);
                }

                Box(parent, "WallLintel", new Vector3((gapStart + gapEnd) * 0.5f,
                        baseY + DoorHeight + (WallHeight - DoorHeight) * 0.5f, z),
                    new Vector3(gapEnd - gapStart, WallHeight - DoorHeight, WallThickness),
                    wallMaterial, true);

                AddDoorFrameX(parent, z, baseY, gapStart, gapEnd);

                cursor = Mathf.Max(cursor, gapEnd);
            }

            if (to > cursor + 0.01f)
            {
                Box(parent, "Wall", new Vector3((cursor + to) * 0.5f,
                        baseY + WallHeight * 0.5f, z),
                    new Vector3(to - cursor, WallHeight, WallThickness), wallMaterial, true);
                AddWallDetailX(parent, z, baseY, cursor, to, railSide);
            }
        }

        private static void WallAlongZ(Transform parent, float x, float baseY,
            float from, float to, List<Vector2> gaps, int railSide = 0)
        {
            if (gaps == null || gaps.Count == 0)
            {
                Box(parent, "Wall", new Vector3(x, baseY + WallHeight * 0.5f, (from + to) * 0.5f),
                    new Vector3(WallThickness, WallHeight, to - from), wallMaterial, true);
                AddWallDetailZ(parent, x, baseY, from, to, railSide);
                return;
            }

            gaps.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = from;

            foreach (Vector2 gap in gaps)
            {
                float gapStart = Mathf.Clamp(gap.x - gap.y * 0.5f, from, to);
                float gapEnd = Mathf.Clamp(gap.x + gap.y * 0.5f, from, to);

                if (gapStart > cursor + 0.01f)
                {
                    Box(parent, "Wall", new Vector3(x, baseY + WallHeight * 0.5f,
                            (cursor + gapStart) * 0.5f),
                        new Vector3(WallThickness, WallHeight, gapStart - cursor), wallMaterial, true);
                    AddWallDetailZ(parent, x, baseY, cursor, gapStart, railSide);
                }

                Box(parent, "WallLintel", new Vector3(x,
                        baseY + DoorHeight + (WallHeight - DoorHeight) * 0.5f,
                        (gapStart + gapEnd) * 0.5f),
                    new Vector3(WallThickness, WallHeight - DoorHeight, gapEnd - gapStart),
                    wallMaterial, true);

                AddDoorFrameZ(parent, x, baseY, gapStart, gapEnd);

                cursor = Mathf.Max(cursor, gapEnd);
            }

            if (to > cursor + 0.01f)
            {
                Box(parent, "Wall", new Vector3(x, baseY + WallHeight * 0.5f, (cursor + to) * 0.5f),
                    new Vector3(WallThickness, WallHeight, to - cursor), wallMaterial, true);
                AddWallDetailZ(parent, x, baseY, cursor, to, railSide);
            }
        }

        /// <summary>墙脚线 + 医院走廊扶手（railSide 决定扶手在哪一面）。</summary>
        private static void AddWallDetailX(Transform parent, float z, float baseY,
            float start, float end, int railSide)
        {
            float length = end - start;
            if (length <= 0.05f)
            {
                return;
            }

            float mid = (start + end) * 0.5f;
            const float skirting = 0.12f;

            Box(parent, "Skirting", new Vector3(mid, baseY + skirting * 0.5f,
                    z + WallThickness * 0.5f + 0.02f),
                new Vector3(length, skirting, 0.04f), trimMaterial);
            Box(parent, "Skirting", new Vector3(mid, baseY + skirting * 0.5f,
                    z - WallThickness * 0.5f - 0.02f),
                new Vector3(length, skirting, 0.04f), trimMaterial);

            if (railSide == 0)
            {
                return;
            }

            float railZ = z + railSide * (WallThickness * 0.5f + 0.09f);
            Box(parent, "Handrail", new Vector3(mid, baseY + 0.95f, railZ),
                new Vector3(length, 0.07f, 0.07f), metalMaterial);

            int brackets = Mathf.Max(1, Mathf.FloorToInt(length / 2.4f));
            for (int i = 0; i <= brackets; i++)
            {
                float x = Mathf.Lerp(start + 0.25f, end - 0.25f, (float)i / brackets);
                Box(parent, "RailBracket",
                    new Vector3(x, baseY + 0.95f, z + railSide * (WallThickness * 0.5f + 0.05f)),
                    new Vector3(0.05f, 0.05f, 0.12f), metalMaterial);
            }
        }

        private static void AddWallDetailZ(Transform parent, float x, float baseY,
            float start, float end, int railSide)
        {
            float length = end - start;
            if (length <= 0.05f)
            {
                return;
            }

            float mid = (start + end) * 0.5f;
            const float skirting = 0.12f;

            Box(parent, "Skirting", new Vector3(x + WallThickness * 0.5f + 0.02f,
                    baseY + skirting * 0.5f, mid),
                new Vector3(0.04f, skirting, length), trimMaterial);
            Box(parent, "Skirting", new Vector3(x - WallThickness * 0.5f - 0.02f,
                    baseY + skirting * 0.5f, mid),
                new Vector3(0.04f, skirting, length), trimMaterial);

            if (railSide == 0)
            {
                return;
            }

            float railX = x + railSide * (WallThickness * 0.5f + 0.09f);
            Box(parent, "Handrail", new Vector3(railX, baseY + 0.95f, mid),
                new Vector3(0.07f, 0.07f, length), metalMaterial);

            int brackets = Mathf.Max(1, Mathf.FloorToInt(length / 2.4f));
            for (int i = 0; i <= brackets; i++)
            {
                float z = Mathf.Lerp(start + 0.25f, end - 0.25f, (float)i / brackets);
                Box(parent, "RailBracket",
                    new Vector3(x + railSide * (WallThickness * 0.5f + 0.05f), baseY + 0.95f, z),
                    new Vector3(0.12f, 0.05f, 0.05f), metalMaterial);
            }
        }

        /// <summary>门洞的门套、门槛，以及可以推开的门扇（宽洞如连廊不装门）。</summary>
        private static void AddDoorFrameX(Transform parent, float z, float baseY,
            float gapStart, float gapEnd)
        {
            float width = gapEnd - gapStart;
            if (width <= 0.1f || width > 3.2f)
            {
                return;
            }

            float mid = (gapStart + gapEnd) * 0.5f;
            float halfWall = WallThickness * 0.5f;

            // 门洞内衬
            Box(parent, "DoorJamb", new Vector3(gapStart + 0.04f, baseY + DoorHeight * 0.5f, z),
                new Vector3(0.08f, DoorHeight, WallThickness + 0.06f), trimMaterial);
            Box(parent, "DoorJamb", new Vector3(gapEnd - 0.04f, baseY + DoorHeight * 0.5f, z),
                new Vector3(0.08f, DoorHeight, WallThickness + 0.06f), trimMaterial);
            Box(parent, "DoorHead", new Vector3(mid, baseY + DoorHeight + 0.04f, z),
                new Vector3(width, 0.08f, WallThickness + 0.06f), trimMaterial);

            // 两面贴脸线脚
            for (int side = -1; side <= 1; side += 2)
            {
                float faceZ = z + side * (halfWall + 0.018f);
                Box(parent, "DoorArchitrave",
                    new Vector3(gapStart - 0.06f, baseY + (DoorHeight + 0.1f) * 0.5f, faceZ),
                    new Vector3(0.12f, DoorHeight + 0.1f, 0.035f), trimMaterial);
                Box(parent, "DoorArchitrave",
                    new Vector3(gapEnd + 0.06f, baseY + (DoorHeight + 0.1f) * 0.5f, faceZ),
                    new Vector3(0.12f, DoorHeight + 0.1f, 0.035f), trimMaterial);
                Box(parent, "DoorArchitrave",
                    new Vector3(mid, baseY + DoorHeight + 0.1f, faceZ),
                    new Vector3(width + 0.36f, 0.12f, 0.035f), trimMaterial);
            }

            // 金属门槛
            Box(parent, "DoorThreshold", new Vector3(mid, baseY + 0.015f, z),
                new Vector3(width - 0.02f, 0.03f, WallThickness + 0.2f), metalMaterial);

            string requiredItem;
            DoorLocks.TryGetValue(DoorKey(z, mid), out requiredItem);

            if (requiredItem == "SKIP")
            {
                return;   // 门扇由 BuildExit / BuildGarage 单独创建
            }

            BuildDoorLeaf(parent, new Vector3(gapStart + 0.09f, baseY, z), width - 0.18f, false,
                requiredItem);
        }

        private static void AddDoorFrameZ(Transform parent, float x, float baseY,
            float gapStart, float gapEnd)
        {
            float width = gapEnd - gapStart;
            if (width <= 0.1f || width > 3.2f)
            {
                return;
            }

            float mid = (gapStart + gapEnd) * 0.5f;
            float halfWall = WallThickness * 0.5f;

            Box(parent, "DoorJamb", new Vector3(x, baseY + DoorHeight * 0.5f, gapStart + 0.04f),
                new Vector3(WallThickness + 0.06f, DoorHeight, 0.08f), trimMaterial);
            Box(parent, "DoorJamb", new Vector3(x, baseY + DoorHeight * 0.5f, gapEnd - 0.04f),
                new Vector3(WallThickness + 0.06f, DoorHeight, 0.08f), trimMaterial);
            Box(parent, "DoorHead", new Vector3(x, baseY + DoorHeight + 0.04f, mid),
                new Vector3(WallThickness + 0.06f, 0.08f, width), trimMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                float faceX = x + side * (halfWall + 0.018f);
                Box(parent, "DoorArchitrave",
                    new Vector3(faceX, baseY + (DoorHeight + 0.1f) * 0.5f, gapStart - 0.06f),
                    new Vector3(0.035f, DoorHeight + 0.1f, 0.12f), trimMaterial);
                Box(parent, "DoorArchitrave",
                    new Vector3(faceX, baseY + (DoorHeight + 0.1f) * 0.5f, gapEnd + 0.06f),
                    new Vector3(0.035f, DoorHeight + 0.1f, 0.12f), trimMaterial);
                Box(parent, "DoorArchitrave",
                    new Vector3(faceX, baseY + DoorHeight + 0.1f, mid),
                    new Vector3(0.035f, 0.12f, width + 0.36f), trimMaterial);
            }

            Box(parent, "DoorThreshold", new Vector3(x, baseY + 0.015f, mid),
                new Vector3(WallThickness + 0.2f, 0.03f, width - 0.02f), metalMaterial);

            string requiredItem;
            DoorLocks.TryGetValue(DoorKey(x, mid), out requiredItem);

            BuildDoorLeaf(parent, new Vector3(x, baseY, gapStart + 0.09f), width - 0.18f, true,
                requiredItem);
        }

        /// <summary>
        /// 在门洞里造一扇可推开的门：铰链在左侧门柱，门扇占据整个门洞，
        /// 除门扇本身外门洞里不再放任何摆件。普通房门打开 5 秒后自动关闭。
        /// </summary>
        private static void BuildDoorLeaf(Transform parent, Vector3 hingePosition,
            float leafWidth, bool alongZ, string requiredItem)
        {
            if (leafWidth <= 0.2f)
            {
                return;
            }

            var pivot = new GameObject("RoomDoor");
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = hingePosition;   // 父节点无旋转，直接用世界坐标即可

            // 门扇统一在本地 +x 方向拼装：门板 + 上下芯板 + 防踢板 + 合页 + 执手
            var leaf = new GameObject("DoorLeafRoot").transform;
            leaf.SetParent(pivot.transform, false);
            leaf.localRotation = alongZ ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;

            float height = DoorHeight - 0.08f;
            const float slab = 0.055f;
            float halfSlab = slab * 0.5f;
            float centerX = leafWidth * 0.5f;

            // 门板本体（唯一的碰撞体，负责挡人）
            Box(leaf, "DoorSlab", new Vector3(centerX, height * 0.5f, 0f),
                new Vector3(leafWidth, height, slab), trimMaterial, true);

            // 上下两块凸起芯板（内外两面）
            float panelW = leafWidth - 0.26f;
            float upperH = height * 0.4f;
            float lowerH = height * 0.26f;
            float upperY = height - 0.2f - upperH * 0.5f;
            float lowerY = 0.34f + lowerH * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * (halfSlab + 0.008f);
                Box(leaf, "DoorPanel", new Vector3(centerX, upperY, z),
                    new Vector3(panelW, upperH, 0.014f), woodDarkMaterial);
                Box(leaf, "DoorPanel", new Vector3(centerX, lowerY, z),
                    new Vector3(panelW, lowerH, 0.014f), woodDarkMaterial);

                // 底部金属防踢板
                Box(leaf, "DoorKickPlate", new Vector3(centerX, 0.13f, z),
                    new Vector3(leafWidth - 0.08f, 0.2f, 0.008f), metalMaterial);
            }

            // 合页：铰链侧三个，两面都有
            for (int i = 0; i < 3; i++)
            {
                float y = Mathf.Lerp(0.32f, height - 0.32f, i / 2f);

                for (int side = -1; side <= 1; side += 2)
                {
                    Box(leaf, "DoorHinge", new Vector3(0.045f, y, side * (halfSlab + 0.004f)),
                        new Vector3(0.08f, 0.11f, 0.012f), metalMaterial);
                }
            }

            // 执手：面板 + 横杆（两面）
            for (int side = -1; side <= 1; side += 2)
            {
                Box(leaf, "DoorRose", new Vector3(leafWidth - 0.13f, 1.02f, side * (halfSlab + 0.012f)),
                    new Vector3(0.06f, 0.24f, 0.014f), metalMaterial);
                Box(leaf, "DoorLever", new Vector3(leafWidth - 0.22f, 1.02f, side * (halfSlab + 0.04f)),
                    new Vector3(0.17f, 0.032f, 0.032f), metalMaterial);
            }

            // 锁具标识：读卡器面板 / 机械锁孔
            if (!string.IsNullOrEmpty(requiredItem))
            {
                bool cardLock = requiredItem.StartsWith("card_");

                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * (halfSlab + 0.012f);
                    Box(leaf, "DoorLockPlate", new Vector3(leafWidth - 0.13f, 1.3f, z),
                        new Vector3(0.09f, 0.14f, 0.012f), metalMaterial);
                    Box(leaf, "DoorLockLamp", new Vector3(leafWidth - 0.13f, 1.34f, side * (halfSlab + 0.02f)),
                        new Vector3(0.05f, 0.04f, 0.01f), cardLock ? glassMaterial : woodDarkMaterial);
                }
            }

            var hinge = pivot.AddComponent<HingedDoor>();
            hinge.Pivot = pivot.transform;
            hinge.OpenAngle = -100f;
            hinge.Duration = 0.8f;
            hinge.AutoCloseDelay = 5f;

            if (string.IsNullOrEmpty(requiredItem))
            {
                var interactable = pivot.AddComponent<SimpleDoorInteractable>();
                interactable.Door = hinge;
                return;
            }

            ItemDefinition definition = ItemDatabase.Get(requiredItem);
            bool isCard = requiredItem.StartsWith("card_");

            var locked = pivot.AddComponent<KeyLockedDoor>();
            locked.Door = hinge;
            locked.RequiredItemId = requiredItem;
            locked.RequiredItemName = definition != null
                ? definition.DisplayName
                : (isCard ? "通行卡" : "钥匙");
            locked.LockedHint = isCard
                ? "读卡器的红灯一直亮着。"
                : "门上是老式机械锁，锁孔里还留着旧钥匙的刮痕。";
        }

        // ------------------------------------------------------------------
        // 楼梯
        // ------------------------------------------------------------------
        private static void BuildStairs(Transform root, float baseY)
        {
            BuildStairFlight(root, StairLeftA, StairLeftB, baseY);
            BuildStairFlight(root, StairRightA, StairRightB, baseY);
        }

        /// <summary>楼梯间内的直跑楼梯：从南侧平台向北上到上一层。</summary>
        private static void BuildStairFlight(Transform root, float x0, float x1, float baseY)
        {
            var group = new GameObject("Stairwell").transform;
            group.SetParent(root, false);

            float centerX = (x0 + x1) * 0.5f;
            float rise = FloorHeight / StairSteps;
            float run = (StairFlightEndZ - StairFlightStartZ) / StairSteps;

            for (int i = 0; i < StairSteps; i++)
            {
                float topY = baseY + rise * (i + 1);
                float z = StairFlightStartZ + run * (i + 0.5f);
                Box(group, "Step", new Vector3(centerX, topY - 0.1f, z),
                    new Vector3(StairWidth, 0.2f, run + 0.02f), metalMaterial, true);

                // 两侧扶手，随台阶逐级升高
                float railOffset = StairWidth * 0.5f - 0.06f;
                Box(group, "StairRail",
                    new Vector3(centerX - railOffset, topY + 0.78f, z),
                    new Vector3(0.06f, 0.06f, run + 0.03f), metalMaterial);
                Box(group, "StairRail",
                    new Vector3(centerX + railOffset, topY + 0.78f, z),
                    new Vector3(0.06f, 0.06f, run + 0.03f), metalMaterial);
            }

            // 楼梯两侧的实心隔墙：把梯段完全封闭，避免从侧面踏空掉下去
            float wallOffset = StairWidth * 0.5f + WallThickness * 0.5f;
            WallAlongZ(group, centerX - wallOffset, baseY, StairFlightStartZ, StairFlightEndZ, null);
            WallAlongZ(group, centerX + wallOffset, baseY, StairFlightStartZ, StairFlightEndZ, null);

            // 楼梯间照明
            PointLight(group, new Vector3(centerX, baseY + WallHeight - 0.4f,
                (StairFlightStartZ + StairFlightEndZ) * 0.5f),
                6f, 0.75f, new Color(0.72f, 0.86f, 0.92f));
        }

        // ------------------------------------------------------------------
        // 房间内容
        // ------------------------------------------------------------------
        private static void BuildRoomContents(Transform root, Room room, float baseY)
        {
            if (room.Group == null)
            {
                room.Group = new GameObject("Room_" + room.Name).transform;
                room.Group.SetParent(root, false);
            }

            // 内容容器绕 Y 轴旋转，让「本地 +z」永远指向房间后墙，
            // 这样家具不管在哪条翼都不会堵住朝大厅的门。
            var content = new GameObject("Contents").transform;
            content.SetParent(room.Group, false);
            content.position = new Vector3(room.Center.x, baseY, room.Center.z);

            switch (room.DoorSide)
            {
                case 'S':
                    content.rotation = Quaternion.Euler(0f, 0f, 0f);
                    break;
                case 'N':
                    content.rotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 'W':
                    content.rotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                default:
                    content.rotation = Quaternion.Euler(0f, 270f, 0f);
                    break;
            }

            // 本地坐标：原点在房间中心，+z 为后墙，-z 为门
            float halfW = room.Width * 0.5f;
            float halfD = room.Depth * 0.5f;
            float x0 = -halfW + 0.6f;
            float x1 = halfW - 0.6f;
            float z0 = -halfD + 0.6f;
            float z1 = halfD - 0.6f;

            if (x1 - x0 < 1f || z1 - z0 < 1f)
            {
                return;
            }

            bool real = BuildRealFurniture(content, room.Type, x0, x1, z0, z1);

            if (!real)
            {
                switch (room.Type)
                {
                    case RoomType.Ward:
                        BuildWard(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Office:
                        BuildOffice(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Exam:
                        BuildExamRoom(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Treatment:
                        BuildTreatmentRoom(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Surgery:
                        BuildSurgeryRoom(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Pharmacy:
                        BuildShelves(content, x0, x1, z0, z1, 0f, 4);
                        break;
                    case RoomType.Duty:
                        BuildDutyRoom(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Storage:
                        BuildShelves(content, x0, x1, z0, z1, 0f, 5);
                        break;
                    case RoomType.Lounge:
                        BuildLounge(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Archive:
                        BuildShelves(content, x0, x1, z0, z1, 0f, 6);
                        break;
                    case RoomType.Utility:
                        BuildUtilityRoom(content, x0, x1, z0, z1, 0f);
                        break;
                    case RoomType.Lobby:
                        BuildLobby(content, x0, x1, z0, z1, 0f);
                        break;
                }

                AddRoomProps(content, room.Type, x0, x1, z0, z1);
            }

            // 记录纸张贴在后方墙面上（墙中心线内缩半个墙厚），不能悬在半空
            AddClueNote(content, room, halfD - WallThickness * 0.5f - 0.03f);
            AddRoomContainer(content, room, x0, x1, z0, z1);

            // B1 配电间：总闸（主线第二步，通电后才能开一层正门）
            if (room.Level == BasementLevel && room.Type == RoomType.Utility && room.Index == 1)
            {
                AddFuseBox(content, halfD);
            }

            // 恐怖陈设：血迹、散纸、翻倒家具、裹尸袋
            AddHorrorDressing(content, room, x0, x1, z0, z1);
        }

        private static readonly string[] CommonLoot =
        {
            ItemIds.Candle, ItemIds.ClothScrap, ItemIds.EmptyBottle, ItemIds.OldCoin
        };

        private static readonly string[] RareLoot =
        {
            ItemIds.OldPhoto, ItemIds.PocketWatch, ItemIds.BrassCompass, ItemIds.VinylRecord,
            ItemIds.Candle, ItemIds.ClothScrap, ItemIds.EmptyBottle, ItemIds.OldCoin
        };

        /// <summary>
        /// 主线与两条备用路线的固定掉落表；返回 null 表示这个房间用普通随机掉落。
        /// 主线：二层卡 → 三枚保险丝 → 药库钥匙/三层卡 → 手术区钥匙/四层卡
        ///       → 病人物品柜钥匙 → 值班钥匙 → 院长室钥匙 → 主门钥匙
        /// 支线：库房钥匙/车库钥匙/卷帘门曲柄 / 消防连廊钥匙/消防斧
        /// </summary>
        private static string[] PlannedLoot(Room room)
        {
            bool south = room.Z0 <= SouthZ + 0.01f;
            bool north = room.Z1 >= NorthZ - 0.01f;

            if (room.Level == BasementLevel)
            {
                if (room.Type == RoomType.Storage && room.Index == 0 && south)
                {
                    return new[] { ItemIds.FuseC };
                }

                if (room.Type == RoomType.Storage && room.Index == 1 && north)
                {
                    return new[] { ItemIds.FuseA };
                }

                if (room.Type == RoomType.Storage && room.Index == 2 && north)
                {
                    return new[] { ItemIds.GarageKey };
                }

                if (room.Type == RoomType.Archive && room.Index == 4 && south)
                {
                    return new[] { ItemIds.FuseB };
                }

                if (room.Type == RoomType.Duty && room.Index == 3 && south)
                {
                    return new[] { ItemIds.ClerkKey };
                }

                if (room.Type == RoomType.Utility && room.Index == 0 && north)
                {
                    return new[] { ItemIds.LockerKey };
                }

                if (room.Type == RoomType.Utility && room.Index == 5 && south)
                {
                    return new[] { ItemIds.GateCrank };
                }

                return null;
            }

            if (room.Level == 1)
            {
                if (room.Type == RoomType.Ward && room.Index == 0 && south)
                {
                    return new[] { ItemIds.Card2F };
                }

                if (room.Type == RoomType.Duty && room.Index == 1 && south)
                {
                    return new[] { ItemIds.WardKey };
                }

                if (room.Type == RoomType.Treatment && room.Index == 3 && south)
                {
                    return new[] { ItemIds.PatientKey };
                }

                return null;
            }

            if (room.Level == 2)
            {
                if (room.Type == RoomType.Duty && room.Index == 1 && south)
                {
                    return new[] { ItemIds.StorageKey };
                }

                if (room.Type == RoomType.Pharmacy && room.Index == 0 && room.X0 < 0f)
                {
                    return new[] { ItemIds.Card3F };
                }

                return null;
            }

            if (room.Level == 3)
            {
                if (room.Type == RoomType.Duty && room.Index == 1 && south)
                {
                    return new[] { ItemIds.SurgeryKey };
                }

                if (room.Type == RoomType.Surgery && room.Index == 0 && room.X0 < 0f)
                {
                    return new[] { ItemIds.Card4F };
                }

                return null;
            }

            if (room.Level == 4)
            {
                if (room.Type == RoomType.Ward && room.Index == 2 && south)
                {
                    return new[] { ItemIds.StaffKey };
                }

                if (room.Type == RoomType.Duty && room.Index == 1 && south)
                {
                    return new[] { ItemIds.DirectorKey };
                }

                if (room.Type == RoomType.Archive && room.Index == 0 && north)
                {
                    return new[] { ItemIds.MainKey };
                }

                if (room.Type == RoomType.Storage && room.Index == 1 && north)
                {
                    return new[] { ItemIds.FireKey };
                }

                if (room.Type == RoomType.Treatment && room.Index == 3 && south)
                {
                    return new[] { ItemIds.FireAxe };
                }
            }

            return null;
        }

        /// <summary>
        /// 在房间里放一个可交互的抽屉或柜子：打开后随机掉出一件道具。
        /// 统一贴后墙摆放，与便签、病床保持间距。
        /// </summary>
        private static void AddRoomContainer(Transform content, Room room,
            float x0, float x1, float z0, float z1)
        {
            Vector3 position;
            bool cabinet;
            float yaw = 180f;
            bool rare = false;
            string label = null;
            string[] loot = null;

            switch (room.Type)
            {
                case RoomType.Ward:
                    // 贴着后墙摆放（z1 已内缩 0.6 m，+0.2 正好让柜背贴住墙面）
                    position = new Vector3(0f, 0f, z1 + 0.2f);
                    cabinet = true;
                    rare = true;
                    break;
                case RoomType.Exam:
                    // 贴着后墙摆放（z1 已内缩 0.6 m，+0.2 正好让柜背贴住墙面）
                    position = new Vector3(0f, 0f, z1 + 0.2f);
                    cabinet = true;
                    break;
                case RoomType.Surgery:
                    // 贴着后墙摆放（z1 已内缩 0.6 m，+0.2 正好让柜背贴住墙面）
                    position = new Vector3(0f, 0f, z1 + 0.2f);
                    cabinet = true;
                    break;
                case RoomType.Treatment:
                    // 贴着后墙摆放（z1 已内缩 0.6 m，+0.2 正好让柜背贴住墙面）
                    position = new Vector3(0f, 0f, z1 + 0.2f);
                    cabinet = false;
                    break;
                case RoomType.Duty:
                    // 贴着后墙摆放（z1 已内缩 0.6 m，+0.2 正好让柜背贴住墙面）
                    position = new Vector3(0f, 0f, z1 + 0.2f);
                    cabinet = false;
                    rare = true;
                    break;
                case RoomType.Pharmacy:
                    position = new Vector3(x0 + 2f, 0f, z0 + 0.75f);
                    cabinet = true;
                    break;
                case RoomType.Archive:
                    position = new Vector3(x1 - 2f, 0f, z0 + 0.75f);
                    cabinet = true;
                    break;
                case RoomType.Storage:
                    // 货架占满后墙，储物柜贴着左侧墙摆、朝房间内侧开门
                    // （x0 已内缩 0.6 m，再减 0.2 m 让柜背贴住墙面）
                    position = new Vector3(x0 - 0.2f, 0f, 0f);
                    yaw = 90f;
                    cabinet = true;
                    label = "储物柜";
                    break;
                case RoomType.Utility:
                    if (room.Level == BasementLevel && room.Index == 5 && room.Z0 < -5f)
                    {
                        // 地下车库：卷帘门旁的工具柜，里面是摇门曲柄
                        position = new Vector3(x0 + 2.4f, 0f, z1 - 0.55f);
                        cabinet = true;
                        label = "工具柜";
                    }
                    else
                    {
                        position = new Vector3(x0 + 2f, 0f, z1 - 0.5f);
                        cabinet = false;
                    }
                    break;
                default:
                    return;
            }

            loot = PlannedLoot(room) ?? loot;

            BuildContainer(content, position, yaw, cabinet,
                label ?? (cabinet ? "器械柜" : "抽屉"),
                loot ?? (rare ? RareLoot : CommonLoot));
        }

        /// <summary>
        /// 柜子 / 抽屉：全部用长方体拼出来——踢脚、侧板、顶底板、背板、层板、
        /// 门套（立框横框）、带凸起芯板的门、铰链、把手、铭牌，
        /// 以及三层抽屉（最上层是能拉出来的斗）。
        /// </summary>
        private static void BuildContainer(Transform parent, Vector3 position, float yaw,
            bool cabinet, string displayName, string[] loot, string requiredItem = null)
        {
            var root = new GameObject("Container_" + displayName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            const float width = 0.82f;
            const float depth = 0.5f;
            float height = cabinet ? 1.35f : 0.85f;

            Transform moving = cabinet
                ? BuildCabinetBody(root.transform, width, height, depth)
                : BuildDrawerBody(root.transform, width, height, depth);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height + 0.02f, depth + 0.06f);
            collider.center = new Vector3(0f, (height + 0.02f) * 0.5f, 0.02f);

            // 内部道具点位：抽屉放在拉出来的斗里，柜子放在中层隔板上
            Vector3[] slotPositions = cabinet
                ? new[]
                {
                    new Vector3(-0.17f, 0.55f, 0.12f),
                    new Vector3(0.17f, 0.55f, 0.12f)
                }
                : new[]
                {
                    new Vector3(-0.18f, 0.63f, 0.42f),
                    new Vector3(0.18f, 0.63f, 0.36f)
                };

            var slots = new ItemSpawnPoint[slotPositions.Length];
            for (int i = 0; i < slotPositions.Length; i++)
            {
                var lootRoot = new GameObject("LootPoint" + (i + 1));
                lootRoot.transform.SetParent(root.transform, false);
                lootRoot.transform.localPosition = slotPositions[i];

                var spawn = lootRoot.AddComponent<ItemSpawnPoint>();
                spawn.SpawnOnStart = false;
                slots[i] = spawn;
            }

            var container = root.AddComponent<OpenableContainer>();
            container.DisplayName = displayName;
            container.Motion = cabinet
                ? OpenableContainer.MotionKind.Cabinet
                : OpenableContainer.MotionKind.Drawer;
            container.MovingPart = moving;
            container.SlideOffset = new Vector3(0f, 0f, 0.3f);
            container.SwingEuler = new Vector3(0f, -105f, 0f);
            container.LootSpawnPoints = slots;
            container.LootSpawnPoint = slots.Length > 0 ? slots[0] : null;
            container.MaxLootItems = slots.Length;
            container.LootItemIds = loot;

            if (!string.IsNullOrEmpty(requiredItem))
            {
                ItemDefinition definition = ItemDatabase.Get(requiredItem);
                container.RequiredItemId = requiredItem;
                container.RequiredItemName = definition != null ? definition.DisplayName : "钥匙";
            }
        }

        /// <summary>拼装带门套、芯板门、层板、铰链的柜体，返回可打开的门枢轴。</summary>
        private static Transform BuildCabinetBody(Transform root, float width, float height,
            float depth)
        {
            const float panel = 0.035f;
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            float frontZ = halfD;

            // 踢脚（略内缩），侧板，顶底板，背板
            Box(root, "CabinetPlinth", new Vector3(0f, 0.04f, -0.015f),
                new Vector3(width - 0.1f, 0.08f, depth - 0.08f), woodDarkMaterial);
            Box(root, "CabinetSide", new Vector3(-halfW + panel * 0.5f, height * 0.5f, 0f),
                new Vector3(panel, height, depth), woodMaterial);
            Box(root, "CabinetSide", new Vector3(halfW - panel * 0.5f, height * 0.5f, 0f),
                new Vector3(panel, height, depth), woodMaterial);
            Box(root, "CabinetTop", new Vector3(0f, height - panel * 0.5f, 0f),
                new Vector3(width - panel * 2f, panel, depth), woodMaterial);
            Box(root, "CabinetBottom", new Vector3(0f, 0.08f + panel * 0.5f, 0f),
                new Vector3(width - panel * 2f, panel, depth), woodMaterial);
            Box(root, "CabinetBack", new Vector3(0f, height * 0.5f, -halfD + 0.015f),
                new Vector3(width - panel * 2f, height - panel * 2f, 0.03f), woodDarkMaterial);

            // 两块层板（把柜子分成三层）
            Box(root, "CabinetShelf", new Vector3(0f, 0.52f, -0.015f),
                new Vector3(width - panel * 2f - 0.005f, 0.03f, depth - 0.075f), woodMaterial);
            Box(root, "CabinetShelf", new Vector3(0f, 0.93f, -0.015f),
                new Vector3(width - panel * 2f - 0.005f, 0.03f, depth - 0.075f), woodMaterial);

            // 门套：左右立框 + 上下横框（贴平在柜口，不与门板打架）
            Box(root, "CabinetFrame", new Vector3(-halfW + 0.05f, height * 0.5f, frontZ + 0.006f),
                new Vector3(0.06f, height - 0.06f, 0.012f), woodDarkMaterial);
            Box(root, "CabinetFrame", new Vector3(halfW - 0.05f, height * 0.5f, frontZ + 0.006f),
                new Vector3(0.06f, height - 0.06f, 0.012f), woodDarkMaterial);
            Box(root, "CabinetFrame", new Vector3(0f, height - 0.05f, frontZ + 0.006f),
                new Vector3(width - 0.1f, 0.06f, 0.012f), woodDarkMaterial);
            Box(root, "CabinetFrame", new Vector3(0f, 0.11f, frontZ + 0.006f),
                new Vector3(width - 0.1f, 0.06f, 0.012f), woodDarkMaterial);

            // 可开的门：铰链在左，门板 + 凸起芯板 + 把手 + 两个合页
            float doorW = width - 0.2f;
            float doorH = height - 0.24f;

            var pivot = new GameObject("ContainerDoor").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(-doorW * 0.5f, 0.14f + doorH * 0.5f, frontZ + 0.03f);

            Box(pivot, "DoorPanel", new Vector3(doorW * 0.5f, 0f, 0f),
                new Vector3(doorW, doorH, 0.032f), woodMaterial);
            Box(pivot, "DoorInset", new Vector3(doorW * 0.5f, 0f, 0.022f),
                new Vector3(doorW - 0.14f, doorH - 0.18f, 0.012f), woodDarkMaterial);
            Box(pivot, "Handle", new Vector3(doorW - 0.07f, 0f, 0.04f),
                new Vector3(0.028f, 0.16f, 0.03f), metalMaterial);
            Box(pivot, "Hinge", new Vector3(0.03f, doorH * 0.32f, 0.012f),
                new Vector3(0.05f, 0.09f, 0.035f), metalMaterial);
            Box(pivot, "Hinge", new Vector3(0.03f, -doorH * 0.32f, 0.012f),
                new Vector3(0.05f, 0.09f, 0.035f), metalMaterial);

            // 门楣上的铭牌
            Box(root, "CabinetPlate", new Vector3(0f, height - 0.05f, frontZ + 0.02f),
                new Vector3(0.24f, 0.028f, 0.008f), metalMaterial);

            // 上层隔板上的病历夹，开门后能看到柜子不是空的
            Box(root, "CabinetFiles", new Vector3(-0.08f, 0.98f, 0.02f),
                new Vector3(0.34f, 0.07f, 0.26f), paperMaterial);

            return pivot;
        }

        /// <summary>拼装三斗抽屉柜，返回最上层（能拉出来的）抽屉。</summary>
        private static Transform BuildDrawerBody(Transform root, float width, float height,
            float depth)
        {
            const float panel = 0.035f;
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            float frontZ = halfD + 0.015f;

            Box(root, "DrawerPlinth", new Vector3(0f, 0.035f, -0.015f),
                new Vector3(width - 0.1f, 0.07f, depth - 0.08f), woodDarkMaterial);
            Box(root, "DrawerSide", new Vector3(-halfW + panel * 0.5f, height * 0.5f, 0f),
                new Vector3(panel, height, depth), woodMaterial);
            Box(root, "DrawerSide", new Vector3(halfW - panel * 0.5f, height * 0.5f, 0f),
                new Vector3(panel, height, depth), woodMaterial);
            Box(root, "DrawerTop", new Vector3(0f, height - 0.02f, 0f),
                new Vector3(width, 0.04f, depth + 0.02f), woodDarkMaterial);
            Box(root, "DrawerBottom", new Vector3(0f, 0.07f + panel * 0.5f, 0f),
                new Vector3(width - panel * 2f, panel, depth), woodMaterial);
            Box(root, "DrawerBack", new Vector3(0f, height * 0.5f, -halfD + 0.015f),
                new Vector3(width - panel * 2f, height - 0.1f, 0.03f), woodDarkMaterial);

            // 内侧滑轨
            Box(root, "DrawerRunner", new Vector3(-halfW + panel + 0.01f, 0.70f, 0f),
                new Vector3(0.02f, 0.03f, depth - 0.1f), metalMaterial);
            Box(root, "DrawerRunner", new Vector3(halfW - panel - 0.01f, 0.70f, 0f),
                new Vector3(0.02f, 0.03f, depth - 0.1f), metalMaterial);

            float frontW = width - 0.08f;
            float frontH = 0.2f;

            BuildDrawerFront(root, frontW, frontH, 0.22f, frontZ);
            BuildDrawerFront(root, frontW, frontH, 0.47f, frontZ);

            // 上层抽屉：面板 + 斗体 + 把手，整体可以拉出来
            var tray = new GameObject("Drawer").transform;
            tray.SetParent(root, false);
            tray.localPosition = new Vector3(0f, 0.70f, 0f);

            Box(tray, "DrawerFront", new Vector3(0f, 0f, frontZ),
                new Vector3(frontW, frontH, 0.03f), woodMaterial);
            Box(tray, "DrawerInset", new Vector3(0f, 0f, frontZ + 0.018f),
                new Vector3(frontW - 0.12f, frontH - 0.06f, 0.008f), woodDarkMaterial);
            Box(tray, "DrawerHandle", new Vector3(0f, 0f, frontZ + 0.032f),
                new Vector3(0.2f, 0.03f, 0.025f), metalMaterial);
            Box(tray, "DrawerTrayBottom", new Vector3(0f, -0.09f, frontZ - 0.19f),
                new Vector3(frontW - 0.06f, 0.02f, 0.36f), woodMaterial);
            Box(tray, "DrawerTraySide", new Vector3(-(frontW * 0.5f - 0.03f), 0f, frontZ - 0.19f),
                new Vector3(0.02f, 0.16f, 0.36f), woodMaterial);
            Box(tray, "DrawerTraySide", new Vector3(frontW * 0.5f - 0.03f, 0f, frontZ - 0.19f),
                new Vector3(0.02f, 0.16f, 0.36f), woodMaterial);
            Box(tray, "DrawerTrayBack", new Vector3(0f, 0f, frontZ - 0.37f),
                new Vector3(frontW - 0.06f, 0.16f, 0.02f), woodMaterial);

            // 抽屉内衬：垫纸 + 叠好的一小块布，让拉开的抽屉不像是空盒子
            Box(tray, "DrawerLiner", new Vector3(0f, -0.078f, frontZ - 0.19f),
                new Vector3(frontW - 0.1f, 0.006f, 0.32f), paperMaterial);
            Box(tray, "DrawerCloth", new Vector3(-0.13f, -0.05f, frontZ - 0.3f),
                new Vector3(0.22f, 0.05f, 0.12f), fabricMaterial);

            return tray;
        }

        private static void BuildDrawerFront(Transform parent, float width, float height,
            float y, float z)
        {
            Box(parent, "DrawerFront", new Vector3(0f, y, z),
                new Vector3(width, height, 0.03f), woodMaterial);
            Box(parent, "DrawerInset", new Vector3(0f, y, z + 0.018f),
                new Vector3(width - 0.12f, height - 0.06f, 0.008f), woodDarkMaterial);
            Box(parent, "DrawerHandle", new Vector3(0f, y, z + 0.032f),
                new Vector3(0.2f, 0.03f, 0.025f), metalMaterial);
        }

        /// <summary>
        /// 用真实模型铺满房间主家具。返回 true 表示已完整布置，调用方跳过程序化家具。
        /// 全部道具都按 y=0 落地，靠墙摆放，不会浮空或堵门。
        /// </summary>
        private static bool BuildRealFurniture(Transform group, RoomType type,
            float x0, float x1, float z0, float z1)
        {
            switch (type)
            {
                case RoomType.Ward:
                {
                    float span = z1 - z0;
                    int beds = span >= 5.5f ? 4 : 2;
                    int placed = 0;

                    for (int i = 0; i < beds; i++)
                    {
                        int column = i % 2;
                        int row = i / 2;
                        float x = column == 0 ? x0 + 1.4f : x1 - 2.6f;
                        float z = z0 + 1.3f + row * (beds > 2 ? 2.4f : 0f);

                        if (SanatoriumProps.Place(group, "GothicBed_01",
                                new Vector3(x, 0f, z), 0f, 1.15f))
                        {
                            placed++;
                        }
                    }

                    Vector3 tablePos = new Vector3(x0 + 0.6f, 0f, z1 - 1.2f);
                    Vector3 lampPos = tablePos + new Vector3(0f, 0.62f, 0f);
                    if (SanatoriumProps.PlaceSized(group, "side_table_01", tablePos, 0f,
                            new Vector3(0.5f, 0.62f, 0.5f)))
                    {
                        SanatoriumProps.PlaceSized(group, "vintage_oil_lamp", lampPos, 0f,
                            new Vector3(0.18f, 0.38f, 0.18f));
                    }

                    SanatoriumProps.Place(group, "potted_plant_04",
                        new Vector3(x1 - 0.8f, 0f, z0 + 1.1f), -15f, 1.15f);

                    // 输液架仍用简单几何，真实模型里没有对应道具
                    Cylinder(group, "IVStand", new Vector3(x0 + 0.55f, 0.85f, z0 + 2.2f),
                        new Vector3(0.04f, 1.7f, 0.04f), metalMaterial);

                    return placed > 0;
                }

                case RoomType.Office:
                {
                    int placed = 0;

                    for (int row = 0; row < 3; row++)
                    {
                        for (int column = 0; column < 4; column++)
                        {
                            float x = Mathf.Lerp(x0 + 1.1f, x1 - 1.1f, column / 3f);
                            float z = Mathf.Lerp(z0 + 1.2f, z1 - 1.2f, row / 2f);

                            if (SanatoriumProps.PlaceSized(group, "small_wooden_table_01",
                                    new Vector3(x, 0f, z), 0f, new Vector3(1.1f, 0.74f, 0.65f)))
                            {
                                placed++;
                            }

                            SanatoriumProps.PlaceSized(group, "WoodenChair_01",
                                new Vector3(x, 0f, z - 0.78f), 180f,
                                new Vector3(0.5f, 0.9f, 0.55f));
                        }
                    }

                    SanatoriumProps.Place(group, "potted_plant_04",
                        new Vector3(x0 + 0.75f, 0f, z0 + 0.8f), 10f, 1.2f);

                    return placed > 0;
                }

                case RoomType.Treatment:
                {
                    int placed = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        float z = Mathf.Lerp(z0 + 1.2f, z1 - 1.2f, i / 2f);
                        if (SanatoriumProps.Place(group, "GothicBed_01",
                                new Vector3(x0 + 1.5f, 0f, z), 0f, 1.15f))
                        {
                            placed++;
                        }

                        Cylinder(group, "IVStand", new Vector3(x0 + 2.6f, 0.85f, z),
                            new Vector3(0.04f, 1.7f, 0.04f), metalMaterial);
                    }

                    if (SanatoriumProps.PlaceSized(group, "painted_wooden_cabinet",
                            new Vector3(x1 - 0.65f, 0f, (z0 + z1) * 0.5f), 270f,
                            new Vector3(0.85f, 1.55f, 0.5f)))
                    {
                        placed++;
                    }

                    return placed > 0;
                }

                case RoomType.Duty:
                {
                    int placed = 0;
                    float cx = (x0 + x1) * 0.5f;

                    if (SanatoriumProps.PlaceSized(group, "small_wooden_table_01",
                            new Vector3(cx, 0f, z0 + 1.3f), 0f,
                            new Vector3(1.3f, 0.74f, 0.7f)))
                    {
                        placed++;
                    }

                    SanatoriumProps.PlaceSized(group, "WoodenChair_01",
                        new Vector3(cx, 0f, z0 + 2.1f), 0f,
                        new Vector3(0.5f, 0.9f, 0.55f));

                    if (SanatoriumProps.Place(group, "GothicBed_01",
                            new Vector3(x1 - 1.5f, 0f, z1 - 1.4f), 0f, 1.15f))
                    {
                        placed++;
                    }

                    if (SanatoriumProps.PlaceSized(group, "GothicCommode_01",
                            new Vector3(x0 + 0.65f, 0f, z1 - 0.9f), 90f,
                            new Vector3(1f, 1f, 0.55f)))
                    {
                        placed++;
                    }

                    // 桌上的登记册
                    SanatoriumProps.PlaceSized(group, "binder_notebook",
                        new Vector3(cx + 0.2f, 0.74f, z0 + 1.3f), 18f,
                        new Vector3(0.32f, 0.05f, 0.24f), false);

                    return placed >= 2;
                }

                case RoomType.Lounge:
                {
                    int placed = 0;
                    float cx = (x0 + x1) * 0.5f;
                    float cz = (z0 + z1) * 0.5f;

                    if (SanatoriumProps.Place(group, "Sofa_01",
                            new Vector3(cx, 0f, cz - 0.8f), 0f, 0.9f))
                    {
                        placed++;
                    }

                    if (SanatoriumProps.PlaceSized(group, "small_wooden_table_01",
                            new Vector3(cx, 0f, cz + 0.6f), 0f,
                            new Vector3(1f, 0.6f, 0.6f)))
                    {
                        placed++;
                    }

                    if (SanatoriumProps.PlaceSized(group, "ArmChair_01",
                            new Vector3(x0 + 1.1f, 0f, z0 + 1.3f), 45f,
                            new Vector3(0.85f, 0.95f, 0.9f)))
                    {
                        placed++;
                    }

                    SanatoriumProps.Place(group, "potted_plant_04",
                        new Vector3(x1 - 0.8f, 0f, z1 - 0.8f), 0f, 1.2f);

                    return placed >= 2;
                }

                case RoomType.Pharmacy:
                    return BuildRealShelves(group, x0, x1, z0, z1, 4, true);

                case RoomType.Storage:
                    return BuildRealShelves(group, x0, x1, z0, z1, 4, true);

                case RoomType.Archive:
                    return BuildRealShelves(group, x0, x1, z0, z1, 5, false);
            }

            return false;
        }

        private static bool BuildRealShelves(Transform group, float x0, float x1,
            float z0, float z1, int count, bool addBottles)
        {
            int placed = 0;

            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(x0 + 0.7f, x1 - 0.7f,
                    count > 1 ? i / (float)(count - 1) : 0.5f);

                if (!SanatoriumProps.PlaceSized(group, "wooden_bookshelf_worn",
                        new Vector3(x, 0f, z1 - 0.45f), 180f,
                        new Vector3(1.4f, 2f, 0.42f)))
                {
                    return false;
                }

                placed++;

                if (addBottles && i % 2 == 0)
                {
                    // 瓶子摆在书架层板上，按层高定位，不会浮空
                    SanatoriumProps.Place(group, "wine_bottles_01",
                        new Vector3(x, 0.95f, z1 - 0.5f), 0f, 0.3f);
                }
            }

            SanatoriumProps.Place(group, "wooden_crate_01",
                new Vector3(x0 + 0.7f, 0f, z0 + 0.8f), 20f, 0.55f);
            SanatoriumProps.Place(group, "Barrel_02",
                new Vector3(x1 - 0.7f, 0f, z0 + 0.8f), 0f, 0.9f);

            return placed >= count;
        }

        /// <summary>
        /// 给每类房间补一到两件真实模型摆件。
        /// 只放在靠门一侧的墙角（门洞在本地 x 约 0 处，墙角不会被堵），
        /// 统一 y=0 落地，由 MansionRealModels 负责缩放与碰撞体，避免浮空和穿模。
        /// </summary>
        private static void AddRoomProps(Transform content, RoomType type,
            float x0, float x1, float z0, float z1)
        {
            float frontLeftX = x0 + 0.65f;
            float frontRightX = x1 - 0.65f;
            float frontZ = z0 + 0.7f;
            float backRightX = x1 - 0.7f;
            float backZ = z1 - 0.8f;

            switch (type)
            {
                case RoomType.Ward:
                    SanatoriumProps.Place(content, "wooden_crate_01",
                        new Vector3(frontLeftX, 0f, frontZ), 18f, 0.55f);
                    SanatoriumProps.Place(content, "potted_plant_04",
                        new Vector3(frontRightX, 0f, frontZ), -12f, 1.15f);
                    break;
                case RoomType.Office:
                    // 办公室被 12 个工位占满，墙角也贴着桌子，不放额外摆件以免穿模
                    break;
                case RoomType.Exam:
                    SanatoriumProps.Place(content, "tool_cart",
                        new Vector3(frontLeftX, 0f, frontZ), 0f, 0.95f);
                    break;
                case RoomType.Treatment:
                    SanatoriumProps.Place(content, "painted_wooden_cabinet",
                        new Vector3(frontRightX, 0f, frontZ), 0f, 1.6f);
                    break;
                case RoomType.Surgery:
                    SanatoriumProps.Place(content, "tool_cart",
                        new Vector3(frontRightX, 0f, frontZ), 0f, 0.95f);
                    break;
                case RoomType.Pharmacy:
                    SanatoriumProps.PlaceSized(content, "wooden_bookshelf_worn",
                        new Vector3(frontLeftX, 0f, frontZ), 90f,
                        new Vector3(1.4f, 2f, 0.42f));
                    break;
                case RoomType.Duty:
                    SanatoriumProps.Place(content, "wooden_table_02",
                        new Vector3(frontLeftX, 0f, frontZ), 0f, 0.75f);
                    break;
                case RoomType.Storage:
                    SanatoriumProps.Place(content, "wooden_crate_02",
                        new Vector3(frontLeftX, 0f, frontZ), 12f, 0.65f);
                    SanatoriumProps.Place(content, "Barrel_02",
                        new Vector3(frontRightX, 0f, frontZ), 0f, 0.9f);
                    break;
                case RoomType.Lounge:
                    SanatoriumProps.Place(content, "ArmChair_01",
                        new Vector3(frontLeftX, 0f, frontZ), 20f, 0.95f);
                    break;
                case RoomType.Archive:
                    SanatoriumProps.PlaceSized(content, "wooden_bookshelf_worn",
                        new Vector3(frontLeftX, 0f, frontZ), 90f,
                        new Vector3(1.4f, 1.95f, 0.42f));
                    break;
                case RoomType.Utility:
                    SanatoriumProps.Place(content, "Barrel_02",
                        new Vector3(frontLeftX, 0f, frontZ), 0f, 0.9f);
                    break;
                case RoomType.Lobby:
                    SanatoriumProps.Place(content, "painted_wooden_bench",
                        new Vector3(backRightX, 0f, backZ), 90f, 0.55f);
                    break;
            }
        }

        private static void BuildWard(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            float spanZ = z1 - z0;
            int beds = spanZ >= 5.5f ? 4 : 2;

            for (int i = 0; i < beds; i++)
            {
                int column = i % 2;
                int row = i / 2;
                float x = Mathf.Lerp(x0 + 1f, x1 - 1f, column == 0 ? 0.22f : 0.78f);
                float z = z0 + 1.1f + row * (beds > 2 ? 2.4f : 0f);

                Bed(group, new Vector3(x, baseY, z));
            }

            // 靠墙的床头柜
            Box(group, "BedsideCabinet", new Vector3(x0 + 0.5f, baseY + 0.4f, z1 - 1.2f),
                new Vector3(0.5f, 0.8f, 0.5f), counterMaterial, true);

            // 独立卫生间：隔断 + 马桶 + 洗手池
            float toiletX = x1 - 1.4f;
            float toiletZ = z1 - 1.0f;
            Box(group, "ToiletPartition", new Vector3(toiletX - 1.1f, baseY + 1.1f, toiletZ),
                new Vector3(0.08f, 2.2f, 2.2f), trimMaterial, true);
            Box(group, "Toilet", new Vector3(toiletX, baseY + 0.2f, toiletZ - 0.4f),
                new Vector3(0.45f, 0.4f, 0.6f), glassMaterial, true);
            Box(group, "ToiletTank", new Vector3(toiletX, baseY + 0.55f, toiletZ - 0.68f),
                new Vector3(0.45f, 0.5f, 0.18f), glassMaterial, true);
            Box(group, "SinkBase", new Vector3(toiletX + 0.2f, baseY + 0.36f, toiletZ + 0.6f),
                new Vector3(0.4f, 0.72f, 0.3f), trimMaterial);
            Box(group, "Sink", new Vector3(toiletX + 0.2f, baseY + 0.78f, toiletZ + 0.6f),
                new Vector3(0.6f, 0.12f, 0.4f), metalMaterial, true);

            // 输液架
            Cylinder(group, "IVStand", new Vector3(x0 + 0.6f, baseY + 0.85f, z0 + 1.6f),
                new Vector3(0.04f, 1.7f, 0.04f), metalMaterial);
        }

        private static void Bed(Transform group, Vector3 position)
        {
            // 床架落地（0~0.5），床垫、枕头依次叠在床架上，床头板与床架相连
            Box(group, "BedFrame", position + new Vector3(0f, 0.25f, 0f),
                new Vector3(1f, 0.5f, 2.1f), metalMaterial, true);
            Box(group, "Mattress", position + new Vector3(0f, 0.57f, 0f),
                new Vector3(0.94f, 0.14f, 2f), fabricMaterial);
            Box(group, "Pillow", position + new Vector3(0f, 0.70f, 0.78f),
                new Vector3(0.6f, 0.12f, 0.34f), paperMaterial);
            Box(group, "Headboard", position + new Vector3(0f, 0.75f, -1.02f),
                new Vector3(1.02f, 0.8f, 0.08f), metalMaterial);
        }

        private static void BuildOffice(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            // 12 个工位：3 行 x 4 列
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    // 列距拉开一点，保证两排桌子之间留出能走人的过道（玩家直径 0.6 m）
                    float x = Mathf.Lerp(x0 + 0.7f, x1 - 0.7f, col / 3f);
                    float z = Mathf.Lerp(z0 + 1f, z1 - 1f, row / 2f);

                    Box(group, "Desk", new Vector3(x, baseY + 0.38f, z),
                        new Vector3(1.3f, 0.76f, 0.65f), counterMaterial, true);
                    Box(group, "DeskTop", new Vector3(x, baseY + 0.79f, z),
                        new Vector3(1.38f, 0.06f, 0.72f), trimMaterial);
                    // 显示器坐在桌面（桌面顶 0.82），椅子落地、椅背接在座面上
                    Box(group, "Monitor", new Vector3(x, baseY + 1.0f, z + 0.16f),
                        new Vector3(0.55f, 0.36f, 0.05f), metalMaterial);
                    Box(group, "Chair", new Vector3(x, baseY + 0.22f, z - 0.65f),
                        new Vector3(0.5f, 0.44f, 0.5f), fabricMaterial, true);
                    Box(group, "ChairBack", new Vector3(x, baseY + 0.55f, z - 0.87f),
                        new Vector3(0.5f, 0.55f, 0.08f), fabricMaterial);
                }
            }
        }

        private static void BuildExamRoom(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            float cx = (x0 + x1) * 0.5f;
            float cz = (z0 + z1) * 0.5f;

            Box(group, "ExamTable", new Vector3(cx, baseY + 0.72f, cz),
                new Vector3(0.9f, 0.14f, 2f), fabricMaterial, true);
            Box(group, "ExamTableLeg", new Vector3(cx, baseY + 0.35f, cz),
                new Vector3(0.7f, 0.7f, 1.7f), metalMaterial);
            // 检查灯改成落地灯（原来悬在半空），洗手池补一个落地柜
            Box(group, "ExamLampStand", new Vector3(cx + 1.1f, baseY + 0.8f, cz),
                new Vector3(0.08f, 1.6f, 0.08f), metalMaterial);
            Box(group, "ExamLamp", new Vector3(cx + 1.1f, baseY + 1.68f, cz),
                new Vector3(0.3f, 0.3f, 0.3f), glassMaterial);
            Box(group, "ExamCabinet", new Vector3(x0 + 0.6f, baseY + 0.9f, z1 - 0.8f),
                new Vector3(0.6f, 1.8f, 1.2f), counterMaterial, true);
            Box(group, "ExamSinkBase", new Vector3(x1 - 0.7f, baseY + 0.35f, z0 + 0.8f),
                new Vector3(0.5f, 0.7f, 0.42f), trimMaterial);
            Box(group, "ExamSink", new Vector3(x1 - 0.7f, baseY + 0.76f, z0 + 0.8f),
                new Vector3(0.6f, 0.12f, 0.5f), metalMaterial, true);
        }

        private static void BuildTreatmentRoom(Transform group, float x0, float x1, float z0,
            float z1, float baseY)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = Mathf.Lerp(z0 + 1f, z1 - 1f, i / 2f);
                Bed(group, new Vector3(x0 + 1.2f, baseY, z));
                Cylinder(group, "IVStand", new Vector3(x0 + 2.1f, baseY + 0.85f, z),
                    new Vector3(0.04f, 1.7f, 0.04f), metalMaterial);
            }

            Box(group, "SupplyCabinet", new Vector3(x1 - 0.6f, baseY + 0.9f, (z0 + z1) * 0.5f),
                new Vector3(0.55f, 1.8f, 1.6f), counterMaterial, true);
        }

        private static void BuildSurgeryRoom(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            float cx = (x0 + x1) * 0.5f;
            float cz = (z0 + z1) * 0.5f;

            Box(group, "OperatingTable", new Vector3(cx, baseY + 0.85f, cz),
                new Vector3(0.9f, 0.16f, 2.1f), metalMaterial, true);
            Box(group, "OperatingBase", new Vector3(cx, baseY + 0.4f, cz),
                new Vector3(0.5f, 0.8f, 0.5f), metalMaterial);
            Box(group, "SurgeryLamp", new Vector3(cx, baseY + 3.05f, cz),
                new Vector3(0.9f, 0.16f, 0.9f), glassMaterial);

            Box(group, "InstrumentTable", new Vector3(cx + 1.4f, baseY + 0.45f, cz + 0.6f),
                new Vector3(0.8f, 0.9f, 0.5f), counterMaterial, true);
            Box(group, "SurgeryCabinet", new Vector3(x0 + 0.6f, baseY + 0.95f, z0 + 1.2f),
                new Vector3(0.55f, 1.9f, 1.6f), counterMaterial, true);
        }

        private static void BuildShelves(Transform group, float x0, float x1, float z0, float z1,
            float baseY, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(x0 + 0.4f, x1 - 0.4f, count > 1 ? i / (float)(count - 1) : 0.5f);
                Box(group, "Shelf", new Vector3(x, baseY + 1f, z1 - 0.35f),
                    new Vector3(0.5f, 2f, 0.55f), counterMaterial, true);

                for (int shelf = 0; shelf < 4; shelf++)
                {
                    Box(group, "ShelfBoard", new Vector3(x, baseY + 0.35f + shelf * 0.45f, z1 - 0.35f),
                        new Vector3(0.52f, 0.04f, 0.6f), trimMaterial);
                }
            }
        }

        private static void BuildDutyRoom(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            float cx = (x0 + x1) * 0.5f;
            Box(group, "DutyDesk", new Vector3(cx, baseY + 0.38f, z0 + 1.2f),
                new Vector3(1.4f, 0.76f, 0.7f), counterMaterial, true);
            Box(group, "DutyMonitor", new Vector3(cx, baseY + 0.93f, z0 + 1.2f),
                new Vector3(0.5f, 0.34f, 0.05f), metalMaterial);
            Bed(group, new Vector3(x1 - 1.1f, baseY, z1 - 1.5f));
            Box(group, "Locker", new Vector3(x0 + 0.5f, baseY + 0.95f, z1 - 1.2f),
                new Vector3(0.5f, 1.9f, 0.9f), metalMaterial, true);
        }

        private static void BuildLounge(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            float cx = (x0 + x1) * 0.5f;
            float cz = (z0 + z1) * 0.5f;

            Box(group, "Sofa", new Vector3(cx, baseY + 0.2f, cz - 0.6f),
                new Vector3(2.2f, 0.4f, 0.8f), fabricMaterial, true);
            Box(group, "SofaBack", new Vector3(cx, baseY + 0.5f, cz - 0.95f),
                new Vector3(2.2f, 0.6f, 0.2f), fabricMaterial);
            Box(group, "CoffeeTable", new Vector3(cx, baseY + 0.4f, cz + 0.6f),
                new Vector3(1.1f, 0.08f, 0.6f), trimMaterial, true);
            Box(group, "CoffeeTableLeg", new Vector3(cx, baseY + 0.2f, cz + 0.6f),
                new Vector3(0.9f, 0.4f, 0.4f), metalMaterial);
        }

        private static void BuildUtilityRoom(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            float cx = (x0 + x1) * 0.5f;
            float cz = (z0 + z1) * 0.5f;

            Box(group, "Boiler", new Vector3(cx - 0.8f, baseY + 0.9f, cz),
                new Vector3(1.2f, 1.8f, 1.2f), metalMaterial, true);
            Cylinder(group, "Pipe", new Vector3(cx + 0.6f, baseY + 1.2f, cz),
                new Vector3(0.12f, 2.4f, 0.12f), metalMaterial);
            // 配电箱贴在后墙面上（原来悬在离墙 0.5 m 的空中）
            Box(group, "BreakerPanel", new Vector3(x1 - 0.4f, baseY + 1.5f, z1 + 0.42f),
                new Vector3(0.15f, 0.9f, 0.7f), trimMaterial);
        }

        private static void BuildLobby(Transform group, float x0, float x1, float z0, float z1,
            float baseY)
        {
            Box(group, "Reception", new Vector3((x0 + x1) * 0.5f, baseY + 0.5f, z1 - 1.4f),
                new Vector3(2.2f, 1f, 0.7f), counterMaterial, true);
            Box(group, "ReceptionTop", new Vector3((x0 + x1) * 0.5f, baseY + 1.04f, z1 - 1.4f),
                new Vector3(2.3f, 0.08f, 0.8f), trimMaterial);

            for (int i = 0; i < 3; i++)
            {
                float x = Mathf.Lerp(x0 + 0.8f, x1 - 0.8f, i / 2f);
                Box(group, "Chair", new Vector3(x, baseY + 0.25f, z0 + 1f),
                    new Vector3(0.5f, 0.5f, 0.5f), fabricMaterial, true);
            }
        }

        // ------------------------------------------------------------------
        // 记录文本
        // ------------------------------------------------------------------
        private static void AddClueNote(Transform room, Room type, float backZ)
        {
            if (room == null)
            {
                return;
            }

            // room 是已旋转的内容容器：本地 +z 永远朝后墙。
            // 纸张贴在墙面上（而不是悬在半空），高度避开柜子与书架顶部。
            // 门厅后墙正中是正门开口，记录要挪到门旁。
            float x = type.Type == RoomType.Lobby ? 2.1f : 0f;

            var note = new GameObject("Record_" + type.Name).transform;
            note.SetParent(room, false);
            note.localPosition = new Vector3(x, 2.2f, backZ);
            note.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Box(note, "Paper", Vector3.zero,
                new Vector3(0.36f, 0.46f, 0.04f), paperMaterial);

            // 纸张很薄，单独放一个看不到的碰撞盒，保证交互射线能稳定命中
            var hit = new GameObject("Hit");
            hit.transform.SetParent(note, false);
            var collider = hit.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.8f, 0.9f, 0.6f);

            string body;
            string title = NoteTitle(type.Type, out body);

            var clue = note.gameObject.AddComponent<ClueInteractable>();
            clue.Title = title;
            clue.Body = body + "\n\n—— 记录位置：" + type.Name;
            clue.Label = "阅读" + title;
            clue.ClueId = "sanatorium_" + type.Name;
        }

        /// <summary>主线提示便条：贴在墙上，用来交代下一步该去哪儿。</summary>
        private static void AddStoryNote(Transform parent, Vector3 localPosition, float yaw,
            string title, string body, string clueId)
        {
            var note = new GameObject("Note_" + clueId).transform;
            note.SetParent(parent, false);
            note.localPosition = localPosition;
            note.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(note, "Paper", Vector3.zero, new Vector3(0.34f, 0.42f, 0.03f), paperMaterial);

            var hit = new GameObject("Hit");
            hit.transform.SetParent(note, false);
            var collider = hit.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.75f, 0.85f, 0.45f);

            var clue = note.gameObject.AddComponent<ClueInteractable>();
            clue.Title = title;
            clue.Body = body;
            clue.Label = "阅读" + title;
            clue.ClueId = clueId;
        }

        /// <summary>B1 配电间的总闸：装上保险丝后全楼供电，一层正门才打得开。</summary>
        private static void AddFuseBox(Transform content, float halfD)
        {
            float z = halfD - WallThickness * 0.5f - 0.08f;   // 贴在后墙上

            Box(content, "FusePanel", new Vector3(0f, 1.45f, z),
                new Vector3(0.62f, 0.78f, 0.1f), metalMaterial);
            Box(content, "FusePanelDoor", new Vector3(0f, 1.45f, z - 0.06f),
                new Vector3(0.48f, 0.6f, 0.03f), trimMaterial);
            Box(content, "FuseLever", new Vector3(0.16f, 1.3f, z - 0.1f),
                new Vector3(0.07f, 0.22f, 0.06f), trimMaterial);
            Box(content, "FuseLamp", new Vector3(-0.16f, 1.66f, z - 0.1f),
                new Vector3(0.07f, 0.07f, 0.05f), glassMaterial);

            var root = new GameObject("FuseBox");
            root.transform.SetParent(content, false);
            root.transform.localPosition = new Vector3(0f, 1.45f, z - 0.06f);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.85f, 0.95f, 0.4f);

            root.AddComponent<FuseBoxInteractable>();

            AddStoryNote(content, new Vector3(0.95f, 1.5f, z - 0.12f), 180f,
                "配电间便条",
                "……总闸烧掉了三枚保险丝。备件分别在库房、档案室和储藏室。\n" +
                "装齐之前，一层门厅的电子门禁和车库卷扬机都是断电的。\n\n—— 后勤 老周",
                "sanatorium_fusebox");
        }

        // ------------------------------------------------------------------
        // 恐怖陈设（血迹、散落纸张、翻倒的家具、裹尸袋、坏掉的轮椅）
        // ------------------------------------------------------------------
        /// <summary>按房型往房间里布置恐怖痕迹；立体摆件用物理检测找空位，不压床也不堵门。</summary>
        private static readonly bool EnableHorrorDressing = true;

        private static void AddHorrorDressing(Transform content, Room room,
            float x0, float x1, float z0, float z1)
        {
            if (!EnableHorrorDressing)
            {
                return;
            }

            float midX = (x0 + x1) * 0.5f;
            float midZ = (z0 + z1) * 0.5f;

            // 地面血迹与散落纸张：扁平贴地，压在床下也合理
            BloodPool(content, new Vector3(midX, 0f, midZ), Random.Range(0.55f, 1.1f));
            BloodSplatter(content, new Vector3(midX, 0f, midZ), 1.8f, Random.Range(6, 11));
            ScatteredPapers(content, new Vector3(midX, 0f, midZ), 1.8f, Random.Range(3, 6));

            // 从门口一路拖进房间的血脚印
            BloodFootprints(content, new Vector3(Random.Range(-0.6f, 0.6f), 0f, z0 + 0.8f),
                new Vector3(midX + Random.Range(-1.2f, 1.2f), 0f, midZ + Random.Range(-1.5f, 1.5f)),
                Random.Range(5, 8));

            // 后墙上的垂流血痕
            BloodDrip(content, midX + Random.Range(-1.4f, 1.4f), z1 + 0.44f,
                Random.Range(1.9f, 2.4f), Random.Range(0.5f, 1.3f));

            // 墙上再多一处喷射血痕或一串血手印
            float smearX = midX + Random.Range(-2.4f, 2.4f);

            if (Random.value < 0.55f)
            {
                WallSmear(content, smearX, z1, Random.Range(0.35f, 0.7f), Random.Range(0.7f, 1.4f));
            }
            else
            {
                WallHandprints(content, smearX, z1 + 0.44f, Random.Range(1.0f, 1.4f),
                    Random.Range(0.2f, 0.3f));
            }

            if (RoomTypeHasBloodTrail(room.Type) &&
                TryFindFreeSpot(content, x0, x1, z0, z1, 0.4f, 0.3f, out Vector3 trailSpot))
            {
                BloodTrail(content, new Vector3(trailSpot.x, 0f, z0 + 0.7f), trailSpot, 0.24f);
            }

            // 立体摆件：一次只放一到两件，避免房间里堆满
            switch (room.Type)
            {
                case RoomType.Ward:
                case RoomType.Treatment:
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.6f, 0.9f, out Vector3 chairSpot))
                    {
                        ToppledChair(content, chairSpot, Random.Range(0f, 360f));
                    }

                    if (room.Index % 2 == 0 &&
                        TryFindFreeSpot(content, x0, x1, z0, z1, 0.75f, 0.5f, out Vector3 bagSpot))
                    {
                        BodyBag(content, bagSpot, Random.Range(-30f, 30f));
                    }
                    break;

                case RoomType.Surgery:
                    BloodPool(content, new Vector3(midX, 0f, midZ), 1.5f);
                    WallSmear(content, midX, z1, 0.55f, 1.2f);
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.5f, 0.9f, out Vector3 traySpot))
                    {
                        ToppledStand(content, traySpot, Random.Range(0f, 360f));
                    }
                    break;

                case RoomType.Exam:
                case RoomType.Pharmacy:
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.6f, 0.8f, out Vector3 crateSpot))
                    {
                        FallenCrate(content, crateSpot, Random.Range(0f, 360f));
                    }
                    break;

                case RoomType.Storage:
                case RoomType.Archive:
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.6f, 0.8f, out Vector3 crateA))
                    {
                        FallenCrate(content, crateA, Random.Range(0f, 360f));
                    }

                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.6f, 0.8f, out Vector3 crateB))
                    {
                        FallenCrate(content, crateB, Random.Range(0f, 360f));
                    }
                    break;

                case RoomType.Duty:
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.55f, 0.9f, out Vector3 dutyChair))
                    {
                        ToppledChair(content, dutyChair, Random.Range(0f, 360f));
                    }

                    WallSmear(content, midX, z1, 0.4f, 0.9f);
                    break;

                case RoomType.Lounge:
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.6f, 0.9f, out Vector3 loungeChair))
                    {
                        ToppledChair(content, loungeChair, Random.Range(0f, 360f));
                    }
                    break;

                case RoomType.Utility:
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.55f, 0.9f, out Vector3 standSpot))
                    {
                        ToppledStand(content, standSpot, Random.Range(0f, 360f));
                    }
                    break;

                case RoomType.Lobby:
                    if (TryFindFreeSpot(content, x0, x1, z0, z1, 0.8f, 0.8f, out Vector3 wheelSpot))
                    {
                        BrokenWheelchair(content, wheelSpot, Random.Range(0f, 360f));
                    }
                    break;
            }

            // 真实模型陈设：石座上的大理石胸像、倾倒的酒瓶（都是 Poly Haven 实拍模型 + PBR 贴图）
            int seed = room.Level * 31 + room.Index * 7 + (int)room.Type;

            if (seed % 3 == 0 &&
                TryFindFreeSpot(content, x0, x1, z0, z1, 0.5f, 1.5f, out Vector3 bustSpot))
            {
                Box(content, "Pedestal", new Vector3(bustSpot.x, 0.45f, bustSpot.z),
                    new Vector3(0.42f, 0.9f, 0.42f), counterMaterial, true);
                SanatoriumProps.Place(content, "marble_bust_01",
                    new Vector3(bustSpot.x, 0.92f, bustSpot.z), Random.Range(0f, 360f), 0.55f);
            }

            if (seed % 4 == 1 &&
                TryFindFreeSpot(content, x0, x1, z0, z1, 0.45f, 0.35f, out Vector3 bottleSpot))
            {
                SanatoriumProps.Place(content, "wine_bottles_01",
                    new Vector3(bottleSpot.x, 0f, bottleSpot.z), Random.Range(0f, 360f), 0.28f);
            }
        }

        private static bool RoomTypeHasBloodTrail(RoomType type)
        {
            return type == RoomType.Surgery || type == RoomType.Treatment ||
                   type == RoomType.Exam || type == RoomType.Utility;
        }

        /// <summary>在一间房里找一个不压家具、不堵门的空地。</summary>
        private static bool TryFindFreeSpot(Transform content, float x0, float x1, float z0, float z1,
            float radius, float height, out Vector3 localPosition)
        {
            localPosition = Vector3.zero;
            Physics.SyncTransforms();

            for (int attempt = 0; attempt < 28; attempt++)
            {
                float x = Random.Range(x0 + radius, x1 - radius);
                float z = Random.Range(z0 + radius, z1 - radius);

                // 门口 1.6 m 内保持畅通
                if (Mathf.Abs(x) < 1.5f && z < z0 + 2.4f)
                {
                    continue;
                }

                Vector3 center = content.TransformPoint(new Vector3(x, 0f, z)) +
                                 Vector3.up * (height * 0.5f + 0.1f);

                if (Physics.CheckBox(center, new Vector3(radius, height * 0.5f, radius),
                        Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                localPosition = new Vector3(x, 0f, z);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 地面血泊：由外到内三层——干涸扩散的不规则外圈、湿润的主血泊、较新的亮红核心，
        /// 外圈用几个错位旋转的扁块拼出自然的边缘。
        /// </summary>
        private static void BloodPool(Transform parent, Vector3 position, float size)
        {
            // 只保留一张带 alpha 的血泊贴图，多余的底色/亮心层都去掉
            var decal = Box(parent, "BloodDecal",
                new Vector3(position.x, position.y + 0.008f, position.z),
                new Vector3(size * 1.45f, 0.004f, size * 1.45f), bloodDecalMaterial);
            decal.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        /// <summary>拖拽血痕：从 from 拖到 to 的一条暗红色带。</summary>
        private static void BloodTrail(Transform parent, Vector3 from, Vector3 to, float width)
        {
            Vector3 delta = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float length = delta.magnitude;

            if (length < 0.3f)
            {
                return;
            }

            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

            float baseHeight = (from.y + to.y) * 0.5f;
            float midX = (from.x + to.x) * 0.5f;
            float midZ = (from.z + to.z) * 0.5f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            // 只保留一张沿拖拽方向拉长的血迹贴图，不再叠加底色和血线
            var decal = Box(parent, "BloodTrail", new Vector3(midX, baseHeight + 0.008f, midZ),
                new Vector3(width * 2.3f, 0.004f, length * 1.02f), bloodSplatterMaterial);
            decal.transform.localRotation = rotation;
        }

        /// <summary>飞溅血点：一滩血周围散落的小血滴。</summary>
        private static void BloodSplatter(Transform parent, Vector3 center, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(radius * 0.35f, radius);

                // 每滴都是一小片带飞溅细节的贴图，远看是血点、近看有溅射形状
                float dropSize = Random.Range(0.18f, 0.36f);

                var drop = Box(parent, "BloodDrop",
                    new Vector3(center.x + Mathf.Cos(angle) * distance, center.y + 0.008f,
                        center.z + Mathf.Sin(angle) * distance),
                    new Vector3(dropSize, 0.004f, dropSize), bloodSplatterMaterial);
                drop.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }

        /// <summary>血脚印：从 from 一路走到 to 的一串脚印。</summary>
        private static void BloodFootprints(Transform parent, Vector3 from, Vector3 to, int steps)
        {
            Vector3 delta = new Vector3(to.x - from.x, 0f, to.z - from.z);

            if (delta.magnitude < 0.6f || steps < 2)
            {
                return;
            }

            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float rad = yaw * Mathf.Deg2Rad;
            var right = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));

            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                Vector3 point = Vector3.Lerp(from, to, t) + right * (i % 2 == 0 ? 0.13f : -0.13f);

                var print = Box(parent, "BloodFootprint",
                    new Vector3(point.x, point.y + 0.006f, point.z),
                    new Vector3(0.11f, 0.005f, 0.24f), bloodMaterial);
                print.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        /// <summary>墙上的垂直血痕（从高处往下淌），z 传墙面前方的位置。</summary>
        private static void BloodDrip(Transform parent, float x, float z, float topY, float length)
        {
            float width = Random.Range(0.03f, 0.08f);

            // 只保留一张竖向拉长的血迹贴图
            var decal = Box(parent, "BloodDrip", new Vector3(x, topY - length * 0.5f, z),
                new Vector3(width * 3.2f, length * 1.15f, 0.005f), bloodSplatterMaterial);
            decal.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
        }

        /// <summary>墙上的血手印/喷射痕：贴在后方墙面上。</summary>
        private static void WallSmear(Transform parent, float x, float wallZ,
            float width, float height, float faceOffset = 0.42f, float yOffset = 0f)
        {
            float centerY = height * 0.5f + 0.5f + yOffset;

            // 主体：带 alpha 的喷溅贴图
            var decal = Box(parent, "WallSmear",
                new Vector3(x, centerY, wallZ + faceOffset),
                new Vector3(width * 1.9f, height * 1.45f, 0.005f), bloodSplatterMaterial);
            decal.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        /// <summary>墙上的血手印：手高度附近几片被拖开的小血印。</summary>
        private static void WallHandprints(Transform parent, float x, float z, float y, float scale)
        {
            int count = Random.Range(2, 5);

            for (int i = 0; i < count; i++)
            {
                var print = Box(parent, "BloodHandprint",
                    new Vector3(x + i * scale * 0.44f, y + Random.Range(-0.08f, 0.08f), z),
                    new Vector3(scale, scale * Random.Range(0.8f, 1.2f), 0.004f),
                    bloodSplatterMaterial);
                print.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-28f, 28f));
            }
        }

        /// <summary>散落的纸张。</summary>
        private static void ScatteredPapers(Transform parent, Vector3 center, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(0.25f, radius);

                var paper = Box(parent, "ScatteredPaper",
                    new Vector3(center.x + Mathf.Cos(angle) * distance, center.y + 0.008f,
                        center.z + Mathf.Sin(angle) * distance),
                    new Vector3(Random.Range(0.18f, 0.26f), 0.006f, Random.Range(0.24f, 0.32f)),
                    paperMaterial);
                paper.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }

        /// <summary>侧翻的椅子。</summary>
        private static void ToppledChair(Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject("ToppledChair").transform;
            root.SetParent(parent, false);
            root.localPosition = position + Vector3.up * 0.2f;
            root.localRotation = Quaternion.Euler(8f, yaw, 96f);

            Box(root, "ChairSeat", Vector3.zero, new Vector3(0.46f, 0.05f, 0.46f), woodMaterial);
            Box(root, "ChairBack", new Vector3(0f, 0.26f, -0.22f),
                new Vector3(0.46f, 0.5f, 0.05f), woodMaterial);
            Box(root, "ChairLeg", new Vector3(-0.19f, -0.22f, 0.19f),
                new Vector3(0.05f, 0.44f, 0.05f), metalMaterial);
            Box(root, "ChairLeg", new Vector3(0.19f, -0.22f, 0.19f),
                new Vector3(0.05f, 0.44f, 0.05f), metalMaterial);
            Box(root, "ChairLeg", new Vector3(-0.19f, -0.22f, -0.19f),
                new Vector3(0.05f, 0.44f, 0.05f), metalMaterial);
            Box(root, "ChairLeg", new Vector3(0.19f, -0.22f, -0.19f),
                new Vector3(0.05f, 0.44f, 0.05f), metalMaterial);
        }

        /// <summary>倒地的输液架。</summary>
        private static void ToppledStand(Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject("ToppledStand").transform;
            root.SetParent(parent, false);
            root.localPosition = position + Vector3.up * 0.12f;
            root.localRotation = Quaternion.Euler(0f, yaw, 88f);

            Cylinder(root, "StandPole", new Vector3(0f, 0.85f, 0f),
                new Vector3(0.05f, 1.7f, 0.05f), metalMaterial);
            Box(root, "StandBase", new Vector3(0f, 0.03f, 0f),
                new Vector3(0.42f, 0.05f, 0.42f), metalMaterial);
            Box(root, "StandHook", new Vector3(0f, 1.72f, 0f),
                new Vector3(0.24f, 0.04f, 0.04f), metalMaterial);
        }

        /// <summary>地上的人形裹尸袋。</summary>
        private static void BodyBag(Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject("BodyBag").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(root, "BagBody", new Vector3(0f, 0.17f, 0f),
                new Vector3(0.64f, 0.34f, 1.95f), bodyBagMaterial, true);
            Box(root, "BagZipper", new Vector3(0f, 0.35f, 0f),
                new Vector3(0.035f, 0.02f, 1.75f), metalMaterial);
            Box(root, "BagStrap", new Vector3(0f, 0.35f, -0.55f),
                new Vector3(0.66f, 0.02f, 0.08f), rustMaterial);
            Box(root, "BagStrap", new Vector3(0f, 0.35f, 0.55f),
                new Vector3(0.66f, 0.02f, 0.08f), rustMaterial);
            Box(root, "BagTag", new Vector3(0.36f, 0.2f, 0.7f),
                new Vector3(0.02f, 0.1f, 0.16f), paperMaterial);
        }

        /// <summary>翻倒的木箱。</summary>
        private static void FallenCrate(Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject("FallenCrate").transform;
            root.SetParent(parent, false);
            root.localPosition = position + Vector3.up * 0.24f;
            root.localRotation = Quaternion.Euler(Random.Range(70f, 105f), yaw, Random.Range(-12f, 12f));

            Box(root, "CrateBody", Vector3.zero, new Vector3(0.62f, 0.5f, 0.5f), woodMaterial, true);
            Box(root, "CrateBand", new Vector3(0f, 0f, 0f), new Vector3(0.64f, 0.07f, 0.52f), rustMaterial);
            Box(root, "CrateSlat", new Vector3(0f, 0.18f, 0.26f), new Vector3(0.56f, 0.05f, 0.03f), woodDarkMaterial);
            Box(root, "CrateSlat", new Vector3(0f, -0.18f, 0.26f), new Vector3(0.56f, 0.05f, 0.03f), woodDarkMaterial);
        }

        /// <summary>翻倒的轮椅。</summary>
        private static void BrokenWheelchair(Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject("BrokenWheelchair").transform;
            root.SetParent(parent, false);
            root.localPosition = position + Vector3.up * 0.28f;
            root.localRotation = Quaternion.Euler(0f, yaw, 100f);

            Box(root, "ChairSeat", Vector3.zero, new Vector3(0.48f, 0.06f, 0.46f), bodyBagMaterial);
            Box(root, "ChairBack", new Vector3(0f, 0.34f, -0.2f),
                new Vector3(0.48f, 0.54f, 0.05f), bodyBagMaterial);
            Box(root, "ChairFrame", new Vector3(0f, -0.24f, 0f),
                new Vector3(0.06f, 0.05f, 0.9f), rustMaterial);
            Box(root, "ChairFootrest", new Vector3(0f, -0.34f, 0.34f),
                new Vector3(0.5f, 0.04f, 0.16f), rustMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject wheel = Cylinder(root, "Wheel",
                    new Vector3(side * 0.3f, -0.1f, -0.08f), new Vector3(0.56f, 0.08f, 0.56f),
                    rustMaterial);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        /// <summary>走廊里的恐怖痕迹：血痕、散纸、翻倒的轮椅。</summary>
        private static void AddCorridorHorror(Transform group, float baseY)
        {
            if (!EnableHorrorDressing)
            {
                return;
            }

            float corridorZ = (CorridorA0 + CorridorA1) * 0.5f;

            float[] trailXs = { -30f, -14f, 2f, 18f, 32f };
            foreach (float x in trailXs)
            {
                BloodTrail(group,
                    new Vector3(x, baseY, corridorZ - 1.1f),
                    new Vector3(x + Random.Range(3f, 6f), baseY, corridorZ + 0.9f),
                    0.26f);
            }

            // 血泊、飞溅血点、血脚印与墙面垂流血痕
            BloodPool(group, new Vector3(-14f, baseY, corridorZ), 1.3f);
            BloodSplatter(group, new Vector3(-14f, baseY, corridorZ), 2.2f, 9);
            BloodSplatter(group, new Vector3(18f, baseY, corridorZ + 0.6f), 1.6f, 7);
            BloodFootprints(group, new Vector3(-24f, baseY, corridorZ + 1f),
                new Vector3(-10f, baseY, corridorZ - 0.8f), 9);

            for (float x = -36f; x <= 36f; x += 12f)
            {
                BloodDrip(group, x + Random.Range(-1.5f, 1.5f), CorridorA0 + 0.14f,
                    baseY + Random.Range(2.1f, 2.6f), Random.Range(0.5f, 1.4f));
            }

            // 走廊两侧墙面上的喷射血痕与血手印
            for (float x = -30f; x <= 30f; x += 15f)
            {
                WallSmear(group, x + Random.Range(-2f, 2f), CorridorA0,
                    Random.Range(0.4f, 0.8f), Random.Range(0.8f, 1.5f), 0.14f, baseY);
                WallHandprints(group, x + Random.Range(-2f, 2f), CorridorA1 - 0.14f,
                    baseY + Random.Range(1.0f, 1.4f), Random.Range(0.2f, 0.3f));
            }

            ScatteredPapers(group, new Vector3(-6f, baseY, corridorZ), 2.4f, 6);
            ScatteredPapers(group, new Vector3(22f, baseY, corridorZ), 2.2f, 5);

            var wheelchair = new GameObject("CorridorWheelchair").transform;
            wheelchair.SetParent(group, false);
            wheelchair.localPosition = new Vector3(10f, baseY, corridorZ - 0.4f);
            wheelchair.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Box(wheelchair, "ChairSeat", new Vector3(0f, 0.5f, 0f),
                new Vector3(0.48f, 0.06f, 0.46f), bodyBagMaterial);
            Box(wheelchair, "ChairBack", new Vector3(0f, 0.84f, -0.2f),
                new Vector3(0.48f, 0.54f, 0.05f), bodyBagMaterial);
            Box(wheelchair, "ChairFrame", new Vector3(0f, 0.26f, 0f),
                new Vector3(0.06f, 0.05f, 0.9f), rustMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject wheel = Cylinder(wheelchair, "Wheel",
                    new Vector3(side * 0.3f, 0.3f, -0.06f), new Vector3(0.56f, 0.08f, 0.56f),
                    rustMaterial);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static string NoteTitle(RoomType roomType, out string body)
        {
            switch (roomType)
            {
                case RoomType.Ward:
                    body = "病历摘录：患者入院第三日起拒绝进食，夜间反复呓语「墙里有人」。\n" +
                           "值班护士在页脚补了一句：今天又有两间病房的人同时安静了下来。";
                    return "病房病历";
                case RoomType.Office:
                    body = "行政记录：全院职工共 96 人，本周缺勤 41 人。\n" +
                           "最后一条记录写着：不要再往地下室送人了，名单已经对不上。";
                    return "办公室值班表";
                case RoomType.Exam:
                    body = "检查报告：瞳孔对光反应消失，体温低于 30℃。\n" +
                           "医生在结论栏只写了两个字：不是。";
                    return "检查报告";
                case RoomType.Treatment:
                    body = "治疗记录：镇静剂剂量加倍后，病人仍能准确说出护士的名字。\n" +
                           "他从未见过这名护士。";
                    return "治疗记录";
                case RoomType.Surgery:
                    body = "手术记录：打开胸腔后未见任何异常，主刀医生中途离场。\n" +
                           "助手在背面写：灯灭过一次，再亮时台上的人已经坐起来了。";
                    return "手术记录";
                case RoomType.Pharmacy:
                    body = "药品清单：镇静剂、止痛剂全部领空，签字人一栏是同一个名字。\n" +
                           "那个人三个月前就已经登记死亡。";
                    return "药品清单";
                case RoomType.Duty:
                    body = "值班日志：凌晨两点，四楼传来拖拽声，持续到天亮。\n" +
                           "我们没有人敢上去。";
                    return "值班日志";
                case RoomType.Storage:
                    body = "物资登记：裹尸袋三个月内领用了两百多个。\n" +
                           "登记人写：库存不够了，先用床单。";
                    return "物资登记簿";
                case RoomType.Lounge:
                    body = "休息室留言板：谁能告诉我，为什么走廊尽头总是同一扇门？\n" +
                           "下面有人回：别数房间。";
                    return "休息室留言";
                case RoomType.Archive:
                    body = "档案标签：1978 至 1981 年的病历全部缺失，编号连续跳过。\n" +
                           "标签背面有一行小字：烧掉了，别找。";
                    return "档案标签";
                case RoomType.Utility:
                    body = "设备记录：锅炉整夜超压运行，排气口有烧焦的布片。\n" +
                           "维修工写着：不要下去看，也别问是谁按的启动。";
                    return "设备记录";
                default:
                    body = "前台登记：最后一位来访者没有留下姓名，只写了「来找我姐姐」。\n" +
                           "登记时间之后的所有页都是空白。";
                    return "前台登记簿";
            }
        }

        // ------------------------------------------------------------------
        // 出口
        // ------------------------------------------------------------------
        private static void BuildExit(Transform root, List<Room> rooms, float baseY)
        {
            Room lobby = null;
            foreach (Room room in rooms)
            {
                if (room.Type == RoomType.Lobby)
                {
                    lobby = room;
                    break;
                }
            }

            float x = lobby != null ? (lobby.X0 + lobby.X1) * 0.5f : 0f;
            float z = SouthZ + 1.1f;

            // 一层正门：需要主门钥匙，打开后直接通关
            var pivot = new GameObject("MainDoor");
            pivot.transform.SetParent(root, false);
            pivot.transform.position = new Vector3(x - 1.15f, baseY, SouthZ + 0.25f);

            Box(pivot.transform, "DoorLeaf", new Vector3(1.15f, DoorHeight * 0.5f, 0f),
                new Vector3(2.3f, DoorHeight, 0.12f), metalMaterial, true);
            Box(pivot.transform, "DoorBar", new Vector3(1.15f, 1.5f, 0.09f),
                new Vector3(2.2f, 0.12f, 0.06f), trimMaterial);
            Box(pivot.transform, "DoorBar", new Vector3(1.15f, 2.05f, 0.09f),
                new Vector3(2.2f, 0.12f, 0.06f), trimMaterial);

            var hinge = pivot.AddComponent<HingedDoor>();
            hinge.Pivot = pivot.transform;
            hinge.OpenAngle = -100f;
            hinge.Duration = 1f;
            hinge.AutoCloseDelay = 0f;

            var exitDoor = pivot.AddComponent<LockedExitDoor>();
            exitDoor.Door = hinge;
            exitDoor.RequiredItemId = ItemIds.MainKey;
            exitDoor.RequiredItemName = "主门钥匙";
            exitDoor.LockedHint = "大门被反锁了，锁孔里还插着半截断掉的旧钥匙。";

            PointLight(root, new Vector3(x, baseY + 3f, z + 0.6f), 6f, 1.6f,
                new Color(0.42f, 0.85f, 0.62f));
        }

        /// <summary>备用出口一：地下车库卷帘门（需要卷帘门曲柄 + 已通电）。</summary>
        private static void BuildGarage(Transform root, float baseY, List<Room> rooms)
        {
            float x = GarageCenter(rooms);
            float wallZ = SouthZ;
            float z = wallZ + 0.14f;

            var group = new GameObject("GarageExit").transform;
            group.SetParent(root, false);

            // 门套由 AddDoorFrameX 统一生成，这里只放卷帘本体
            // 卷帘本体：整块上移来“开门”
            var shutter = new GameObject("GarageShutter");
            shutter.transform.SetParent(group, false);
            shutter.transform.localPosition = new Vector3(x, baseY + 1.14f, z);

            Box(shutter.transform, "ShutterPanel", Vector3.zero,
                new Vector3(3f, 2.24f, 0.08f), metalMaterial);
            for (int i = 0; i < 5; i++)
            {
                Box(shutter.transform, "ShutterRib",
                    new Vector3(0f, -0.84f + i * 0.42f, -0.06f),
                    new Vector3(2.92f, 0.07f, 0.05f), trimMaterial);
            }

            var collider = shutter.AddComponent<BoxCollider>();
            collider.size = new Vector3(3f, 2.2f, 0.16f);

            var use = shutter.AddComponent<GarageShutterInteractable>();
            use.Shutter = shutter.transform;
            use.OpenHeight = 2.3f;

            // 卷扬机与地面标线（卷扬机放在室内一侧，不能嵌进墙里）
            Box(group, "Hoist", new Vector3(x + 1.95f, baseY + 2.6f, wallZ + 0.7f),
                new Vector3(0.6f, 0.5f, 0.4f), metalMaterial, true);
            Box(group, "GarageLine", new Vector3(x, baseY + 0.02f, z + 3f),
                new Vector3(3f, 0.02f, 5f), trimMaterial);
        }

        /// <summary>备用出口二：四层东侧消防连廊与外墙消防梯（需要消防斧撬开盖板）。</summary>
        private static void BuildFireEscape(Transform root, float baseY)
        {
            var group = new GameObject("FireEscape").transform;
            group.SetParent(root, false);

            float x = HalfWidth;
            float z = FireExitZ;

            // 外挂平台
            Box(group, "FireBalcony", new Vector3(x + 1.7f, baseY - 0.06f, z),
                new Vector3(3.4f, 0.12f, 3.6f), metalMaterial, true);

            // 三面护栏：防止玩家从四层高度掉下去
            Box(group, "FireRail", new Vector3(x + 1.7f, baseY + 0.55f, z + 1.76f),
                new Vector3(3.4f, 1.1f, 0.08f), metalMaterial, true);
            Box(group, "FireRail", new Vector3(x + 1.7f, baseY + 0.55f, z - 1.76f),
                new Vector3(3.4f, 1.1f, 0.08f), metalMaterial, true);
            Box(group, "FireRail", new Vector3(x + 3.36f, baseY + 0.55f, z - 1.2f),
                new Vector3(0.08f, 1.1f, 1.2f), metalMaterial, true);
            Box(group, "FireRail", new Vector3(x + 3.36f, baseY + 0.55f, z + 1.2f),
                new Vector3(0.08f, 1.1f, 1.2f), metalMaterial, true);

            // 外墙消防梯（纯视觉，从平台一直垂到地面）
            for (int i = 0; i < 26; i++)
            {
                Box(group, "FireLadderRung",
                    new Vector3(x + 3.56f, baseY - 0.5f - i * 0.5f, z),
                    new Vector3(0.5f, 0.05f, 0.9f), metalMaterial);
            }

            // 护栏上的锈死小门：用消防斧撬开即通关
            var gate = Box(group, "FireGate", new Vector3(x + 3.36f, baseY + 0.55f, z),
                new Vector3(0.08f, 1.1f, 1.2f), trimMaterial, true);

            var use = gate.AddComponent<FireEscapeInteractable>();
            use.Hatch = gate.transform;
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------
        private static void AddCeilingLight(Transform root, Room room, float baseY)
        {
            Vector3 center = room.Center;
            float y = baseY + WallHeight - 0.35f;

            if (room.Type != RoomType.Storage && room.Type != RoomType.Utility)
            {
                Box(root, "CeilingPanel",
                    new Vector3(center.x, baseY + WallHeight - 0.06f, center.z),
                    new Vector3(0.75f, 0.04f, 0.75f), panelMaterial);
            }

            PointLight(root, new Vector3(center.x, y, center.z),
                7f, room.Type == RoomType.Ward ? 0.75f : 0.85f,
                new Color(0.86f, 0.88f, 0.9f));
        }

        /// <summary>室内灯光整体亮度系数：0.45 表示全部压到原来的一半以下。</summary>
        private const float LightIntensityScale = 0.45f;

        private static Light PointLight(Transform parent, Vector3 position, float range,
            float intensity, Color color)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity * LightIntensityScale;
            light.shadows = LightShadows.None;
            return light;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position,
            Vector3 size, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            RemoveCollider(go, keepCollider);
            return go;
        }

        /// <summary>圆柱：size.y 按“实际高度”理解（Unity 圆柱原始高度是 2）。</summary>
        private static GameObject Cylinder(Transform parent, string name, Vector3 position,
            Vector3 size, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);

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
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private const string RealTextureRoot = "Assets/Art/Textures/Real";

        private static void CreateMaterials()
        {
            // 结构面：真实扫描的抹灰墙、拼花木地板、抹灰天花板
            wallMaterial = CreateRealMaterial("SanatoriumWall", "Wall_Plaster",
                new Color(0.95f, 0.95f, 0.93f), 0.14f, 0.35f);
            floorMaterial = CreateRealMaterial("SanatoriumFloor", "Floor_Parquet",
                new Color(0.78f, 0.74f, 0.68f), 0.32f, 0.4f);
            ceilingMaterial = CreateRealMaterial("SanatoriumCeiling", "Wall_Plaster",
                new Color(0.86f, 0.86f, 0.84f), 0.08f, 0.32f);

            // 门、门框、踢脚线：深色实木
            trimMaterial = CreateRealMaterial("SanatoriumTrim", "Wood_Dark",
                new Color(0.95f, 0.9f, 0.84f), 0.25f, 0.9f);

            // 扶手、栏杆、把手：真实不锈钢（金属度压低，没有反射探针时才不会发黑）
            metalMaterial = CreateRealMaterial("SanatoriumMetal", "Metal_Silver",
                new Color(0.94f, 0.96f, 0.98f), 0.5f, 1.1f, 0.35f);

            counterMaterial = CreateRealMaterial("SanatoriumCounter", "Wood_Fine",
                new Color(0.96f, 0.93f, 0.88f), 0.22f, 0.9f);
            fabricMaterial = CreateRealMaterial("SanatoriumFabric", "Fabric_Linen",
                new Color(0.9f, 0.91f, 0.89f), 0.05f, 1.4f);

            paperMaterial = CreateMaterial("SanatoriumPaper", new Color(0.86f, 0.84f, 0.74f), 0.05f);
            glassMaterial = CreateMaterial("SanatoriumGlass", new Color(0.72f, 0.8f, 0.82f), 0.7f, 0.1f);
            panelMaterial = CreateMaterial("SanatoriumLampPanel", new Color(0.95f, 0.97f, 0.94f), 0.6f);
            panelMaterial.EnableKeyword("_EMISSION");
            panelMaterial.SetColor("_EmissionColor", new Color(0.55f, 0.58f, 0.55f));

            // 柜子/抽屉：柜体浅木、门板与内格深木
            woodMaterial = CreateRealMaterial("SanatoriumWood", "Wood_Fine",
                new Color(0.97f, 0.9f, 0.8f), 0.22f, 1.3f);
            woodDarkMaterial = CreateRealMaterial("SanatoriumWoodDark", "Wood_Dark",
                new Color(0.82f, 0.74f, 0.66f), 0.18f, 1.3f);

            // 恐怖陈设：干涸血迹、锈蚀金属、裹尸袋帆布
            // 血迹分三层：干涸的暗色外圈、湿润的主血泊、较新的亮红核心
            // 血迹全部做成哑光，避免地面/墙面上的血迹被灯光照出一片反光
            bloodDarkMaterial = CreateMaterial("SanatoriumBloodDark", new Color(0.14f, 0.018f, 0.016f), 0.02f);
            bloodMaterial = CreateMaterial("SanatoriumBlood", new Color(0.3f, 0.035f, 0.03f), 0.05f);
            bloodFreshMaterial = CreateMaterial("SanatoriumBloodFresh", new Color(0.4f, 0.05f, 0.04f), 0.08f);

            // 真实血迹贴片：程序化生成的带 alpha 飞溅图，透明混合贴在血泊/墙面之上
            bloodDecalMaterial = CreateBloodDecalMaterial("SanatoriumBloodDecal",
                "Assets/Art/Textures/Generated/BloodPool.png");
            bloodSplatterMaterial = CreateBloodDecalMaterial("SanatoriumBloodSplatter",
                "Assets/Art/Textures/Generated/BloodSplatter.png");
            rustMaterial = CreateRealMaterial("SanatoriumRust", "Metal_Rusty",
                new Color(1f, 0.97f, 0.92f), 0.22f, 0.8f, 0.2f);
            bodyBagMaterial = CreateRealMaterial("SanatoriumBodyBag", "Leather_Brown",
                new Color(0.55f, 0.55f, 0.55f), 0.3f, 1.2f);
        }

        /// <summary>用 Assets/Art/Textures/Real 下的真实扫描贴图（Albedo）建材质。</summary>
        private static Material CreateRealMaterial(string name, string textureSet,
            Color color, float smoothness, float worldScale, float metallic = 0f)
        {
            string folder = RealTextureRoot + "/" + textureSet;
            return CreateTexturedMaterial(name, folder + "/" + textureSet + "_Albedo.jpg",
                color, smoothness, worldScale, metallic);
        }

        /// <summary>
        /// 血迹贴片材质：Standard 透明混合 + 带 alpha 的血迹贴图，贴在地面/墙面上。
        /// </summary>
        private static Material CreateBloodDecalMaterial(string name, string texturePath)
        {
            Material material = null;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets/Art", "Materials");
                }

                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials/Sanatorium"))
                {
                    AssetDatabase.CreateFolder("Assets/Art/Materials", "Sanatorium");
                }

                string path = "Assets/Art/Materials/Sanatorium/" + name + ".mat";
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard"));
                    material.name = name;
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = Shader.Find("Standard");

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture != null)
                {
                    material.mainTexture = texture;
                }
                else
                {
                    Debug.LogWarning("[PuzzleEscape] 缺少血迹贴图：" + texturePath);
                }

                EditorUtility.SetDirty(material);
            }
#endif

            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.name = name;
            }

            // 血迹是完全哑光的：不反射高光、不反射环境，否则贴在地上会像一层亮塑料
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Glossiness", 0.02f);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");

            // Standard 的透明（Fade）模式：只混合颜色，不写深度
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }
#endif

            return material;
        }

        /// <summary>
        /// 带真实贴图的材质：用世界坐标三向投影平铺，方块尺寸再大也不会被拉伸。
        /// </summary>
        private static Material CreateTexturedMaterial(string name, string texturePath,
            Color color, float smoothness, float worldScale, float metallic = 0f)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets/Art", "Materials");
                }

                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials/Sanatorium"))
                {
                    AssetDatabase.CreateFolder("Assets/Art/Materials", "Sanatorium");
                }

                string path = "Assets/Art/Materials/Sanatorium/" + name + ".mat";
                var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (asset == null)
                {
                    asset = new Material(Shader.Find("Standard"));
                    asset.name = name;
                    AssetDatabase.CreateAsset(asset, path);
                }

                Shader shader = Shader.Find("PuzzleEscape/WorldTriplanar");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                asset.shader = shader;

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture != null)
                {
                    asset.SetTexture("_MainTex", texture);
                }

                asset.SetColor("_Color", color);
                asset.SetFloat("_Glossiness", smoothness);
                asset.SetFloat("_Metallic", metallic);

                if (asset.HasProperty("_Scale"))
                {
                    asset.SetFloat("_Scale", worldScale);
                }

                EditorUtility.SetDirty(asset);
                return asset;
            }
#endif

            Shader runtimeShader = Shader.Find("PuzzleEscape/WorldTriplanar");
            if (runtimeShader == null)
            {
                runtimeShader = Shader.Find("Standard");
            }

            var material = new Material(runtimeShader);
            material.name = name;
            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (material.HasProperty("_Scale"))
            {
                material.SetFloat("_Scale", worldScale);
            }

            return material;
        }

        private static void ConfigureRendering()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.09f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.008f, 0.011f, 0.014f);
            RenderSettings.fogDensity = 0.012f;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness,
            float metallic = 0f)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets/Art", "Materials");
                }

                if (!AssetDatabase.IsValidFolder("Assets/Art/Materials/Sanatorium"))
                {
                    AssetDatabase.CreateFolder("Assets/Art/Materials", "Sanatorium");
                }

                string path = "Assets/Art/Materials/Sanatorium/" + name + ".mat";
                var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (asset == null)
                {
                    asset = new Material(Shader.Find("Standard"));
                    asset.name = name;
                    AssetDatabase.CreateAsset(asset, path);
                }

                asset.shader = Shader.Find("Standard");
                asset.color = color;
                asset.SetFloat("_Glossiness", smoothness);
                asset.SetFloat("_Metallic", metallic);
                EditorUtility.SetDirty(asset);
                return asset;
            }
#endif

            var material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            return material;
        }
    }
}
