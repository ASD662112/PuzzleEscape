using System.Collections.Generic;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 案件档案：笔记日志、三级提示、成就进度。
    /// 通过 J 键或屏幕上的“档案”按钮打开。
    /// </summary>
    public class CaseFileUI : MonoBehaviour
    {
        private static readonly Color Accent = new Color(0.5f, 0.89f, 0.76f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.94f, 0.96f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.6f, 0.66f, 1f);
        private static readonly Color PanelColor = new Color(0.055f, 0.07f, 0.09f, 0.98f);

        public static CaseFileUI Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private GameObject openButton;
        private GameObject overlay;
        private GameObject journalTab;
        private GameObject hintsTab;
        private GameObject achievementsTab;
        private Button journalTabButton;
        private Button hintsTabButton;
        private Button achievementsTabButton;
        private Button mapTabButton;
        private GameObject mapTab;
        private Button mapSurfaceButton;
        private Button mapSecondButton;
        private Button mapBasementButton;
        private RectTransform mapBlocksRoot;
        private Text mapStatusText;
        private int mapFloor = 1;

        private Button[] entryButtons;
        private Text[] entryLabels;
        private Text journalBody;
        private Text pageLabel;
        private Button previousPageButton;
        private Button nextPageButton;
        private int pageIndex;
        private int selectedEntry = -1;

        private Text hintObjectiveText;
        private Text hintBody;
        private int selectedHint;

        private Text achievementsText;

        private const int EntriesPerPage = 6;

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
            if (openButton != null)
            {
                bool otherModal = UIRoot.Instance != null && UIRoot.Instance.IsModalOpen && !IsOpen;
                openButton.SetActive(!IsOpen && !otherModal);
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                if (IsOpen)
                {
                    Close();
                }
                else if (UIRoot.Instance == null || !UIRoot.Instance.IsModalOpen)
                {
                    Open();
                }
            }

            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Build()
        {
            BuildOpenButton();
            BuildPanel();
        }

        private void BuildOpenButton()
        {
            var canvasGo = new GameObject("CaseFileButtonCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            UIFactory.Stretch((RectTransform)canvasGo.transform);

            bool mobile = UIRoot.Instance != null && UIRoot.Instance.IsMobileUI;
            var button = UIFactory.CreateButton(canvasGo.transform, "CaseFileButton",
                mobile ? "档案" : "档案（J）", new Color(0.1f, 0.15f, 0.18f), Accent, Open, 18);
            UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(44f, 150f), new Vector2(170f, 44f));
            openButton = button.gameObject;
        }

        private void BuildPanel()
        {
            var canvasGo = new GameObject("CaseFileCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            UIFactory.Stretch((RectTransform)canvasGo.transform);

            var backdrop = UIFactory.CreateImage(canvasGo.transform, "CaseFileBackdrop",
                new Color(0f, 0f, 0f, 0.72f), true);
            UIFactory.Stretch(backdrop.rectTransform);
            overlay = backdrop.gameObject;

            var card = UIFactory.CreateImage(backdrop.rectTransform, "CaseFileCard", PanelColor, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(960f, 640f));

            var accentBar = UIFactory.CreateImage(card.rectTransform, "CaseFileAccent", Accent);
            accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "CaseFileTitle", "案件档案", 40,
                TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -24f), new Vector2(360f, 52f));

            var closeButton = UIFactory.CreateButton(card.rectTransform, "CaseFileClose", "关闭",
                new Color(0.11f, 0.15f, 0.18f), Muted, Close, 18);
            UIFactory.Place(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(80f, 38f));

            BuildTabs(card.rectTransform);
            BuildJournal(card.rectTransform);
            BuildHints(card.rectTransform);
            BuildAchievements(card.rectTransform);
            BuildMap(card.rectTransform);
            SelectTab(0);

            overlay.SetActive(false);
        }

        private void BuildTabs(RectTransform card)
        {
            journalTabButton = UIFactory.CreateButton(card, "CaseFileJournalTab", "日志",
                new Color(0.1f, 0.14f, 0.17f), Accent, () => SelectTab(0), 20);
            UIFactory.Place(journalTabButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(40f, -86f), new Vector2(150f, 44f));

            hintsTabButton = UIFactory.CreateButton(card, "CaseFileHintsTab", "提示",
                new Color(0.1f, 0.14f, 0.17f), Accent, () => SelectTab(1), 20);
            UIFactory.Place(hintsTabButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(202f, -86f), new Vector2(150f, 44f));

            achievementsTabButton = UIFactory.CreateButton(card, "CaseFileAchievementsTab", "成就",
                new Color(0.1f, 0.14f, 0.17f), Accent, () => SelectTab(2), 20);
            UIFactory.Place(achievementsTabButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(364f, -86f), new Vector2(150f, 44f));

            mapTabButton = UIFactory.CreateButton(card, "CaseFileMapTab", "地图",
                new Color(0.1f, 0.14f, 0.17f), Accent, () => SelectTab(3), 20);
            UIFactory.Place(mapTabButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(526f, -86f), new Vector2(150f, 44f));
        }

        private void BuildJournal(RectTransform card)
        {
            journalTab = new GameObject("CaseFileJournalTabRoot", typeof(RectTransform));
            journalTab.transform.SetParent(card, false);
            UIFactory.Stretch((RectTransform)journalTab.transform);

            entryButtons = new Button[EntriesPerPage];
            entryLabels = new Text[EntriesPerPage];

            for (int i = 0; i < EntriesPerPage; i++)
            {
                int captured = i;
                var button = UIFactory.CreateButton(journalTab.transform, "CaseFileEntry" + i, "—",
                    new Color(0.1f, 0.14f, 0.17f), Accent, () => SelectEntry(captured), 17);
                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                    new Vector2(0f, 1f), new Vector2(40f, 500f - i * 66f), new Vector2(260f, 54f));
                entryButtons[i] = button;
                entryLabels[i] = button.GetComponentInChildren<Text>();
                entryLabels[i].alignment = TextAnchor.MiddleLeft;
            }

            previousPageButton = UIFactory.CreateButton(journalTab.transform, "CaseFilePrevPage", "上一页",
                new Color(0.1f, 0.14f, 0.17f), Accent, PreviousPage, 16);
            UIFactory.Place(previousPageButton.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(120f, 36f));

            nextPageButton = UIFactory.CreateButton(journalTab.transform, "CaseFileNextPage", "下一页",
                new Color(0.1f, 0.14f, 0.17f), Accent, NextPage, 16);
            UIFactory.Place(nextPageButton.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(180f, 24f), new Vector2(120f, 36f));

            pageLabel = UIFactory.CreateText(journalTab.transform, "CaseFilePageLabel", "0 / 0", 15, Muted);
            UIFactory.Place(pageLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 66f), new Vector2(260f, 24f));

            var bodyBox = UIFactory.CreateImage(journalTab.transform, "CaseFileBodyBox",
                new Color(0.025f, 0.035f, 0.045f, 1f), false);
            UIFactory.Place(bodyBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(330f, 500f), new Vector2(590f, 430f));

            journalBody = UIFactory.CreateText(bodyBox.rectTransform, "CaseFileBody",
                "阅读房间里的便签、日记或纸张后，会自动记录在这里。", 18, TextColor,
                TextAnchor.UpperLeft);
            UIFactory.Stretch(journalBody.rectTransform);
            journalBody.rectTransform.offsetMin = new Vector2(18f, 16f);
            journalBody.rectTransform.offsetMax = new Vector2(-18f, -16f);
            journalBody.lineSpacing = 1.15f;
        }

        private void BuildHints(RectTransform card)
        {
            hintsTab = new GameObject("CaseFileHintsTabRoot", typeof(RectTransform));
            hintsTab.transform.SetParent(card, false);
            UIFactory.Stretch((RectTransform)hintsTab.transform);

            hintObjectiveText = UIFactory.CreateText(hintsTab.transform, "CaseFileHintObjective",
                string.Empty, 20, Accent, TextAnchor.MiddleLeft);
            UIFactory.Place(hintObjectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -150f), new Vector2(860f, 34f));

            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;
                var button = UIFactory.CreateButton(hintsTab.transform, "CaseFileHint" + level,
                    "提示 " + level, new Color(0.1f, 0.14f, 0.17f), Accent,
                    () => ShowHint(level), 18);
                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(40f + i * 156f, -200f), new Vector2(144f, 44f));
            }

            var hintBox = UIFactory.CreateImage(hintsTab.transform, "CaseFileHintBox",
                new Color(0.025f, 0.035f, 0.045f, 1f), false);
            UIFactory.Place(hintBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(40f, 520f), new Vector2(880f, 300f));

            hintBody = UIFactory.CreateText(hintBox.rectTransform, "CaseFileHintBody",
                "选择提示等级。提示会逐级给出方向、物件和答案。", 19, TextColor,
                TextAnchor.UpperLeft);
            UIFactory.Stretch(hintBody.rectTransform);
            hintBody.rectTransform.offsetMin = new Vector2(18f, 16f);
            hintBody.rectTransform.offsetMax = new Vector2(-18f, -16f);
            hintBody.lineSpacing = 1.15f;
        }

        private void BuildAchievements(RectTransform card)
        {
            achievementsTab = new GameObject("CaseFileAchievementsTabRoot", typeof(RectTransform));
            achievementsTab.transform.SetParent(card, false);
            UIFactory.Stretch((RectTransform)achievementsTab.transform);

            var box = UIFactory.CreateImage(achievementsTab.transform, "CaseFileAchievementBox",
                new Color(0.025f, 0.035f, 0.045f, 1f), false);
            UIFactory.Place(box.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(40f, 470f), new Vector2(880f, 440f));

            achievementsText = UIFactory.CreateText(box.rectTransform, "CaseFileAchievements",
                string.Empty, 19, TextColor, TextAnchor.UpperLeft);
            UIFactory.Stretch(achievementsText.rectTransform);
            achievementsText.rectTransform.offsetMin = new Vector2(18f, 16f);
            achievementsText.rectTransform.offsetMax = new Vector2(-18f, -16f);
            achievementsText.lineSpacing = 1.35f;
        }

        private void BuildMap(RectTransform card)
        {
            mapTab = new GameObject("CaseFileMapTabRoot", typeof(RectTransform));
            mapTab.transform.SetParent(card, false);
            UIFactory.Stretch((RectTransform)mapTab.transform);

            mapSurfaceButton = CreateMapFloorButton(mapTab.transform, "CaseFileMapSurface",
                "一层", 40f, 1);
            mapSecondButton = CreateMapFloorButton(mapTab.transform, "CaseFileMapSecond",
                "二层", 178f, 2);
            mapBasementButton = CreateMapFloorButton(mapTab.transform, "CaseFileMapBasement",
                "地下室", 316f, -1);

            mapStatusText = UIFactory.CreateText(mapTab.transform, "CaseFileMapStatus",
                string.Empty, 17, Muted, TextAnchor.MiddleRight);
            UIFactory.Place(mapStatusText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -144f), new Vector2(480f, 30f));

            var panel = UIFactory.CreateImage(mapTab.transform, "CaseFileMapPanel",
                new Color(0.025f, 0.035f, 0.045f, 1f), false);
            UIFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -186f), new Vector2(880f, 380f));

            mapBlocksRoot = UIFactory.CreateRect(panel.rectTransform, "CaseFileMapBlocks");
            UIFactory.Stretch(mapBlocksRoot);

            var note = UIFactory.CreateText(mapTab.transform, "CaseFileMapNote",
                "◆ 当前区域    · 已探索    · 灰色为未探索区域", 14, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 18f), new Vector2(880f, 24f));
        }

        private Button CreateMapFloorButton(Transform parent, string name, string label,
            float x, int floor)
        {
            var button = UIFactory.CreateButton(parent, name, label,
                new Color(0.1f, 0.14f, 0.17f), Accent, () => SelectMapFloor(floor), 18);
            UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(x, -142f), new Vector2(126f, 38f));
            return button;
        }

        private void SelectTab(int index)
        {
            if (journalTab != null)
            {
                journalTab.SetActive(index == 0);
            }

            if (hintsTab != null)
            {
                hintsTab.SetActive(index == 1);
            }

            if (achievementsTab != null)
            {
                achievementsTab.SetActive(index == 2);
            }

            if (mapTab != null)
            {
                mapTab.SetActive(index == 3);
            }

            journalTabButton.GetComponent<Image>().color = index == 0
                ? Accent
                : new Color(0.1f, 0.14f, 0.17f);
            hintsTabButton.GetComponent<Image>().color = index == 1
                ? Accent
                : new Color(0.1f, 0.14f, 0.17f);
            achievementsTabButton.GetComponent<Image>().color = index == 2
                ? Accent
                : new Color(0.1f, 0.14f, 0.17f);
            mapTabButton.GetComponent<Image>().color = index == 3
                ? Accent
                : new Color(0.1f, 0.14f, 0.17f);

            Color activeLabelColor = new Color(0.03f, 0.09f, 0.07f);
            journalTabButton.GetComponentInChildren<Text>().color = index == 0 ? activeLabelColor : Accent;
            hintsTabButton.GetComponentInChildren<Text>().color = index == 1 ? activeLabelColor : Accent;
            achievementsTabButton.GetComponentInChildren<Text>().color = index == 2 ? activeLabelColor : Accent;
            mapTabButton.GetComponentInChildren<Text>().color = index == 3 ? activeLabelColor : Accent;

            if (index == 0)
            {
                RefreshJournal();
            }
            else if (index == 1)
            {
                RefreshHints();
            }
            else if (index == 2)
            {
                RefreshAchievements();
            }
            else
            {
                RefreshMap();
            }
        }

        private void RefreshJournal()
        {
            IList<CaseFileSystem.Entry> entries = CaseFileSystem.Entries;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)EntriesPerPage));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);

            int start = pageIndex * EntriesPerPage;
            for (int i = 0; i < EntriesPerPage; i++)
            {
                int entryIndex = start + i;
                bool has = entryIndex >= 0 && entryIndex < entries.Count;
                entryButtons[i].gameObject.SetActive(has);
                if (has)
                {
                    CaseFileSystem.Entry entry = entries[entryIndex];
                    entryLabels[i].text = entry.Category + " · " + entry.Title;
                }
            }

            pageLabel.text = (pageIndex + 1) + " / " + pageCount;
            previousPageButton.interactable = pageIndex > 0;
            nextPageButton.interactable = pageIndex < pageCount - 1;

            if (entries.Count == 0)
            {
                journalBody.text = "还没有记录。阅读房间里的便签、日记或纸张后，会自动记录在这里。";
                selectedEntry = -1;
                return;
            }

            if (selectedEntry < 0 || selectedEntry >= entries.Count)
            {
                selectedEntry = start;
            }

            if (selectedEntry < entries.Count)
            {
                CaseFileSystem.Entry entry = entries[selectedEntry];
                journalBody.text = "<color=#7FE3C4>" + Localization.T(entry.Title) + "</color>\n\n" +
                                   Localization.T(entry.Body);
            }
        }

        private void SelectEntry(int pageSlot)
        {
            int entryIndex = pageIndex * EntriesPerPage + pageSlot;
            if (entryIndex < 0 || entryIndex >= CaseFileSystem.Entries.Count)
            {
                return;
            }

            selectedEntry = entryIndex;
            CaseFileSystem.Entry entry = CaseFileSystem.Entries[entryIndex];
            journalBody.text = "<color=#7FE3C4>" + Localization.T(entry.Title) + "</color>\n\n" +
                               Localization.T(entry.Body);
            AudioManager.Instance?.PlayClick();
        }

        private void PreviousPage()
        {
            pageIndex = Mathf.Max(0, pageIndex - 1);
            selectedEntry = -1;
            RefreshJournal();
            AudioManager.Instance?.PlayClick();
        }

        private void NextPage()
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(CaseFileSystem.Entries.Count / (float)EntriesPerPage));
            pageIndex = Mathf.Min(pageCount - 1, pageIndex + 1);
            selectedEntry = -1;
            RefreshJournal();
            AudioManager.Instance?.PlayClick();
        }

        private void RefreshHints()
        {
            string objective = GameManager.Instance != null ? GameManager.Instance.CurrentObjective : "未知目标";
            hintObjectiveText.text = Localization.T("当前目标：") + Localization.T(objective);

            if (selectedHint <= 0)
            {
                hintBody.text = "选择提示等级。提示会逐级给出方向、物件和答案。";
            }
        }

        private void ShowHint(int level)
        {
            string objective = GameManager.Instance != null ? GameManager.Instance.CurrentObjective : string.Empty;
            string[] hints = CaseFileSystem.GetHints(objective);
            int index = Mathf.Clamp(level - 1, 0, hints.Length - 1);
            selectedHint = level;
            hintBody.text = "<color=#7FE3C4>" + Localization.Format("提示 {0}", level) +
                            "</color>\n\n" + Localization.T(hints[index]);
            CaseFileSystem.MarkHintUsed();
            AudioManager.Instance?.PlayClick();
        }

        private void RefreshAchievements()
        {
            achievementsText.text = CaseFileSystem.GetAchievementText();
        }

        private void SelectMapFloor(int floor)
        {
            if (mapFloor == floor)
            {
                return;
            }

            mapFloor = floor;
            RefreshMap();
            AudioManager.Instance?.PlayClick();
        }

        private void RefreshMap()
        {
            if (mapBlocksRoot == null)
            {
                return;
            }

            for (int i = mapBlocksRoot.childCount - 1; i >= 0; i--)
            {
                GameObject oldBlock = mapBlocksRoot.GetChild(i).gameObject;
                oldBlock.SetActive(false);
                Destroy(oldBlock);
            }

            SetMapFloorButtonState(mapSurfaceButton, 1);
            SetMapFloorButtonState(mapSecondButton, 2);
            SetMapFloorButtonState(mapBasementButton, -1);

            string floorName = mapFloor == 1 ? "一层" : (mapFloor == 2 ? "二层" : "地下室");
            MapSystem map = MapSystem.Instance;
            if (map == null)
            {
                mapStatusText.text = floorName + "  ·  地图数据未加载";
                return;
            }

            List<MapSystem.RoomInfo> rooms = map.GetRooms(mapFloor);
            int visitedCount = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (map.IsVisited(rooms[i].Id))
                {
                    visitedCount++;
                }
            }

            mapStatusText.text = floorName + "  ·  已探索 " + visitedCount + " / " + rooms.Count + " 间";

            if (rooms.Count == 0)
            {
                return;
            }

            List<List<MapSystem.RoomInfo>> rows = GroupMapRooms(rooms);
            BuildMapRows(map, rows);
        }

        private void SetMapFloorButtonState(Button button, int floor)
        {
            if (button == null)
            {
                return;
            }

            bool active = mapFloor == floor;
            button.GetComponent<Image>().color = active
                ? Accent
                : new Color(0.1f, 0.14f, 0.17f);
            button.GetComponentInChildren<Text>().color = active
                ? new Color(0.03f, 0.09f, 0.07f)
                : Accent;
        }

        private static List<List<MapSystem.RoomInfo>> GroupMapRooms(List<MapSystem.RoomInfo> rooms)
        {
            const int maxColumns = 4;
            const int maxRows = 7;
            float band = 6f;
            List<List<MapSystem.RoomInfo>> rows = BuildMapRowsByBand(rooms, maxColumns, band);

            while (rows.Count > maxRows && band < 24f)
            {
                band += 3f;
                rows = BuildMapRowsByBand(rooms, maxColumns, band);
            }

            if (rows.Count > maxRows)
            {
                var packed = new List<List<MapSystem.RoomInfo>>();
                int perRow = Mathf.CeilToInt(rooms.Count / (float)maxRows);
                for (int i = 0; i < rooms.Count; i += perRow)
                {
                    var row = new List<MapSystem.RoomInfo>();
                    for (int j = i; j < rooms.Count && j < i + perRow; j++)
                    {
                        row.Add(rooms[j]);
                    }

                    packed.Add(row);
                }

                return packed;
            }

            return rows;
        }

        private static List<List<MapSystem.RoomInfo>> BuildMapRowsByBand(
            List<MapSystem.RoomInfo> rooms, int maxColumns, float band)
        {
            var rows = new List<List<MapSystem.RoomInfo>>();

            for (int i = 0; i < rooms.Count; i++)
            {
                MapSystem.RoomInfo room = rooms[i];
                if (rows.Count == 0)
                {
                    rows.Add(new List<MapSystem.RoomInfo>());
                }

                List<MapSystem.RoomInfo> last = rows[rows.Count - 1];
                bool full = last.Count >= maxColumns;
                bool far = last.Count > 0 && Mathf.Abs(room.Center.z - last[0].Center.z) > band;

                if (full || far)
                {
                    rows.Add(new List<MapSystem.RoomInfo>());
                    last = rows[rows.Count - 1];
                }

                last.Add(room);
            }

            return rows;
        }

        private void BuildMapRows(MapSystem map, List<List<MapSystem.RoomInfo>> rows)
        {
            const float panelWidth = 880f;
            const float panelHeight = 380f;
            const float padX = 20f;
            const float padY = 18f;
            const float gapX = 12f;
            const float gapY = 10f;

            int rowCount = rows.Count;
            float blockHeight = Mathf.Clamp(
                (panelHeight - padY * 2f - gapY * (rowCount - 1)) / rowCount, 26f, 52f);
            float blockWidth = Mathf.Min(190f,
                (panelWidth - padX * 2f - gapX * 3f) / 4f);
            int fontSize = blockHeight >= 42f ? 14 : 12;

            for (int r = 0; r < rowCount; r++)
            {
                List<MapSystem.RoomInfo> row = rows[r];
                float rowWidth = row.Count * blockWidth + (row.Count - 1) * gapX;
                float startX = (panelWidth - rowWidth) * 0.5f;
                float y = padY + r * (blockHeight + gapY);

                if (row.Count > 1)
                {
                    var corridor = UIFactory.CreateImage(mapBlocksRoot,
                        "CaseFileMapCorridor_" + mapFloor + "_" + r,
                        new Color(0.5f, 0.89f, 0.76f, 0.10f));
                    UIFactory.Place(corridor.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(startX - 8f, -(y + blockHeight * 0.5f - 2f)),
                        new Vector2(rowWidth + 16f, 4f));
                }

                for (int c = 0; c < row.Count; c++)
                {
                    float x = startX + c * (blockWidth + gapX);
                    BuildMapRoomBlock(map, row[c], x, y, blockWidth, blockHeight, fontSize, r, c);
                }
            }
        }

        private void BuildMapRoomBlock(MapSystem map, MapSystem.RoomInfo room,
            float x, float y, float width, float height, int fontSize, int row, int column)
        {
            bool discovered = map.IsVisited(room.Id);
            bool current = discovered && map.CurrentRoomId == room.Id;
            Color background = current
                ? new Color(0.14f, 0.30f, 0.26f, 1f)
                : (discovered
                    ? new Color(0.09f, 0.12f, 0.15f, 1f)
                    : new Color(0.05f, 0.06f, 0.07f, 1f));

            var block = UIFactory.CreateImage(mapBlocksRoot,
                "CaseFileMapRoom_" + mapFloor + "_" + row + "_" + column, background);
            UIFactory.Place(block.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -y), new Vector2(width, height));

            if (current)
            {
                var marker = UIFactory.CreateImage(block.rectTransform,
                    "CaseFileMapMarker", new Color(0.5f, 0.89f, 0.76f, 1f));
                marker.rectTransform.anchorMin = new Vector2(0f, 0f);
                marker.rectTransform.anchorMax = new Vector2(1f, 0f);
                marker.rectTransform.pivot = new Vector2(0.5f, 0f);
                marker.rectTransform.anchoredPosition = Vector2.zero;
                marker.rectTransform.sizeDelta = new Vector2(0f, 3f);
            }

            var label = UIFactory.CreateText(block.rectTransform,
                "CaseFileMapRoomLabel",
                discovered
                    ? (current ? "◆ " + room.DisplayName : room.DisplayName)
                    : "未探索",
                fontSize,
                current ? TextColor : (discovered ? TextColor : Muted));
            UIFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(4f, 3f);
            label.rectTransform.offsetMax = new Vector2(-4f, -3f);
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            overlay.SetActive(true);
            pageIndex = 0;
            selectedEntry = -1;
            selectedHint = 0;
            SelectTab(0);
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
            if (overlay != null)
            {
                overlay.SetActive(false);
            }

            UIRoot.Instance?.NotifyExternalModalChanged();
            AudioManager.Instance?.PlayClick();
        }
    }
}
