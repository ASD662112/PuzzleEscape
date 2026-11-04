using PuzzleEscape.Audio;
using PuzzleEscape.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 背包道具检查面板：图标、名称、描述，以及可拖拽旋转的 3D 实物预览。
    /// 预览使用 Resources/Items 下的真实模型预制体，缺少预制体时回退为彩色图标。
    /// </summary>
    public class ItemDetailUI : MonoBehaviour
    {
        private const int PreviewLayer = 31;

        public static ItemDetailUI Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private GameObject overlay;
        private Image icon;
        private RawImage previewImage;
        private RectTransform previewRect;
        private Text previewHint;
        private Text title;
        private Text meta;
        private Text description;
        private Text lore;

        private RenderTexture previewTexture;
        private Camera previewCamera;
        private Transform previewRig;
        private Transform previewModel;
        private Transform previewSource;

        private float previewYaw = 24f;
        private float previewPitch = -8f;
        private float previewZoom = 1f;
        private bool dragging;
        private Vector3 lastPointer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            ReleasePreview();
            if (previewRig != null)
            {
                Destroy(previewRig.gameObject);
            }

            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
                previewTexture = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            UpdatePreviewInput();
        }

        private void Build()
        {
            var canvasGo = new GameObject("ItemDetailCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            UIFactory.Stretch((RectTransform)canvasGo.transform);

            var backdrop = UIFactory.CreateImage(canvasGo.transform, "ItemDetailBackdrop",
                new Color(0f, 0f, 0f, 0.62f), true);
            UIFactory.Stretch(backdrop.rectTransform);
            overlay = backdrop.gameObject;

            var card = UIFactory.CreateImage(backdrop.rectTransform, "ItemDetailCard",
                new Color(0.04f, 0.06f, 0.075f, 0.99f), true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(620f, 560f));

            var accent = UIFactory.CreateImage(card.rectTransform, "ItemDetailAccent",
                new Color(0.5f, 0.89f, 0.76f, 1f));
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var header = UIFactory.CreateText(card.rectTransform, "ItemDetailHeader", "物品检查", 26,
                new Color(0.55f, 0.6f, 0.66f, 1f), TextAnchor.MiddleLeft);
            UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -26f), new Vector2(280f, 34f));

            var closeButton = UIFactory.CreateButton(card.rectTransform, "ItemDetailClose", "关闭",
                new Color(0.11f, 0.15f, 0.18f), new Color(0.55f, 0.6f, 0.66f, 1f), Close, 17);
            UIFactory.Place(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(80f, 34f));

            var previewBox = UIFactory.CreateImage(card.rectTransform, "ItemDetailPreviewBox",
                new Color(0.02f, 0.03f, 0.04f, 1f), false);
            UIFactory.Place(previewBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -86f), new Vector2(180f, 180f));
            previewRect = previewBox.rectTransform;

            var rawRect = UIFactory.CreateRect(previewBox.rectTransform, "ItemDetailPreview");
            UIFactory.Stretch(rawRect);
            previewImage = rawRect.gameObject.AddComponent<RawImage>();
            previewImage.raycastTarget = false;
            previewImage.color = Color.white;

            var iconImage = UIFactory.CreateImage(previewBox.rectTransform, "ItemDetailIcon",
                Color.white, false);
            UIFactory.Place(iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(96f, 96f));
            iconImage.preserveAspect = true;
            icon = iconImage;

            previewHint = UIFactory.CreateText(previewBox.rectTransform, "ItemDetailPreviewHint",
                "拖动旋转 · 滚轮缩放", 13, new Color(0.55f, 0.6f, 0.66f, 0.8f), TextAnchor.LowerCenter);
            UIFactory.Place(previewHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(174f, 20f));

            title = UIFactory.CreateText(card.rectTransform, "ItemDetailTitle", string.Empty, 30,
                new Color(0.91f, 0.94f, 0.96f, 1f), TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(232f, -98f), new Vector2(350f, 44f));

            meta = UIFactory.CreateText(card.rectTransform, "ItemDetailMeta", string.Empty, 16,
                new Color(0.55f, 0.6f, 0.66f, 1f), TextAnchor.MiddleLeft);
            UIFactory.Place(meta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(234f, -144f), new Vector2(344f, 26f));

            var bodyBox = UIFactory.CreateImage(card.rectTransform, "ItemDetailBodyBox",
                new Color(0.025f, 0.035f, 0.045f, 1f), false);
            UIFactory.Place(bodyBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(36f, 196f), new Vector2(548f, 108f));

            description = UIFactory.CreateText(bodyBox.rectTransform, "ItemDetailDescription",
                string.Empty, 19, new Color(0.78f, 0.82f, 0.86f, 1f), TextAnchor.UpperLeft);
            UIFactory.Stretch(description.rectTransform);
            description.rectTransform.offsetMin = new Vector2(16f, 12f);
            description.rectTransform.offsetMax = new Vector2(-16f, -12f);
            description.lineSpacing = 1.2f;

            var divider = UIFactory.CreateImage(card.rectTransform, "ItemDetailDivider",
                new Color(0.18f, 0.24f, 0.28f, 1f), false);
            UIFactory.Place(divider.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(36f, 182f), new Vector2(548f, 2f));

            var loreBox = UIFactory.CreateImage(card.rectTransform, "ItemDetailLoreBox",
                new Color(0.02f, 0.028f, 0.036f, 1f), false);
            UIFactory.Place(loreBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(36f, 56f), new Vector2(548f, 116f));

            var loreTag = UIFactory.CreateText(loreBox.rectTransform, "ItemDetailLoreTag", "档案记录", 13,
                new Color(0.5f, 0.89f, 0.76f, 0.75f), TextAnchor.UpperLeft);
            UIFactory.Place(loreTag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -10f), new Vector2(200f, 18f));

            lore = UIFactory.CreateText(loreBox.rectTransform, "ItemDetailLore",
                string.Empty, 16, new Color(0.6f, 0.65f, 0.69f, 1f), TextAnchor.UpperLeft);
            lore.rectTransform.anchorMin = Vector2.zero;
            lore.rectTransform.anchorMax = Vector2.one;
            lore.rectTransform.offsetMin = new Vector2(16f, 10f);
            lore.rectTransform.offsetMax = new Vector2(-16f, -32f);
            lore.lineSpacing = 1.2f;
            lore.fontStyle = FontStyle.Italic;

            BuildPreviewRig();
            overlay.SetActive(false);
        }

        private void BuildPreviewRig()
        {
            // 预览只需要 256×256；不使用 MSAA，保证 RawImage 与截图读回都能拿到有效像素。
            previewTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            previewTexture.name = "ItemPreviewTexture";
            previewTexture.antiAliasing = 1;
            previewTexture.Create();

            previewRig = new GameObject("ItemPreviewRig").transform;
            previewRig.SetParent(transform, false);
            previewRig.position = new Vector3(0f, -1000f, 0f);

            var cameraGo = new GameObject("ItemPreviewCamera");
            cameraGo.transform.SetParent(previewRig, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -3f);
            cameraGo.transform.localRotation = Quaternion.identity;
            previewCamera = cameraGo.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.015f, 0.022f, 0.03f, 1f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 10f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.targetTexture = previewTexture;
            previewCamera.enabled = false;

            CreatePreviewLight("PreviewKey", LightType.Directional, new Color(1f, 0.94f, 0.86f, 1f),
                1.15f, Quaternion.Euler(35f, -35f, 0f), new Vector3(0f, 0f, 0f));
            CreatePreviewLight("PreviewFill", LightType.Point, new Color(0.62f, 0.75f, 0.9f, 1f),
                1.6f, Quaternion.identity, new Vector3(1.4f, 0.6f, 1.2f));
            CreatePreviewLight("PreviewRim", LightType.Point, new Color(0.5f, 0.89f, 0.76f, 1f),
                1.1f, Quaternion.identity, new Vector3(-1.3f, 0.8f, 0.9f));

            previewImage.texture = previewTexture;
        }

        private void CreatePreviewLight(string lightName, LightType type, Color color,
            float intensity, Quaternion rotation, Vector3 position)
        {
            var lightGo = new GameObject(lightName);
            lightGo.transform.SetParent(previewRig, false);
            lightGo.transform.localPosition = position;
            lightGo.transform.localRotation = rotation;
            var light = lightGo.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = type == LightType.Point ? 6f : 10f;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        public void Show(ItemDefinition item)
        {
            if (item == null)
            {
                return;
            }

            IsOpen = true;
            overlay.SetActive(true);
            Sprite itemIcon = ItemIconLibrary.Get(item.Id);
            icon.sprite = itemIcon;
            icon.color = itemIcon != null ? Color.white : item.Color;
            title.text = item.DisplayName;
            meta.text = (item.Unique ? "唯一物品" : "可重复杂物") + "  ·  " + item.Id;
            description.text = item.Description;
            lore.text = string.IsNullOrEmpty(item.Lore) ? "（没有找到关于这件东西的记录）" : item.Lore;
            BuildPreview(item);
            UIRoot.Instance?.NotifyExternalModalChanged();
            AudioManager.Instance?.PlayClick();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            ReleasePreview();
            if (overlay != null)
            {
                overlay.SetActive(false);
            }

            UIRoot.Instance?.NotifyExternalModalChanged();
            AudioManager.Instance?.PlayClick();
        }

        private void BuildPreview(ItemDefinition item)
        {
            ReleasePreview();
            previewYaw = 24f;
            previewPitch = -8f;
            previewZoom = 1f;
            previewCamera.orthographicSize = 1f;

            GameObject prefab = Resources.Load<GameObject>("Items/" + item.Id);
            if (prefab == null)
            {
                previewImage.gameObject.SetActive(false);
                icon.gameObject.SetActive(true);
                previewHint.text = string.Empty;
                return;
            }

            var pivotGo = new GameObject("PreviewPivot");
            pivotGo.transform.SetParent(previewRig, false);
            pivotGo.transform.localPosition = Vector3.zero;
            pivotGo.transform.localRotation = Quaternion.identity;
            pivotGo.transform.localScale = Vector3.one;

            GameObject model = Instantiate(prefab, pivotGo.transform);
            model.name = "PreviewModel";
            SetLayerRecursively(model, PreviewLayer);

            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Collider modelCollider in model.GetComponentsInChildren<Collider>(true))
            {
                modelCollider.enabled = false;
            }

            float fitScale = 1.55f;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds worldBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    worldBounds.Encapsulate(renderers[i].bounds);
                }

                Vector3 localCenter = pivotGo.transform.InverseTransformPoint(worldBounds.center);
                model.transform.localPosition -= localCenter;

                float maxDimension = Mathf.Max(worldBounds.size.x,
                    Mathf.Max(worldBounds.size.y, worldBounds.size.z));
                if (maxDimension > 0.0001f)
                {
                    fitScale = 1.55f / maxDimension;
                }
            }

            pivotGo.transform.localScale = Vector3.one * fitScale;
            previewModel = pivotGo.transform;
            previewSource = model.transform;
            ApplyPreviewRotation();

            previewImage.gameObject.SetActive(true);
            icon.gameObject.SetActive(false);
            previewHint.text = "拖动旋转 · 滚轮缩放";
            previewCamera.enabled = true;
        }

        private void ReleasePreview()
        {
            if (previewCamera != null)
            {
                previewCamera.enabled = false;
            }

            if (previewModel != null)
            {
                Destroy(previewModel.gameObject);
                previewModel = null;
            }
            else if (previewSource != null)
            {
                Destroy(previewSource.gameObject);
            }

            previewSource = null;
        }

        private void UpdatePreviewInput()
        {
            if (previewModel == null || previewRect == null)
            {
                return;
            }

            bool overPreview = RectTransformUtility.RectangleContainsScreenPoint(
                previewRect, Input.mousePosition, null);

            if (overPreview && Input.GetMouseButtonDown(0))
            {
                dragging = true;
                lastPointer = Input.mousePosition;
            }

            if (dragging && Input.GetMouseButton(0))
            {
                Vector3 pointer = Input.mousePosition;
                Vector3 delta = pointer - lastPointer;
                lastPointer = pointer;

                previewYaw += delta.x * 0.45f;
                previewPitch = Mathf.Clamp(previewPitch - delta.y * 0.45f, -80f, 80f);
            }

            if (Input.GetMouseButtonUp(0))
            {
                dragging = false;
            }

            if (overPreview && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                previewZoom = Mathf.Clamp(previewZoom + Input.mouseScrollDelta.y * 0.12f, 0.65f, 1.65f);
                previewCamera.orthographicSize = 1f / previewZoom;
            }

            if (!dragging)
            {
                previewYaw += 18f * Time.unscaledDeltaTime;
            }

            ApplyPreviewRotation();
        }

        private void ApplyPreviewRotation()
        {
            if (previewModel != null)
            {
                previewModel.localRotation = Quaternion.Euler(previewPitch, previewYaw, 0f);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }
    }
}
