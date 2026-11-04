using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 三槽位存档面板：从主界面读取、从设置保存，也可删除指定槽位。
    /// </summary>
    public class SaveSlotUI : MonoBehaviour
    {
        public static SaveSlotUI Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private const int SlotCount = SaveGameService.SlotCount;
        private static readonly Color Accent = new Color(0.5f, 0.89f, 0.76f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.94f, 0.96f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.6f, 0.66f, 1f);
        private static readonly Color RowColor = new Color(0.065f, 0.085f, 0.105f, 0.98f);
        private static readonly Color DeleteColor = new Color(0.24f, 0.11f, 0.12f, 1f);

        private GameObject overlay;
        private Text subtitleText;
        private Text statusText;
        private readonly Text[] slotTitles = new Text[SlotCount];
        private readonly Text[] slotSummaries = new Text[SlotCount];
        private readonly Button[] primaryButtons = new Button[SlotCount];
        private readonly Text[] primaryLabels = new Text[SlotCount];
        private readonly Button[] deleteButtons = new Button[SlotCount];
        private readonly Text[] deleteLabels = new Text[SlotCount];
        private bool loadMode;
        private int pendingDeleteSlot = -1;

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
            var canvasGo = new GameObject("SaveSlotCanvas", typeof(RectTransform));
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

            var backdrop = UIFactory.CreateImage(canvasGo.transform, "SaveSlotBackdrop",
                new Color(0f, 0f, 0f, 0.68f), true);
            UIFactory.Stretch(backdrop.rectTransform);
            overlay = backdrop.gameObject;

            var card = UIFactory.CreateImage(backdrop.rectTransform, "SaveSlotCard",
                new Color(0.04f, 0.06f, 0.075f, 0.99f), true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(880f, 620f));

            var accent = UIFactory.CreateImage(card.rectTransform, "SaveSlotAccent", Accent);
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "SaveSlotTitle", "存档", 40,
                TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -26f), new Vector2(260f, 52f));

            subtitleText = UIFactory.CreateText(card.rectTransform, "SaveSlotSubtitle", string.Empty,
                18, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(42f, -82f), new Vector2(600f, 28f));

            var closeButton = UIFactory.CreateButton(card.rectTransform, "SaveSlotClose", "关闭",
                new Color(0.11f, 0.15f, 0.18f), Muted, Close, 17);
            UIFactory.Place(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(84f, 36f));

            for (int i = 0; i < SlotCount; i++)
            {
                BuildSlotRow(card.rectTransform, i);
            }

            statusText = UIFactory.CreateText(card.rectTransform, "SaveSlotStatus", string.Empty,
                17, Accent, TextAnchor.MiddleCenter);
            UIFactory.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(760f, 30f));

            overlay.SetActive(false);
        }

        private void BuildSlotRow(RectTransform card, int slot)
        {
            int captured = slot;
            float y = -126f - slot * 132f;

            var row = UIFactory.CreateImage(card, "SaveSlotRow" + (slot + 1), RowColor, true);
            UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, y), new Vector2(800f, 116f));

            var slotTitle = UIFactory.CreateText(row.rectTransform, "SaveSlotTitle" + (slot + 1),
                "槽位 " + (slot + 1), 24, TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(slotTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -12f), new Vector2(260f, 34f));
            slotTitles[slot] = slotTitle;

            var summary = UIFactory.CreateText(row.rectTransform, "SaveSlotSummary" + (slot + 1),
                string.Empty, 16, Muted, TextAnchor.UpperLeft);
            UIFactory.Place(summary.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -52f), new Vector2(500f, 52f));
            summary.lineSpacing = 1.1f;
            slotSummaries[slot] = summary;

            var primary = UIFactory.CreateButton(row.rectTransform, "SaveSlotPrimary" + (slot + 1),
                "读取", Accent, new Color(0.03f, 0.09f, 0.07f), () => OnPrimary(captured), 18);
            UIFactory.Place(primary.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(148f, 44f));
            primaryButtons[slot] = primary;
            primaryLabels[slot] = primary.GetComponentInChildren<Text>();

            var delete = UIFactory.CreateButton(row.rectTransform, "SaveSlotDelete" + (slot + 1),
                "删除", DeleteColor, new Color(0.88f, 0.62f, 0.62f, 1f), () => OnDelete(captured), 17);
            UIFactory.Place(delete.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-182f, -18f), new Vector2(136f, 44f));
            deleteButtons[slot] = delete;
            deleteLabels[slot] = delete.GetComponentInChildren<Text>();
        }

        public void Show(bool load)
        {
            loadMode = load;
            pendingDeleteSlot = -1;
            IsOpen = true;
            overlay.SetActive(true);
            subtitleText.text = load ? "选择要读取的存档槽位" : "选择要保存到的存档槽位";
            statusText.text = load ? "空槽位不可读取" : "保存会覆盖该槽位原有进度";
            RefreshRows();
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
            pendingDeleteSlot = -1;
            overlay.SetActive(false);
            UIRoot.Instance?.NotifyExternalModalChanged();
            AudioManager.Instance?.PlayClick();
        }

        private void OnPrimary(int slot)
        {
            pendingDeleteSlot = -1;

            if (loadMode)
            {
                if (!SaveGameService.HasSave(slot))
                {
                    statusText.text = "槽位 " + (slot + 1) + " 还没有存档。";
                    RefreshRows();
                    return;
                }

                Close();
                SaveGameService.LoadFromMenu(slot);
                return;
            }

            GameManager game = GameManager.Instance;
            if (game == null || !game.HasStarted || game.IsWon)
            {
                statusText.text = "尚未进入游戏，无法保存。";
                RefreshRows();
                return;
            }

            SaveGameService.SaveGame(slot, false);
            statusText.text = "已保存到槽位 " + (slot + 1) + "。";
            RefreshRows();
            AudioManager.Instance?.PlaySuccess();
        }

        private void OnDelete(int slot)
        {
            if (!SaveGameService.HasSave(slot))
            {
                statusText.text = "槽位 " + (slot + 1) + " 是空的。";
                pendingDeleteSlot = -1;
                RefreshRows();
                return;
            }

            if (pendingDeleteSlot != slot)
            {
                pendingDeleteSlot = slot;
                statusText.text = "再次点击「确认删除」会永久删除槽位 " + (slot + 1) + "。";
                RefreshRows();
                return;
            }

            SaveGameService.DeleteSave(slot);
            pendingDeleteSlot = -1;
            statusText.text = "已删除槽位 " + (slot + 1) + "。";
            RefreshRows();
            AudioManager.Instance?.PlayClick();
        }

        private void RefreshRows()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                SaveSlotInfo info = SaveGameService.GetSlotInfo(i);
                bool isPending = pendingDeleteSlot == i;

                slotTitles[i].text = "槽位 " + (i + 1) + (info.Active ? "  ·  当前" : string.Empty);

                if (!info.Exists)
                {
                    slotSummaries[i].text = "空槽位";
                    slotSummaries[i].color = Muted;
                }
                else if (info.Corrupt)
                {
                    slotSummaries[i].text = "存档无法读取，可删除后重新保存";
                    slotSummaries[i].color = new Color(0.88f, 0.55f, 0.55f, 1f);
                }
                else
                {
                    GameMap map = GameMapCatalog.Get(info.MapId);
                    slotSummaries[i].text =
                        map.Title + "  ·  用时 " + FormatElapsed(info.Elapsed) +
                        "  ·  笔记 " + info.Notes +
                        "  ·  谜题 " + info.Puzzles +
                        "  ·  区域 " + info.Rooms +
                        "\n最近保存 " + info.SavedAt;
                    slotSummaries[i].color = Muted;
                }

                primaryLabels[i].text = loadMode
                    ? (info.Exists ? "读取" : "空槽位")
                    : (info.Exists ? "覆盖保存" : "保存到此");
                primaryButtons[i].interactable = loadMode ? info.Exists : true;

                deleteButtons[i].interactable = info.Exists;
                deleteLabels[i].text = isPending ? "确认删除" : "删除";
                deleteLabels[i].color = isPending
                    ? new Color(1f, 0.78f, 0.62f, 1f)
                    : new Color(0.88f, 0.62f, 0.62f, 1f);
            }
        }

        private static string FormatElapsed(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            return hours > 0
                ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, secs)
                : string.Format("{0:00}:{1:00}", minutes, secs);
        }
    }
}
