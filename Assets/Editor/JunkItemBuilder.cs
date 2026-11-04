#if UNITY_EDITOR
using System.IO;
using PuzzleEscape.Data;
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 为 6 种新杂物（旧电池、绷带、药瓶、旧报纸、火柴盒、肥皂）生成真实感模型与贴图：
    ///  · 形状用零件拼出细节（瓶盖、标签、缠绕的布条、火柴头等）；
    ///  · 材质复用 Assets/Art/Textures/Real 下的扫描 PBR（金属、亚麻、木纹、大理石）；
    ///  · 旧报纸与标签额外生成专用贴图，避免纯色块。
    /// 输出到 Assets/Resources/Items/&lt;id&gt;.prefab，完成后自动烘焙背包图标。
    ///
    /// 菜单：PuzzleEscape → 生成杂物真实模型
    /// </summary>
    public static class JunkItemBuilder
    {
        private const string PrefabRoot = "Assets/Resources/Items";
        private const string MaterialRoot = "Assets/Art/Materials/Items";
        private const string GeneratedTextureRoot = "Assets/Art/Textures/Items";
        private const string RealTextureRoot = "Assets/Art/Textures/Real";

        [MenuItem("PuzzleEscape/生成杂物真实模型", false, 20)]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory(MaterialRoot);

            BuildBattery();
            BuildBandage();
            BuildPillBottle();
            BuildNewspaper();
            BuildMatchbox();
            BuildSoap();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 模型就绪后顺手把背包/物品栏图标一起烘焙。
            ItemIconBaker.BakeAll();

            Debug.Log("[PuzzleEscape] 杂物真实模型已生成：6 件（电池/绷带/药瓶/报纸/火柴盒/肥皂）。");
        }

        // ------------------------------------------------------------------
        // 旧电池：金属筒身 + 彩色标签 + 正极凸头
        // ------------------------------------------------------------------
        private static void BuildBattery()
        {
            var root = new GameObject(ItemIds.Battery);

            Material metal = RealMaterial("Item_BatteryShell", "Metal_Rusty",
                new Color(0.78f, 0.74f, 0.66f), 0.35f, 0.15f);
            Material label = RealMaterial("Item_BatteryLabel", "Wall_Plaster",
                new Color(0.72f, 0.35f, 0.2f), 0.2f);
            Material cap = RealMaterial("Item_BatteryCap", "Metal_Silver",
                new Color(0.9f, 0.9f, 0.86f), 0.55f, 0.5f);

            Cylinder(root.transform, "Shell", new Vector3(0f, 0.028f, 0f),
                new Vector3(0.028f, 0.052f, 0.028f), metal);
            Cylinder(root.transform, "Label", new Vector3(0f, 0.028f, 0f),
                new Vector3(0.0288f, 0.03f, 0.0288f), label);
            Cylinder(root.transform, "Cap", new Vector3(0f, 0.055f, 0f),
                new Vector3(0.012f, 0.006f, 0.012f), cap);
            Cylinder(root.transform, "Bottom", new Vector3(0f, 0.002f, 0f),
                new Vector3(0.027f, 0.004f, 0.027f), cap);

            Save(root, ItemIds.Battery);
        }

        // ------------------------------------------------------------------
        // 绷带：卷起的亚麻布卷 + 翘起的一截
        // ------------------------------------------------------------------
        private static void BuildBandage()
        {
            var root = new GameObject(ItemIds.Bandage);

            Material linen = RealMaterial("Item_Bandage", "Fabric_Linen",
                new Color(0.94f, 0.92f, 0.86f), 0.08f);
            Material edge = RealMaterial("Item_BandageEdge", "Fabric_Linen",
                new Color(0.84f, 0.81f, 0.74f), 0.06f);

            Cylinder(root.transform, "Roll", new Vector3(0f, 0.03f, 0f),
                new Vector3(0.05f, 0.06f, 0.05f), linen);
            Cylinder(root.transform, "RollInner", new Vector3(0f, 0.03f, 0f),
                new Vector3(0.024f, 0.062f, 0.024f), edge);

            // 缠在外面的几圈布条
            for (int i = 0; i < 3; i++)
            {
                Cylinder(root.transform, "Wrap", new Vector3(0f, 0.014f + i * 0.017f, 0f),
                    new Vector3(0.052f, 0.008f, 0.052f), edge);
            }

            // 翘出来的一截
            Box(root.transform, "LooseEnd", new Vector3(0.042f, 0.012f, 0.012f),
                new Vector3(0.06f, 0.002f, 0.028f), linen,
                Quaternion.Euler(0f, 18f, -8f));

            Save(root, ItemIds.Bandage);
        }

        // ------------------------------------------------------------------
        // 药瓶：半透明瓶身 + 旋盖 + 标签
        // ------------------------------------------------------------------
        private static void BuildPillBottle()
        {
            var root = new GameObject(ItemIds.PillBottle);

            Material body = RealMaterial("Item_PillBottleBody", "Marble_Cream",
                new Color(0.72f, 0.82f, 0.74f), 0.35f);
            Material cap = RealMaterial("Item_PillBottleCap", "Metal_Silver",
                new Color(0.86f, 0.86f, 0.82f), 0.4f, 0.35f);
            Material label = RealMaterial("Item_PillBottleLabel", "Wall_Plaster",
                new Color(0.92f, 0.9f, 0.82f), 0.1f);

            Cylinder(root.transform, "Body", new Vector3(0f, 0.035f, 0f),
                new Vector3(0.03f, 0.062f, 0.03f), body);
            Cylinder(root.transform, "Shoulder", new Vector3(0f, 0.068f, 0f),
                new Vector3(0.022f, 0.012f, 0.022f), body);
            Cylinder(root.transform, "Cap", new Vector3(0f, 0.078f, 0f),
                new Vector3(0.024f, 0.014f, 0.024f), cap);
            Cylinder(root.transform, "Label", new Vector3(0f, 0.034f, 0f),
                new Vector3(0.0308f, 0.034f, 0.0308f), label);

            // 瓶里剩下的几粒药片
            for (int i = 0; i < 4; i++)
            {
                Cylinder(root.transform, "Pill", new Vector3(
                        Mathf.Cos(i * 1.7f) * 0.012f, 0.008f + i * 0.004f,
                        Mathf.Sin(i * 1.7f) * 0.012f),
                    new Vector3(0.008f, 0.004f, 0.008f), body);
            }

            Save(root, ItemIds.PillBottle);
        }

        // ------------------------------------------------------------------
        // 旧报纸：几张叠放、微微错开的报纸
        // ------------------------------------------------------------------
        private static void BuildNewspaper()
        {
            var root = new GameObject(ItemIds.Newspaper);

            Texture2D newsprint = GenerateNewspaperTexture();
            Material paper = RealMaterial("Item_Newsprint", "Wall_Plaster",
                new Color(0.86f, 0.84f, 0.76f), 0.05f);
            if (newsprint != null)
            {
                paper.mainTexture = newsprint;
                paper.mainTextureScale = new Vector2(1f, 1f);
            }

            Material ink = RealMaterial("Item_NewsprintTitle", "Wall_Plaster",
                new Color(0.24f, 0.24f, 0.26f), 0.12f);

            Box(root.transform, "Sheet", new Vector3(0f, 0.0015f, 0f),
                new Vector3(0.21f, 0.002f, 0.29f), paper,
                Quaternion.Euler(0f, 6f, 0f));
            Box(root.transform, "SheetTop", new Vector3(0.014f, 0.0045f, 0.008f),
                new Vector3(0.205f, 0.002f, 0.285f), paper,
                Quaternion.Euler(0f, -7f, 0f));
            Box(root.transform, "Masthead", new Vector3(0.016f, 0.0062f, 0.118f),
                new Vector3(0.17f, 0.001f, 0.022f), ink,
                Quaternion.Euler(0f, -7f, 0f));
            Box(root.transform, "Fold", new Vector3(0.014f, 0.0061f, 0f),
                new Vector3(0.205f, 0.0008f, 0.004f), ink,
                Quaternion.Euler(0f, -7f, 0f));

            Save(root, ItemIds.Newspaper);
        }

        // ------------------------------------------------------------------
        // 火柴盒：纸盒 + 摩擦条 + 散落的火柴
        // ------------------------------------------------------------------
        private static void BuildMatchbox()
        {
            var root = new GameObject(ItemIds.Matchbox);

            Material carton = RealMaterial("Item_MatchboxCarton", "Wood_Fine",
                new Color(0.62f, 0.3f, 0.22f), 0.2f);
            Material strip = RealMaterial("Item_MatchboxStrip", "Metal_Rusty",
                new Color(0.32f, 0.28f, 0.24f), 0.15f, 0.1f);
            Material stick = RealMaterial("Item_MatchStick", "Wood_Fine",
                new Color(0.9f, 0.86f, 0.74f), 0.15f);
            Material head = RealMaterial("Item_MatchHead", "Metal_Rusty",
                new Color(0.72f, 0.2f, 0.16f), 0.2f, 0.05f);

            Box(root.transform, "Sleeve", new Vector3(0f, 0.017f, 0f),
                new Vector3(0.07f, 0.034f, 0.048f), carton);
            Box(root.transform, "StrikingStrip", new Vector3(0f, 0.017f, 0.0255f),
                new Vector3(0.062f, 0.026f, 0.004f), strip);
            Box(root.transform, "SleeveBand", new Vector3(0f, 0.017f, 0f),
                new Vector3(0.072f, 0.012f, 0.05f), strip);

            // 三根搭在盒子上的火柴
            for (int i = 0; i < 3; i++)
            {
                float angle = -12f + i * 12f;
                Vector3 offset = new Vector3(0f, 0.036f + i * 0.001f, -0.006f + i * 0.004f);

                Box(root.transform, "Stick", offset,
                    new Vector3(0.055f, 0.004f, 0.004f), stick,
                    Quaternion.Euler(0f, angle, 0f));
                Box(root.transform, "MatchHead",
                    offset + Quaternion.Euler(0f, angle, 0f) * new Vector3(0.028f, 0f, 0f),
                    new Vector3(0.01f, 0.006f, 0.006f), head,
                    Quaternion.Euler(0f, angle, 0f));
            }

            Save(root, ItemIds.Matchbox);
        }

        // ------------------------------------------------------------------
        // 肥皂：磨圆的长方体 + 纸包装
        // ------------------------------------------------------------------
        private static void BuildSoap()
        {
            var root = new GameObject(ItemIds.Soap);

            Material soap = RealMaterial("Item_Soap", "Marble_Cream",
                new Color(0.93f, 0.93f, 0.86f), 0.45f);
            Material wrapper = RealMaterial("Item_SoapWrapper", "Fabric_Linen",
                new Color(0.86f, 0.82f, 0.68f), 0.1f);

            // 主体：中间厚、边缘收一点，接近真实肥皂的圆角
            Box(root.transform, "Body", new Vector3(0f, 0.014f, 0f),
                new Vector3(0.085f, 0.028f, 0.055f), soap);
            Box(root.transform, "BodyTop", new Vector3(0f, 0.029f, 0f),
                new Vector3(0.076f, 0.005f, 0.047f), soap);
            Box(root.transform, "BodyBottom", new Vector3(0f, 0.0025f, 0f),
                new Vector3(0.076f, 0.005f, 0.047f), soap);

            // 裹在中间的纸带
            Box(root.transform, "Wrapper", new Vector3(0f, 0.014f, 0f),
                new Vector3(0.03f, 0.032f, 0.058f), wrapper);
            Box(root.transform, "WrapperTop", new Vector3(0f, 0.0305f, 0f),
                new Vector3(0.03f, 0.004f, 0.05f), wrapper);

            Save(root, ItemIds.Soap);
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------
        private static void Save(GameObject root, string itemId)
        {
            string path = PrefabRoot + "/" + itemId + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 scale, Material material, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;

            Object.DestroyImmediate(go.GetComponent<Collider>());

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            return go;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position,
            Vector3 size, Material material, Quaternion? rotation = null)
        {
            return Part(parent, name, PrimitiveType.Cube, position, size, material,
                rotation ?? Quaternion.identity);
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 position,
            Vector3 size, Material material)
        {
            // Unity 圆柱原始高度是 2，这里按“实际高度”理解。
            var scale = new Vector3(size.x, size.y * 0.5f, size.z);
            return Part(parent, name, PrimitiveType.Cylinder, position, scale, material,
                Quaternion.identity);
        }

        private static Material RealMaterial(string assetName, string textureSet, Color tint,
            float smoothness, float metallic = 0f)
        {
            string path = MaterialRoot + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                material = new Material(shader);
                material.name = assetName;
                AssetDatabase.CreateAsset(material, path);
            }

            string folder = RealTextureRoot + "/" + textureSet;
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                folder + "/" + textureSet + "_Albedo.jpg") ??
                AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + textureSet + "_Albedo.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                folder + "/" + textureSet + "_Normal.jpg") ??
                AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + textureSet + "_Normal.png");
            Texture2D packed = AssetDatabase.LoadAssetAtPath<Texture2D>(
                folder + "/" + textureSet + "_MetallicSmoothness.png");

            material.shader = Shader.Find("Standard");
            material.color = new Color(tint.r, tint.g, tint.b, 1f);
            material.mainTexture = albedo;

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.8f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }

            // 小物件不需要金属贴图的强反射，直接用标量更稳定。
            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>生成一张带分栏与标题块的旧报纸贴图（256×256）。</summary>
        private static Texture2D GenerateNewspaperTexture()
        {
            const string path = GeneratedTextureRoot + "/Newsprint_Albedo.png";

            if (!AssetDatabase.IsValidFolder(GeneratedTextureRoot))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Art/Textures"))
                {
                    AssetDatabase.CreateFolder("Assets/Art", "Textures");
                }

                AssetDatabase.CreateFolder("Assets/Art/Textures", "Items");
            }

            if (File.Exists(path))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            const int size = 256;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
                    byte shade = (byte)Mathf.Lerp(206f, 226f, noise);
                    pixels[y * size + x] = new Color32(shade, (byte)(shade - 4), (byte)(shade - 22), 255);
                }
            }

            // 报头：顶部一条深色块
            for (int y = size - 26; y < size - 8; y++)
            {
                for (int x = 14; x < size - 14; x++)
                {
                    pixels[y * size + x] = new Color32(44, 44, 48, 255);
                }
            }

            // 正文：几列短横线模拟文字
            for (int column = 0; column < 3; column++)
            {
                int left = 16 + column * 78;

                for (int line = 0; line < 16; line++)
                {
                    int y = size - 46 - line * 11;

                    if (y < 12)
                    {
                        break;
                    }

                    int width = line % 5 == 4 ? 48 : 66;

                    for (int x = left; x < left + width && x < size - 12; x++)
                    {
                        pixels[y * size + x] = new Color32(70, 70, 74, 255);
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
#endif
