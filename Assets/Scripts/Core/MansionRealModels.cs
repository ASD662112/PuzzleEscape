#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 把 Poly Haven 的 CC0 真实模型（FBX）放进程序化关卡：
    /// 统一站立姿态（源文件为 Z-up）、按目标高度缩放、落地对齐、生成碰撞体，
    /// 并用下载的真实 albedo / normal / metallic-smoothness 贴图创建 Standard 材质。
    /// </summary>
    internal static class MansionRealModels
    {
        private const string ModelRoot = "Assets/Art/Models";
        private const string MaterialRoot = "Assets/Art/Materials/Models";

        private static readonly Dictionary<string, GameObject> PrefabCache =
            new Dictionary<string, GameObject>();

        private static readonly Dictionary<string, Material> MaterialCache =
            new Dictionary<string, Material>();

        public static bool Has(string modelId)
        {
            return LoadPrefab(modelId) != null;
        }

        /// <param name="parent">父节点（房间根节点）</param>
        /// <param name="modelId">Assets/Art/Models 下的模型文件夹名</param>
        /// <param name="worldPosition">目标世界坐标，用于对齐水平中心与落地高度</param>
        /// <param name="yaw">与程序化摆件一致的朝向</param>
        /// <param name="targetHeight">目标世界高度（米）</param>
        /// <param name="addCollider">是否根据网格包围盒生成 BoxCollider</param>
        /// <param name="yawOffset">模型自身正面的朝向补偿</param>
        /// <param name="topWorldY">若提供，则按顶部对齐（用于吊灯悬挂）</param>
        /// <param name="standUp">额外绕 X 轴 -90° 立起（Poly Haven FBX 已自带轴向修正，默认不需要）</param>
        /// <param name="targetSize">若提供，按容器本地轴 (宽, 高, 深) 精确缩放；用于餐桌/书桌/书架等需要匹配原布局的家具</param>
        /// <param name="overrideMaterials">可选的多材质列表（按子网格槽位分配）；用于书脊/书页等多材质模型</param>
        /// <param name="fitInside">配合 targetSize 使用：等比缩放到目标包围盒内部，避免非等比拉伸</param>
        public static GameObject Spawn(Transform parent, string modelId, Vector3 worldPosition, float yaw,
            float targetHeight, bool addCollider, float yawOffset = 0f, float? topWorldY = null,
            bool standUp = false, Vector3? targetSize = null, Material[] overrideMaterials = null,
            bool fitInside = false)
        {
            GameObject prefab = LoadPrefab(modelId);
            if (prefab == null)
            {
                return null;
            }

            GameObject model = Object.Instantiate(prefab);
            model.name = modelId + "_Model";

            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(model))
                {
                    PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }
            }
            catch
            {
                // 直接 Instantiate 的模型即使不是 prefab 实例也不影响后续布置。
            }

            // 用独立的容器承担朝向与缩放，保留模型自身的导入旋转/缩放，
            // 避免把部分 FBX 自带的 -90° 轴向修正覆盖掉。
            var container = new GameObject("Real_" + modelId);
            container.transform.SetParent(parent, false);
            Quaternion baseRotation = parent != null ? parent.rotation : Quaternion.identity;
            Quaternion axisFix = standUp ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            container.transform.rotation = baseRotation *
                                           Quaternion.Euler(0f, yaw + yawOffset, 0f) *
                                           axisFix;
            container.transform.position = worldPosition;

            model.transform.SetParent(container.transform, false);
            model.transform.localPosition = Vector3.zero;

            foreach (Camera cameraComponent in model.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(cameraComponent.gameObject);
            }

            foreach (Light lightComponent in model.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(lightComponent);
            }

            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                Object.DestroyImmediate(animator);
            }

            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            Material singleMaterial = overrideMaterials == null || overrideMaterials.Length == 0
                ? GetOrCreateMaterial(modelId)
                : null;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (singleMaterial != null || (overrideMaterials != null && overrideMaterials.Length > 0))
                {
                    int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                    var materials = new Material[slotCount];
                    for (int i = 0; i < slotCount; i++)
                    {
                        materials[i] = overrideMaterials != null && overrideMaterials.Length > 0
                            ? overrideMaterials[Mathf.Min(i, overrideMaterials.Length - 1)]
                            : singleMaterial;
                    }

                    renderer.sharedMaterials = materials;
                }

                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            Bounds localBounds = ComputeLocalBounds(container);
            if (localBounds.size.y <= 0.0001f || localBounds.size.x <= 0.0001f || localBounds.size.z <= 0.0001f)
            {
                Object.DestroyImmediate(container);
                return null;
            }

            if (targetSize.HasValue)
            {
                Vector3 size = localBounds.size;
                if (fitInside)
                {
                    float fitScale = Mathf.Min(
                        targetSize.Value.x / size.x,
                        targetSize.Value.y / size.y,
                        targetSize.Value.z / size.z);
                    container.transform.localScale = Vector3.one * fitScale;
                }
                else
                {
                    container.transform.localScale = new Vector3(
                        targetSize.Value.x / size.x,
                        targetSize.Value.y / size.y,
                        targetSize.Value.z / size.z);
                }
            }
            else
            {
                Bounds worldBounds = ComputeWorldBounds(container);
                float scale = targetHeight / worldBounds.size.y;
                container.transform.localScale = Vector3.one * scale;
            }

            Bounds finalWorldBounds = ComputeWorldBounds(container);
            float desiredMinY = topWorldY.HasValue
                ? topWorldY.Value - finalWorldBounds.size.y
                : worldPosition.y;
            Vector3 delta = new Vector3(worldPosition.x - finalWorldBounds.center.x, 0f,
                worldPosition.z - finalWorldBounds.center.z);
            delta.y = desiredMinY - finalWorldBounds.min.y;
            container.transform.position += delta;

            if (addCollider)
            {
                Bounds colliderBounds = ComputeLocalBounds(container);
                var box = container.AddComponent<BoxCollider>();
                box.size = colliderBounds.size;
                box.center = colliderBounds.center;
            }

            return container;
        }

        private static GameObject LoadPrefab(string modelId)
        {
            if (PrefabCache.TryGetValue(modelId, out GameObject cached))
            {
                return cached;
            }

            string path = ModelRoot + "/" + modelId + "/" + modelId + ".fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            PrefabCache[modelId] = prefab;
            return prefab;
        }

        private static Material GetOrCreateMaterial(string modelId)
        {
            if (MaterialCache.TryGetValue(modelId, out Material cached))
            {
                return cached;
            }

            string materialPath = MaterialRoot + "/" + modelId + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool created = false;

            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Legacy Shaders/Diffuse");
                }

                if (shader == null)
                {
                    return null;
                }

                EnsureMaterialFolder();
                material = new Material(shader);
                material.name = modelId;
                created = true;
            }

            ApplyModelMaterial(material, modelId);

