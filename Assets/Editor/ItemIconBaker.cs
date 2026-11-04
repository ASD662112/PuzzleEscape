#if UNITY_EDITOR
using System.IO;
using PuzzleEscape.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 用现有的 Resources/Items 模型预烘焙背包图标。
    /// 菜单：PuzzleEscape → 烘焙物品图标
    /// </summary>
    public static class ItemIconBaker
    {
        private const int IconSize = 192;
        private const string IconDirectory = "Assets/Resources/ItemIcons";

        [MenuItem("PuzzleEscape/烘焙物品图标")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(IconDirectory);

            int baked = 0;
            foreach (ItemDefinition item in ItemDatabase.All)
            {
                string prefabPath = "Assets/Resources/Items/" + item.Id + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning("[PuzzleEscape] 物品图标缺少预制体：" + prefabPath);
                    continue;
                }

                Texture2D icon = RenderIcon(prefab);
                if (icon == null)
                {
                    continue;
                }

                string path = IconDirectory + "/" + item.Id + ".png";
                File.WriteAllBytes(path, icon.EncodeToPNG());
                Object.DestroyImmediate(icon);
                baked++;
            }

            AssetDatabase.Refresh();
            ConfigureImporters();
            AssetDatabase.SaveAssets();
            Debug.Log("[PuzzleEscape] 物品图标烘焙完成：" + baked + " / " + ItemDatabase.All.Count + "。");
        }

        private static Texture2D RenderIcon(GameObject prefab)
        {
            var root = new GameObject("ItemIconBakerRig");
            root.transform.position = new Vector3(0f, -1000f, 0f);

            var pivot = new GameObject("IconPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localRotation = Quaternion.Euler(16f, -28f, 0f);

            GameObject model = Object.Instantiate(prefab, pivot);
            model.transform.localPosition = Vector3.zero;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(root);
                return null;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            // 先把模型中心移到 pivot 原点，再统一缩放，保证任意尺寸的物品都居中。
            Vector3 centerLocal = pivot.InverseTransformPoint(worldBounds.center);
            model.transform.localPosition -= centerLocal;

            float maxDimension = Mathf.Max(worldBounds.size.x,
                Mathf.Max(worldBounds.size.y, worldBounds.size.z));
            float scale = maxDimension > 0.0001f ? 1.5f / maxDimension : 1f;
            pivot.localScale = Vector3.one * scale;

            SetLayerRecursively(root, 31);

            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            var cameraGo = new GameObject("IconCamera");
            cameraGo.transform.SetParent(root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -2.4f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.022f, 0.03f, 0f);
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var renderTexture = new RenderTexture(IconSize, IconSize, 16,
                RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = 1;
            renderTexture.Create();
            camera.targetTexture = renderTexture;

            CreateLight(root.transform, "IconKeyLight", LightType.Directional,
                new Color(1f, 0.94f, 0.86f, 1f), 1.8f, 10f,
                Quaternion.Euler(35f, -35f, 0f), Vector3.zero);
            CreateLight(root.transform, "IconFillLight", LightType.Point,
                new Color(0.72f, 0.82f, 1f, 1f), 2.6f, 6f,
                Quaternion.identity, new Vector3(1.4f, 0.6f, 1.2f));
            CreateLight(root.transform, "IconRimLight", LightType.Point,
                new Color(0.5f, 0.89f, 0.76f, 1f), 1.4f, 5f,
                Quaternion.identity, new Vector3(-1.3f, 0.6f, 0.9f));

            AmbientMode oldMode = RenderSettings.ambientMode;
            Color oldAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

            Texture2D icon = null;
            try
            {
                camera.Render();
                RenderTexture.active = renderTexture;
                icon = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                icon.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                icon.Apply();
                RenderTexture.active = null;
            }
            finally
            {
                RenderSettings.ambientMode = oldMode;
                RenderSettings.ambientLight = oldAmbient;
                camera.targetTexture = null;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(root);
            }

            return icon;
        }

        private static void CreateLight(Transform parent, string name, LightType type,
            Color color, float intensity, float range, Quaternion rotation, Vector3 position)
        {
            var lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = position;
            lightGo.transform.localRotation = rotation;
            var light = lightGo.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.cullingMask = 1 << 31;
            light.shadows = LightShadows.None;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }

        private static void ConfigureImporters()
        {
            foreach (ItemDefinition item in ItemDatabase.All)
            {
                string path = IconDirectory + "/" + item.Id + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
