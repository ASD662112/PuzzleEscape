using System.Collections.Generic;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 主界面的地图选择面板。地图清单来自 <see cref="GameMapCatalog"/>，
    /// 选中后写入 PlayerPrefs，主界面文案立刻跟随，点「开始游戏」时进入对应场景。
    /// </summary>
    public class MapSelectUI : MonoBehaviour
    {
        public static MapSelectUI Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private static readonly Color Accent = new Color(0.5f, 0.89f, 0.76f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.94f, 0.96f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.6f, 0.66f, 1f);
        private static readonly Color RowColor = new Color(0.065f, 0.085f, 0.105f, 0.98f);

        private GameObject overlay;
        private Text statusText;
        private readonly List<Text> rowTitles = new List<Text>();
        private readonly List<Text> rowDescriptions = new List<Text>();
        private readonly List<Button> rowButtons = new List<Button>();
        private readonly List<Text> rowButtonLabels = new List<Text>();

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
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Build()
        {
            var canvasGo = new GameObject("MapSelectCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 26;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            UIFactory.Stretch((RectTransform)canvasGo.transform);

            var backdrop = UIFactory.CreateImage(canvasGo.transform, "MapSelectBackdrop",
                new Color(0f, 0f, 0f, 0.68f), true);
            UIFactory.Stretch(backdrop.rectTransform);
            overlay = backdrop.gameObject;

            var card = UIFactory.CreateImage(backdrop.rectTransform, "MapSelectCard",
                new Color(0.04f, 0.06f, 0.075f, 0.99f), true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(800f, 620f));

            var accent = UIFactory.CreateImage(card.rectTransform, "MapSelectAccent", Accent);
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "MapSelectTitle", "选择地图", 40,
                TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -26f), new Vector2(300f, 52f));

            var subtitle = UIFactory.CreateText(card.rectTransform, "MapSelectSubtitle",
                "选中后主界面文案会切换，点「开始游戏」进入该地图", 18, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(42f, -82f), new Vector2(620f, 28f));

            var closeButton = UIFactory.CreateButton(card.rectTransform, "MapSelectClose", "关闭",
                new Color(0.11f, 0.15f, 0.18f), Muted, Close, 17);
            UIFactory.Place(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(84f, 36f));

            for (int i = 0; i < GameMapCatalog.Maps.Length; i++)
            {
                BuildRow(card.rectTransform, i);
            }

            var note = UIFactory.CreateText(card.rectTransform, "MapSelectNote",
                "更多地图将在现有两张地图内容完成后加入。", 16, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(42f, 62f), new Vector2(620f, 26f));

            statusText = UIFactory.CreateText(card.rectTransform, "MapSelectStatus", string.Empty,
                17, Accent, TextAnchor.MiddleCenter);
            UIFactory.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(700f, 28f));

            overlay.SetActive(false);
            Debug.Log("[PuzzleEscape] 地图选择面板已构建，可选地图 " + GameMapCatalog.Maps.Length + " 张。");
        }

        private void BuildRow(RectTransform card, int index)
        {
            int captured = index;
            GameMap map = GameMapCatalog.Maps[index];
            float y = -122f - index * 104f;

            var row = UIFactory.CreateImage(card, "MapSelectRow" + (index + 1), RowColor, true);
            UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, y), new Vector2(720f, 92f));

            var rowTitle = UIFactory.CreateText(row.rectTransform, "MapSelectRowTitle" + (index + 1),
                map.Title, 26, TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(rowTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -14f), new Vector2(360f, 34f));
            rowTitles.Add(rowTitle);

            var rowDescription = UIFactory.CreateText(row.rectTransform,
                "MapSelectRowDescription" + (index + 1), map.Description, 15, Muted,
                TextAnchor.UpperLeft);
            UIFactory.Place(rowDescription.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -50f), new Vector2(470f, 36f));
            rowDescription.lineSpacing = 1.1f;
            rowDescriptions.Add(rowDescription);

            var selectButton = UIFactory.CreateButton(row.rectTransform,
                "MapSelectRowButton" + (index + 1), "选择", Accent,
                new Color(0.03f, 0.09f, 0.07f), () => OnSelect(captured), 18);
            UIFactory.Place(selectButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(148f, 44f));
            rowButtons.Add(selectButton);
            rowButtonLabels.Add(selectButton.GetComponentInChildren<Text>());
        }

        public void Show()
        {
            IsOpen = true;
            overlay.SetActive(true);
            statusText.text = string.Empty;
            RefreshRows();
            UIRoot.Instance?.NotifyExternalModalChanged();
            AudioManager.Instance?.PlayClick();
            Debug.Log("[PuzzleEscape] 地图选择面板已打开，当前地图：" + GameMapCatalog.Current.Title);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            overlay.SetActive(false);
            UIRoot.Instance?.NotifyExternalModalChanged();
            AudioManager.Instance?.PlayClick();
        }

        private void OnSelect(int index)
        {
            GameMap map = GameMapCatalog.Maps[index];
            if (!map.Available)
            {
                statusText.text = "「" + map.Title + "」尚未完成。";
                return;
            }

            if (map.Id == GameMapCatalog.CurrentId)
            {
                statusText.text = "已经是当前地图。";
                return;
            }

            GameMapCatalog.Select(map.Id);
            statusText.text = "已选择「" + map.Title + "」，点「开始游戏」进入。";
            RefreshRows();
            UIRoot.Instance?.ApplySelectedMap();
            AudioManager.Instance?.PlayClick();
            Debug.Log("[PuzzleEscape] 地图已切换：" + map.Title);
        }

        private void RefreshRows()
        {
            GameMap current = GameMapCatalog.Current;

            for (int i = 0; i < GameMapCatalog.Maps.Length; i++)
            {
                GameMap map = GameMapCatalog.Maps[i];
                bool selected = map.Id == current.Id;
                bool canSelect = map.Available && !selected;

                rowTitles[i].text = map.Title + (selected ? "  ·  当前地图" : string.Empty);
                rowTitles[i].color = map.Available ? TextColor : Muted;
                rowDescriptions[i].text = map.Available
                    ? map.Description
                    : "该地图尚未制作，完成后可选。";
                rowDescriptions[i].color = map.Available ? Muted : new Color(0.45f, 0.5f, 0.55f, 1f);

                rowButtons[i].interactable = canSelect;
                rowButtonLabels[i].text = selected ? "已选择" : (map.Available ? "选择" : "未开放");
                rowButtonLabels[i].color = selected ? Muted : Accent;
            }
        }
    }
}
