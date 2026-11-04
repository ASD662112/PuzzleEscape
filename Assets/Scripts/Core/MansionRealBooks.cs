#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 真实书挡/书架书本：使用 Poly Haven 的百科全書套装（20 册/组），
    /// 书脊与书页分别是两个材质槽，按层板高度批量摆放到真实书架上。
    /// </summary>
    internal static class MansionRealBooks
    {
        public const string BookSetModelId = "book_encyclopedia_set_01";

        private const string ModelFolder = "Assets/Art/Models/book_encyclopedia_set_01";
        private const string MaterialFolder = "Assets/Art/Materials/Models";
        private const float NaturalWidth = 0.55f;
        private const float NaturalHeight = 0.24f;
        private const float NaturalDepth = 0.16f;

        private static readonly float[] ShelfFractions = { 0.053f, 0.189f, 0.325f, 0.466f, 0.621f };

        private static Material coverMaterial;
        private static Material paperMaterial;

        public static bool Available => MansionRealModels.Has(BookSetModelId);

        /// <returns>成功摆放的书本组数量</returns>
        public static int AddRuns(Transform parent, Vector3 position, float yaw, float width, float height,
            float depth, int seed, int levels, int maxSetsPerLevel)
        {
            if (!Available)
            {
                return 0;
            }

            Material[] materials = { GetCoverMaterial(), GetPaperMaterial() };
            if (materials[0] == null || materials[1] == null)
            {
                return 0;
            }

            float shelfGap = height * 0.136f;
            float setHeight = Mathf.Min(NaturalHeight, shelfGap * 0.82f);
            float scale = setHeight / NaturalHeight;
            float setWidth = NaturalWidth * scale;
            float setDepth = NaturalDepth * scale;

            int setsPerLevel = Mathf.Clamp(
                Mathf.FloorToInt(width * 0.9f / (setWidth + 0.04f)), 1, Mathf.Max(1, maxSetsPerLevel));

            float totalWidth = setsPerLevel * setWidth + (setsPerLevel - 1) * 0.06f;
            float startX = -totalWidth * 0.5f + setWidth * 0.5f;
            float frontZ = depth * 0.5f - setDepth * 0.5f - 0.02f;
            int levelCount = Mathf.Min(levels, ShelfFractions.Length);

            var runRoot = new GameObject("BookRun");
            runRoot.transform.SetParent(parent, false);
            runRoot.transform.localPosition = position;
            runRoot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            int spawned = 0;
            for (int level = 0; level < levelCount; level++)
            {
                float shelfY = height * ShelfFractions[level] + 0.003f;
                for (int setIndex = 0; setIndex < setsPerLevel; setIndex++)
                {
                    float x = startX + setIndex * (setWidth + 0.06f);
                    float jitterZ = ((seed + level + setIndex) % 3 - 1) * 0.006f;
                    float yawJitter = ((seed + setIndex * 2 + level) % 3 - 1) * 1.4f;
                    Vector3 local = new Vector3(x, shelfY, frontZ + jitterZ);
                    Vector3 world = runRoot.transform.TransformPoint(local);

                    GameObject bookSet = MansionRealModels.Spawn(runRoot.transform, BookSetModelId, world,
                        yawJitter, setHeight, false, 0f, null, false, null, materials);
                    if (bookSet != null)
                    {
                        spawned++;
                    }
                }
            }

            return spawned;
        }

        private static Material GetCoverMaterial()
        {
            if (coverMaterial != null)
            {
                return coverMaterial;
            }

            coverMaterial = GetOrCreateBookMaterial("cover", Color.white, 0.55f, 1f);
            return coverMaterial;
        }

        private static Material GetPaperMaterial()
        {
            if (paperMaterial != null)
            {
                return paperMaterial;
            }

            paperMaterial = GetOrCreateBookMaterial("paper", new Color(0.95f, 0.92f, 0.85f), 0.12f, 0.8f);
            return paperMaterial;
        }

        private static Material GetOrCreateBookMaterial(string setName, Color tint, float gloss, float bumpScale)
        {
            string materialPath = MaterialFolder + "/BookEncyclopedia_" + setName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool created = false;

            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    return null;
                }

                EnsureMaterialFolder();
                material = new Material(shader);
                material.name = "BookEncyclopedia_" + setName;
                created = true;
            }

            material.color = tint;
            material.enableInstancing = true;

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ModelFolder + "/" + setName + "_Albedo.jpg");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ModelFolder + "/" + setName + "_Normal.jpg");
            Texture2D packed = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ModelFolder + "/" + setName + "_MetallicSmoothness.png");

            if (albedo != null)
            {
                material.mainTexture = albedo;
            }

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", bumpScale);
            }

            if (packed != null)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Glossiness", gloss);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);

            if (created)
            {
                AssetDatabase.CreateAsset(material, materialPath);
            }

            return material;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Materials");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Models");
            }
        }
    }
}
#endif
