#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 把 Assets/Art/Models/Monster 下的 OBJ + PBR 贴图做成运行时可直接加载的怪物 Prefab，
    /// 保存到 Assets/Resources/Monster/MonsterModel.prefab。
    /// </summary>
    public static class MonsterPrefabBuilder
    {
        private const string ModelFolder = "Assets/Art/Models/Monster";
        private const string MaterialFolder = "Assets/Art/Materials/Monster";
        private const string PrefabFolder = "Assets/Resources/Monster";
        private const string PrefabPath = PrefabFolder + "/MonsterModel.prefab";
        private const float TargetHeight = 2.1f;

        [MenuItem("PuzzleEscape/生成怪物 Prefab", false, 15)]
        public static void Build()
        {
            AssetDatabase.Refresh();

            string modelPath = FindModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogError("[PuzzleEscape] 没找到怪物模型 OBJ：" + ModelFolder);
                return;
            }

            ConfigureTextures();
            Material material = CreateMaterial();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogError("[PuzzleEscape] 怪物模型导入失败：" + modelPath);
                return;
            }

            var root = new GameObject("MonsterModel");
            GameObject model = Object.Instantiate(source, root.transform);
            model.name = "Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            ApplyMaterial(model, material);
            RemoveColliders(root);

            Bounds bounds = CalculateBounds(root);
            float scale = bounds.size.y > 0.001f ? TargetHeight / bounds.size.y : 1f;
            model.transform.localScale = Vector3.one * scale;

            bounds = CalculateBounds(root);
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            bounds = CalculateBounds(root);
            Debug.Log(string.Format(
                "[PuzzleEscape] 怪物模型尺寸：{0:F2} x {1:F2} x {2:F2} m（缩放 {3:F3}，最低点 {4:F2}）。",
                bounds.size.x, bounds.size.y, bounds.size.z, scale, bounds.min.y));

            EnsureFolder();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PuzzleEscape] 怪物 Prefab 已生成：" + PrefabPath);
        }

        private static string FindModelPath()
        {
            if (!Directory.Exists(ModelFolder))
            {
                return null;
            }

            string[] files = Directory.GetFiles(ModelFolder, "*.obj", SearchOption.TopDirectoryOnly);
            return files.Length > 0 ? files[0].Replace('\\', '/') : null;
        }

        private static void ConfigureTextures()
        {
            Configure(ModelFolder + "/texture_pbr_20250901.png", TextureImporterType.Default, 2048, true);
            Configure(ModelFolder + "/texture_pbr_20250901_normal.png", TextureImporterType.NormalMap, 1024, false);
            Configure(ModelFolder + "/texture_pbr_20250901_metallic.png", TextureImporterType.Default, 512, false);
            Configure(ModelFolder + "/texture_pbr_20250901_roughness.png", TextureImporterType.Default, 512, false);
        }

        private static void Configure(string path, TextureImporterType type, int maxSize, bool sRgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = type;
            importer.maxTextureSize = maxSize;
            importer.sRGBTexture = sRgb;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static Material CreateMaterial()
        {
            EnsureFolder();

            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Materials");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Monster");
            }

            string path = MaterialFolder + "/MonsterBody.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.name = "MonsterBody";
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Standard");

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ModelFolder + "/texture_pbr_20250901.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ModelFolder + "/texture_pbr_20250901_normal.png");

            material.SetTexture("_MainTex", albedo);
            material.color = Color.white;

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Metallic", 0.05f);
            material.SetFloat("_Glossiness", 0.38f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyMaterial(GameObject model, Material material)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void RemoveColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Bounds CalculateBounds(GameObject root)
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

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Monster");
            }
        }
    }
}
#endif
