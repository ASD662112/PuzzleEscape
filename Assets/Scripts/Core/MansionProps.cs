using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 用多个基础几何体拼装出可辨认的生活物件细节：
    /// 椅子、书桌抽屉、餐桌餐具、沙发靠垫、床品、吊灯、壁灯、钢琴、望远镜、台球桌、锅炉仪表等。
    /// 细节件均不添加碰撞体，主体家具仍由关卡几何提供碰撞。
    /// </summary>
    internal static class MansionProps
    {
        public static GameObject AddChair(Transform parent, Vector3 position, float yaw, bool armchair = false)
        {
#if UNITY_EDITOR
            Vector3 worldPosition = parent != null ? parent.TransformPoint(position) : position;
            GameObject realChair = MansionRealModels.Spawn(parent,
                armchair ? "ArmChair_01" : "dining_chair_02", worldPosition, yaw,
                armchair ? 1.08f : 1.0f, true);
            if (realChair != null)
            {
                return realChair;
            }
#endif

            var root = NewRoot(parent, armchair ? "Armchair" : "Chair", position, yaw);
            Material upholstery = armchair ? MansionLevelBuilder.leatherMaterial : MansionLevelBuilder.fabricMaterial;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float legHeight = armchair ? 0.42f : 0.44f;
                    Cylinder(root.transform, "ChairLeg", new Vector3(sx * 0.19f, 0.22f, sz * 0.19f),
                        new Vector3(0.026f, legHeight * 0.5f, 0.026f), Quaternion.identity,
                        MansionLevelBuilder.woodDarkMaterial);
                    Box(root.transform, "LegCollar", new Vector3(sx * 0.19f, 0.09f, sz * 0.19f),
                        new Vector3(0.065f, 0.035f, 0.065f), MansionLevelBuilder.woodLightMaterial);
                }
            }

            Box(root.transform, "SeatFrame", new Vector3(0f, 0.45f, 0f),
                new Vector3(0.52f, 0.06f, 0.52f), MansionLevelBuilder.woodMaterial);
            Box(root.transform, "SeatApronFront", new Vector3(0f, 0.4f, 0.22f),
                new Vector3(0.46f, 0.08f, 0.03f), MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "SeatApronBack", new Vector3(0f, 0.4f, -0.22f),
                new Vector3(0.46f, 0.08f, 0.03f), MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "SeatApronLeft", new Vector3(-0.22f, 0.4f, 0f),
                new Vector3(0.03f, 0.08f, 0.42f), MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "SeatApronRight", new Vector3(0.22f, 0.4f, 0f),
                new Vector3(0.03f, 0.08f, 0.42f), MansionLevelBuilder.woodLightMaterial);

            Box(root.transform, "SeatCushion", new Vector3(0f, 0.53f, 0.02f),
                new Vector3(0.44f, 0.1f, 0.44f), upholstery);
            Box(root.transform, "CushionPipingFront", new Vector3(0f, 0.575f, 0.245f),
                new Vector3(0.45f, 0.012f, 0.018f), MansionLevelBuilder.porcelainMaterial);
            Box(root.transform, "CushionPipingBack", new Vector3(0f, 0.575f, -0.205f),
                new Vector3(0.45f, 0.012f, 0.018f), MansionLevelBuilder.porcelainMaterial);
            Box(root.transform, "CushionPipingLeft", new Vector3(-0.225f, 0.575f, 0.02f),
                new Vector3(0.018f, 0.012f, 0.44f), MansionLevelBuilder.porcelainMaterial);
            Box(root.transform, "CushionPipingRight", new Vector3(0.225f, 0.575f, 0.02f),
                new Vector3(0.018f, 0.012f, 0.44f), MansionLevelBuilder.porcelainMaterial);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Sphere(root.transform, "CushionButton",
                        new Vector3(sx * 0.11f, 0.585f, 0.02f + sz * 0.11f),
                        new Vector3(0.032f, 0.018f, 0.032f), MansionLevelBuilder.goldMaterial);
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(root.transform, "BackStile", new Vector3(sx * 0.23f, 0.79f, -0.245f),
                    new Vector3(0.045f, 0.62f, 0.045f), MansionLevelBuilder.woodMaterial);
            }
            Cylinder(root.transform, "BackCrestRail", new Vector3(0f, 1.085f, -0.245f),
                new Vector3(0.036f, 0.26f, 0.036f), Quaternion.Euler(0f, 0f, 90f),
                MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "BackBottomRail", new Vector3(0f, 0.51f, -0.245f),
                new Vector3(0.48f, 0.05f, 0.05f), MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "BackPanel", new Vector3(0f, 0.8f, -0.225f),
                new Vector3(0.4f, 0.5f, 0.035f), upholstery);
            Sphere(root.transform, "BackButtonA", new Vector3(-0.1f, 0.89f, -0.202f),
                new Vector3(0.028f, 0.018f, 0.028f), MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "BackButtonB", new Vector3(0.1f, 0.89f, -0.202f),
                new Vector3(0.028f, 0.018f, 0.028f), MansionLevelBuilder.goldMaterial);

            if (armchair)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    Cylinder(root.transform, "ArmRoll", new Vector3(sx * 0.29f, 0.66f, -0.02f),
                        new Vector3(0.055f, 0.24f, 0.055f), Quaternion.Euler(90f, 0f, 0f), upholstery);
                    Box(root.transform, "ArmPost", new Vector3(sx * 0.29f, 0.48f, 0.15f),
                        new Vector3(0.06f, 0.3f, 0.06f), MansionLevelBuilder.woodDarkMaterial);
                }

                Box(root.transform, "BackCushion", new Vector3(0f, 0.88f, -0.18f),
                    new Vector3(0.46f, 0.42f, 0.07f), upholstery);
                Box(root.transform, "ArmchairThrow", new Vector3(-0.27f, 0.76f, -0.03f),
                    new Vector3(0.34f, 0.035f, 0.5f), MansionLevelBuilder.porcelainMaterial);
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.55f, armchair ? 1.1f : 1f, 0.55f);
            collider.center = new Vector3(0f, armchair ? 0.55f : 0.5f, 0f);
            return root;
        }

        public static void AddDeskDetails(Transform parent, Vector3 center, float yaw, float width)
        {
            var root = NewRoot(parent, "DeskDetails", center, yaw);

            Box(root.transform, "DrawerPedestal", new Vector3(0.26f, 0.35f, 0.36f),
                new Vector3(width * 0.5f, 0.62f, 0.5f), MansionLevelBuilder.woodDarkMaterial);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.18f + i * 0.17f;
                Box(root.transform, "DrawerFront", new Vector3(0.26f, y, 0.63f),
                    new Vector3(width * 0.42f, 0.13f, 0.02f), MansionLevelBuilder.woodMaterial);
                Box(root.transform, "DrawerHandle", new Vector3(0.26f, y, 0.66f),
                    new Vector3(0.12f, 0.025f, 0.025f), MansionLevelBuilder.goldMaterial);
            }

            Box(root.transform, "SidePedestal", new Vector3(-width * 0.62f, 0.35f, 0.36f),
                new Vector3(0.35f, 0.62f, 0.5f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "DeskBlotter", new Vector3(-0.05f, 0.87f, 0.02f),
                new Vector3(width * 0.52f, 0.008f, 0.42f), MansionLevelBuilder.paperMaterial);
            Cylinder(root.transform, "Inkwell", new Vector3(-0.3f, 0.9f, 0.17f),
                new Vector3(0.06f, 0.06f, 0.06f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "InkwellLid", new Vector3(-0.3f, 0.94f, 0.17f),
                new Vector3(0.03f, 0.02f, 0.03f), Quaternion.identity, MansionLevelBuilder.goldMaterial);

            Cylinder(root.transform, "DeskLampBase", new Vector3(-width * 0.45f, 0.87f, -0.28f),
                new Vector3(0.12f, 0.03f, 0.12f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "DeskLampPole", new Vector3(-width * 0.45f, 1.13f, -0.28f),
                new Vector3(0.03f, 0.5f, 0.03f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "DeskLampShade", new Vector3(-width * 0.45f, 1.37f, -0.28f),
                new Vector3(0.15f, 0.09f, 0.15f), Quaternion.identity, MansionLevelBuilder.lampShadeMaterial);
            Cylinder(root.transform, "DeskLampShadeTop", new Vector3(-width * 0.45f, 1.46f, -0.28f),
                new Vector3(0.152f, 0.008f, 0.152f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "DeskLampShadeBottom", new Vector3(-width * 0.45f, 1.28f, -0.28f),
                new Vector3(0.155f, 0.008f, 0.155f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "DeskLampBulb", new Vector3(-width * 0.45f, 1.33f, -0.28f),
                new Vector3(0.06f, 0.08f, 0.06f), MansionLevelBuilder.flameMaterial);

            Box(root.transform, "PaperStack", new Vector3(0.32f, 0.9f, 0.1f),
                new Vector3(0.24f, 0.05f, 0.32f), MansionLevelBuilder.paperMaterial);
            Box(root.transform, "Book", new Vector3(-0.18f, 0.91f, -0.25f),
                new Vector3(0.3f, 0.05f, 0.22f), MansionLevelBuilder.bookMaterials[0]);
        }

        public static void AddDiningSet(Transform parent, Vector3 center, float width, float depth)
        {
            AddChandelier(parent, new Vector3(center.x, 2.55f, center.z));

            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            float[] xs = { center.x - width / 3f, center.x, center.x + width / 3f };

            foreach (float x in xs)
            {
                AddChair(parent, new Vector3(x, 0f, center.z + halfDepth + 0.35f), 180f);
                AddChair(parent, new Vector3(x, 0f, center.z - halfDepth - 0.35f), 0f);
            }

            AddChair(parent, new Vector3(center.x - halfWidth - 0.3f, 0f, center.z), 90f);
            AddChair(parent, new Vector3(center.x + halfWidth + 0.3f, 0f, center.z), 270f);

            Box(parent, "TableRunner", new Vector3(center.x, 0.866f, center.z),
                new Vector3(width * 0.72f, 0.006f, depth * 0.32f), MansionLevelBuilder.rugMaterial);

            foreach (float x in xs)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float z = center.z + side * (halfDepth - 0.22f);
                    Cylinder(parent, "DinnerPlate", new Vector3(x, 0.875f, z),
                        new Vector3(0.16f, 0.008f, 0.16f), Quaternion.identity, MansionLevelBuilder.paperMaterial);
                    Cylinder(parent, "PlateRim", new Vector3(x, 0.883f, z),
                        new Vector3(0.1f, 0.004f, 0.1f), Quaternion.identity, MansionLevelBuilder.porcelainMaterial);

                    // 高脚杯：杯脚、杯柄、杯身与酒液
                    Cylinder(parent, "WineGlassFoot", new Vector3(x + 0.24f, 0.865f, z),
                        new Vector3(0.045f, 0.005f, 0.045f), Quaternion.identity, MansionLevelBuilder.glassMaterial);
                    Cylinder(parent, "WineGlassStem", new Vector3(x + 0.24f, 0.91f, z),
                        new Vector3(0.012f, 0.045f, 0.012f), Quaternion.identity, MansionLevelBuilder.glassMaterial);
                    Sphere(parent, "WineGlassBowl", new Vector3(x + 0.24f, 0.975f, z),
                        new Vector3(0.045f, 0.035f, 0.045f), MansionLevelBuilder.glassMaterial);
                    Cylinder(parent, "WineSurface", new Vector3(x + 0.24f, 0.99f, z),
                        new Vector3(0.034f, 0.002f, 0.034f), Quaternion.identity, MansionLevelBuilder.bookMaterials[0]);

                    // 刀叉与餐巾
                    Box(parent, "TableKnife", new Vector3(x + 0.08f, 0.868f, z - 0.02f),
                        new Vector3(0.012f, 0.004f, 0.15f), MansionLevelBuilder.metalMaterial);
                    Box(parent, "TableFork", new Vector3(x - 0.08f, 0.868f, z - 0.02f),
                        new Vector3(0.014f, 0.004f, 0.15f), MansionLevelBuilder.metalMaterial);
                    Box(parent, "Napkin", new Vector3(x - 0.075f, 0.872f, z + 0.03f),
                        new Vector3(0.09f, 0.004f, 0.12f), MansionLevelBuilder.paperMaterial);
                }
            }

            // 三枝烛台：金属底座、灯臂、烛杯与蜡烛
            Cylinder(parent, "CandelabraBase", new Vector3(center.x, 0.875f, center.z),
                new Vector3(0.11f, 0.015f, 0.11f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Rod(parent, "CandelabraStem", new Vector3(center.x, 0.875f, center.z),
                new Vector3(center.x, 1.0f, center.z), 0.022f, MansionLevelBuilder.goldMaterial);
            Vector3[] cups =
            {
                new Vector3(center.x - 0.18f, 0.99f, center.z),
                new Vector3(center.x, 1.02f, center.z),
                new Vector3(center.x + 0.18f, 0.99f, center.z)
            };
            Rod(parent, "CandelabraArmLeft", new Vector3(center.x, 0.995f, center.z), cups[0], 0.014f,
                MansionLevelBuilder.goldMaterial);
            Rod(parent, "CandelabraArmRight", new Vector3(center.x, 0.995f, center.z), cups[2], 0.014f,
                MansionLevelBuilder.goldMaterial);
            foreach (Vector3 cup in cups)
            {
                Cylinder(parent, "CandleCup", cup, new Vector3(0.045f, 0.012f, 0.045f),
                    Quaternion.identity, MansionLevelBuilder.goldMaterial);
                Cylinder(parent, "Candle", cup + new Vector3(0f, 0.09f, 0f),
                    new Vector3(0.022f, 0.08f, 0.022f), Quaternion.identity, MansionLevelBuilder.waxMaterial);
                Sphere(parent, "Flame", cup + new Vector3(0f, 0.19f, 0f),
                    new Vector3(0.038f, 0.058f, 0.038f), MansionLevelBuilder.flameMaterial);
            }
        }

        public static void AddSofaDetails(Transform parent, Vector3 center, float yaw, float width)
        {
            var root = NewRoot(parent, "SofaDetails", new Vector3(center.x, 0f, center.z), yaw);

            for (int i = -1; i <= 1; i++)
            {
                Box(root.transform, "SeatCushion", new Vector3(i * width / 3.4f, 0.66f, 0.06f),
                    new Vector3(width / 3.4f - 0.08f, 0.14f, 0.68f), MansionLevelBuilder.rugMaterial);
                Box(root.transform, "BackCushion", new Vector3(i * width / 3.4f, 0.83f, -0.32f),
                    new Vector3(width / 3.4f - 0.08f, 0.42f, 0.08f), MansionLevelBuilder.rugMaterial);
            }

            Box(root.transform, "ArmLeft", new Vector3(-width * 0.5f + 0.12f, 0.7f, 0f),
                new Vector3(0.14f, 0.25f, 0.78f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "ArmRight", new Vector3(width * 0.5f - 0.12f, 0.7f, 0f),
                new Vector3(0.14f, 0.25f, 0.78f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "SofaBase", new Vector3(0f, 0.55f, 0.02f),
                new Vector3(width * 0.94f, 0.16f, 0.74f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "SofaSkirt", new Vector3(0f, 0.36f, 0.18f),
                new Vector3(width * 0.92f, 0.35f, 0.05f), MansionLevelBuilder.fabricMaterial);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(root.transform, "ThrowPillow", new Vector3(i * width * 0.3f, 0.9f, -0.24f),
                    new Vector3(0.3f, 0.28f, 0.1f), i < 0 ? MansionLevelBuilder.fabricMaterial
                        : MansionLevelBuilder.porcelainMaterial);
                Box(root.transform, "ThrowPillowSeam", new Vector3(i * width * 0.3f, 0.9f, -0.185f),
                    new Vector3(0.3f, 0.012f, 0.012f), MansionLevelBuilder.goldMaterial);
            }

            Box(root.transform, "ThrowBlanket", new Vector3(-width * 0.28f, 0.74f, 0.1f),
                new Vector3(0.42f, 0.035f, 0.58f), MansionLevelBuilder.paperMaterial);

            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    Box(root.transform, "SofaFoot", new Vector3(i * (width * 0.5f - 0.16f), 0.09f,
                        j * 0.32f), new Vector3(0.08f, 0.18f, 0.08f), MansionLevelBuilder.woodDarkMaterial);
                }
            }
        }

        public static void AddBedDetails(Transform parent, Vector3 center, float yaw, float width, float length)
        {
            var root = NewRoot(parent, "BedDetails", center, yaw);

            Box(root.transform, "Blanket", new Vector3(0f, 0.66f, length * 0.22f),
                new Vector3(width * 0.94f, 0.05f, length * 0.58f), MansionLevelBuilder.rugMaterial);
            Box(root.transform, "BlanketFold", new Vector3(0f, 0.685f, length * 0.48f),
                new Vector3(width * 0.95f, 0.035f, 0.16f), MansionLevelBuilder.fabricMaterial);
            Box(root.transform, "ExtraPillow", new Vector3(width * 0.18f, 0.78f, -length * 0.5f + 0.3f),
                new Vector3(0.52f, 0.13f, 0.34f), MansionLevelBuilder.paperMaterial);
            Box(root.transform, "PillowSeam", new Vector3(width * 0.18f, 0.845f, -length * 0.5f + 0.3f),
                new Vector3(0.5f, 0.012f, 0.012f), MansionLevelBuilder.porcelainMaterial);
            Box(root.transform, "SecondPillow", new Vector3(-width * 0.18f, 0.78f, -length * 0.5f + 0.3f),
                new Vector3(0.52f, 0.13f, 0.34f), MansionLevelBuilder.paperMaterial);
            Box(root.transform, "SecondPillowSeam", new Vector3(-width * 0.18f, 0.845f, -length * 0.5f + 0.3f),
                new Vector3(0.5f, 0.012f, 0.012f), MansionLevelBuilder.porcelainMaterial);
            Box(root.transform, "BedThrow", new Vector3(width * 0.14f, 0.705f, length * 0.1f),
                new Vector3(width * 0.55f, 0.035f, 0.28f), MansionLevelBuilder.fabricMaterial);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(root.transform, "BedPost", new Vector3(x * (width * 0.5f - 0.05f), 1.05f,
                        z * (length * 0.5f - 0.05f)), new Vector3(0.08f, 2.1f, 0.08f),
                        MansionLevelBuilder.woodDarkMaterial);
                    Sphere(root.transform, "BedFinial", new Vector3(x * (width * 0.5f - 0.05f), 2.15f,
                        z * (length * 0.5f - 0.05f)), new Vector3(0.12f, 0.12f, 0.12f),
                        MansionLevelBuilder.goldMaterial);
                }
            }
        }

        public static void AddChandelier(Transform parent, Vector3 position)
        {
#if UNITY_EDITOR
            Vector3 worldPosition = parent != null ? parent.TransformPoint(position) : position;
            GameObject realChandelier = MansionRealModels.Spawn(parent, "Chandelier_01",
                worldPosition, 0f, 1.12f, false, 0f, worldPosition.y + 0.45f, false);
            if (realChandelier != null)
            {
                AddLampLight(parent, new Vector3(position.x, position.y - 0.35f, position.z), 4.5f, 0.9f);
                return;
            }
#endif

            var root = NewRoot(parent, "Chandelier", position, 0f);

            // 吊杆、顶盘与中央灯体（吊杆长度按 3 米层高收紧，避免穿过天花板）
            Cylinder(root.transform, "Stem", new Vector3(0f, 0.24f, 0f),
                new Vector3(0.04f, 0.2f, 0.04f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "CeilingRose", new Vector3(0f, 0.44f, 0f),
                new Vector3(0.22f, 0.02f, 0.22f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "Body", new Vector3(0f, 0f, 0f),
                new Vector3(0.19f, 0.13f, 0.19f), MansionLevelBuilder.goldMaterial);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                float radians = angle * Mathf.Deg2Rad;
                float x = Mathf.Sin(radians) * 0.6f;
                float z = Mathf.Cos(radians) * 0.6f;

                // 两段金属灯臂，略带弧度地向外延伸
                Vector3 inner = new Vector3(x * 0.18f, 0.04f, z * 0.18f);
                Vector3 mid = new Vector3(x * 0.62f, 0.02f, z * 0.62f);
                Vector3 outer = new Vector3(x, -0.1f, z);
                Rod(root.transform, "ArmInner", inner, mid, 0.024f, MansionLevelBuilder.goldMaterial);
                Rod(root.transform, "ArmOuter", mid, outer, 0.019f, MansionLevelBuilder.goldMaterial);

                Cylinder(root.transform, "DripPan", outer + new Vector3(0f, -0.015f, 0f),
                    new Vector3(0.078f, 0.012f, 0.078f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
                Cylinder(root.transform, "CandleCup", outer + new Vector3(0f, 0.02f, 0f),
                    new Vector3(0.05f, 0.022f, 0.05f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
                Cylinder(root.transform, "Candle", outer + new Vector3(0f, 0.13f, 0f),
                    new Vector3(0.026f, 0.09f, 0.026f), Quaternion.identity, MansionLevelBuilder.waxMaterial);
                Sphere(root.transform, "Flame", outer + new Vector3(0f, 0.235f, 0f),
                    new Vector3(0.038f, 0.065f, 0.038f), MansionLevelBuilder.flameMaterial);
                Rod(root.transform, "CrystalDrop", outer + new Vector3(0f, -0.02f, 0f),
                    outer + new Vector3(0f, -0.11f, 0f), 0.006f, MansionLevelBuilder.goldMaterial);
                Sphere(root.transform, "Crystal", outer + new Vector3(0f, -0.165f, 0f),
                    new Vector3(0.03f, 0.055f, 0.03f), MansionLevelBuilder.glassMaterial);
            }

            AddLampLight(root.transform, new Vector3(0f, 0.05f, 0f), 5.5f, 1.2f);
        }

        public static void AddWallSconce(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "WallSconce", position, yaw);

            Box(root.transform, "Bracket", new Vector3(0f, 0f, 0.08f),
                new Vector3(0.08f, 0.12f, 0.18f), MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "Cup", new Vector3(0f, 0.06f, 0.16f),
                new Vector3(0.09f, 0.04f, 0.09f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "Candle", new Vector3(0f, 0.16f, 0.16f),
                new Vector3(0.025f, 0.14f, 0.025f), Quaternion.identity, MansionLevelBuilder.waxMaterial);
            Sphere(root.transform, "Flame", new Vector3(0f, 0.28f, 0.16f),
                new Vector3(0.04f, 0.06f, 0.04f), MansionLevelBuilder.flameMaterial);
            AddLampLight(root.transform, new Vector3(0f, 0.22f, 0.16f), 2.8f, 0.5f);
        }

        public static void AddPendantLamp(Transform parent, Vector3 lightPosition)
        {
            var root = NewRoot(parent, "PendantLamp", lightPosition, 0f);

            Cylinder(root.transform, "Canopy", new Vector3(0f, 0.22f, 0f),
                new Vector3(0.08f, 0.025f, 0.08f), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "Cord", new Vector3(0f, 0.12f, 0f),
                new Vector3(0.012f, 0.12f, 0.012f), Quaternion.identity,
                MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "Shade", new Vector3(0f, 0f, 0f),
                new Vector3(0.17f, 0.09f, 0.17f), Quaternion.identity,
                MansionLevelBuilder.lampShadeMaterial);
            Cylinder(root.transform, "ShadeTopRing", new Vector3(0f, 0.09f, 0f),
                new Vector3(0.172f, 0.008f, 0.172f), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "ShadeBottomRing", new Vector3(0f, -0.09f, 0f),
                new Vector3(0.175f, 0.008f, 0.175f), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "Bulb", new Vector3(0f, -0.04f, 0f),
                new Vector3(0.08f, 0.1f, 0.08f), MansionLevelBuilder.flameMaterial);
        }

        public static void AddTableLamp(Transform parent, Vector3 surfacePosition, float scale = 1f)
        {
            var root = NewRoot(parent, "TableLamp", surfacePosition, 0f);

            Cylinder(root.transform, "Base", new Vector3(0f, 0.025f * scale, 0f),
                new Vector3(0.14f * scale, 0.025f * scale, 0.14f * scale), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "BaseRing", new Vector3(0f, 0.06f * scale, 0f),
                new Vector3(0.1f * scale, 0.02f * scale, 0.1f * scale), Quaternion.identity,
                MansionLevelBuilder.copperMaterial);
            Cylinder(root.transform, "Pole", new Vector3(0f, 0.26f * scale, 0f),
                new Vector3(0.025f * scale, 0.2f * scale, 0.025f * scale), Quaternion.identity,
                MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "Shade", new Vector3(0f, 0.47f * scale, 0f),
                new Vector3(0.15f * scale, 0.1f * scale, 0.15f * scale), Quaternion.identity,
                MansionLevelBuilder.lampShadeMaterial);
            Cylinder(root.transform, "ShadeTopRing", new Vector3(0f, 0.57f * scale, 0f),
                new Vector3(0.152f * scale, 0.008f * scale, 0.152f * scale), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "ShadeBottomRing", new Vector3(0f, 0.37f * scale, 0f),
                new Vector3(0.155f * scale, 0.008f * scale, 0.155f * scale), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "Bulb", new Vector3(0f, 0.45f * scale, 0f),
                new Vector3(0.07f * scale, 0.09f * scale, 0.07f * scale),
                MansionLevelBuilder.flameMaterial);
            Sphere(root.transform, "Finial", new Vector3(0f, 0.61f * scale, 0f),
                new Vector3(0.035f * scale, 0.05f * scale, 0.035f * scale),
                MansionLevelBuilder.goldMaterial);
            AddLampLight(root.transform, new Vector3(0f, 0.42f * scale, 0f), 2.6f * scale, 0.38f);
        }

        public static void AddCrateDetails(Transform parent, Vector3 center, Vector3 size)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(parent, "CratePost", center + new Vector3(x * size.x * 0.5f, size.y * 0.5f,
                            z * size.z * 0.5f), new Vector3(0.05f, size.y, 0.05f),
                        MansionLevelBuilder.woodDarkMaterial);
                }
            }

            for (int level = -1; level <= 1; level++)
            {
                float y = center.y + level * size.y * 0.28f;
                Box(parent, "CrateFrontSlat", new Vector3(center.x, y, center.z + size.z * 0.5f + 0.03f),
                    new Vector3(size.x - 0.06f, 0.07f, 0.03f), MansionLevelBuilder.woodLightMaterial);
                Box(parent, "CrateBackSlat", new Vector3(center.x, y, center.z - size.z * 0.5f - 0.03f),
                    new Vector3(size.x - 0.06f, 0.07f, 0.03f), MansionLevelBuilder.woodLightMaterial);
                Box(parent, "CrateSideSlat", new Vector3(center.x + size.x * 0.5f + 0.03f, y, center.z),
                    new Vector3(0.03f, 0.07f, size.z - 0.06f), MansionLevelBuilder.woodLightMaterial);
                Box(parent, "CrateSideSlat", new Vector3(center.x - size.x * 0.5f - 0.03f, y, center.z),
                    new Vector3(0.03f, 0.07f, size.z - 0.06f), MansionLevelBuilder.woodLightMaterial);
            }

            Box(parent, "CrateLid", center + new Vector3(0f, size.y * 0.5f + 0.03f, 0f),
                new Vector3(size.x + 0.06f, 0.05f, size.z + 0.06f), MansionLevelBuilder.woodMaterial);
            Box(parent, "CrateLidPlank", center + new Vector3(0f, size.y * 0.5f + 0.065f, 0f),
                new Vector3(size.x + 0.02f, 0.012f, size.z * 0.32f), MansionLevelBuilder.woodLightMaterial);
            Box(parent, "CrateBrace", center + new Vector3(0f, size.y * 0.2f, size.z * 0.5f + 0.02f),
                new Vector3(size.x * 0.7f, 0.05f, 0.02f), MansionLevelBuilder.goldMaterial,
                Quaternion.Euler(0f, 0f, -38f));
            Box(parent, "CrateLabel", center + new Vector3(-size.x * 0.2f, size.y * 0.1f, size.z * 0.5f + 0.045f),
                new Vector3(size.x * 0.28f, size.y * 0.2f, 0.012f), MansionLevelBuilder.paperMaterial);
        }

        public static void AddBarrelDetails(Transform parent, Vector3 position)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                Cylinder(parent, "BarrelBand", position + new Vector3(i * 0.16f, 0f, 0f),
                    new Vector3(0.48f, 0.012f, 0.48f), Quaternion.Euler(0f, 0f, 90f),
                    MansionLevelBuilder.metalMaterial);
            }

            Cylinder(parent, "BarrelTopRim", position + new Vector3(0f, 0.44f, 0f),
                new Vector3(0.47f, 0.015f, 0.47f), Quaternion.identity, MansionLevelBuilder.copperMaterial);
            Cylinder(parent, "BarrelBottomRim", position + new Vector3(0f, -0.44f, 0f),
                new Vector3(0.47f, 0.015f, 0.47f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(parent, "BarrelLid", position + new Vector3(0f, 0.46f, 0f),
                new Vector3(0.43f, 0.02f, 0.43f), Quaternion.identity, MansionLevelBuilder.woodLightMaterial);
            Sphere(parent, "BarrelBung", position + new Vector3(0f, 0.47f, 0f),
                new Vector3(0.08f, 0.07f, 0.08f), MansionLevelBuilder.goldMaterial);
        }

        public static void AddPlant(Transform parent, Vector3 position, float scale = 1f)
        {
            var root = NewRoot(parent, "PottedPlant", position, 0f);

            Cylinder(root.transform, "Saucer", new Vector3(0f, 0.015f * scale, 0f),
                new Vector3(0.22f * scale, 0.015f * scale, 0.22f * scale), Quaternion.identity,
                MansionLevelBuilder.stoneMaterial);
            Cylinder(root.transform, "Pot", new Vector3(0f, 0.16f * scale, 0f),
                new Vector3(0.2f * scale, 0.15f * scale, 0.2f * scale), Quaternion.identity,
                MansionLevelBuilder.rugMaterial);
            Cylinder(root.transform, "PotRim", new Vector3(0f, 0.3f * scale, 0f),
                new Vector3(0.205f * scale, 0.018f * scale, 0.205f * scale), Quaternion.identity,
                MansionLevelBuilder.woodLightMaterial);
            Cylinder(root.transform, "Soil", new Vector3(0f, 0.3f * scale, 0f),
                new Vector3(0.17f * scale, 0.01f * scale, 0.17f * scale), Quaternion.identity,
                MansionLevelBuilder.woodDarkMaterial);

            for (int i = 0; i < 7; i++)
            {
                float angle = i * 51.4f;
                float radians = angle * Mathf.Deg2Rad;
                float x = Mathf.Sin(radians) * 0.14f * scale;
                float z = Mathf.Cos(radians) * 0.14f * scale;
                float stemHeight = (0.36f + (i % 3) * 0.08f) * scale;
                Cylinder(root.transform, "Stem", new Vector3(x * 0.5f, 0.3f * scale + stemHeight * 0.5f,
                        z * 0.5f), new Vector3(0.014f * scale, stemHeight * 0.5f, 0.014f * scale),
                    Quaternion.Euler(14f + (i % 2) * 6f, angle, 0f), MansionLevelBuilder.bookMaterials[3]);
                Sphere(root.transform, "LeafA", new Vector3(x * 1.3f, 0.3f * scale + stemHeight, z * 1.3f),
                    new Vector3(0.2f * scale, 0.07f * scale, 0.12f * scale),
                    MansionLevelBuilder.bookMaterials[3]);
                Sphere(root.transform, "LeafB", new Vector3(x * 0.65f, 0.34f * scale + stemHeight * 0.82f,
                        z * 0.65f), new Vector3(0.15f * scale, 0.055f * scale, 0.1f * scale),
                    MansionLevelBuilder.bookMaterials[5]);
            }
        }

        public static void AddPianoDetails(Transform parent, Vector3 center, float yaw)
        {
            var root = NewRoot(parent, "PianoDetails", center, yaw);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Cylinder(root.transform, "PianoLeg", new Vector3(x * 0.95f, 0.22f, z * 0.42f),
                        new Vector3(0.1f, 0.44f, 0.1f), Quaternion.identity,
                        MansionLevelBuilder.woodDarkMaterial);
                }
            }

            Box(root.transform, "PedalBoard", new Vector3(0f, 0.06f, -0.6f),
                new Vector3(0.42f, 0.06f, 0.16f), MansionLevelBuilder.woodDarkMaterial);
            for (int i = -1; i <= 1; i++)
            {
                Box(root.transform, "Pedal", new Vector3(i * 0.12f, 0.1f, -0.66f),
                    new Vector3(0.06f, 0.025f, 0.14f), MansionLevelBuilder.goldMaterial);
            }

            Box(root.transform, "MusicStand", new Vector3(0f, 1.16f, -0.38f),
                new Vector3(0.7f, 0.05f, 0.28f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "SheetMusic", new Vector3(0f, 1.22f, -0.38f),
                new Vector3(0.5f, 0.015f, 0.34f), MansionLevelBuilder.paperMaterial);
            Sphere(root.transform, "PianoKnob", new Vector3(0f, 0.86f, -0.62f),
                new Vector3(0.06f, 0.06f, 0.06f), MansionLevelBuilder.goldMaterial);
        }

        public static void AddTelescopeDetails(Transform parent, Vector3 position)
        {
            var root = NewRoot(parent, "TelescopeDetails", position, 0f);

            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f;
                float radians = angle * Mathf.Deg2Rad;
                float x = Mathf.Sin(radians) * 0.32f;
                float z = Mathf.Cos(radians) * 0.32f;
                Cylinder(root.transform, "TripodLeg", new Vector3(x * 0.5f, 0.25f, z * 0.5f),
                    new Vector3(0.04f, 0.5f, 0.04f), Quaternion.Euler(18f, angle, 0f),
                    MansionLevelBuilder.metalMaterial);
            }

            Cylinder(root.transform, "LensRim", new Vector3(0f, 1.55f, -0.45f),
                new Vector3(0.3f, 0.03f, 0.3f), Quaternion.Euler(55f, 0f, 0f),
                MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "Eyepiece", new Vector3(0f, 0.98f, 0.18f),
                new Vector3(0.08f, 0.16f, 0.08f), Quaternion.Euler(55f, 0f, 0f),
                MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "CounterWeight", new Vector3(0f, 0.55f, 0.3f),
                new Vector3(0.14f, 0.14f, 0.14f), MansionLevelBuilder.metalMaterial);
        }

        public static void AddPoolTableDetails(Transform parent, Vector3 center)
        {
            Box(parent, "PoolRailNorth", center + new Vector3(0f, 0.88f, 0.62f),
                new Vector3(2.55f, 0.07f, 0.1f), MansionLevelBuilder.woodDarkMaterial);
            Box(parent, "PoolRailSouth", center + new Vector3(0f, 0.88f, -0.62f),
                new Vector3(2.55f, 0.07f, 0.1f), MansionLevelBuilder.woodDarkMaterial);
            Box(parent, "PoolRailWest", center + new Vector3(-1.2f, 0.88f, 0f),
                new Vector3(0.1f, 0.07f, 1.2f), MansionLevelBuilder.woodDarkMaterial);
            Box(parent, "PoolRailEast", center + new Vector3(1.2f, 0.88f, 0f),
                new Vector3(0.1f, 0.07f, 1.2f), MansionLevelBuilder.woodDarkMaterial);

            foreach (float x in new[] { -1.18f, 0f, 1.18f })
            {
                foreach (float z in new[] { -0.58f, 0.58f })
                {
                    Sphere(parent, "PoolPocket", center + new Vector3(x, 0.9f, z),
                        new Vector3(0.12f, 0.04f, 0.12f), MansionLevelBuilder.woodDarkMaterial);
                }
            }

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(parent, "PoolTableLeg", center + new Vector3(x * 0.95f, 0.25f, z * 0.45f),
                        new Vector3(0.14f, 0.5f, 0.14f), MansionLevelBuilder.woodDarkMaterial);
                }
            }

            Box(parent, "PoolTriangle", center + new Vector3(-0.5f, 0.89f, 0.15f),
                new Vector3(0.24f, 0.015f, 0.24f), MansionLevelBuilder.paperMaterial,
                Quaternion.Euler(0f, 0f, 30f));
            Box(parent, "PoolChalk", center + new Vector3(0.2f, 0.9f, 0.42f),
                new Vector3(0.06f, 0.04f, 0.06f), MansionLevelBuilder.bookMaterials[4]);
        }

        public static void AddKitchenDetails(Transform parent)
        {
            for (int i = 0; i < 4; i++)
            {
                float z = -4.5f - i * 0.9f;
                Box(parent, "KitchenCabinetDoor", new Vector3(-20.74f, 0.42f, z),
                    new Vector3(0.03f, 0.55f, 0.7f), MansionLevelBuilder.woodMaterial);
                Box(parent, "KitchenCabinetHandle", new Vector3(-20.7f, 0.42f, z + 0.18f),
                    new Vector3(0.03f, 0.04f, 0.16f), MansionLevelBuilder.goldMaterial);
            }

            foreach (float x in new[] { -21.4f, -21.0f })
            {
                foreach (float z in new[] { -3.8f, -3.4f })
                {
                    Cylinder(parent, "StoveBurner", new Vector3(x, 0.93f, z),
                        new Vector3(0.12f, 0.015f, 0.12f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
                }
            }

            Box(parent, "OvenDoor", new Vector3(-20.74f, 0.45f, -3.6f),
                new Vector3(0.03f, 0.55f, 0.72f), MansionLevelBuilder.metalMaterial);
            Box(parent, "OvenHandle", new Vector3(-20.7f, 0.7f, -3.6f),
                new Vector3(0.04f, 0.04f, 0.55f), MansionLevelBuilder.goldMaterial);

            Cylinder(parent, "Faucet", new Vector3(-20.85f, 1.05f, -8f),
                new Vector3(0.035f, 0.3f, 0.035f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Box(parent, "FaucetSpout", new Vector3(-21.05f, 1.2f, -8f),
                new Vector3(0.22f, 0.04f, 0.04f), MansionLevelBuilder.metalMaterial);

            Box(parent, "FridgeBody", new Vector3(-16.2f, 0.9f, -4.5f),
                new Vector3(0.8f, 1.8f, 0.8f), MansionLevelBuilder.metalMaterial, true);
            Box(parent, "FridgeDoor", new Vector3(-15.79f, 0.95f, -4.5f),
                new Vector3(0.03f, 1.65f, 0.72f), MansionLevelBuilder.paperMaterial);
            Box(parent, "FridgeHandle", new Vector3(-15.76f, 0.95f, -4.16f),
                new Vector3(0.03f, 0.5f, 0.04f), MansionLevelBuilder.goldMaterial);

            for (int i = 0; i < 3; i++)
            {
                Cylinder(parent, "KitchenJar", new Vector3(-15.2f + i * 0.35f, 1.47f, -8.3f),
                    new Vector3(0.1f, 0.14f, 0.1f), Quaternion.identity, MansionLevelBuilder.paperMaterial);
            }
        }

        public static void AddBoilerDetails(Transform parent)
        {
            foreach (float y in new[] { 1.1f, 1.5f })
            {
                Cylinder(parent, "BoilerGauge", new Vector3(-23.0f, y, -6.2f),
                    new Vector3(0.14f, 0.03f, 0.14f), Quaternion.Euler(90f, 0f, 0f),
                    MansionLevelBuilder.paperMaterial);
            }

            for (int i = 0; i < 2; i++)
            {
                Cylinder(parent, "BoilerValve", new Vector3(-22.9f, 0.9f + i * 0.5f, -5.2f),
                    new Vector3(0.16f, 0.03f, 0.16f), Quaternion.Euler(0f, 0f, 90f),
                    MansionLevelBuilder.goldMaterial);
            }

            Box(parent, "BoilerLadder", new Vector3(-25.5f, 1.3f, -5.0f),
                new Vector3(0.1f, 2.4f, 0.05f), MansionLevelBuilder.metalMaterial);
            for (int i = 0; i < 4; i++)
            {
                Box(parent, "BoilerLadderRung", new Vector3(-25.35f, 0.5f + i * 0.5f, -5.0f),
                    new Vector3(0.5f, 0.04f, 0.06f), MansionLevelBuilder.metalMaterial);
            }

            Sphere(parent, "BoilerWarningLight", new Vector3(-22.9f, 1.8f, -6.2f),
                new Vector3(0.09f, 0.09f, 0.09f), MansionLevelBuilder.flameMaterial);
        }

        public static void AddGrandfatherClock(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "GrandfatherClock", position, yaw);

            Box(root.transform, "ClockBase", new Vector3(0f, 0.35f, 0f),
                new Vector3(0.72f, 0.7f, 0.42f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "ClockBody", new Vector3(0f, 1.55f, 0f),
                new Vector3(0.68f, 1.8f, 0.38f), MansionLevelBuilder.woodMaterial);
            Box(root.transform, "ClockCrown", new Vector3(0f, 2.55f, 0f),
                new Vector3(0.8f, 0.2f, 0.45f), MansionLevelBuilder.woodDarkMaterial);
            Sphere(root.transform, "ClockFinial", new Vector3(0f, 2.75f, 0f),
                new Vector3(0.12f, 0.12f, 0.12f), MansionLevelBuilder.goldMaterial);

            Cylinder(root.transform, "ClockFace", new Vector3(0f, 2.15f, 0.2f),
                new Vector3(0.42f, 0.02f, 0.42f), Quaternion.Euler(90f, 0f, 0f),
                MansionLevelBuilder.clockFaceMaterial);
            Cylinder(root.transform, "PendulumRod", new Vector3(0f, 1.15f, 0.2f),
                new Vector3(0.03f, 0.6f, 0.03f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "PendulumBob", new Vector3(0f, 0.8f, 0.2f),
                new Vector3(0.18f, 0.18f, 0.04f), MansionLevelBuilder.goldMaterial);
            Box(root.transform, "ClockGlass", new Vector3(0f, 1.5f, 0.21f),
                new Vector3(0.5f, 1.1f, 0.01f), MansionLevelBuilder.glassMaterial);
            Box(root.transform, "ClockDoor", new Vector3(0f, 0.35f, 0.21f),
                new Vector3(0.5f, 0.55f, 0.02f), MansionLevelBuilder.woodDarkMaterial);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.8f, 2.9f, 0.5f);
            collider.center = new Vector3(0f, 1.45f, 0f);
        }

        public static void AddGramophone(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "Gramophone", position, yaw);

            Box(root.transform, "Base", new Vector3(0f, 0.15f, 0f),
                new Vector3(0.6f, 0.3f, 0.45f), MansionLevelBuilder.woodDarkMaterial);
            Cylinder(root.transform, "Turntable", new Vector3(0f, 0.32f, 0f),
                new Vector3(0.28f, 0.02f, 0.28f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "Horn", new Vector3(0f, 0.72f, -0.05f),
                new Vector3(0.22f, 0.34f, 0.22f), Quaternion.Euler(20f, 0f, 0f),
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "HornMouth", new Vector3(0f, 0.92f, -0.12f),
                new Vector3(0.32f, 0.06f, 0.32f), Quaternion.Euler(20f, 0f, 0f),
                MansionLevelBuilder.goldMaterial);
            Box(root.transform, "ToneArm", new Vector3(0.25f, 0.4f, 0.08f),
                new Vector3(0.3f, 0.03f, 0.05f), MansionLevelBuilder.metalMaterial);
        }

        public static void AddFireplaceDetails(Transform parent, Vector3 center)
        {
            Box(parent, "FireplaceMantel", center + new Vector3(0f, 1.85f, 0f),
                new Vector3(2.9f, 0.12f, 0.4f), MansionLevelBuilder.woodMaterial);
            Box(parent, "FireplaceHearth", center + new Vector3(0f, 0.04f, -0.42f),
                new Vector3(2.8f, 0.06f, 0.9f), MansionLevelBuilder.metalMaterial);
            Box(parent, "Firebox", center + new Vector3(0f, 0.5f, -0.08f),
                new Vector3(1.5f, 1.1f, 0.12f), MansionLevelBuilder.woodDarkMaterial);
            foreach (int i in new[] { -1, 1 })
            {
                Cylinder(parent, "FireLog", center + new Vector3(i * 0.25f, 0.25f, -0.25f),
                    new Vector3(0.12f, 0.5f, 0.12f), Quaternion.Euler(0f, 0f, 90f),
                    MansionLevelBuilder.woodDarkMaterial);
            }
            Sphere(parent, "Embers", center + new Vector3(0f, 0.15f, -0.28f),
                new Vector3(0.5f, 0.1f, 0.25f), MansionLevelBuilder.flameMaterial);
            Box(parent, "FireplaceStockingA", center + new Vector3(-0.6f, 1.65f, -0.24f),
                new Vector3(0.2f, 0.4f, 0.02f), MansionLevelBuilder.rugMaterial);
            Box(parent, "FireplaceStockingB", center + new Vector3(0.5f, 1.65f, -0.24f),
                new Vector3(0.2f, 0.4f, 0.02f), MansionLevelBuilder.paperMaterial);
        }

        public static void AddCabinetDetails(Transform parent, Vector3 center, float yaw, float width, float height)
        {
            var root = NewRoot(parent, "CabinetDetails", center, yaw);

            Box(root.transform, "CabinetCrown", new Vector3(0f, height * 0.5f + 0.04f, 0f),
                new Vector3(width + 0.08f, 0.08f, 0.6f), MansionLevelBuilder.woodDarkMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(root.transform, "CabinetDoor", new Vector3(side * width * 0.23f, 0f, 0.31f),
                    new Vector3(width * 0.42f, height - 0.2f, 0.03f), MansionLevelBuilder.woodMaterial);
                Box(root.transform, "CabinetHandle", new Vector3(side * width * 0.06f, 0f, 0.34f),
                    new Vector3(0.03f, 0.18f, 0.03f), MansionLevelBuilder.goldMaterial);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Box(root.transform, "CabinetFoot", new Vector3(side * width * 0.4f, -height * 0.5f - 0.04f, 0.2f),
                    new Vector3(0.1f, 0.16f, 0.1f), MansionLevelBuilder.woodDarkMaterial);
            }
        }

        public static void AddRugBorder(Transform parent, Vector3 center, float width, float depth)
        {
            Box(parent, "RugBorderNorth", center + new Vector3(0f, 0.03f, depth * 0.5f - 0.04f),
                new Vector3(width, 0.006f, 0.08f), MansionLevelBuilder.goldMaterial);
            Box(parent, "RugBorderSouth", center + new Vector3(0f, 0.03f, -depth * 0.5f + 0.04f),
                new Vector3(width, 0.006f, 0.08f), MansionLevelBuilder.goldMaterial);
            Box(parent, "RugBorderWest", center + new Vector3(-width * 0.5f + 0.04f, 0.03f, 0f),
                new Vector3(0.08f, 0.006f, depth), MansionLevelBuilder.goldMaterial);
            Box(parent, "RugBorderEast", center + new Vector3(width * 0.5f - 0.04f, 0.03f, 0f),
                new Vector3(0.08f, 0.006f, depth), MansionLevelBuilder.goldMaterial);
        }

        public static GameObject AddFloorLamp(Transform parent, Vector3 position, float scale = 1f)
        {
            var root = NewRoot(parent, "FloorLamp", position, 0f);
            Cylinder(root.transform, "LampBase", new Vector3(0f, 0.03f * scale, 0f),
                new Vector3(0.2f * scale, 0.03f * scale, 0.2f * scale), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "LampBaseRing", new Vector3(0f, 0.075f * scale, 0f),
                new Vector3(0.14f * scale, 0.02f * scale, 0.14f * scale), Quaternion.identity,
                MansionLevelBuilder.copperMaterial);
            Cylinder(root.transform, "LampPole", new Vector3(0f, 0.82f * scale, 0f),
                new Vector3(0.022f * scale, 0.72f * scale, 0.022f * scale), Quaternion.identity,
                MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "LampPoleJoint", new Vector3(0f, 1.08f * scale, 0f),
                new Vector3(0.04f * scale, 0.035f * scale, 0.04f * scale), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "LampShade", new Vector3(0f, 1.58f * scale, 0f),
                new Vector3(0.19f * scale, 0.15f * scale, 0.19f * scale), Quaternion.identity,
                MansionLevelBuilder.lampShadeMaterial);
            Cylinder(root.transform, "LampShadeTopRing", new Vector3(0f, 1.73f * scale, 0f),
                new Vector3(0.192f * scale, 0.01f * scale, 0.192f * scale), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "LampShadeBottomRing", new Vector3(0f, 1.43f * scale, 0f),
                new Vector3(0.196f * scale, 0.01f * scale, 0.196f * scale), Quaternion.identity,
                MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "LampBulb", new Vector3(0f, 1.5f * scale, 0f),
                new Vector3(0.085f * scale, 0.1f * scale, 0.085f * scale),
                MansionLevelBuilder.flameMaterial);
            Sphere(root.transform, "LampFinial", new Vector3(0f, 1.77f * scale, 0f),
                new Vector3(0.04f * scale, 0.055f * scale, 0.04f * scale),
                MansionLevelBuilder.goldMaterial);
            AddLampLight(root.transform, new Vector3(0f, 1.5f * scale, 0f), 4f * scale, 0.6f);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.25f, 1.8f, 0.25f) * scale;
            collider.center = new Vector3(0f, 0.9f * scale, 0f);
            return root;
        }

        public static GameObject AddConsoleTable(Transform parent, Vector3 position, float yaw,
            float width, float depth = 0.45f)
        {
#if UNITY_EDITOR
            Vector3 worldPosition = parent != null ? parent.TransformPoint(position) : position;
            GameObject realConsole = MansionRealModels.Spawn(parent, "ClassicConsole_01",
                worldPosition, yaw, 0.84f, true);
            if (realConsole != null)
            {
                return realConsole;
            }
#endif

            var root = NewRoot(parent, "ConsoleTable", position, yaw);
            Box(root.transform, "ConsoleTop", new Vector3(0f, 0.8f, 0f),
                new Vector3(width, 0.07f, depth), MansionLevelBuilder.woodMaterial);
            Box(root.transform, "ConsoleTopEdge", new Vector3(0f, 0.84f, 0f),
                new Vector3(width + 0.04f, 0.03f, depth + 0.04f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "ConsoleApron", new Vector3(0f, 0.73f, 0f),
                new Vector3(width - 0.1f, 0.12f, depth - 0.08f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "ConsoleDrawer", new Vector3(0f, 0.72f, depth * 0.5f + 0.005f),
                new Vector3(width * 0.34f, 0.1f, 0.02f), MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "ConsoleDrawerHandle", new Vector3(0f, 0.72f, depth * 0.5f + 0.03f),
                new Vector3(0.18f, 0.025f, 0.025f), MansionLevelBuilder.goldMaterial);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Cylinder(root.transform, "ConsoleLeg", new Vector3(x * (width * 0.5f - 0.08f),
                        0.38f, z * (depth * 0.5f - 0.06f)), new Vector3(0.05f, 0.76f, 0.05f),
                        Quaternion.identity, MansionLevelBuilder.woodDarkMaterial);
                    Cylinder(root.transform, "ConsoleLegCollar", new Vector3(x * (width * 0.5f - 0.08f),
                        0.62f, z * (depth * 0.5f - 0.06f)), new Vector3(0.065f, 0.012f, 0.065f),
                        Quaternion.identity, MansionLevelBuilder.goldMaterial);
                    Sphere(root.transform, "ConsoleFoot", new Vector3(x * (width * 0.5f - 0.08f),
                        0.035f, z * (depth * 0.5f - 0.06f)), new Vector3(0.07f, 0.035f, 0.07f),
                        MansionLevelBuilder.goldMaterial);
                }
            }
            Box(root.transform, "ConsoleShelf", new Vector3(0f, 0.22f, 0f),
                new Vector3(width - 0.15f, 0.04f, depth - 0.1f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "ConsoleStretcher", new Vector3(0f, 0.1f, 0f),
                new Vector3(width - 0.12f, 0.04f, 0.04f), MansionLevelBuilder.woodDarkMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, 0.85f, depth);
            collider.center = new Vector3(0f, 0.42f, 0f);
            return root;
        }

        public static GameObject AddSideTable(Transform parent, Vector3 position, float yaw = 0f,
            float width = 0.8f, float depth = 0.8f)
        {
            var root = NewRoot(parent, "SideTable", position, yaw);
            Box(root.transform, "SideTableTop", new Vector3(0f, 0.72f, 0f),
                new Vector3(width, 0.06f, depth), MansionLevelBuilder.woodMaterial);
            Box(root.transform, "SideTableTopEdge", new Vector3(0f, 0.755f, 0f),
                new Vector3(width + 0.03f, 0.025f, depth + 0.03f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "SideTableApronFront", new Vector3(0f, 0.65f, depth * 0.5f - 0.02f),
                new Vector3(width - 0.1f, 0.1f, 0.025f), MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "SideTableApronSide", new Vector3(width * 0.5f - 0.02f, 0.65f, 0f),
                new Vector3(0.025f, 0.1f, depth - 0.1f), MansionLevelBuilder.woodLightMaterial);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Cylinder(root.transform, "SideTableLeg", new Vector3(x * (width * 0.5f - 0.07f),
                        0.34f, z * (depth * 0.5f - 0.07f)), new Vector3(0.045f, 0.68f, 0.045f),
                        Quaternion.identity, MansionLevelBuilder.woodDarkMaterial);
                    Cylinder(root.transform, "SideTableCollar", new Vector3(x * (width * 0.5f - 0.07f),
                        0.56f, z * (depth * 0.5f - 0.07f)), new Vector3(0.06f, 0.012f, 0.06f),
                        Quaternion.identity, MansionLevelBuilder.goldMaterial);
                }
            }
            Box(root.transform, "SideTableShelf", new Vector3(0f, 0.22f, 0f),
                new Vector3(width - 0.12f, 0.035f, depth - 0.12f), MansionLevelBuilder.woodDarkMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, 0.78f, depth);
            collider.center = new Vector3(0f, 0.39f, 0f);
            return root;
        }

        public static GameObject AddDresser(Transform parent, Vector3 position, float yaw,
            float width = 1.4f, float height = 0.95f, bool withMirror = true)
        {
            var root = NewRoot(parent, withMirror ? "DresserWithMirror" : "Dresser", position, yaw);
            Box(root.transform, "DresserBody", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, 0.55f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "DresserPlinth", new Vector3(0f, 0.04f, 0f),
                new Vector3(width + 0.04f, 0.08f, 0.6f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "DresserTop", new Vector3(0f, height + 0.04f, 0f),
                new Vector3(width + 0.08f, 0.07f, 0.62f), MansionLevelBuilder.woodMaterial);
            Box(root.transform, "DresserTopEdge", new Vector3(0f, height + 0.085f, 0f),
                new Vector3(width + 0.1f, 0.025f, 0.64f), MansionLevelBuilder.woodLightMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(root.transform, "DresserSideTrim", new Vector3(side * (width * 0.5f - 0.01f),
                    height * 0.5f, 0.29f), new Vector3(0.03f, height - 0.1f, 0.03f),
                    MansionLevelBuilder.woodLightMaterial);
            }

            for (int row = 0; row < 3; row++)
            {
                float y = 0.18f + row * 0.25f;
                Box(root.transform, "DresserDrawer", new Vector3(0f, y, 0.28f),
                    new Vector3(width - 0.2f, 0.2f, 0.02f), MansionLevelBuilder.woodMaterial);
                Box(root.transform, "DresserDrawerInset", new Vector3(0f, y, 0.295f),
                    new Vector3(width - 0.34f, 0.13f, 0.012f), MansionLevelBuilder.woodLightMaterial);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(root.transform, "DresserHandle", new Vector3(side * 0.16f, y, 0.32f),
                        new Vector3(0.14f, 0.025f, 0.025f), MansionLevelBuilder.goldMaterial);
                }
            }

            for (int i = -1; i <= 1; i += 2)
            {
                Box(root.transform, "DresserFoot", new Vector3(i * (width * 0.38f), 0.06f, 0.2f),
                    new Vector3(0.1f, 0.12f, 0.1f), MansionLevelBuilder.woodDarkMaterial);
            }

            if (withMirror)
            {
                Box(root.transform, "DresserMirrorFrame", new Vector3(0f, height + 0.7f, 0.02f),
                    new Vector3(width * 0.78f, 1.1f, 0.05f), MansionLevelBuilder.goldMaterial);
                Box(root.transform, "DresserMirrorInner", new Vector3(0f, height + 0.7f, 0.06f),
                    new Vector3(width * 0.72f, 1.02f, 0.02f), MansionLevelBuilder.woodDarkMaterial);
                Box(root.transform, "DresserMirrorGlass", new Vector3(0f, height + 0.7f, 0.08f),
                    new Vector3(width * 0.68f, 0.95f, 0.02f), MansionLevelBuilder.glassMaterial);
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 0.6f);
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            return root;
        }

        /// <summary>
        /// 在真实书架模型中补充书本。
        /// 优先摆放 Poly Haven 的二十册百科全书套装（书脊/书页双材质），
        /// 模型缺失时回退到程序化书本。
        /// </summary>
        public static void AddRealShelfBooks(Transform parent, Vector3 position, float yaw, float width, float height,
            float depth, int seed, int levels = 3, int maxSetsPerLevel = 2)
        {
#if UNITY_EDITOR
            if (MansionRealBooks.Available)
            {
                int spawned = MansionRealBooks.AddRuns(parent, position, yaw, width, height, depth, seed,
                    levels, maxSetsPerLevel);
                if (spawned > 0)
                {
                    if (levels < 5)
                    {
                        var vaseRoot = NewRoot(parent, "ShelfVase", position, yaw);
                        float vaseY = height * 0.466f;
                        Cylinder(vaseRoot.transform, "ShelfVaseBody",
                            new Vector3(width * 0.36f, vaseY + 0.11f, 0f),
                            new Vector3(0.055f, 0.11f, 0.055f), Quaternion.identity,
                            MansionLevelBuilder.porcelainMaterial);
                    }

                    return;
                }
            }
#endif

            var root = NewRoot(parent, "ShelfBooks", position, yaw);
            float[] shelfFractions = { 0.053f, 0.189f, 0.325f, 0.466f, 0.621f };

            int levelCount = Mathf.Min(levels, shelfFractions.Length);
            const int booksPerLevel = 6;
            for (int level = 0; level < levelCount; level++)
            {
                float y = height * shelfFractions[level];
                for (int b = 0; b < booksPerLevel; b++)
                {
                    float h = 0.13f + ((b + level + seed) % 3) * 0.03f;
                    float bookWidth = 0.055f + (b % 3) * 0.016f;
                    float x = -width * 0.42f + b * (width * 0.84f / (booksPerLevel - 1));
                    float z = depth * 0.08f + ((b + level) % 2) * depth * 0.06f;
                    Box(root.transform, "ShelfBook", new Vector3(x, y + h * 0.5f, z),
                        new Vector3(bookWidth, h, depth * (0.55f + (b % 3) * 0.08f)),
                        MansionLevelBuilder.bookMaterials[(seed + b + level) % MansionLevelBuilder.bookMaterials.Length],
                        Quaternion.Euler(0f, 0f, (b % 7 == 6) ? 7f : 0f));
                }
            }

            if (levels < shelfFractions.Length)
            {
                float y = height * shelfFractions[levels];
                Cylinder(root.transform, "ShelfVase", new Vector3(width * 0.36f, y + 0.11f, 0f),
                    new Vector3(0.055f, 0.11f, 0.055f), Quaternion.identity,
                    MansionLevelBuilder.porcelainMaterial);
            }
        }

        public static GameObject AddShelfUnit(Transform parent, Vector3 position, float yaw,
            float width, float height, float depth, int bookSeed = 0)
        {
#if UNITY_EDITOR
            if (MansionRealModels.Has("wooden_bookshelf_worn"))
            {
                Vector3 worldPosition = parent != null ? parent.TransformPoint(position) : position;
                GameObject realShelf = MansionRealModels.Spawn(parent, "wooden_bookshelf_worn",
                    worldPosition, yaw, height, true, 0f, null, false,
                    new Vector3(width, height, depth));
                if (realShelf != null)
                {
                    AddRealShelfBooks(parent, position, yaw, width, height, depth, bookSeed, 3, 2);
                    return realShelf;
                }
            }
#endif

            var root = NewRoot(parent, "ShelfUnit", position, yaw);
            Box(root.transform, "ShelfPlinth", new Vector3(0f, 0.04f, 0.02f),
                new Vector3(width + 0.08f, 0.08f, depth + 0.04f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "ShelfBack", new Vector3(0f, height * 0.5f, -depth * 0.5f),
                new Vector3(width, height, 0.04f), MansionLevelBuilder.woodDarkMaterial);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(root.transform, "ShelfSide", new Vector3(i * width * 0.5f, height * 0.5f, 0f),
                    new Vector3(0.06f, height, depth), MansionLevelBuilder.woodDarkMaterial);
                Box(root.transform, "ShelfSideTrim", new Vector3(i * width * 0.5f, height * 0.5f, depth * 0.5f),
                    new Vector3(0.07f, height - 0.06f, 0.025f), MansionLevelBuilder.woodLightMaterial);
            }
            Box(root.transform, "ShelfCrown", new Vector3(0f, height + 0.02f, 0.02f),
                new Vector3(width + 0.12f, 0.07f, depth + 0.06f), MansionLevelBuilder.woodLightMaterial);

            int shelfCount = 4;
            for (int i = 0; i < shelfCount; i++)
            {
                float y = 0.1f + i * (height - 0.2f) / (shelfCount - 1f);
                Box(root.transform, "ShelfBoard", new Vector3(0f, y, 0f),
                    new Vector3(width - 0.08f, 0.05f, depth), MansionLevelBuilder.woodMaterial);
                Box(root.transform, "ShelfBoardLip", new Vector3(0f, y - 0.005f, depth * 0.5f),
                    new Vector3(width - 0.06f, 0.035f, 0.025f), MansionLevelBuilder.woodLightMaterial);

                int bookCount = 8;
                for (int b = 0; b < bookCount; b++)
                {
                    float h = 0.16f + ((b + i) % 4) * 0.045f;
                    float bookWidth = 0.055f + (b % 3) * 0.016f;
                    float x = -width * 0.42f + b * width * 0.105f;
                    float lean = (b % 5 == 4) ? 8f : 0f;
                    Box(root.transform, "ShelfBook", new Vector3(x,
                        y + 0.025f + h * 0.5f, 0.02f + (b % 2) * 0.015f),
                        new Vector3(bookWidth, h, depth * (0.62f + (b % 3) * 0.06f)),
                        MansionLevelBuilder.bookMaterials[(bookSeed + b + i) % MansionLevelBuilder.bookMaterials.Length],
                        Quaternion.Euler(0f, 0f, lean));
                }

                if (i % 2 == 0)
                {
                    Cylinder(root.transform, "ShelfVase", new Vector3(width * 0.37f, y + 0.14f, 0f),
                        new Vector3(0.055f, 0.11f, 0.055f), Quaternion.identity,
                        MansionLevelBuilder.porcelainMaterial);
                }
                else
                {
                    Box(root.transform, "ShelfBookend", new Vector3(-width * 0.4f, y + 0.075f, 0f),
                        new Vector3(0.05f, 0.14f, depth * 0.45f), MansionLevelBuilder.goldMaterial);
                }
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, depth);
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            return root;
        }

        public static GameObject AddBench(Transform parent, Vector3 position, float yaw,
            float width, bool withBack = true)
        {
            var root = NewRoot(parent, "Bench", position, yaw);
            Box(root.transform, "BenchSeat", new Vector3(0f, 0.44f, 0f),
                new Vector3(width, 0.08f, 0.55f), MansionLevelBuilder.woodMaterial);
            Box(root.transform, "BenchCushion", new Vector3(0f, 0.5f, 0f),
                new Vector3(width - 0.1f, 0.06f, 0.48f), MansionLevelBuilder.rugMaterial);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(root.transform, "BenchLeg", new Vector3(x * (width * 0.5f - 0.15f), 0.2f,
                        z * 0.18f), new Vector3(0.09f, 0.4f, 0.09f), MansionLevelBuilder.woodDarkMaterial);
                }
            }

            if (withBack)
            {
                Box(root.transform, "BenchBack", new Vector3(0f, 0.75f, -0.22f),
                    new Vector3(width, 0.5f, 0.06f), MansionLevelBuilder.woodMaterial);
                Box(root.transform, "BenchBackCushion", new Vector3(0f, 0.73f, -0.17f),
                    new Vector3(width - 0.15f, 0.35f, 0.04f), MansionLevelBuilder.rugMaterial);
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, withBack ? 1f : 0.6f, 0.6f);
            collider.center = new Vector3(0f, withBack ? 0.5f : 0.3f, 0f);
            return root;
        }

        public static GameObject AddLuggage(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "Luggage", position, yaw);
            Box(root.transform, "Suitcase", new Vector3(0f, 0.22f, 0f),
                new Vector3(0.7f, 0.42f, 0.38f), MansionLevelBuilder.rugMaterial);
            Box(root.transform, "SuitcaseTrim", new Vector3(0f, 0.22f, 0.2f),
                new Vector3(0.72f, 0.06f, 0.02f), MansionLevelBuilder.goldMaterial);
            Box(root.transform, "SuitcaseHandle", new Vector3(0f, 0.48f, 0f),
                new Vector3(0.24f, 0.18f, 0.05f), MansionLevelBuilder.woodDarkMaterial);
            for (int i = -1; i <= 1; i += 2)
            {
                Sphere(root.transform, "SuitcaseWheel", new Vector3(i * 0.25f, 0.06f, 0.12f),
                    new Vector3(0.1f, 0.1f, 0.1f), MansionLevelBuilder.woodDarkMaterial);
            }
            return root;
        }

        public static GameObject AddVase(Transform parent, Vector3 surfacePosition, int seed = 0)
        {
#if UNITY_EDITOR
            // 每隔一个花瓶换成真实的青花瓷模型，避免整屋重复同一件摆件。
            if (seed % 2 == 0 && MansionRealModels.Has("antique_ceramic_vase_01"))
            {
                Vector3 worldPosition = parent != null ? parent.TransformPoint(surfacePosition) : surfacePosition;
                GameObject realVase = MansionRealModels.Spawn(parent, "antique_ceramic_vase_01",
                    worldPosition, seed * 37f, 0.5f, false, 0f, null, false);
                if (realVase != null)
                {
                    return realVase;
                }
            }
#endif

            var root = NewRoot(parent, "FlowerVase", surfacePosition, 0f);
            Cylinder(root.transform, "VaseFoot", new Vector3(0f, 0.025f, 0f),
                new Vector3(0.1f, 0.025f, 0.1f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Cylinder(root.transform, "VaseBody", new Vector3(0f, 0.16f, 0f),
                new Vector3(0.16f, 0.14f, 0.16f), Quaternion.identity, MansionLevelBuilder.porcelainMaterial);
            Cylinder(root.transform, "VaseShoulder", new Vector3(0f, 0.27f, 0f),
                new Vector3(0.11f, 0.05f, 0.11f), Quaternion.identity, MansionLevelBuilder.porcelainMaterial);
            Cylinder(root.transform, "VaseNeck", new Vector3(0f, 0.33f, 0f),
                new Vector3(0.075f, 0.06f, 0.075f), Quaternion.identity, MansionLevelBuilder.porcelainMaterial);
            Cylinder(root.transform, "VaseRim", new Vector3(0f, 0.38f, 0f),
                new Vector3(0.085f, 0.015f, 0.085f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f + seed * 17f;
                float radians = angle * Mathf.Deg2Rad;
                Cylinder(root.transform, "VaseStem", new Vector3(Mathf.Sin(radians) * 0.09f, 0.54f,
                    Mathf.Cos(radians) * 0.09f), new Vector3(0.012f, 0.22f, 0.012f),
                    Quaternion.Euler(10f + (i % 2) * 6f, angle, 0f), MansionLevelBuilder.bookMaterials[3]);
                Sphere(root.transform, "VaseLeaf", new Vector3(Mathf.Sin(radians) * 0.18f, 0.6f,
                    Mathf.Cos(radians) * 0.18f), new Vector3(0.12f, 0.04f, 0.07f),
                    MansionLevelBuilder.bookMaterials[3]);
                Sphere(root.transform, "VaseFlower", new Vector3(Mathf.Sin(radians) * 0.16f, 0.74f,
                    Mathf.Cos(radians) * 0.16f), new Vector3(0.09f, 0.08f, 0.09f),
                    MansionLevelBuilder.bookMaterials[(seed + i) % MansionLevelBuilder.bookMaterials.Length]);
            }
            return root;
        }

        public static GameObject AddDisplayCase(Transform parent, Vector3 position, float yaw, float width = 1.2f)
        {
            var root = NewRoot(parent, "DisplayCase", position, yaw);
            Box(root.transform, "DisplayBase", new Vector3(0f, 0.35f, 0f),
                new Vector3(width, 0.7f, 0.55f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "DisplayBaseTrim", new Vector3(0f, 0.71f, 0f),
                new Vector3(width + 0.06f, 0.05f, 0.6f), MansionLevelBuilder.woodLightMaterial);
            Box(root.transform, "DisplayGlass", new Vector3(0f, 1.1f, 0f),
                new Vector3(width - 0.1f, 0.8f, 0.45f), MansionLevelBuilder.glassMaterial);
            Box(root.transform, "DisplayFrameTop", new Vector3(0f, 1.5f, 0f),
                new Vector3(width, 0.06f, 0.5f), MansionLevelBuilder.goldMaterial);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(root.transform, "DisplayFramePost", new Vector3(x * (width * 0.5f - 0.04f), 1.1f,
                        z * 0.2f), new Vector3(0.04f, 0.8f, 0.04f), MansionLevelBuilder.woodLightMaterial);
                }
            }
            Box(root.transform, "DisplayArtifactStand", new Vector3(0f, 0.8f, 0f),
                new Vector3(0.3f, 0.08f, 0.3f), MansionLevelBuilder.marbleMaterial);
            Sphere(root.transform, "DisplayArtifact", new Vector3(0f, 0.95f, 0f),
                new Vector3(0.22f, 0.22f, 0.22f), MansionLevelBuilder.goldMaterial);
            Box(root.transform, "DisplayLabel", new Vector3(0f, 1.1f, 0.24f),
                new Vector3(0.4f, 0.12f, 0.02f), MansionLevelBuilder.paperMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, 1.5f, 0.6f);
            collider.center = new Vector3(0f, 0.75f, 0f);
            return root;
        }

        public static void AddCurtains(Transform parent, Vector3 center, float yaw, float width)
        {
            var root = NewRoot(parent, "Curtains", center, yaw);
            Cylinder(root.transform, "CurtainRod", new Vector3(0f, 0.9f, 0f),
                new Vector3(0.03f, width * 0.55f, 0.03f), Quaternion.Euler(0f, 0f, 90f),
                MansionLevelBuilder.goldMaterial);
            Box(root.transform, "CurtainValance", new Vector3(0f, 0.82f, 0f),
                new Vector3(width * 1.05f, 0.18f, 0.04f), MansionLevelBuilder.fabricMaterial);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(root.transform, "CurtainPanel", new Vector3(i * width * 0.28f, 0.25f, 0f),
                    new Vector3(width * 0.18f, 1.8f, 0.05f), MansionLevelBuilder.rugMaterial);
                for (int fold = 0; fold < 2; fold++)
                {
                    Box(root.transform, "CurtainFold", new Vector3(i * width * 0.28f + (fold - 0.5f) * width * 0.06f,
                        0.25f, 0.03f), new Vector3(width * 0.045f, 1.78f, 0.035f),
                        MansionLevelBuilder.fabricMaterial);
                }
                Cylinder(root.transform, "CurtainTieback", new Vector3(i * width * 0.28f, 0.6f, 0.04f),
                    new Vector3(0.04f, 0.1f, 0.04f), Quaternion.Euler(0f, 0f, 90f),
                    MansionLevelBuilder.goldMaterial);
            }
        }

        public static void AddMirror(Transform parent, Vector3 position, float yaw, float width, float height)
        {
            var root = NewRoot(parent, "Mirror", position, yaw);
            Box(root.transform, "MirrorFrame", new Vector3(0f, 0f, 0f),
                new Vector3(width, height, 0.04f), MansionLevelBuilder.goldMaterial);
            Box(root.transform, "MirrorGlass", new Vector3(0f, 0f, -0.02f),
                new Vector3(width - 0.08f, height - 0.08f, 0.02f), MansionLevelBuilder.glassMaterial);
        }

        public static void AddPainting(Transform parent, Vector3 position, float yaw, float width,
            float height, int colorIndex)
        {
            var root = NewRoot(parent, "WallPainting", position, yaw);
            Box(root.transform, "PaintingFrame", new Vector3(0f, 0f, 0f),
                new Vector3(width, height, 0.04f), MansionLevelBuilder.goldMaterial);
            Box(root.transform, "PaintingFrameInner", new Vector3(0f, 0f, -0.012f),
                new Vector3(width - 0.06f, height - 0.06f, 0.03f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "PaintingMat", new Vector3(0f, 0f, -0.025f),
                new Vector3(width - 0.14f, height - 0.14f, 0.012f), MansionLevelBuilder.paperMaterial);
            Material canvasMaterial = MansionLevelBuilder.paintingMaterials != null &&
                                      MansionLevelBuilder.paintingMaterials.Length > 0
                ? MansionLevelBuilder.paintingMaterials[colorIndex % MansionLevelBuilder.paintingMaterials.Length]
                : MansionLevelBuilder.bookMaterials[colorIndex % MansionLevelBuilder.bookMaterials.Length];
            Box(root.transform, "PaintingCanvas", new Vector3(0f, 0f, -0.034f),
                new Vector3(width - 0.2f, height - 0.2f, 0.012f), canvasMaterial);
            Box(root.transform, "PaintingPlaque", new Vector3(0f, -height * 0.5f - 0.08f, 0f),
                new Vector3(width * 0.35f, 0.08f, 0.02f), MansionLevelBuilder.paperMaterial);
            Box(root.transform, "PaintingPlaqueFrame", new Vector3(0f, -height * 0.5f - 0.08f, -0.01f),
                new Vector3(width * 0.37f, 0.1f, 0.012f), MansionLevelBuilder.goldMaterial);
        }

        public static void AddGlobe(Transform parent, Vector3 surfacePosition)
        {
            var root = NewRoot(parent, "Globe", surfacePosition, 0f);
            Cylinder(root.transform, "GlobeBase", new Vector3(0f, 0.04f, 0f),
                new Vector3(0.16f, 0.04f, 0.16f), Quaternion.identity, MansionLevelBuilder.woodDarkMaterial);
            Cylinder(root.transform, "GlobeStand", new Vector3(0f, 0.2f, 0f),
                new Vector3(0.03f, 0.24f, 0.03f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Sphere(root.transform, "GlobeSphere", new Vector3(0f, 0.5f, 0f),
                new Vector3(0.34f, 0.34f, 0.34f), MansionLevelBuilder.bookMaterials[4]);
            Cylinder(root.transform, "GlobeRing", new Vector3(0f, 0.5f, 0f),
                new Vector3(0.4f, 0.015f, 0.4f), Quaternion.Euler(20f, 0f, 0f),
                MansionLevelBuilder.goldMaterial);
        }

        public static void AddBookStack(Transform parent, Vector3 surfacePosition, int count = 4)
        {
            var root = NewRoot(parent, "BookStack", surfacePosition, 0f);
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                float h = 0.07f + (i % 2) * 0.03f;
                Box(root.transform, "StackBook", new Vector3(i * 0.02f, y + h * 0.5f, 0f),
                    new Vector3(0.32f, h, 0.24f),
                    MansionLevelBuilder.bookMaterials[i % MansionLevelBuilder.bookMaterials.Length],
                    new Quaternion(0f, 0f, 0f, 1f));
                y += h;
            }
        }

        public static GameObject AddBarStool(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "BarStool", position, yaw);
            Cylinder(root.transform, "StoolSeat", new Vector3(0f, 0.7f, 0f),
                new Vector3(0.22f, 0.05f, 0.22f), Quaternion.identity, MansionLevelBuilder.rugMaterial);
            Box(root.transform, "StoolCushion", new Vector3(0f, 0.74f, 0f),
                new Vector3(0.4f, 0.06f, 0.4f), MansionLevelBuilder.rugMaterial);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(root.transform, "StoolLeg", new Vector3(x * 0.16f, 0.36f, z * 0.16f),
                        new Vector3(0.05f, 0.72f, 0.05f), MansionLevelBuilder.woodDarkMaterial);
                }
            }
            Box(root.transform, "StoolFootrest", new Vector3(0f, 0.24f, 0f),
                new Vector3(0.35f, 0.04f, 0.35f), MansionLevelBuilder.goldMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.45f, 0.8f, 0.45f);
            collider.center = new Vector3(0f, 0.4f, 0f);
            return root;
        }

        public static void AddHangingPlant(Transform parent, Vector3 ceilingPosition, float scale = 1f)
        {
            var root = NewRoot(parent, "HangingPlant", ceilingPosition, 0f);
            Cylinder(root.transform, "Hanger", new Vector3(0f, -0.2f * scale, 0f),
                new Vector3(0.02f, 0.2f * scale, 0.02f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "HangingPot", new Vector3(0f, -0.42f * scale, 0f),
                new Vector3(0.2f * scale, 0.14f * scale, 0.2f * scale), Quaternion.identity,
                MansionLevelBuilder.rugMaterial);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                float radians = angle * Mathf.Deg2Rad;
                Sphere(root.transform, "HangingLeaf", new Vector3(Mathf.Sin(radians) * 0.24f,
                    -0.65f * scale - (i % 2) * 0.08f, Mathf.Cos(radians) * 0.24f),
                    new Vector3(0.16f, 0.24f, 0.16f) * scale, MansionLevelBuilder.bookMaterials[3]);
            }
        }

        public static GameObject AddGenerator(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "Generator", position, yaw);
            Box(root.transform, "GeneratorBase", new Vector3(0f, 0.1f, 0f),
                new Vector3(2.6f, 0.2f, 1.2f), MansionLevelBuilder.metalMaterial);
            Box(root.transform, "GeneratorBody", new Vector3(0f, 0.9f, 0f),
                new Vector3(2.4f, 1.5f, 1f), MansionLevelBuilder.metalMaterial);
            Box(root.transform, "GeneratorTop", new Vector3(0f, 1.7f, 0f),
                new Vector3(2.5f, 0.1f, 1.1f), MansionLevelBuilder.woodDarkMaterial);
            for (int i = -1; i <= 1; i++)
            {
                Box(root.transform, "GeneratorVent", new Vector3(i * 0.7f, 0.8f, 0.51f),
                    new Vector3(0.4f, 0.5f, 0.04f), MansionLevelBuilder.woodDarkMaterial);
            }

            Box(root.transform, "GeneratorPanel", new Vector3(0f, 1.1f, 0.54f),
                new Vector3(0.55f, 0.4f, 0.04f), MansionLevelBuilder.paperMaterial);
            Sphere(root.transform, "GeneratorLight", new Vector3(0f, 1.22f, 0.57f),
                new Vector3(0.08f, 0.08f, 0.08f), MansionLevelBuilder.flameMaterial);
            Cylinder(root.transform, "GeneratorStack", new Vector3(0.95f, 1.9f, -0.2f),
                new Vector3(0.1f, 0.5f, 0.1f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "GeneratorPipe", new Vector3(-0.8f, 1.45f, 0.45f),
                new Vector3(0.05f, 0.8f, 0.05f), Quaternion.Euler(90f, 0f, 0f), MansionLevelBuilder.metalMaterial);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.6f, 1.9f, 1.2f);
            collider.center = new Vector3(0f, 0.95f, 0f);
            return root;
        }

        public static GameObject AddToolCart(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "ToolCart", position, yaw);
            for (int level = 0; level < 3; level++)
            {
                Box(root.transform, "CartShelf", new Vector3(0f, 0.16f + level * 0.3f, 0f),
                    new Vector3(1f, 0.05f, 0.6f), MansionLevelBuilder.metalMaterial);
            }
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(root.transform, "CartPost", new Vector3(x * 0.45f, 0.45f, z * 0.25f),
                        new Vector3(0.04f, 0.9f, 0.04f), MansionLevelBuilder.metalMaterial);
                    Sphere(root.transform, "CartWheel", new Vector3(x * 0.45f, 0.06f, z * 0.25f),
                        new Vector3(0.12f, 0.12f, 0.12f), MansionLevelBuilder.woodDarkMaterial);
                }
            }
            Cylinder(root.transform, "CartHandle", new Vector3(0f, 0.95f, -0.35f),
                new Vector3(0.035f, 0.45f, 0.035f), Quaternion.identity, MansionLevelBuilder.goldMaterial);
            Box(root.transform, "CartToolbox", new Vector3(0f, 0.52f, 0f),
                new Vector3(0.5f, 0.2f, 0.28f), MansionLevelBuilder.woodDarkMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.1f, 1f, 0.7f);
            collider.center = new Vector3(0f, 0.5f, 0f);
            return root;
        }

        public static GameObject AddFumeHood(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "FumeHood", position, yaw);
            Box(root.transform, "FumeBase", new Vector3(0f, 0.45f, 0f),
                new Vector3(1.8f, 0.9f, 0.8f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "FumeTop", new Vector3(0f, 1.58f, 0f),
                new Vector3(1.9f, 0.12f, 0.85f), MansionLevelBuilder.metalMaterial);
            Box(root.transform, "FumeGlass", new Vector3(0f, 1.1f, 0f),
                new Vector3(1.7f, 0.85f, 0.7f), MansionLevelBuilder.glassMaterial);
            Box(root.transform, "FumeBack", new Vector3(0f, 1.1f, -0.36f),
                new Vector3(1.7f, 0.85f, 0.04f), MansionLevelBuilder.woodDarkMaterial);
            Box(root.transform, "FumePanel", new Vector3(0f, 0.75f, 0.42f),
                new Vector3(0.6f, 0.2f, 0.03f), MansionLevelBuilder.paperMaterial);
            Sphere(root.transform, "FumeIndicator", new Vector3(0.22f, 0.8f, 0.45f),
                new Vector3(0.06f, 0.06f, 0.06f), MansionLevelBuilder.flameMaterial);
            Cylinder(root.transform, "FumeVent", new Vector3(0f, 1.9f, -0.2f),
                new Vector3(0.14f, 0.25f, 0.14f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.9f, 1.7f, 0.85f);
            collider.center = new Vector3(0f, 0.85f, 0f);
            return root;
        }

        public static GameObject AddSculpture(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "Sculpture", position, yaw);
            Cylinder(root.transform, "SculpturePedestal", new Vector3(0f, 0.55f, 0f),
                new Vector3(0.45f, 0.55f, 0.45f), Quaternion.identity, MansionLevelBuilder.paperMaterial);
            Box(root.transform, "SculptureTorso", new Vector3(0f, 1.3f, 0f),
                new Vector3(0.45f, 0.7f, 0.3f), MansionLevelBuilder.paperMaterial);
            Sphere(root.transform, "SculptureHead", new Vector3(0f, 1.85f, 0f),
                new Vector3(0.32f, 0.38f, 0.32f), MansionLevelBuilder.paperMaterial);
            Box(root.transform, "SculptureArmL", new Vector3(-0.32f, 1.35f, 0f),
                new Vector3(0.14f, 0.55f, 0.18f), MansionLevelBuilder.paperMaterial);
            Box(root.transform, "SculptureArmR", new Vector3(0.32f, 1.35f, 0f),
                new Vector3(0.14f, 0.55f, 0.18f), MansionLevelBuilder.paperMaterial);
            Box(root.transform, "SculpturePlaque", new Vector3(0f, 0.25f, 0.46f),
                new Vector3(0.5f, 0.2f, 0.02f), MansionLevelBuilder.goldMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.9f, 2.1f, 0.7f);
            collider.center = new Vector3(0f, 1.05f, 0f);
            return root;
        }

        public static void AddWallSign(Transform parent, Vector3 position, float yaw, int colorIndex)
        {
            var root = NewRoot(parent, "WallSign", position, yaw);
            Box(root.transform, "SignBoard", new Vector3(0f, 0f, 0f),
                new Vector3(0.8f, 0.55f, 0.04f), MansionLevelBuilder.goldMaterial);
            Box(root.transform, "SignPaper", new Vector3(0f, 0f, -0.025f),
                new Vector3(0.68f, 0.43f, 0.02f),
                MansionLevelBuilder.bookMaterials[colorIndex % MansionLevelBuilder.bookMaterials.Length]);
            Box(root.transform, "SignStripe", new Vector3(0f, -0.16f, -0.04f),
                new Vector3(0.55f, 0.05f, 0.02f), MansionLevelBuilder.paperMaterial);
        }

        public static void AddFireExtinguisher(Transform parent, Vector3 position)
        {
            var root = NewRoot(parent, "FireExtinguisher", position, 0f);
            Cylinder(root.transform, "ExtinguisherBody", new Vector3(0f, 0.35f, 0f),
                new Vector3(0.14f, 0.32f, 0.14f), Quaternion.identity, MansionLevelBuilder.rugMaterial);
            Cylinder(root.transform, "ExtinguisherCap", new Vector3(0f, 0.68f, 0f),
                new Vector3(0.07f, 0.08f, 0.07f), Quaternion.identity, MansionLevelBuilder.metalMaterial);
            Cylinder(root.transform, "ExtinguisherHose", new Vector3(0.14f, 0.5f, 0f),
                new Vector3(0.02f, 0.22f, 0.02f), Quaternion.Euler(0f, 0f, 18f),
                MansionLevelBuilder.woodDarkMaterial);
        }

        public static void AddCoalPile(Transform parent, Vector3 position, int count = 10)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 137.5f;
                float radius = (i % 3) * 0.18f;
                float radians = angle * Mathf.Deg2Rad;
                Sphere(parent, "Coal", position + new Vector3(Mathf.Sin(radians) * radius,
                    0.1f + (i % 2) * 0.06f, Mathf.Cos(radians) * radius),
                    new Vector3(0.22f, 0.16f, 0.22f), MansionLevelBuilder.woodDarkMaterial);
            }
        }

        public static void AddDryingRack(Transform parent, Vector3 position, float yaw)
        {
            var root = NewRoot(parent, "DryingRack", position, yaw);
            Box(root.transform, "RackTop", new Vector3(0f, 1.7f, 0f),
                new Vector3(1.2f, 0.08f, 0.08f), MansionLevelBuilder.woodDarkMaterial);
            for (int x = -1; x <= 1; x += 2)
            {
                Box(root.transform, "RackPost", new Vector3(x * 0.55f, 0.85f, 0f),
                    new Vector3(0.06f, 1.7f, 0.06f), MansionLevelBuilder.woodDarkMaterial);
            }
            for (int i = 0; i < 4; i++)
            {
                Box(root.transform, "HangingCloth", new Vector3(-0.4f + i * 0.27f, 1.3f, 0f),
                    new Vector3(0.2f, 0.65f, 0.04f),
                    i % 2 == 0 ? MansionLevelBuilder.rugMaterial : MansionLevelBuilder.paperMaterial);
            }
        }

        private static void Rod(Transform parent, string objectName, Vector3 from, Vector3 to, float radius,
            Material material)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.0001f)
            {
                return;
            }

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            Cylinder(parent, objectName, (from + to) * 0.5f, new Vector3(radius, length * 0.5f, radius),
                rotation, material);
        }

        private static void AddLampLight(Transform parent, Vector3 localPosition, float range, float intensity)
        {
            var lightGo = new GameObject("LampLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = localPosition;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.56f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private static GameObject NewRoot(Transform parent, string objectName, Vector3 position, float yaw)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return root;
        }

        private static GameObject Box(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale,
            Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            RemoveCollider(go, keepCollider);
            return go;
        }

        private static GameObject Box(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale,
            Material material, Quaternion localRotation)
        {
            var go = Box(parent, objectName, localPosition, localScale, material);
            go.transform.localRotation = localRotation;
            return go;
        }

        private static GameObject Cylinder(Transform parent, string objectName, Vector3 localPosition,
            Vector3 localScale, Quaternion localRotation, Material material)
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

            RemoveCollider(go, false);
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
