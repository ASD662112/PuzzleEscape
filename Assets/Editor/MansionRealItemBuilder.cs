using System.Collections.Generic;
using PuzzleEscape.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 把 Poly Haven 真实模型 / PBR 贴图烘焙成可放入背包的拾取物预制体，
    /// 输出到 Assets/Resources/Items，运行时由 ItemSpawnPoint 通过 Resources.Load 读取。
    /// 没有现成 CC0 扫描模型的小物件（钥匙、保险丝、徽记、齿轮、硬币、黑胶、布片、蜡烛、灯芯）
    /// 使用程序化精细网格 + 真实扫描 PBR 材质（黄铜 / 银 / 锈铁 / 蜡 / 亚麻 / 唱片沟槽）。
    /// </summary>
    public static class MansionRealItemBuilder
    {
        private const string ModelRoot = "Assets/Art/Models";
        private const string RealTextureRoot = "Assets/Art/Textures/Real";
        private const string ItemTextureRoot = "Assets/Art/Textures/Items";
        private const string MaterialRoot = "Assets/Art/Materials/Items";
        private const string MeshRoot = "Assets/Art/Meshes/Items";
        private const string PrefabRoot = "Assets/Resources/Items";

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
        private static readonly List<string> BuiltItems = new List<string>();

        [MenuItem("PuzzleEscape/生成物品真实模型")]
        public static void BuildAll()
        {
            MaterialCache.Clear();
            MeshCache.Clear();
            BuiltItems.Clear();

            EnsureFolder("Assets/Art/Materials", "Items");
            EnsureFolder("Assets/Art", "Meshes");
            EnsureFolder("Assets/Art/Meshes", "Items");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Items");

            BuildPocketWatch();
            BuildCompass();
            BuildSilverTeacup();
            BuildFlashlight();
            BuildSilverCandlestick();
            BuildWineBottle();
            BuildEmptyBottle();
            BuildOldDiary();
            BuildOldPhoto();
            BuildMusicBox();
            BuildPorcelainVase();
            BuildBrassKey();
            BuildBasementKey();
            BuildFuse();
            BuildSeals();
            BuildElevatorGear();
            BuildOldCoin();
            BuildCandle();
            BuildVinylRecord();
            BuildClothScrap();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 新建/更新的网格资产在本帧内还没有完成导入，强制重导入预制体，
            // 让 MeshFilter 的 GUID 引用真正解析到网格，否则运行时会显示为空白。
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[PuzzleEscape] 物品真实模型生成完成：" + BuiltItems.Count + " 件。\n" +
                      string.Join("\n", BuiltItems));
        }

        // ---------------------------------------------------------------- 真实模型物品

        private static void BuildPocketWatch()
        {
            GameObject source = LoadModel("vintage_pocket_watch");
            if (source == null)
            {
                return;
            }

            Material body = GetMaterial("Item_PocketWatch", ModelRoot + "/vintage_pocket_watch", "main",
                new Color(1f, 0.93f, 0.8f), false, 1f, null, Color.black, 1f);
            body.SetFloat("_Metallic", 0.55f);
            Material glass = GetMaterial("Item_PocketWatchGlass", ModelRoot + "/vintage_pocket_watch", "main",
                new Color(0.82f, 0.9f, 1f), true, 0.22f, null, Color.black, 1f);

            AssignSlots(source, (slot, index) => slot.Contains("glass") ? glass : body);
            BuildPrefab(ItemIds.PocketWatch, source, new Vector3(0.075f, 0.095f, 0.022f), true, Vector3.zero);
        }

        private static void BuildCompass()
        {
            GameObject source = LoadModel("seadogs_compass");
            if (source == null)
            {
                return;
            }

            Material material = GetMaterial("Item_Compass", ModelRoot + "/seadogs_compass", "main",
                new Color(0.92f, 0.86f, 0.72f), false, 1f, null, Color.black, 1f);
            AssignSlots(source, (slot, index) => material);
            BuildPrefab(ItemIds.BrassCompass, source, new Vector3(0.105f, 0.105f, 0.160f), true, Vector3.zero);
        }

        private static void BuildSilverTeacup()
        {
            GameObject model = LoadModel("tea_set_01");
            if (model == null)
            {
                return;
            }

            GameObject source = ExtractChild(model, "tea_set_01_cup_small_01");
            if (source == null)
            {
                return;
            }

            Material silver = GetPlainMetal("Item_TeaSilver", new Color(0.86f, 0.88f, 0.92f), 0.45f, 0.8f);
            AssignSlots(source, (slot, index) => silver);
            BuildPrefab(ItemIds.SilverTeacup, source, new Vector3(0.125f, 0.095f, 0.11f), true, Vector3.zero);
        }

        private static void BuildFlashlight()
        {
            GameObject source = LoadModel("signal_flashlight");
            if (source == null)
            {
                return;
            }

            Material body = GetMaterial("Item_FlashlightBody", ModelRoot + "/signal_flashlight", "main",
                new Color(0.78f, 0.82f, 0.72f), false, 1f, null, Color.black, 0.6f);
            body.SetFloat("_Metallic", 0.55f);

            Material glass = GetMaterial("Item_FlashlightGlass", ModelRoot + "/signal_flashlight", "main",
                new Color(0.9f, 0.95f, 1f), true, 0.3f, null, Color.black, 1f);

            Material lens = GetMaterial("Item_FlashlightLens", ModelRoot + "/signal_flashlight", "main",
                new Color(1f, 0.96f, 0.82f), false, 1f, null, Color.black, 0.4f);
            if (lens.mainTexture != null)
            {
                lens.SetTexture("_EmissionMap", lens.mainTexture);
            }

            lens.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.58f) * 1.4f);
            lens.EnableKeyword("_EMISSION");

            AssignSlots(source, (slot, index) =>
            {
                if (slot.Contains("glass"))
                {
                    return glass;
                }

                // 槽名 signal_flashlight_light 才是灯泡；主体 signal_flashlight 里也含 "light"，
                // 因此必须匹配 "_light" 后缀而不是 "light"。
                return slot.Contains("_light") ? lens : body;
            });

            // 手电筒在场景中处于关闭状态，不再像提灯那样自带常亮点光。
            BuildPrefab(ItemIds.Lantern, source, new Vector3(0.09f, 0.17f, 0.06f), true, Vector3.zero);
        }

        private static void BuildSilverCandlestick()
        {
            GameObject model = LoadModel("brass_candleholders");
            if (model == null)
            {
                return;
            }

            GameObject source = ExtractChild(model, "brass_candleholder_01");
            if (source == null)
            {
                return;
            }

            Material silver = GetMaterial("Item_SilverCandlestick", ModelRoot + "/brass_candleholders", "01",
                new Color(0.95f, 0.96f, 1f), false, 1f, null, Color.black, 1f);
            silver.SetFloat("_Metallic", 0.4f);
            Material hidden = GetHiddenMaterial();

            AssignSlots(source, (slot, index) => slot.Contains("flame") ? hidden : silver);
            BuildPrefab(ItemIds.SilverCandlestick, source, new Vector3(0.085f, 0.21f, 0.085f), true,
                Vector3.zero);
        }

        private static void BuildWineBottle()
        {
            GameObject model = LoadModel("wine_bottles_01");
            if (model == null)
            {
                return;
            }

            GameObject source = ExtractChild(model, "wine_bottles_01_bordeaux");
            if (source == null)
            {
                return;
            }

            Material glass = GetMaterial("Item_WineGlassBordeaux", ModelRoot + "/wine_bottles_01", "main",
                new Color(0.68f, 0.32f, 0.2f), false, 1f, null, Color.black, 1f);
            Material cork = GetMaterial("Item_WineCork", ModelRoot + "/wine_bottles_01", "main",
                new Color(0.48f, 0.33f, 0.2f), false, 1f, null, Color.black, 0.25f);
            Material wine = GetMaterial("Item_WineRed", ModelRoot + "/wine_bottles_01", "main",
                new Color(0.42f, 0.06f, 0.08f), false, 1f, null, Color.black, 1f);
            Material label = GetMaterial("Item_WineLabelFtero", ModelRoot + "/wine_bottles_01",
                "label_ftero_vinea_02", Color.white, false, 1f, null, Color.black, 0.35f);

            AssignSlots(source, (slot, index) =>
            {
                if (slot.Contains("glass"))
                {
                    return glass;
                }
                if (slot.Contains("seal") || slot.Contains("cork"))
                {
                    return cork;
                }
                // 注意材质名统一带 wine_bottles_01_ 前缀，不能用 Contains("wine")。
                if (slot.Contains("_wine_"))
                {
                    return wine;
                }
                return label;
            });
            BuildPrefab(ItemIds.WineBottle, source, new Vector3(0.1f, 0.315f, 0.1f), true, Vector3.zero);
        }

        private static void BuildEmptyBottle()
        {
            GameObject model = LoadModel("wine_bottles_01");
            if (model == null)
            {
                return;
            }

            GameObject source = ExtractChild(model, "wine_bottles_01_alsace");
            if (source == null)
            {
                return;
            }

            Material glass = GetMaterial("Item_WineGlassAlsace", ModelRoot + "/wine_bottles_01", "main",
                new Color(0.82f, 0.9f, 0.78f), false, 1f, null, Color.black, 1f);
            Material cork = GetMaterial("Item_WineCork", ModelRoot + "/wine_bottles_01", "main",
                new Color(0.48f, 0.33f, 0.2f), false, 1f, null, Color.black, 0.25f);
            Material hidden = GetHiddenMaterial();
            Material label = GetMaterial("Item_WineLabelClearwater", ModelRoot + "/wine_bottles_01",
                "label_clearwater_crystals_01", Color.white, false, 1f, null, Color.black, 0.35f);

            AssignSlots(source, (slot, index) =>
            {
                if (slot.Contains("glass"))
                {
                    return glass;
                }
                if (slot.Contains("seal") || slot.Contains("cork"))
                {
                    return cork;
                }
                // empty bottle 的 clearwater 标签同样带 wine_bottles_01_ 前缀。
                if (slot.Contains("_wine_"))
                {
                    return hidden;
                }
                return label;
            });
            BuildPrefab(ItemIds.EmptyBottle, source, new Vector3(0.095f, 0.3f, 0.095f), true, Vector3.zero);
        }

        private static void BuildOldDiary()
        {
            GameObject model = LoadModel("binder_notebook");
            if (model == null)
            {
                return;
            }

            GameObject source = ExtractChild(model, "binder_notebook_closed");
            if (source == null)
            {
                return;
            }

            Material leather = GetMaterial("Item_OldDiary", ModelRoot + "/binder_notebook", "main",
                new Color(0.9f, 0.82f, 0.72f), false, 1f, null, Color.black, 1f);
            AssignSlots(source, (slot, index) => leather);
            BuildPrefab(ItemIds.OldDiary, source, new Vector3(0.19f, 0.19f, 0.06f), true,
                new Vector3(72f, 0f, 0f));
        }

        private static void BuildOldPhoto()
        {
            GameObject model = LoadModel("postcard_set_01");
            if (model == null)
            {
                return;
            }

            GameObject source = ExtractChild(model, "postcard_01");
            if (source == null)
            {
                return;
            }

            // 明信片是单面片状网格，复制一份翻转 180°，保证旋转时背面也可见。
            MeshRenderer renderer = source.GetComponentInChildren<MeshRenderer>(true);
            if (renderer != null)
            {
                GameObject back = Object.Instantiate(renderer.gameObject, renderer.transform.parent);
                back.name = "PostcardBack";
                back.transform.localPosition = renderer.transform.localPosition + new Vector3(0f, 0f, -0.0008f);
                back.transform.localRotation = renderer.transform.localRotation * Quaternion.Euler(0f, 180f, 0f);
                back.transform.localScale = renderer.transform.localScale;
            }

            Material paper = GetMaterial("Item_OldPhoto", ModelRoot + "/postcard_set_01", "main",
                new Color(0.95f, 0.9f, 0.8f), false, 1f, null, Color.black, 0.35f);
            AssignSlots(source, (slot, index) => paper);
            BuildPrefab(ItemIds.OldPhoto, source, new Vector3(0.15f, 0.108f, 0.02f), true, Vector3.zero);
        }

        private static void BuildMusicBox()
        {
            GameObject model = LoadModel("cigarette_case");
            if (model == null)
            {
                return;
            }

            // 模型里同时包含闭合与打开两种烟盒；闭合盒体是名为 cigarette_case 的子网格。
            GameObject source = ExtractChild(model, "cigarette_case");
            if (source == null)
            {
                return;
            }

            Material metal = GetMaterial("Item_MusicBox", ModelRoot + "/cigarette_case", "main",
                new Color(0.92f, 0.86f, 0.74f), false, 1f, null, Color.black, 1f);
            AssignSlots(source, (slot, index) => metal);
            BuildPrefab(ItemIds.MusicBox, source, new Vector3(0.18f, 0.06f, 0.18f), true, Vector3.zero);
        }

        private static void BuildPorcelainVase()
        {
            GameObject source = LoadModel("ceramic_vase_02");
            if (source == null)
            {
                return;
            }

            Material porcelain = GetMaterial("Item_PorcelainVase", ModelRoot + "/ceramic_vase_02",
                "ceramic_vase_02", Color.white, false, 1f, null, Color.black, 1f);
            AssignSlots(source, (slot, index) => porcelain);
            BuildPrefab(ItemIds.PorcelainVase, source, new Vector3(0.14f, 0.19f, 0.14f), true, Vector3.zero);
        }

        // ---------------------------------------------------------------- 程序化精细网格物品

        private static void BuildBrassKey()
        {
            Material brass = GetMaterial("Item_KeyBrass", ModelRoot + "/Lantern_01", "brass",
                new Color(0.96f, 0.8f, 0.45f), false, 1f, null, Color.black, 1f);
            GameObject source = CreateKeySource(brass);
            BuildPrefab(ItemIds.BrassKey, source, new Vector3(0.11f, 0.06f, 0.11f), true,
                new Vector3(65f, 0f, 0f));
        }

        private static void BuildBasementKey()
        {
            Material rusty = GetMaterial("Item_KeyRusty", RealTextureRoot + "/Metal_Rusty", "Metal_Rusty",
                new Color(0.72f, 0.5f, 0.34f), false, 1f, null, Color.black, 0.6f);
            GameObject source = CreateKeySource(rusty);
            BuildPrefab(ItemIds.BasementKey, source, new Vector3(0.115f, 0.065f, 0.115f), true,
                new Vector3(65f, 0f, 0f));
        }

        private static void BuildFuse()
        {
            Material ceramic = GetMaterial("Item_FuseCeramic", ModelRoot + "/brass_candleholders", "candles",
                new Color(0.9f, 0.87f, 0.8f), false, 1f, null, Color.black, 0.55f);
            Material silver = GetPlainMetal("Item_PureSilver", new Color(0.84f, 0.86f, 0.9f), 0.45f, 0.75f);
            GameObject source = CreateFuseSource(ceramic, silver);
            BuildPrefab(ItemIds.Fuse, source, new Vector3(0.075f, 0.032f, 0.032f), true, Vector3.zero);
        }

        private static void BuildSeals()
        {
            Material body = GetMaterial("Item_SealBody", ModelRoot + "/Lantern_01", "brass",
                new Color(0.86f, 0.8f, 0.68f), false, 1f, null, Color.black, 1f);

            BuildSeal(ItemIds.SealSun, "Seal_Sun", new Color(1f, 0.72f, 0.25f), body);
            BuildSeal(ItemIds.SealMoon, "Seal_Moon", new Color(0.42f, 0.7f, 1f), body);
            BuildSeal(ItemIds.SealStar, "Seal_Star", new Color(0.75f, 0.5f, 1f), body);
            BuildSeal(ItemIds.SealShadow, "Seal_Shadow", new Color(0.55f, 0.35f, 0.75f), body);
        }

        private static void BuildSeal(string itemId, string textureFolder, Color tint, Material body)
        {
            Texture2D emission = LoadPrepared(ItemTextureRoot + "/" + textureFolder, textureFolder,
                "Emission", false, false);
            Material emblem = GetMaterial("Item_" + textureFolder, ItemTextureRoot + "/" + textureFolder,
                textureFolder, tint, false, 1f, emission,
                new Color(tint.r * 2f, tint.g * 2f, tint.b * 2f, 1f), 1f);
            emblem.SetFloat("_Metallic", 0.35f);

            GameObject source = CreateSealSource(body, emblem);
            BuildPrefab(itemId, source, new Vector3(0.13f, 0.13f, 0.13f), true, new Vector3(72f, 0f, 0f));
        }

        private static void BuildElevatorGear()
        {
            Material brass = GetMaterial("Item_GearBrass", ModelRoot + "/Lantern_01", "brass",
                new Color(0.84f, 0.68f, 0.4f), false, 1f, null, Color.black, 1f);
            GameObject source = CreateGearSource(brass);
            BuildPrefab(ItemIds.ElevatorGear, source, new Vector3(0.19f, 0.19f, 0.19f), true,
                new Vector3(72f, 0f, 0f));
        }

        private static void BuildOldCoin()
        {
            Material coin = GetMaterial("Item_OldCoin", ItemTextureRoot + "/Coin_Silver", "Coin_Silver",
                Color.white, false, 1f, null, Color.black, 1f);
            coin.SetFloat("_Metallic", 0.35f);
            GameObject source = CreateCoinSource(coin);
            BuildPrefab(ItemIds.OldCoin, source, new Vector3(0.072f, 0.072f, 0.072f), true,
                new Vector3(72f, 0f, 0f));
        }

        private static void BuildCandle()
        {
            Material wax = GetMaterial("Item_CandleWax", ModelRoot + "/brass_candleholders", "candles",
                new Color(0.88f, 0.82f, 0.7f), false, 1f, null, Color.black, 0.55f);
            Material wick = GetMaterial("Item_Wick", RealTextureRoot + "/Metal_Rusty", "Metal_Rusty",
                new Color(0.18f, 0.13f, 0.09f), false, 1f, null, Color.black, 0.35f);
            GameObject source = CreateCandleSource(wax, wick);
            BuildPrefab(ItemIds.Candle, source, new Vector3(0.055f, 0.12f, 0.055f), true, Vector3.zero);
        }

        private static void BuildVinylRecord()
        {
            Material vinyl = GetMaterial("Item_Vinyl", ItemTextureRoot + "/Vinyl", "Vinyl",
                new Color(0.05f, 0.05f, 0.055f), false, 1f, null, Color.black, 1f);
            Material label = GetMaterial("Item_VinylLabel", ItemTextureRoot + "/Vinyl", "VinylLabel",
                Color.white, false, 1f, null, Color.black, 0.3f);

            GameObject source = CreateVinylSource(vinyl, label);
            BuildPrefab(ItemIds.VinylRecord, source, new Vector3(0.23f, 0.23f, 0.23f), true,
                new Vector3(72f, 0f, 0f));
        }

        private static void BuildClothScrap()
        {
            Material linen = GetMaterial("Item_Linen", RealTextureRoot + "/Fabric_Linen", "Fabric_Linen",
                new Color(0.78f, 0.72f, 0.64f), false, 1f, null, Color.black, 1f);
            GameObject source = CreateClothSource(linen);
            BuildPrefab(ItemIds.ClothScrap, source, new Vector3(0.2f, 0.2f, 0.2f), true,
                new Vector3(60f, 0f, 0f));
        }

        // ---------------------------------------------------------------- 自定义网格

        private static GameObject CreateKeySource(Material material)
        {
            var source = new GameObject("Source");

            // 钥匙平放于 XZ 平面：环形匙柄 + 长轴 + 齿。
            GameObject bow = AddMeshObject("KeyBow",
                CreateTorusMesh("ItemKeyBow", 0.021f, 0.0045f, 28, 8),
                new[] { material }, source.transform);
            bow.transform.localPosition = new Vector3(-0.043f, 0f, 0f);

            GameObject shaft = AddPrimitive(PrimitiveType.Cylinder, source.transform,
                new Vector3(0.006f, 0f, 0f), new Vector3(0.011f, 0.03f, 0.011f),
                new Vector3(0f, 0f, 90f), material);
            shaft.name = "KeyShaft";

            GameObject collar = AddPrimitive(PrimitiveType.Cylinder, source.transform,
                new Vector3(-0.018f, 0f, 0f), new Vector3(0.018f, 0.005f, 0.018f),
                new Vector3(0f, 0f, 90f), material);
            collar.name = "KeyCollar";

            GameObject bitA = AddPrimitive(PrimitiveType.Cube, source.transform,
                new Vector3(0.036f, 0f, 0.007f), new Vector3(0.02f, 0.006f, 0.006f),
                Vector3.zero, material);
            bitA.name = "KeyBitA";

            GameObject bitB = AddPrimitive(PrimitiveType.Cube, source.transform,
                new Vector3(0.046f, 0f, -0.006f), new Vector3(0.012f, 0.006f, 0.008f),
                Vector3.zero, material);
            bitB.name = "KeyBitB";

            GameObject tip = AddPrimitive(PrimitiveType.Cylinder, source.transform,
                new Vector3(0.052f, 0f, 0f), new Vector3(0.011f, 0.002f, 0.011f),
                new Vector3(0f, 0f, 90f), material);
            tip.name = "KeyTip";

            return source;
        }

        private static GameObject CreateFuseSource(Material ceramic, Material metal)
        {
            var source = new GameObject("Source");

            GameObject body = AddPrimitive(PrimitiveType.Cylinder, source.transform, Vector3.zero,
                new Vector3(0.024f, 0.025f, 0.024f), new Vector3(0f, 0f, 90f), ceramic);
            body.name = "FuseBody";

            for (int i = -1; i <= 1; i += 2)
            {
                GameObject cap = AddPrimitive(PrimitiveType.Cylinder, source.transform,
                    new Vector3(i * 0.026f, 0f, 0f), new Vector3(0.027f, 0.004f, 0.027f),
                    new Vector3(0f, 0f, 90f), metal);
                cap.name = "FuseCap";
            }

            return source;
        }

        private static GameObject CreateSealSource(Material body, Material emblem)
        {
            var source = new GameObject("Source");

            GameObject disc = AddMeshObject("SealDisc",
                CreateCylinderMesh("ItemSealDisc", 0.056f, 0.011f, 40),
                new[] { body, emblem, emblem }, source.transform);
            disc.name = "SealDisc";

            GameObject rim = AddMeshObject("SealRim",
                CreateTorusMesh("ItemSealRim", 0.052f, 0.0032f, 32, 8),
                new[] { body }, source.transform, new Vector3(0f, 0.005f, 0f));
            rim.name = "SealRim";

            return source;
        }

        private static GameObject CreateGearSource(Material material)
        {
            var source = new GameObject("Source");

            GameObject disc = AddMeshObject("GearDisc",
                CreateGearMesh("ItemGearDisc", 18, 0.085f, 0.068f, 0.014f),
                new[] { material, material, material }, source.transform);
            disc.name = "GearDisc";

            GameObject hub = AddPrimitive(PrimitiveType.Cylinder, source.transform, Vector3.zero,
                new Vector3(0.042f, 0.013f, 0.042f), Vector3.zero, material);
            hub.name = "GearHub";

            GameObject axle = AddPrimitive(PrimitiveType.Cylinder, source.transform, Vector3.zero,
                new Vector3(0.018f, 0.020f, 0.018f), Vector3.zero, material);
            axle.name = "GearAxle";

            return source;
        }

        private static GameObject CreateCoinSource(Material coin)
        {
            var source = new GameObject("Source");
            AddMeshObject("CoinDisc", CreateCylinderMesh("ItemCoinDisc", 0.029f, 0.0038f, 48),
                new[] { coin, coin, coin }, source.transform);
            return source;
        }

        private static GameObject CreateCandleSource(Material wax, Material wick)
        {
            var source = new GameObject("Source");

            GameObject body = AddMeshObject("CandleBody",
                CreateCylinderMesh("ItemCandleBody", 0.019f, 0.086f, 24),
                new[] { wax, wax, wax }, source.transform);
            body.name = "CandleBody";

            GameObject melted = AddPrimitive(PrimitiveType.Sphere, source.transform,
                new Vector3(0f, 0.043f, 0f), new Vector3(0.038f, 0.014f, 0.038f),
                Vector3.zero, wax);
            melted.name = "CandleMeltedWax";

            GameObject wickObject = AddPrimitive(PrimitiveType.Cylinder, source.transform,
                new Vector3(0.002f, 0.056f, 0f), new Vector3(0.0024f, 0.007f, 0.0024f),
                new Vector3(0f, 0f, 6f), wick);
            wickObject.name = "CandleWick";

            return source;
        }

        private static GameObject CreateVinylSource(Material vinyl, Material label)
        {
            var source = new GameObject("Source");

            AddMeshObject("VinylDisc", CreateCylinderMesh("ItemVinylDisc", 0.102f, 0.0042f, 64),
                new[] { vinyl, vinyl, vinyl }, source.transform);

            Mesh labelMesh = CreateCylinderMesh("ItemVinylLabelDisc", 0.038f, 0.001f, 32);
            for (int i = -1; i <= 1; i += 2)
            {
                AddMeshObject("VinylLabel", labelMesh,
                    new[] { label, label, label }, source.transform,
                    new Vector3(0f, i * 0.0026f, 0f));
            }

            return source;
        }

        private static GameObject CreateClothSource(Material linen)
        {
            var source = new GameObject("Source");
            AddMeshObject("Cloth", CreateClothMesh("ItemCloth", 0.16f, 0.14f, 14, 12, 0.012f),
                new[] { linen }, source.transform);
            return source;
        }

        // ---------------------------------------------------------------- 网格生成

        private static Mesh CreateCylinderMesh(string assetName, float radius, float height, int segments)
        {
            var mesh = new Mesh();
            mesh.name = assetName;
            mesh.subMeshCount = 3;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var side = new List<int>();
            float half = height * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, -half, z));
                uvs.Add(new Vector2((float)i / segments, 0f));
                vertices.Add(new Vector3(x, half, z));
                uvs.Add(new Vector2((float)i / segments, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int current = i * 2;
                int next = ((i + 1) % segments) * 2;
                // 顺时针/逆时针方向需让侧面法线朝外。
                side.Add(current);
                side.Add(current + 1);
                side.Add(next);
                side.Add(next);
                side.Add(current + 1);
                side.Add(next + 1);
            }

            var top = new List<int>();
            var bottom = new List<int>();
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, half, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -half, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                int topIndex = vertices.Count;
                vertices.Add(new Vector3(x, half, z));
                uvs.Add(new Vector2(x / (radius * 2f) + 0.5f, z / (radius * 2f) + 0.5f));
                int bottomIndex = vertices.Count;
                vertices.Add(new Vector3(x, -half, z));
                uvs.Add(new Vector2(x / (radius * 2f) + 0.5f, z / (radius * 2f) + 0.5f));

                int nextTop = (i == segments - 1) ? topCenter + 2 : topIndex + 2;
                int nextBottom = nextTop + 1;

                top.Add(topCenter);
                top.Add(topIndex);
                top.Add(nextTop);

                bottom.Add(bottomCenter);
                bottom.Add(nextBottom);
                bottom.Add(bottomIndex);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(side, 0, true);
            mesh.SetTriangles(top, 1, true);
            mesh.SetTriangles(bottom, 2, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return SaveMeshAsset(assetName, mesh);
        }

        private static Mesh CreateTorusMesh(string assetName, float majorRadius, float minorRadius,
            int majorSegments, int minorSegments)
        {
            var mesh = new Mesh();
            mesh.name = assetName;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < majorSegments; i++)
            {
                float u = i * Mathf.PI * 2f / majorSegments;
                Vector3 center = new Vector3(Mathf.Cos(u) * majorRadius, 0f, Mathf.Sin(u) * majorRadius);
                Vector3 outward = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));

                for (int j = 0; j < minorSegments; j++)
                {
                    float v = j * Mathf.PI * 2f / minorSegments;
                    Vector3 point = center + outward * (Mathf.Cos(v) * minorRadius) +
                                    Vector3.up * (Mathf.Sin(v) * minorRadius);
                    vertices.Add(point);
                    uvs.Add(new Vector2((float)i / majorSegments, (float)j / minorSegments));
                }
            }

            for (int i = 0; i < majorSegments; i++)
            {
                int nextI = (i + 1) % majorSegments;
                for (int j = 0; j < minorSegments; j++)
                {
                    int nextJ = (j + 1) % minorSegments;
                    int a = i * minorSegments + j;
                    int b = nextI * minorSegments + j;
                    int c = nextI * minorSegments + nextJ;
                    int d = i * minorSegments + nextJ;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(a);
                    triangles.Add(d);
                    triangles.Add(c);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return SaveMeshAsset(assetName, mesh);
        }

        private static Mesh CreateGearMesh(string assetName, int teeth, float outerRadius, float rootRadius,
            float height)
        {
            var mesh = new Mesh();
            mesh.name = assetName;
            mesh.subMeshCount = 3;

            int profileCount = teeth * 4;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var side = new List<int>();

            float half = height * 0.5f;
            for (int i = 0; i < profileCount; i++)
            {
                float angle = i * Mathf.PI * 2f / profileCount;
                float radius = (i % 4 < 2) ? outerRadius : rootRadius;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, -half, z));
                uvs.Add(new Vector2((float)i / profileCount, 0f));
                vertices.Add(new Vector3(x, half, z));
                uvs.Add(new Vector2((float)i / profileCount, 1f));
            }

            for (int i = 0; i < profileCount; i++)
            {
                int current = i * 2;
                int next = ((i + 1) % profileCount) * 2;
                side.Add(current);
                side.Add(current + 1);
                side.Add(next);
                side.Add(next);
                side.Add(current + 1);
                side.Add(next + 1);
            }

            var top = new List<int>();
            var bottom = new List<int>();
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, half, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -half, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            int firstTop = vertices.Count;
            for (int i = 0; i < profileCount; i++)
            {
                float angle = i * Mathf.PI * 2f / profileCount;
                float radius = (i % 4 < 2) ? outerRadius : rootRadius;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, half, z));
                uvs.Add(new Vector2(x / (outerRadius * 2f) + 0.5f, z / (outerRadius * 2f) + 0.5f));
                vertices.Add(new Vector3(x, -half, z));
                uvs.Add(new Vector2(x / (outerRadius * 2f) + 0.5f, z / (outerRadius * 2f) + 0.5f));
            }

            for (int i = 0; i < profileCount; i++)
            {
                int topIndex = firstTop + i * 2;
                int bottomIndex = topIndex + 1;
                int nextTop = firstTop + ((i + 1) % profileCount) * 2;
                int nextBottom = nextTop + 1;

                top.Add(topCenter);
                top.Add(topIndex);
                top.Add(nextTop);

                bottom.Add(bottomCenter);
                bottom.Add(nextBottom);
                bottom.Add(bottomIndex);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(side, 0, true);
            mesh.SetTriangles(top, 1, true);
            mesh.SetTriangles(bottom, 2, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return SaveMeshAsset(assetName, mesh);
        }

        private static Mesh CreateClothMesh(string assetName, float width, float depth, int segmentsX,
            int segmentsZ, float waveAmplitude)
        {
            var mesh = new Mesh();
            mesh.name = assetName;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int z = 0; z <= segmentsZ; z++)
            {
                float v = (float)z / segmentsZ;
                for (int x = 0; x <= segmentsX; x++)
                {
                    float u = (float)x / segmentsX;
                    float px = (u - 0.5f) * width;
                    float pz = (v - 0.5f) * depth;
                    float py = Mathf.Sin(u * Mathf.PI * 2f) * Mathf.Sin(v * Mathf.PI) * waveAmplitude +
                               Mathf.Sin(v * Mathf.PI * 3f) * waveAmplitude * 0.35f;
                    vertices.Add(new Vector3(px, py, pz));
                    uvs.Add(new Vector2(u * 2f, v * 2f));
                }
            }

            int stride = segmentsX + 1;
            for (int z = 0; z < segmentsZ; z++)
            {
                for (int x = 0; x < segmentsX; x++)
                {
                    int a = z * stride + x;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            // 布料很薄，补一组反向面，避免旋转时背面消失。
            var combined = new List<Vector3>(vertices);
            var combinedUvs = new List<Vector2>(uvs);
            var combinedTriangles = new List<int>(triangles);
            int offset = vertices.Count;
            for (int i = 0; i < vertices.Count; i++)
            {
                combined.Add(vertices[i] - new Vector3(0f, 0.0006f, 0f));
                combinedUvs.Add(uvs[i]);
            }
            for (int i = 0; i < triangles.Count; i += 3)
            {
                combinedTriangles.Add(triangles[i] + offset);
                combinedTriangles.Add(triangles[i + 2] + offset);
                combinedTriangles.Add(triangles[i + 1] + offset);
            }

            var backMesh = new Mesh();
            backMesh.name = assetName;
            backMesh.SetVertices(combined);
            backMesh.SetUVs(0, combinedUvs);
            backMesh.SetTriangles(combinedTriangles, 0, true);
            backMesh.RecalculateNormals();
            backMesh.RecalculateTangents();
            Object.DestroyImmediate(mesh);
            return SaveMeshAsset(assetName, backMesh);
        }

        // ---------------------------------------------------------------- 组装 / 缩放 / 落地

        private static void BuildPrefab(string itemId, GameObject source, Vector3 targetSize,
            bool fitInside, Vector3 poseEuler)
        {
            if (source == null)
            {
                return;
            }

            var visual = new GameObject("Visual");
            var pose = new GameObject("Pose");
            pose.transform.SetParent(visual.transform, false);
            pose.transform.localRotation = Quaternion.Euler(poseEuler);

            source.transform.SetParent(pose.transform, false);
            source.transform.localPosition = Vector3.zero;

            Bounds bounds = ComputeBounds(visual);
            if (bounds.size.x <= 0.0001f || bounds.size.y <= 0.0001f || bounds.size.z <= 0.0001f)
            {
                Debug.LogError("[PuzzleEscape] 物品网格为空：" + itemId);
                Object.DestroyImmediate(visual);
                return;
            }

            float scaleX = targetSize.x / bounds.size.x;
            float scaleY = targetSize.y / bounds.size.y;
            float scaleZ = targetSize.z / bounds.size.z;

            if (fitInside)
            {
                float scale = Mathf.Min(scaleX, scaleY, scaleZ);
                visual.transform.localScale = Vector3.one * scale;
            }
            else
            {
                visual.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            }

            Bounds finalBounds = ComputeBounds(visual);
            Vector3 localScale = visual.transform.localScale;
            pose.transform.localPosition += new Vector3(
                -finalBounds.center.x / localScale.x,
                -finalBounds.min.y / localScale.y,
                -finalBounds.center.z / localScale.z);

            Bounds placed = ComputeBounds(visual);
            if (Mathf.Abs(placed.min.y) > 0.006f || Mathf.Abs(placed.center.x) > 0.006f ||
                Mathf.Abs(placed.center.z) > 0.006f)
            {
                Debug.LogWarning("[PuzzleEscape] 物品落地对齐偏差：" + itemId + " minY=" + placed.min.y +
                                 " center=" + placed.center);
            }

            int vertexCount = CountVertices(source);

            string path = PrefabRoot + "/" + itemId + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(visual, path);
            Object.DestroyImmediate(visual);

            BuiltItems.Add(itemId + "  " + targetSize.ToString("F3") +
                           "  顶点≈" + vertexCount);
        }

        private static int CountVertices(GameObject source)
        {
            int count = 0;
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    count += filter.sharedMesh.vertexCount;
                }
            }
            return count;
        }

        private static Bounds ComputeBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        // ---------------------------------------------------------------- 模型与材质

        private static GameObject LoadModel(string modelId)
        {
            string path = ModelRoot + "/" + modelId + "/" + modelId + ".fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[PuzzleEscape] 找不到真实模型：" + path);
                return null;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = modelId + "_Source";

            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                {
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }
            }
            catch
            {
                // 部分模型以普通 GameObject 导入，无需解包。
            }

            foreach (Camera cameraComponent in instance.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(cameraComponent.gameObject);
            }

            foreach (Light lightComponent in instance.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(lightComponent);
            }

            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                Object.DestroyImmediate(animator);
            }

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            return instance;
        }

        private static GameObject ExtractChild(GameObject model, string childName)
        {
            Transform found = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                {
                    found = t;
                    break;
                }
            }

            if (found == null)
            {
                Debug.LogError("[PuzzleEscape] 模型 " + model.name + " 缺少子对象：" + childName);
                Object.DestroyImmediate(model);
                return null;
            }

            var source = new GameObject("Source");
            found.SetParent(source.transform, true);
            found.localPosition = Vector3.zero;
            Object.DestroyImmediate(model);
            return source;
        }

        private static void AssignSlots(GameObject source, System.Func<string, int, Material> resolver)
        {
            foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    continue;
                }

                for (int i = 0; i < materials.Length; i++)
                {
                    string slot = materials[i] != null ? materials[i].name.ToLowerInvariant() : string.Empty;
                    materials[i] = resolver(slot, i);
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static Material GetHiddenMaterial()
        {
            if (MaterialCache.TryGetValue("Item_Hidden", out Material cached))
            {
                return cached;
            }

            Material material = GetOrCreateMaterial("Item_Hidden", Shader.Find("Standard"));
            ApplyOpaqueOrTransparent(material, Color.clear, true, 0.001f);
            material.mainTexture = null;
            material.SetFloat("_Glossiness", 0f);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            MaterialCache["Item_Hidden"] = material;
            return material;
        }

        private static Material GetMaterial(string assetName, string folder, string prefix, Color tint,
            bool transparent, float alpha, Texture2D emissionMap, Color emissionColor,
            float fallbackSmoothness)
        {
            if (MaterialCache.TryGetValue(assetName, out Material cached))
            {
                return cached;
            }

            string path = MaterialRoot + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = false;
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    Debug.LogError("[PuzzleEscape] 缺少 Standard 着色器，无法创建物品材质。");
                    return null;
                }

                material = new Material(shader);
                material.name = assetName;
                AssetDatabase.CreateAsset(material, path);
                created = true;
            }

            Texture2D albedo = LoadPrepared(folder, prefix, "Albedo", false, false);
            Texture2D normal = LoadPrepared(folder, prefix, "Normal", true, true);
            Texture2D packed = LoadPrepared(folder, prefix, "MetallicSmoothness", false, true);

            material.shader = Shader.Find("Standard");
            material.color = transparent
                ? new Color(tint.r, tint.g, tint.b, alpha)
                : new Color(tint.r, tint.g, tint.b, 1f);
            material.mainTexture = albedo;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", Vector2.one);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }

            if (packed != null)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.SetTextureScale("_MetallicGlossMap", Vector2.one);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Glossiness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                material.SetTexture("_MetallicGlossMap", null);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Glossiness", fallbackSmoothness);
                material.DisableKeyword("_METALLICGLOSSMAP");
            }

            if (emissionMap != null)
            {
                material.SetTexture("_EmissionMap", emissionMap);
                material.SetColor("_EmissionColor", emissionColor);
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.SetTexture("_EmissionMap", null);
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
            }

            ApplyOpaqueOrTransparent(material, material.color, transparent,
                transparent ? alpha : 1f);

            if (!transparent)
            {
                material.SetFloat("_Glossiness", packed != null ? 1f : fallbackSmoothness);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            MaterialCache[assetName] = material;

            if (created)
            {
                AssetDatabase.SaveAssets();
            }

            return material;
        }

        private static Material GetPlainMetal(string assetName, Color color, float metallic, float glossiness)
        {
            if (MaterialCache.TryGetValue(assetName, out Material cached))
            {
                return cached;
            }

            string path = MaterialRoot + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.name = assetName;
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Standard");
            material.mainTexture = null;
            material.color = new Color(color.r, color.g, color.b, 1f);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", glossiness);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_METALLICGLOSSMAP");
            material.DisableKeyword("_EMISSION");
            ApplyOpaqueOrTransparent(material, material.color, false, 1f);
            material.SetFloat("_Glossiness", glossiness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            MaterialCache[assetName] = material;
            return material;
        }

        private static void ApplyOpaqueOrTransparent(Material material, Color color, bool transparent,
            float alpha)
        {
            if (transparent)
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.color = new Color(color.r, color.g, color.b, alpha);
            }
            else
            {
                material.SetFloat("_Mode", 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
            }
        }

        private static Material GetOrCreateMaterial(string assetName, Shader shader)
        {
            string path = MaterialRoot + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                material.name = assetName;
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            return material;
        }

        private static Texture2D LoadPrepared(string folder, string prefix, string suffix, bool normal,
            bool linear)
        {
            string path = FindTexture(folder, prefix, suffix);
            if (path == null)
            {
                return null;
            }

            ConfigureTexture(path, normal, linear);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string FindTexture(string folder, string prefix, string suffix)
        {
            string[] extensions = { ".jpg", ".png", ".jpeg" };
            foreach (string extension in extensions)
            {
                string candidate = folder + "/" + prefix + "_" + suffix + extension;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(candidate) != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ConfigureTexture(string path, bool normal, bool linear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            TextureImporterType desiredType =
                normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != desiredType)
            {
                importer.textureType = desiredType;
                changed = true;
            }

            bool desiredSrgb = !normal && !linear;
            if (importer.sRGBTexture != desiredSrgb)
            {
                importer.sRGBTexture = desiredSrgb;
                changed = true;
            }

            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Trilinear)
            {
                importer.filterMode = FilterMode.Trilinear;
                changed = true;
            }

            if (importer.anisoLevel < 8)
            {
                importer.anisoLevel = 8;
                changed = true;
            }

            if (importer.mipmapEnabled != true)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- 小工具

        private static void AddGlow(GameObject source, Vector3 localPosition, Color color, float range,
            float intensity)
        {
            var glow = new GameObject("Glow");
            glow.transform.SetParent(source.transform, false);
            glow.transform.localPosition = localPosition;

            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        private static GameObject AddPrimitive(PrimitiveType type, Transform parent, Vector3 localPosition,
            Vector3 localScale, Vector3 localEuler, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = localScale;
            gameObject.transform.localEulerAngles = localEuler;

            Object.DestroyImmediate(gameObject.GetComponent<Collider>());

            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return gameObject;
        }

        private static GameObject AddMeshObject(string name, Mesh mesh, Material[] materials, Transform parent,
            Vector3 localPosition = default)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;

            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            return gameObject;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static Mesh SaveMeshAsset(string assetName, Mesh mesh)
        {
            if (MeshCache.TryGetValue(assetName, out Mesh cached))
            {
                Object.DestroyImmediate(mesh);
                return cached;
            }

            string path = MeshRoot + "/" + assetName + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // 已有网格资产直接更新内容，避免删除/重建导致 GUID 变化与预制体引用丢失。
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                MeshCache[assetName] = existing;
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            MeshCache[assetName] = mesh;
            return mesh;
        }
    }
}
