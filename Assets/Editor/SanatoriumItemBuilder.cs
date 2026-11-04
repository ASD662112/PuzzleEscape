#if UNITY_EDITOR
using System.IO;
using PuzzleEscape.Data;
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 为疗养院缺失的 19 件剧情道具生成真实感模型与 PBR 材质：
    /// 三张通行卡、11 把钥匙、3 枚保险丝、卷帘门曲柄、消防斧。
    ///
    /// 形状按实物拆件（钥匙环 + 匙杆 + 齿口、卡片 + 磁条、玻璃管 + 两端铜帽、L 形曲柄、斧柄 + 斧刃），
    /// 材质复用 Assets/Art/Textures/Real 下的扫描贴图，并按物品调色区分。
    /// 输出到 Assets/Resources/Items/&lt;id&gt;.prefab，完成后自动烘焙背包图标。
    ///
    /// 菜单：PuzzleEscape → 生成疗养院剧情道具模型
    /// </summary>
    public static class SanatoriumItemBuilder
    {
        private const string PrefabRoot = "Assets/Resources/Items";
        private const string MaterialRoot = "Assets/Art/Materials/Items";
        private const string RealTextureRoot = "Assets/Art/Textures/Real";

        [MenuItem("PuzzleEscape/生成疗养院剧情道具模型", false, 21)]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory(MaterialRoot);

            // 三张通行卡
            BuildCard(ItemIds.Card2F, "2F", new Color(0.45f, 0.68f, 0.86f));
            BuildCard(ItemIds.Card3F, "3F", new Color(0.36f, 0.74f, 0.64f));
            BuildCard(ItemIds.Card4F, "4F", new Color(0.62f, 0.55f, 0.82f));

            // 主线钥匙
            BuildKey(ItemIds.WardKey, "病房钥匙", new Color(0.82f, 0.8f, 0.7f), 2);
            BuildKey(ItemIds.PatientKey, "病人物品柜钥匙", new Color(0.72f, 0.8f, 0.84f), 1);
            BuildKey(ItemIds.StorageKey, "药库钥匙", new Color(0.85f, 0.72f, 0.36f), 2);
            BuildKey(ItemIds.SurgeryKey, "手术区钥匙", new Color(0.78f, 0.84f, 0.9f), 3);
            BuildKey(ItemIds.StaffKey, "值班钥匙", new Color(0.7f, 0.76f, 0.82f), 1);
            BuildKey(ItemIds.DirectorKey, "院长室钥匙", new Color(0.92f, 0.78f, 0.42f), 2);
            BuildKey(ItemIds.MainKey, "主门钥匙", new Color(0.95f, 0.86f, 0.52f), 3);

            // 支线钥匙
            BuildKey(ItemIds.ClerkKey, "职员钥匙", new Color(0.86f, 0.7f, 0.42f), 1);
            BuildKey(ItemIds.LockerKey, "库房钥匙", new Color(0.66f, 0.72f, 0.78f), 2);
            BuildKey(ItemIds.GarageKey, "车库钥匙", new Color(0.8f, 0.66f, 0.36f), 3);
            BuildKey(ItemIds.FireKey, "消防连廊钥匙", new Color(0.8f, 0.34f, 0.3f), 2);

            // 三枚保险丝
            BuildFuse(ItemIds.FuseA, new Color(0.9f, 0.4f, 0.26f));
            BuildFuse(ItemIds.FuseB, new Color(0.92f, 0.52f, 0.26f));
            BuildFuse(ItemIds.FuseC, new Color(0.94f, 0.66f, 0.32f));

            // 机关件
            BuildCrank();
            BuildFireAxe();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 道具模型就绪后统一烘焙图标，背包与快捷栏会自动换成真实模型。
            ItemIconBaker.BakeAll();

            Debug.Log("[PuzzleEscape] 疗养院剧情道具模型已生成：19 件（3 通行卡 / 11 钥匙 / 3 保险丝 / 曲柄 / 消防斧）。");
        }

        /// <summary>全部缺失物品一次补齐：6 种杂物 + 19 件剧情道具，最后统一烘焙图标。</summary>
        [MenuItem("PuzzleEscape/生成全部缺失物品模型", false, 22)]
        public static void BuildEverything()
        {
            JunkItemBuilder.BuildAll();
            BuildAll();
            Debug.Log("[PuzzleEscape] 全部缺失物品模型已补齐（杂物 6 + 剧情道具 19）。");
        }

        // ------------------------------------------------------------------
        // 通行卡：塑料卡片 + 磁条 + 色块
        // ------------------------------------------------------------------
        private static void BuildCard(string itemId, string label, Color tint)
        {
            var root = new GameObject(itemId);

            Material plastic = RealMaterial("Item_Card_" + label, "Wall_Plaster", tint, 0.45f);
            Material strip = RealMaterial("Item_CardStrip", "Metal_Rusty",
                new Color(0.24f, 0.24f, 0.26f), 0.35f, 0.2f);
            Material print = RealMaterial("Item_CardPrint", "Wall_Plaster",
                new Color(0.95f, 0.95f, 0.92f), 0.15f);

            Box(root.transform, "CardBody", new Vector3(0f, 0.0009f, 0f),
                new Vector3(0.086f, 0.0018f, 0.054f), plastic);
            Box(root.transform, "MagneticStrip", new Vector3(0f, 0.0019f, 0.014f),
                new Vector3(0.082f, 0.0006f, 0.01f), strip);
            Box(root.transform, "PhotoBlock", new Vector3(-0.024f, 0.0020f, -0.008f),
                new Vector3(0.018f, 0.0005f, 0.022f), print);
            Box(root.transform, "TextBlock", new Vector3(0.014f, 0.0020f, -0.012f),
                new Vector3(0.036f, 0.0005f, 0.006f), print);
            Box(root.transform, "CornerChip", new Vector3(0.041f, 0.0019f, -0.025f),
                new Vector3(0.008f, 0.0008f, 0.008f), strip,
                Quaternion.Euler(0f, 45f, 0f));

            Save(root, itemId);
        }

        // ------------------------------------------------------------------
        // 钥匙：圆形匙环 + 匙杆 + 齿口 + 彩色标签
        // ------------------------------------------------------------------
        private static void BuildKey(string itemId, string label, Color tint, int teeth)
        {
            var root = new GameObject(itemId);

            Material metal = RealMaterial("Item_Key_" + itemId, "Metal_Silver", tint, 0.5f, 0.45f);
            Material tag = RealMaterial("Item_KeyTag_" + itemId, "Wall_Plaster",
                new Color(0.92f, 0.9f, 0.84f), 0.1f);

            // 匙杆
            Box(root.transform, "Shaft", new Vector3(0f, 0.0045f, 0f),
                new Vector3(0.072f, 0.004f, 0.006f), metal);

            // 匙环（扁圆柱读作钥匙头）
            Cylinder(root.transform, "Bow", new Vector3(-0.046f, 0.0045f, 0f),
                new Vector3(0.03f, 0.004f, 0.03f), metal);
            Cylinder(root.transform, "BowHole", new Vector3(-0.046f, 0.0055f, 0f),
                new Vector3(0.014f, 0.004f, 0.014f), metal);

            // 齿口
            for (int i = 0; i < teeth; i++)
            {
                Box(root.transform, "Tooth", new Vector3(0.024f + i * 0.009f, 0.0045f, 0.007f),
                    new Vector3(0.006f, 0.004f, 0.009f), metal);
            }

            // 挂在匙环上的小标签
            Box(root.transform, "Tag", new Vector3(-0.046f, 0.0062f, -0.02f),
                new Vector3(0.022f, 0.0008f, 0.014f), tag,
                Quaternion.Euler(0f, 12f, 0f));

            Save(root, itemId);
        }

        // ------------------------------------------------------------------
        // 保险丝：玻璃管 + 两端铜帽 + 内部熔丝
        // ------------------------------------------------------------------
        private static void BuildFuse(string itemId, Color tint)
        {
            var root = new GameObject(itemId);

            Material glass = RealMaterial("Item_FuseGlass", "Marble_Cream",
                new Color(0.86f, 0.8f, 0.66f), 0.75f);
            Material cap = RealMaterial("Item_FuseCap_" + itemId, "Metal_Rusty", tint, 0.4f, 0.5f);
            Material wire = RealMaterial("Item_FuseWire", "Metal_Silver",
                new Color(0.9f, 0.86f, 0.7f), 0.55f, 0.6f);

            // 玻璃管横放
            Cylinder(root.transform, "Tube", new Vector3(0f, 0.011f, 0f),
                new Vector3(0.019f, 0.062f, 0.019f), glass, Quaternion.Euler(0f, 0f, 90f));
            Cylinder(root.transform, "Cap", new Vector3(-0.033f, 0.011f, 0f),
                new Vector3(0.021f, 0.012f, 0.021f), cap, Quaternion.Euler(0f, 0f, 90f));
            Cylinder(root.transform, "Cap", new Vector3(0.033f, 0.011f, 0f),
                new Vector3(0.021f, 0.012f, 0.021f), cap, Quaternion.Euler(0f, 0f, 90f));
            Box(root.transform, "Element", new Vector3(0f, 0.011f, 0f),
                new Vector3(0.05f, 0.002f, 0.002f), wire);

            Save(root, itemId);
        }

        // ------------------------------------------------------------------
        // 卷帘门曲柄：L 形手摇柄 + 方形轴头
        // ------------------------------------------------------------------
        private static void BuildCrank()
        {
            var root = new GameObject(ItemIds.GateCrank);

            Material iron = RealMaterial("Item_Crank", "Metal_Rusty",
                new Color(0.72f, 0.74f, 0.78f), 0.42f, 0.55f);
            Material grip = RealMaterial("Item_CrankGrip", "Wood_Dark",
                new Color(0.9f, 0.86f, 0.8f), 0.2f);

            Cylinder(root.transform, "Shaft", new Vector3(0f, 0.014f, 0.05f),
                new Vector3(0.014f, 0.13f, 0.014f), iron, Quaternion.Euler(90f, 0f, 0f));
            Cylinder(root.transform, "Arm", new Vector3(-0.037f, 0.014f, -0.012f),
                new Vector3(0.014f, 0.08f, 0.014f), iron, Quaternion.Euler(0f, 0f, 90f));
            Cylinder(root.transform, "Grip", new Vector3(-0.08f, 0.014f, -0.012f),
                new Vector3(0.02f, 0.055f, 0.02f), grip, Quaternion.Euler(90f, 0f, 0f));
            Box(root.transform, "Socket", new Vector3(0f, 0.014f, -0.018f),
                new Vector3(0.026f, 0.026f, 0.03f), iron);

            Save(root, ItemIds.GateCrank);
        }

        // ------------------------------------------------------------------
        // 消防斧：木柄 + 红色斧头
        // ------------------------------------------------------------------
        private static void BuildFireAxe()
        {
            var root = new GameObject(ItemIds.FireAxe);

            Material handle = RealMaterial("Item_AxeHandle", "Wood_Fine",
                new Color(0.92f, 0.86f, 0.74f), 0.25f);
            Material head = RealMaterial("Item_AxeHead", "Metal_Rusty",
                new Color(0.82f, 0.32f, 0.26f), 0.45f, 0.5f);
            Material blade = RealMaterial("Item_AxeBlade", "Metal_Silver",
                new Color(0.9f, 0.9f, 0.92f), 0.6f, 0.6f);

            // 木柄斜放
            Cylinder(root.transform, "Handle", new Vector3(0f, 0.02f, 0f),
                new Vector3(0.022f, 0.24f, 0.022f), handle, Quaternion.Euler(0f, 0f, 90f));

            // 斧头：主体 + 刃口
            Box(root.transform, "Head", new Vector3(-0.075f, 0.028f, 0f),
                new Vector3(0.05f, 0.045f, 0.026f), head);
            Box(root.transform, "Blade", new Vector3(-0.104f, 0.028f, 0f),
                new Vector3(0.024f, 0.06f, 0.012f), blade,
                Quaternion.Euler(0f, 0f, 0f));
            Box(root.transform, "Butt", new Vector3(-0.048f, 0.028f, 0f),
                new Vector3(0.012f, 0.03f, 0.03f), head);

            Save(root, ItemIds.FireAxe);
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

        private static void Box(Transform parent, string name, Vector3 position,
            Vector3 size, Material material, Quaternion? rotation = null)
        {
            Part(parent, name, PrimitiveType.Cube, position, size, material,
                rotation ?? Quaternion.identity);
        }

        private static void Cylinder(Transform parent, string name, Vector3 position,
            Vector3 size, Material material, Quaternion? rotation = null)
        {
            // Unity 圆柱原始高度是 2，这里按“实际高度”理解。
            var scale = new Vector3(size.x, size.y * 0.5f, size.z);
            Part(parent, name, PrimitiveType.Cylinder, position, scale, material,
                rotation ?? Quaternion.identity);
        }

        private static Material RealMaterial(string assetName, string textureSet, Color tint,
            float smoothness, float metallic = 0f)
        {
            string path = MaterialRoot + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
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

            material.shader = Shader.Find("Standard");
            material.color = new Color(tint.r, tint.g, tint.b, 1f);
            material.mainTexture = albedo;

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.7f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }

            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif
