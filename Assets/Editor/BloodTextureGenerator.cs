#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 程序化生成真实感的血迹贴图（带 alpha 的飞溅/血泊），
    /// 供疗养院的血迹贴片使用。生成结果保存为 PNG 资源，可直接在材质里调。
    /// 菜单：PuzzleEscape → 生成血迹贴图
    /// </summary>
    public static class BloodTextureGenerator
    {
        private const string Folder = "Assets/Art/Textures/Generated";
        private const int Size = 512;

        public const string PoolPath = Folder + "/BloodPool.png";
        public const string SplatterPath = Folder + "/BloodSplatter.png";

        [MenuItem("PuzzleEscape/生成血迹贴图", false, 44)]
        public static void GenerateAll()
        {
            EnsureFolder();
            GeneratePool();
            GenerateSplatter();
            AssetDatabase.Refresh();
            Debug.Log("[PuzzleEscape] 血迹贴图已生成：" + PoolPath + " / " + SplatterPath);
        }

        /// <summary>场景生成时自动确保贴图存在，并且导入设置是带 alpha 的正确版本。</summary>
        public static void Ensure()
        {
            if (!File.Exists(PoolPath) || !File.Exists(SplatterPath) ||
                NeedsRegenerate(PoolPath) || NeedsRegenerate(SplatterPath))
            {
                GenerateAll();
            }
        }

        /// <summary>旧版本的贴图背景被填成浅灰（Alpha Is Transparency / 压缩丢了 alpha），需要重新生成。</summary>
        private static bool NeedsRegenerate(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            return importer == null ||
                   importer.alphaIsTransparency ||
                   importer.textureCompression != TextureImporterCompression.Uncompressed;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Textures"))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Textures");
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Art/Textures", "Generated");
            }
        }

        private static void GeneratePool()
        {
            var blobs = new List<Vector4>();   // x, y, radius, strength

            // 主血泊：中心一大团 + 若干偏移的圆，边缘不规则
            blobs.Add(new Vector4(0.5f, 0.5f, 0.30f, 1f));

            for (int i = 0; i < 7; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(0.06f, 0.22f);

                blobs.Add(new Vector4(
                    0.5f + Mathf.Cos(angle) * distance,
                    0.5f + Mathf.Sin(angle) * distance,
                    Random.Range(0.1f, 0.22f),
                    Random.Range(0.55f, 0.95f)));
            }

            WriteTexture(PoolPath, blobs, 1f);
        }

        private static void GenerateSplatter()
        {
            var blobs = new List<Vector4>();
            blobs.Add(new Vector4(0.5f, 0.5f, 0.16f, 0.95f));

            // 向外飞溅的血点，越远越小
            for (int i = 0; i < 150; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Mathf.Pow(Random.Range(0.05f, 0.46f), 0.85f);

                blobs.Add(new Vector4(
                    0.5f + Mathf.Cos(angle) * distance,
                    0.5f + Mathf.Sin(angle) * distance * Random.Range(0.7f, 1f),
                    Random.Range(0.005f, 0.03f) * (1.1f - distance),
                    Random.Range(0.5f, 1f)));
            }

            WriteTexture(SplatterPath, blobs, 0.95f);
        }

        /// <summary>把若干圆形血团叠加成一张带 alpha 的贴图。</summary>
        private static void WriteTexture(string path, List<Vector4> blobs, float maxAlpha)
        {
            var pixels = new Color32[Size * Size];
            var alpha = new float[Size * Size];

            foreach (Vector4 blob in blobs)
            {
                float radius = blob.z;
                int minX = Mathf.Max(0, Mathf.FloorToInt((blob.x - radius) * Size));
                int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt((blob.x + radius) * Size));
                int minY = Mathf.Max(0, Mathf.FloorToInt((blob.y - radius) * Size));
                int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt((blob.y + radius) * Size));

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = (x / (float)Size) - blob.x;
                        float dy = (y / (float)Size) - blob.y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        if (dist >= radius)
                        {
                            continue;
                        }

                        float falloff = Mathf.Pow(1f - dist / radius, 1.6f);
                        alpha[y * Size + x] += falloff * blob.w;
                    }
                }
            }

            for (int i = 0; i < alpha.Length; i++)
            {
                if (alpha[i] <= 0.01f)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // 用低频噪声打散边缘，避免看起来像纯圆
                int x = i % Size;
                int y = i / Size;
                float noise = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);
                float a = Mathf.Clamp01(alpha[i] * Mathf.Lerp(0.65f, 1.15f, noise)) * maxAlpha;

                float shade = Mathf.Lerp(0.7f, 1f, noise);
                pixels[i] = new Color32(
                    (byte)(78 * shade),
                    (byte)(10 * shade),
                    (byte)(9 * shade),
                    (byte)(Mathf.Clamp01(a) * 255));
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            texture.SetPixels32(pixels);
            texture.Apply(true);

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                // 必须关闭 Alpha Is Transparency：它会把全透明区域填成浅灰/白，
                // 贴片一旦按不透明渲染就会看到一块淡灰色方块。
                importer.alphaIsTransparency = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 512;
                // 用未压缩的 RGBA，避免 DXT1 把 alpha 丢掉
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