#if UNITY_EDITOR
            EditorUtility.SetDirty(material);
#endif

            if (created)
            {
                AssetDatabase.CreateAsset(material, materialPath);
            }

            MaterialCache[modelId] = material;
            return material;
        }

        private static void ApplyModelMaterial(Material material, string modelId)
        {
            Color tint = Color.white;
            float gloss = 0.6f;
            float metallic = 0f;

            switch (modelId)
            {
                case "wooden_table_02":
                    // 餐桌/书桌的木色压暗，与庄园里的深色家具统一。
                    tint = new Color(0.66f, 0.55f, 0.46f);
                    gloss = 0.45f;
                    break;
                case "wooden_bookshelf_worn":
                    tint = new Color(0.82f, 0.78f, 0.72f);
                    gloss = 0.5f;
                    break;
                case "vintage_oil_lamp":
                    // 黄铜底座需要轻微金属感，否则在室内会显得像塑料。
                    gloss = 0.55f;
                    metallic = 0.35f;
                    break;
                case "industrial_wall_sconce":
                    gloss = 0.5f;
                    metallic = 0.4f;
                    break;
            }

            material.color = tint;
            material.enableInstancing = true;

            Texture2D albedo = LoadModelTexture(modelId, "Albedo", ".jpg");
            Texture2D normal = LoadModelTexture(modelId, "Normal", ".jpg");
            Texture2D packed = LoadModelTexture(modelId, "MetallicSmoothness", ".png");

            if (albedo != null)
            {
                material.mainTexture = albedo;
            }
            else
            {
                // 没有反照率时至少不要留成刺眼的白模：压成中性灰木色
                material.color = new Color(0.52f, 0.5f, 0.47f);
                Debug.LogWarning("[PuzzleEscape] 模型缺少贴图，已用灰色代替：" + modelId);
            }

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", Vector2.one);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 1f);
            }

            if (packed != null)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Glossiness", gloss);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", gloss);
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            material.DisableKeyword("_EMISSION");
        }

        private static Texture2D LoadModelTexture(string modelId, string suffix, string extension)
        {
            string folder = ModelRoot + "/" + modelId;

            // Poly Haven 的 FBX 导出通常叫 main_Albedo.jpg，旧资源可能叫 <模型名>_Albedo.jpg
            string[] candidates =
            {
                folder + "/" + modelId + "_" + suffix + extension,
                folder + "/main_" + suffix + extension
            };

            foreach (string path in candidates)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    return texture;
                }
            }

            // 兜底：在模型目录里找任意以 _Albedo / _Normal / _MetallicSmoothness 结尾的贴图
            if (AssetDatabase.IsValidFolder(folder))
            {
                string suffixWithExt = "_" + suffix + extension;

                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(suffixWithExt, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    }
                }
            }

            return null;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Materials");
            }

            if (!AssetDatabase.IsValidFolder(MaterialRoot))
            {
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Models");
            }
        }

        private static Bounds ComputeWorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            var bounds = new Bounds(root.transform.position, Vector3.zero);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static Bounds ComputeLocalBounds(GameObject root)
        {
            bool hasBounds = false;
            var bounds = new Bounds();
            Matrix4x4 rootToLocal = root.transform.worldToLocalMatrix;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 matrix = rootToLocal * filter.transform.localToWorldMatrix;
                Bounds meshBounds = mesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        meshBounds.center.x + (((i & 1) == 0) ? -meshBounds.extents.x : meshBounds.extents.x),
                        meshBounds.center.y + (((i & 2) == 0) ? -meshBounds.extents.y : meshBounds.extents.y),
                        meshBounds.center.z + (((i & 4) == 0) ? -meshBounds.extents.z : meshBounds.extents.z));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);

                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return bounds;
        }
    }
}
#endif
