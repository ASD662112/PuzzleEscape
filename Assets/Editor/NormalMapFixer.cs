#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 把 Assets/Art/Models 下所有 *_Normal 贴图标记为法线贴图（Normal Map）。
    /// 部分 Poly Haven 模型导入时贴图类型仍是 Default / sRGB，会导致材质报警告且法线失效。
    /// 菜单：PuzzleEscape → 修复法线贴图导入设置
    /// </summary>
    public static class NormalMapFixer
    {
        private const string ModelRoot = "Assets/Art/Models";

        [MenuItem("PuzzleEscape/修复法线贴图导入设置", false, 42)]
        public static void FixAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ModelRoot });
            var fixedPaths = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!fileName.EndsWith("_Normal") && !fileName.EndsWith("_normal") &&
                    !fileName.EndsWith("_NormalMap"))
                {
                    continue;
                }

                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    continue;
                }

                if (importer.textureType == TextureImporterType.NormalMap &&
                    !importer.sRGBTexture)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();

                fixedPaths.Add(path);
            }

            AssetDatabase.Refresh();

            Debug.Log("[PuzzleEscape] 法线贴图修复完成，共处理 " + fixedPaths.Count + " 张：" +
                      (fixedPaths.Count > 0 ? string.Join("；", fixedPaths) : "无需修复"));
        }
    }
}
#endif
