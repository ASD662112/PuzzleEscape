using System.Collections;
using PuzzleEscape.AI;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Interaction;
using PuzzleEscape.Player;
using PuzzleEscape.Puzzle;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 运行时 HUD：PC 与手机两套控制布局，共享线索/密码/背包/通关面板。
    /// PC：背包按钮+按键提示在左下，快捷物品栏右下，背包占左侧 1/3，4×8 格。
    /// 手机：虚拟摇杆、交互按钮、全屏视角拖拽与背包按钮。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class UIRoot : MonoBehaviour
    {
        private class SlotView
        {
            public Image Background;
            public Image Icon;
            public Text Label;
        }

        public static UIRoot Instance { get; private set; }

        /// <summary>
        /// 编辑器/开发机预览手机布局时可临时设为 true。
        /// </summary>
        public static bool ForceMobileUI { get; set; }

        private static readonly Color Accent = new Color(0.5f, 0.89f, 0.76f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.94f, 0.96f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.6f, 0.66f, 1f);
        private static readonly Color Danger = new Color(0.88f, 0.42f, 0.42f, 1f);
        private static readonly Color PanelColor = new Color(0.055f, 0.07f, 0.09f, 0.98f);
        private static readonly Color SlotEmpty = new Color(0.08f, 0.1f, 0.13f, 0.94f);
        private static readonly Color SlotFilled = new Color(0.12f, 0.16f, 0.19f, 0.98f);
        private static readonly Color SlotSelected = new Color(0.2f, 0.33f, 0.29f, 1f);

        private Canvas canvas;
        private RectTransform modalLayer;
        private RectTransform cluePanel;
        private Text clueTitle;
        private Text clueBody;
        private RectTransform promptRoot;
        private Text promptText;
        private Text objectiveText;
        private Text timerText;
        private Image staminaFill;
        private Text staminaLabel;
        private int lastStaminaShown = -1;
        private Text toastText;
        private CanvasGroup toastGroup;
        private Text cursorLockHint;
        private RectTransform winPanel;
        private Text winTimeText;
        private Text winTitleText;
        private Text winSubtitleText;
        private Text winRankText;

        private RectTransform backpackOverlay;
        private Text backpackCountText;
        private SlotView[] quickSlots;
        private SlotView[] bagSlots;

        private int selectedSlot;
        private int dragSourceIndex = -1;
        private RectTransform dragGhost;

        private RectTransform settingsOverlay;
        private Text settingsVolumeValue;
        private Text settingsSensitivityValue;
        private Text settingsInvertValue;
        private Text settingsFullscreenValue;
        private Text settingsFrameRateValue;
        private Text settingsFieldOfViewValue;
        private Text settingsUiScaleValue;
        private Text settingsDifficultyValue;
        private RectTransform difficultyOverlay;
        private Image[] difficultyOptionBackgrounds;
        private Text[] difficultyOptionNames;
        private int pendingDifficulty;

        private static readonly string[] DifficultyDescriptions =
        {
            "没有怪物，专心探索解谜",
            "怪物速度与视野较低，容错空间大",
            "标准体验，怪物会追逐但追不上奔跑",
            "推荐难度，接近原有的怪物手感",
            "追逐速度比玩家奔跑快十分之一"
        };

        private Text settingsSubtitleText;
        private Text settingsResumeText;
        private Image[] qualityButtonImages;
        private Text[] qualityButtonLabels;

        private RectTransform mainMenuOverlay;
        private Button mainMenuContinueButton;
        private Text mainMenuTitleText;
        private Text mainMenuTitleEnText;
        private Text mainMenuDescriptionText;
        private Image fadeOverlay;
        private Text fadeLabel;
        private RectTransform losePanel;
        private Text loseStatsText;
        private bool loseOpen;

        private KeypadUI keypadUI;
        private PlayerInventory boundInventory;
        private PlayerInteractor boundInteractor;
        private FirstPersonController boundController;
        private Coroutine toastRoutine;

        private bool clueOpen;
        private bool backpackOpen;
        private bool winOpen;
        private bool settingsOpen;
        private bool mainMenuOpen;
        private bool customizingControls;
        private float masterVolume = 0.7f;
        private float fieldOfView = 70f;

        // 手机端自定义控制布局：摇杆与交互按钮的位置保存在 PlayerPrefs。
        private const string JoystickXPref = "PuzzleEscape.Controls.JoystickX";
        private const string JoystickYPref = "PuzzleEscape.Controls.JoystickY";
        private const string InteractXPref = "PuzzleEscape.Controls.InteractX";
        private const string InteractYPref = "PuzzleEscape.Controls.InteractY";
        private const string RunXPref = "PuzzleEscape.Controls.RunX";
        private const string RunYPref = "PuzzleEscape.Controls.RunY";
        private const string DropXPref = "PuzzleEscape.Controls.DropX";
        private const string DropYPref = "PuzzleEscape.Controls.DropY";
        private static readonly Vector2 JoystickDefaultPosition = new Vector2(46f, 46f);
        private static readonly Vector2 InteractDefaultPosition = new Vector2(-46f, 56f);
        private static readonly Vector2 RunDefaultPosition = new Vector2(-46f, 214f);
        private static readonly Vector2 DropDefaultPosition = new Vector2(-46f, 330f);

        private RectTransform mobileJoystickRect;
        private RectTransform mobileInteractRect;
        private RectTransform mobileRunRect;
        private RectTransform mobileDropRect;
        private VirtualJoystick mobileJoystick;
        private MobileLookPad mobileLookPad;
        private Button mobileRunButton;
        private Button mobileDropButton;
        private Text mobileRunLabel;
        private GameObject customControlsOverlay;
        private int joystickSiblingIndex;
        private int interactSiblingIndex;
        private int runSiblingIndex;
        private int dropSiblingIndex;

        public bool IsMobileUI { get; private set; }

        /// <summary>当前选中的快捷栏槽位（0~7），半截蜡烛只在该槽位被选中时照明。</summary>
        public int SelectedSlotIndex => selectedSlot;

        /// <summary>当前快捷栏选中格里的物品，没有则为 null。</summary>
        public ItemDefinition SelectedItem =>
            boundInventory != null ? boundInventory.ItemAt(selectedSlot) : null;

        public bool IsModalOpen => mainMenuOpen || clueOpen || backpackOpen || winOpen || loseOpen || settingsOpen ||
                                   customizingControls ||
                                   (difficultyOverlay != null && difficultyOverlay.gameObject.activeSelf) ||
                                   (keypadUI != null && keypadUI.IsOpen) ||
                                   (CaseFileUI.Instance != null && CaseFileUI.Instance.IsOpen) ||
                                   (ItemDetailUI.Instance != null && ItemDetailUI.Instance.IsOpen) ||
                                   (TutorialUI.Instance != null && TutorialUI.Instance.IsOpen) ||
                                   (SaveSlotUI.Instance != null && SaveSlotUI.Instance.IsOpen) ||
                                   (MapSelectUI.Instance != null && MapSelectUI.Instance.IsOpen);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            IsMobileUI = Application.isMobilePlatform || ForceMobileUI ||
                         PlayerPrefs.GetInt("PuzzleEscape.ForceMobileUI", 0) == 1;
            masterVolume = PlayerPrefs.GetFloat("PuzzleEscape.MasterVolume", 0.7f);
            fieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat("PuzzleEscape.FieldOfView", 70f), 60f, 90f);

            EnsureEventSystem();
            BuildCanvas();
            BuildHud();
            BuildCluePanel();
            BuildKeypad();
            BuildBackpack();
            BuildWinPanel();
            BuildMainMenu();
            BuildDifficultySelect();
            BuildSettings();
            BuildFade();
            BuildLosePanel();
        }

        private void OnDestroy()
        {
            if (boundInventory != null)
            {
                boundInventory.Changed -= RefreshInventory;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ObjectiveChanged -= SetObjective;
                GameManager.Instance.ToastRequested -= ShowToast;
                GameManager.Instance.Won -= HandleWon;
                GameManager.Instance.Lost -= HandleLost;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ObjectiveChanged += SetObjective;
                GameManager.Instance.ToastRequested += ShowToast;
                GameManager.Instance.Won += HandleWon;
                GameManager.Instance.Lost += HandleLost;
                SetObjective(GameManager.Instance.CurrentObjective);
            }

            // 主界面默认显示，由 RefreshModalState 负责解锁鼠标。
            RefreshModalState();

            // 从主界面切换地图后重新加载了场景：直接进入游戏，不再弹一次主界面。
            if (GameSession.AutoStartNewGame)
            {
                GameSession.AutoStartNewGame = false;
                StartGame(true);
            }
        }

        private void Update()
        {
            UpdateTimer();
            UpdateStamina();
            UpdateCursorHint();

            if (CaseFileUI.Instance != null && CaseFileUI.Instance.IsOpen)
            {
                return;
            }

            if (ItemDetailUI.Instance != null && ItemDetailUI.Instance.IsOpen)
            {
                return;
            }

            if (TutorialUI.Instance != null && TutorialUI.Instance.IsOpen)
            {
                return;
            }

            if (SaveSlotUI.Instance != null && SaveSlotUI.Instance.IsOpen)
            {
                return;
            }

            if (MapSelectUI.Instance != null && MapSelectUI.Instance.IsOpen)
            {
                return;
            }

            if (winOpen || loseOpen)
            {
                return;
            }

            // 难度选择只在开始新游戏时出现，Esc 返回主界面。
            if (difficultyOverlay != null && difficultyOverlay.gameObject.activeSelf)
            {
                if (!IsMobileUI && Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseDifficultySelect();
                }

                return;
            }

            if (mainMenuOpen)
            {
                if (!IsMobileUI && Input.GetKeyDown(KeyCode.Escape))
                {
                    if (settingsOpen)
                    {
                        CloseSettings();
                    }
                    else
                    {
                        OpenSettings();
                    }
                }

                return;
            }

            if (!IsMobileUI)
            {
                if (Input.GetKeyDown(KeyCode.B))
                {
                    ToggleBackpack();
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (settingsOpen)
                    {
                        CloseSettings();
                    }
                    else if (backpackOpen)
                    {
                        CloseBackpack();
                    }
                    else if (keypadUI != null && keypadUI.IsOpen)
                    {
                        CloseKeypad();
                    }
                    else if (clueOpen)
                    {
                        CloseClue();
                    }
                    else
                    {
                        OpenSettings();
                    }
                }

                if (!IsModalOpen)
                {
                    float scroll = Input.mouseScrollDelta.y;
                    if (scroll > 0.01f)
                    {
                        CycleSelection(1);
                    }
                    else if (scroll < -0.01f)
                    {
                        CycleSelection(-1);
                    }

                    if (Input.GetKeyDown(KeyCode.Q))
                    {
                        DropSelectedItem();
                    }
                }
            }
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(transform, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            UIFactory.Stretch((RectTransform)canvasGo.transform);
        }

        private void BuildHud()
        {
            objectiveText = UIFactory.CreateText(canvas.transform, "ObjectiveText", string.Empty, 26,
                TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, -32f), new Vector2(1080f, 44f));
            AddShadow(objectiveText.gameObject, new Vector2(1f, -1f));

            timerText = UIFactory.CreateText(canvas.transform, "TimerText", "00:00", 22, Muted,
                TextAnchor.MiddleRight);
            UIFactory.Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-44f, -32f), new Vector2(240f, 44f));
            AddShadow(timerText.gameObject, new Vector2(1f, -1f));

            // 体力条：位于左上角目标文字下方，PC 与手机共用。
            staminaLabel = UIFactory.CreateText(canvas.transform, "StaminaLabel", "体力 100 / 100", 16,
                Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(staminaLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, -80f), new Vector2(260f, 22f));
            AddShadow(staminaLabel.gameObject, new Vector2(1f, -1f));

            var staminaBackground = UIFactory.CreateImage(canvas.transform, "StaminaBar",
                new Color(0.08f, 0.1f, 0.13f, 0.94f));
            UIFactory.Place(staminaBackground.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, -106f), new Vector2(260f, 16f));

            staminaFill = UIFactory.CreateImage(staminaBackground.rectTransform, "StaminaFill",
                Accent, false);
            staminaFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            staminaFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            staminaFill.rectTransform.offsetMin = Vector2.zero;
            staminaFill.rectTransform.offsetMax = Vector2.zero;

            toastText = UIFactory.CreateText(canvas.transform, "ToastText", string.Empty, 27, TextColor);
            UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(1100f, 70f));
            AddShadow(toastText.gameObject, new Vector2(1f, -1f));
            toastGroup = toastText.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;

            var ring = UIFactory.CreateImage(canvas.transform, "CrosshairRing",
                new Color(0.5f, 0.89f, 0.76f, 0.14f));
            UIFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(20f, 20f));

            var dot = UIFactory.CreateImage(canvas.transform, "CrosshairDot",
                new Color(1f, 1f, 1f, 0.8f));
            UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(5f, 5f));

            var prompt = UIFactory.CreateImage(canvas.transform, "PromptBar",
                new Color(0.04f, 0.06f, 0.08f, 0.88f));
            promptRoot = prompt.rectTransform;
            UIFactory.Place(promptRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, IsMobileUI ? 126f : 88f), new Vector2(660f, 54f));
            promptRoot.gameObject.SetActive(false);

            promptText = UIFactory.CreateText(promptRoot, "PromptText", string.Empty, 25, Accent);
            UIFactory.Stretch(promptText.rectTransform);
            promptText.rectTransform.offsetMin = new Vector2(16f, 4f);
            promptText.rectTransform.offsetMax = new Vector2(-16f, -4f);

            if (IsMobileUI)
            {
                BuildMobileControls();
            }
            else
            {
                BuildPcControls();
            }
        }

        private void BuildPcControls()
        {
            var quickRoot = UIFactory.CreateRect(canvas.transform, "PCInventoryBar");
            UIFactory.Place(quickRoot, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-44f, 40f), new Vector2(686f, 96f));

            var label = UIFactory.CreateText(quickRoot, "InventoryLabel", "物品栏", 18, Muted,
                TextAnchor.MiddleRight);
            UIFactory.Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 24f), new Vector2(300f, 24f));

            quickSlots = BuildSlotRow(quickRoot, 8, new Vector2(76f, 96f), 10f, 0f, false, 12);
            MakeQuickSlotsInteractive();

            var backpackButton = UIFactory.CreateButton(canvas.transform, "BackpackButton", "背包",
                new Color(0.1f, 0.15f, 0.18f), Accent, ToggleBackpack, 22);
            UIFactory.Place(backpackButton.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(44f, 74f), new Vector2(160f, 56f));

            var hint = UIFactory.CreateText(canvas.transform, "BackpackHint", "B 键打开背包", 16, Muted,
                TextAnchor.MiddleLeft);
            UIFactory.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(44f, 38f), new Vector2(240f, 26f));
            AddShadow(hint.gameObject, new Vector2(1f, -1f));

            var settingsHint = UIFactory.CreateText(canvas.transform, "SettingsHint",
                IsMobileUI ? "Esc 设置" : "滚轮 选择 · Q 丢弃 · E 手电筒 · Esc 设置", 16, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(settingsHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(44f, 16f), new Vector2(300f, 26f));
            AddShadow(settingsHint.gameObject, new Vector2(1f, -1f));

            cursorLockHint = UIFactory.CreateText(canvas.transform, "CursorLockHint",
                "点击游戏画面锁定鼠标", 20, Accent, TextAnchor.MiddleCenter);
            UIFactory.Place(cursorLockHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 180f), new Vector2(520f, 40f));
            AddShadow(cursorLockHint.gameObject, new Vector2(1f, -1f));
            cursorLockHint.gameObject.SetActive(false);
        }

        private void BuildMobileControls()
        {
            var lookPad = UIFactory.CreateImage(canvas.transform, "MobileLookPad",
                new Color(1f, 1f, 1f, 0.002f), true);
            UIFactory.Stretch(lookPad.rectTransform);
            mobileLookPad = lookPad.gameObject.AddComponent<MobileLookPad>();

            var joystickBg = UIFactory.CreateImage(canvas.transform, "MobileJoystick",
                new Color(0.06f, 0.09f, 0.11f, 0.85f), true);
            UIFactory.Place(joystickBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                JoystickDefaultPosition, new Vector2(220f, 220f));
            Sprite knobSprite = GetBuiltinSprite("UI/Skin/Knob.psd");
            if (knobSprite != null)
            {
                joystickBg.sprite = knobSprite;
            }

            var knob = UIFactory.CreateImage(joystickBg.rectTransform, "Knob",
                new Color(0.5f, 0.89f, 0.76f, 0.85f));
            UIFactory.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(86f, 86f));
            knob.raycastTarget = false;
            if (knobSprite != null)
            {
                knob.sprite = knobSprite;
            }

            var joystick = joystickBg.gameObject.AddComponent<VirtualJoystick>();
            joystick.Background = joystickBg.rectTransform;
            joystick.Knob = knob.rectTransform;
            joystick.Radius = 76f;
            mobileJoystick = joystick;
            mobileJoystickRect = joystickBg.rectTransform;
            mobileJoystickRect.anchoredPosition = LoadControlPosition(
                JoystickXPref, JoystickYPref, JoystickDefaultPosition);

            var backpackButton = UIFactory.CreateButton(canvas.transform, "MobileBackpackButton", "背包",
                new Color(0.1f, 0.15f, 0.18f), Accent, ToggleBackpack, 21);
            UIFactory.Place(backpackButton.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(46f, 292f), new Vector2(150f, 58f));

            var interactButton = UIFactory.CreateButton(canvas.transform, "MobileInteractButton", "交互",
                Accent, new Color(0.04f, 0.1f, 0.08f), OnInteractPressed, 24);
            UIFactory.Place(interactButton.GetComponent<RectTransform>(), new Vector2(1f, 0f),
                new Vector2(1f, 0f), InteractDefaultPosition, new Vector2(132f, 132f));
            mobileInteractRect = interactButton.GetComponent<RectTransform>();
            mobileInteractRect.anchoredPosition = LoadControlPosition(
                InteractXPref, InteractYPref, InteractDefaultPosition);
            if (knobSprite != null)
            {
                interactButton.GetComponent<Image>().sprite = knobSprite;
            }

            // 奔跑切换按钮：点击在走路 / 奔跑之间切换。
            var runButton = UIFactory.CreateButton(canvas.transform, "MobileRunButton", "奔跑",
                new Color(0.1f, 0.15f, 0.18f), Accent, ToggleRunMode, 22);
            UIFactory.Place(runButton.GetComponent<RectTransform>(), new Vector2(1f, 0f),
                new Vector2(1f, 0f), RunDefaultPosition, new Vector2(110f, 110f));
            mobileRunRect = runButton.GetComponent<RectTransform>();
            mobileRunRect.anchoredPosition = LoadControlPosition(RunXPref, RunYPref, RunDefaultPosition);
            mobileRunButton = runButton;
            mobileRunLabel = runButton.GetComponentInChildren<Text>();
            RefreshRunButton();

            // 丢弃按钮：与电脑端 Q 键相同，丢弃当前选中的快捷栏物品。
            var dropButton = UIFactory.CreateButton(canvas.transform, "MobileDropButton", "丢弃",
                new Color(0.18f, 0.1f, 0.11f), Danger, DropSelectedItem, 20);
            UIFactory.Place(dropButton.GetComponent<RectTransform>(), new Vector2(1f, 0f),
                new Vector2(1f, 0f), DropDefaultPosition, new Vector2(110f, 110f));
            mobileDropRect = dropButton.GetComponent<RectTransform>();
            mobileDropRect.anchoredPosition = LoadControlPosition(DropXPref, DropYPref, DropDefaultPosition);
            mobileDropButton = dropButton;

            var quickRoot = UIFactory.CreateRect(canvas.transform, "MobileInventoryBar");
            UIFactory.Place(quickRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(568f, 76f));
            quickSlots = BuildSlotRow(quickRoot, 8, new Vector2(64f, 76f), 8f, 0f, false, 11);
            MakeQuickSlotsInteractive();

            var settingsButton = UIFactory.CreateButton(canvas.transform, "MobileSettingsButton", "设置",
                new Color(0.1f, 0.15f, 0.18f), Accent, ToggleSettings, 19);
            UIFactory.Place(settingsButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-46f, -92f), new Vector2(110f, 46f));
        }

        private SlotView[] BuildSlotRow(Transform parent, int count, Vector2 slotSize, float gap, float startX,
            bool showIndex, int labelSize)
        {
            var slots = new SlotView[count];

            for (int i = 0; i < count; i++)
            {
                var background = UIFactory.CreateImage(parent, "Slot_" + i, SlotEmpty, false);
                UIFactory.Place(background.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(startX + i * (slotSize.x + gap), 0f), slotSize);

                var icon = UIFactory.CreateImage(background.rectTransform, "Icon", Color.white, false);
                float iconSize = Mathf.Min(slotSize.x - 24f, slotSize.y * 0.56f);
                UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 12f), new Vector2(iconSize, iconSize));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.gameObject.SetActive(false);

                var label = UIFactory.CreateText(background.rectTransform, "Label", "—", labelSize, Muted);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 5f), new Vector2(slotSize.x - 8f, slotSize.y * 0.3f));

                slots[i] = new SlotView
                {
                    Background = background,
                    Icon = icon,
                    Label = label
                };
            }

            return slots;
        }

        private void BuildCluePanel()
        {
            var backdrop = UIFactory.CreateImage(canvas.transform, "ClueModal",
                new Color(0f, 0f, 0f, 0.74f), true);
            UIFactory.Stretch(backdrop.rectTransform);
            modalLayer = backdrop.rectTransform;

            var card = UIFactory.CreateImage(modalLayer, "ClueCard", PanelColor, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(780f, 500f));
            cluePanel = card.rectTransform;

            var accentBar = UIFactory.CreateImage(card.rectTransform, "AccentBar", Accent);
            accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

            clueTitle = UIFactory.CreateText(card.rectTransform, "ClueTitle", string.Empty, 32, TextColor,
                TextAnchor.MiddleLeft);
            UIFactory.Place(clueTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -34f), new Vector2(700f, 52f));

            clueBody = UIFactory.CreateText(card.rectTransform, "ClueBody", string.Empty, 24,
                new Color(0.78f, 0.82f, 0.86f, 1f), TextAnchor.UpperLeft);
            UIFactory.Place(clueBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -102f), new Vector2(700f, 300f));
            clueBody.lineSpacing = 1.25f;

            var closeButton = UIFactory.CreateButton(card.rectTransform, "CloseClueButton", "关闭（Esc）",
                new Color(0.11f, 0.15f, 0.18f), Accent, CloseClue, 21);
            UIFactory.Place(closeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(220f, 52f));

            cluePanel.gameObject.SetActive(false);
            modalLayer.gameObject.SetActive(false);
        }

        private void BuildKeypad()
        {
            var container = UIFactory.CreateRect(modalLayer, "KeypadContainer");
            UIFactory.Stretch(container);

            keypadUI = container.gameObject.AddComponent<KeypadUI>();
            keypadUI.Initialize(this, container);
        }

        private void BuildBackpack()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "BackpackOverlay",
                new Color(0f, 0f, 0f, 0.45f), true);
            UIFactory.Stretch(overlay.rectTransform);
            backpackOverlay = overlay.rectTransform;

            var panel = UIFactory.CreateImage(backpackOverlay, "BackpackPanel",
                new Color(0.04f, 0.06f, 0.08f, 0.99f), true);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f / 3f, 1f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var accentBar = UIFactory.CreateImage(panelRect, "BagAccentBar", Accent);
            accentBar.rectTransform.anchorMin = new Vector2(1f, 0f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(1f, 0.5f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(3f, 0f);

            var title = UIFactory.CreateText(panelRect, "BagTitle", "背包", 36, TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -30f), new Vector2(240f, 50f));

            var subtitle = UIFactory.CreateText(panelRect, "BagSubtitle", "4 × 8 · 32 格 · 点击物品查看详情", 17, Muted,
                TextAnchor.MiddleLeft);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(38f, -80f), new Vector2(260f, 26f));

            backpackCountText = UIFactory.CreateText(panelRect, "BagCount", "已收集 0 / 32", 17, Muted,
                TextAnchor.MiddleRight);
            UIFactory.Place(backpackCountText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-118f, -80f), new Vector2(240f, 26f));

            var closeButton = UIFactory.CreateButton(panelRect, "CloseBackpackButton", "关闭",
                new Color(0.11f, 0.15f, 0.18f), Accent, CloseBackpack, 18);
            UIFactory.Place(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(84f, 40f));

            var grid = UIFactory.CreateRect(panelRect, "BackpackGrid");
            UIFactory.Place(grid, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -120f), new Vector2(560f, 866f));

            bagSlots = new SlotView[32];
            for (int i = 0; i < 32; i++)
            {
                int column = i % 4;
                int row = i / 4;

                var background = UIFactory.CreateImage(grid, "BagSlot_" + i, SlotEmpty, false);
                UIFactory.Place(background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(column * 144f, -row * 110f), new Vector2(128f, 96f));

                background.raycastTarget = true;
                var slotDrag = background.gameObject.AddComponent<InventorySlotDrag>();
                slotDrag.Owner = this;
                slotDrag.Index = i;

                var index = UIFactory.CreateText(background.rectTransform, "Index", (i + 1).ToString(),
                    12, Muted, TextAnchor.UpperLeft);
                UIFactory.Place(index.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(6f, -4f), new Vector2(36f, 18f));

                var icon = UIFactory.CreateImage(background.rectTransform, "Icon", Color.white, false);
                UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 12f), new Vector2(54f, 54f));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.gameObject.SetActive(false);

                var label = UIFactory.CreateText(background.rectTransform, "Label", "—", 15, Muted);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 5f), new Vector2(118f, 28f));

                bagSlots[i] = new SlotView
                {
                    Background = background,
                    Icon = icon,
                    Label = label
                };
            }

            backpackOverlay.gameObject.SetActive(false);
        }

        private void BuildWinPanel()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "WinOverlay",
                new Color(0.012f, 0.022f, 0.035f, 0.94f), true);
            UIFactory.Stretch(overlay.rectTransform);
            winPanel = overlay.rectTransform;

            var card = UIFactory.CreateImage(winPanel, "WinCard",
                new Color(0.04f, 0.06f, 0.075f, 0.98f), true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 460f));

            var accentBar = UIFactory.CreateImage(card.rectTransform, "WinAccentBar", Accent);
            accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "WinTitle", "逃出庄园", 64, Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 112f), new Vector2(600f, 90f));
            winTitleText = title;

            var subtitle = UIFactory.CreateText(card.rectTransform, "WinSubtitle",
                "封印大门在你身后缓缓合拢，走廊尽头是清晨的光。", 22, Muted);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 36f), new Vector2(620f, 40f));
            winSubtitleText = subtitle;

            winTimeText = UIFactory.CreateText(card.rectTransform, "WinTime", string.Empty, 25, TextColor);
            UIFactory.Place(winTimeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(400f, 40f));

            winRankText = UIFactory.CreateText(card.rectTransform, "WinRank", string.Empty, 18, Muted);
            UIFactory.Place(winRankText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -72f), new Vector2(520f, 28f));

            var restart = UIFactory.CreateButton(card.rectTransform, "RestartButton", "重新开始",
                new Color(0.1f, 0.15f, 0.17f), Accent, Restart, 23);
            UIFactory.Place(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(240f, 56f));

            winPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 进入游戏时的主界面：标题 + 开始游戏 / 设置 / 退出游戏。
        /// 铺满全屏且不透明，同时作为模态层屏蔽 HUD 交互；设置面板创建在其之上，可从主界面直接打开。
        /// </summary>
        private void BuildMainMenu()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "MainMenuOverlay",
                new Color(0.016f, 0.024f, 0.032f, 1f), true);
            UIFactory.Stretch(overlay.rectTransform);
            mainMenuOverlay = overlay.rectTransform;

            var sideBar = UIFactory.CreateImage(mainMenuOverlay, "MainMenuSideBar",
                new Color(0.5f, 0.89f, 0.76f, 0.85f));
            UIFactory.Place(sideBar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(140f, 0f), new Vector2(3f, 660f));

            var title = UIFactory.CreateText(mainMenuOverlay, "MainMenuTitle", "谜宅逃脱", 86,
                TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(184f, 246f), new Vector2(720f, 108f));
            AddShadow(title.gameObject, new Vector2(2f, -2f));
            mainMenuTitleText = title;

            var titleEn = UIFactory.CreateText(mainMenuOverlay, "MainMenuTitleEn",
                "P U Z Z L E   E S C A P E", 22, Accent, TextAnchor.MiddleLeft);
            UIFactory.Place(titleEn.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(188f, 176f), new Vector2(560f, 32f));
            mainMenuTitleEnText = titleEn;

            var divider = UIFactory.CreateImage(mainMenuOverlay, "MainMenuDivider",
                new Color(0.5f, 0.89f, 0.76f, 0.28f));
            UIFactory.Place(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(188f, 138f), new Vector2(440f, 2f));

            var description = UIFactory.CreateText(mainMenuOverlay, "MainMenuDescription",
                "你在一座两层庄园的书房里醒来。\n找到日、月、星、影四枚徽记，打开西侧的封印大门。",
                22, new Color(0.72f, 0.77f, 0.81f, 1f), TextAnchor.UpperLeft);
            UIFactory.Place(description.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(188f, 62f), new Vector2(760f, 76f));
            description.lineSpacing = 1.15f;
            mainMenuDescriptionText = description;

            var continueButton = UIFactory.CreateButton(mainMenuOverlay, "MainMenuContinueButton", "继续游戏",
                new Color(0.1f, 0.15f, 0.18f), Accent, ContinueGame, 26);
            UIFactory.Place(continueButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(188f, -34f), new Vector2(360f, 64f));
            mainMenuContinueButton = continueButton;

            var startButton = UIFactory.CreateButton(mainMenuOverlay, "MainMenuStartButton", "开始游戏",
                Accent, new Color(0.03f, 0.09f, 0.07f), StartGameFromMenu, 26);
            UIFactory.Place(startButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(188f, -104f), new Vector2(360f, 64f));

            var mapButton = UIFactory.CreateButton(mainMenuOverlay, "MainMenuMapButton", "切换地图",
                new Color(0.1f, 0.15f, 0.18f), Accent, OpenMapSelect, 24);
            UIFactory.Place(mapButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(188f, -174f), new Vector2(360f, 64f));

            var settingsButton = UIFactory.CreateButton(mainMenuOverlay, "MainMenuSettingsButton", "设置",
                new Color(0.1f, 0.15f, 0.18f), Accent, OpenSettings, 24);
            UIFactory.Place(settingsButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(188f, -244f), new Vector2(360f, 64f));

            var quitButton = UIFactory.CreateButton(mainMenuOverlay, "MainMenuQuitButton", "退出游戏",
                new Color(0.14f, 0.1f, 0.11f), Danger, QuitGame, 24);
            UIFactory.Place(quitButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(188f, -314f), new Vector2(360f, 64f));

            var hint = UIFactory.CreateText(mainMenuOverlay, "MainMenuHint",
                IsMobileUI
                    ? "左侧摇杆移动 · 右侧拖动转视角 · 交互按钮拾取道具 · 「奔跑」切换走/跑"
                    : "W A S D 移动 · F 交互 · E 手电筒 · B 背包 · J 档案 · Esc 设置",
                17, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(188f, -388f), new Vector2(920f, 28f));

            var version = UIFactory.CreateText(mainMenuOverlay, "MainMenuVersion",
                "室内解谜 · 第一人称", 16, new Color(0.4f, 0.45f, 0.5f, 1f), TextAnchor.LowerRight);
            UIFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-56f, 44f), new Vector2(420f, 26f));

            mainMenuOpen = true;
            mainMenuOverlay.gameObject.SetActive(true);
            ApplySelectedMap();
            RefreshContinueButton();
        }

        /// <summary>把当前选择的地图标题与简介同步到主界面文本。</summary>
        public void ApplySelectedMap()
        {
            GameMap map = GameMapCatalog.Current;

            if (mainMenuTitleText != null)
            {
                mainMenuTitleText.text = map.Title;
            }

            if (mainMenuTitleEnText != null)
            {
                mainMenuTitleEnText.text = map.TitleEn;
            }

            if (mainMenuDescriptionText != null)
            {
                mainMenuDescriptionText.text = map.Description;
            }
        }

        private void OpenMapSelect()
        {
            MapSelectUI.Instance?.Show();
        }

        private void StartGameFromMenu()
        {
            ShowDifficultySelect();
        }

        /// <summary>
        /// 新游戏开始前的难度选择：选定后写入设置，本局内无法再修改。
        /// </summary>
        private void ShowDifficultySelect()
        {
            if (difficultyOverlay == null)
            {
                return;
            }

            pendingDifficulty = (int)DifficultySettings.Current;
            RefreshDifficultyOptions();
            difficultyOverlay.gameObject.SetActive(true);
            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        private void SelectDifficultyOption(int index)
        {
            pendingDifficulty = Mathf.Clamp(index, 0, DifficultySettings.Names.Length - 1);
            RefreshDifficultyOptions();
            AudioManager.Instance?.PlayClick();
        }

        private void RefreshDifficultyOptions()
        {
            if (difficultyOptionBackgrounds == null)
            {
                return;
            }

            for (int i = 0; i < difficultyOptionBackgrounds.Length; i++)
            {
                bool selected = i == pendingDifficulty;

                if (difficultyOptionBackgrounds[i] != null)
                {
                    difficultyOptionBackgrounds[i].color = selected
                        ? new Color(0.16f, 0.32f, 0.27f, 1f)
                        : new Color(0.1f, 0.14f, 0.17f, 1f);
                }

                if (difficultyOptionNames[i] != null)
                {
                    difficultyOptionNames[i].color = selected ? Accent : TextColor;
                }
            }
        }

        private void ConfirmDifficultyAndStart()
        {
            DifficultySettings.Set((GameDifficulty)pendingDifficulty);

            if (difficultyOverlay != null)
            {
                difficultyOverlay.gameObject.SetActive(false);
            }

            StartGame(true);
        }

        private void CloseDifficultySelect()
        {
            if (difficultyOverlay != null)
            {
                difficultyOverlay.gameObject.SetActive(false);
            }

            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        private void BuildDifficultySelect()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "DifficultyOverlay",
                new Color(0f, 0f, 0f, 0.88f), true);
            UIFactory.Stretch(overlay.rectTransform);
            difficultyOverlay = overlay.rectTransform;

            var card = UIFactory.CreateImage(difficultyOverlay, "DifficultyCard", PanelColor, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 640f));

            var accentBar = UIFactory.CreateImage(card.rectTransform, "DifficultyAccentBar", Accent);
            accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "DifficultyTitle", "选择难度", 42,
                TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -28f), new Vector2(420f, 56f));

            var subtitle = UIFactory.CreateText(card.rectTransform, "DifficultySubtitle",
                "本局开始后无法在游戏中更改。和平难度不会出现怪物。", 17, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(42f, -86f), new Vector2(680f, 28f));

            difficultyOptionBackgrounds = new Image[DifficultySettings.Names.Length];
            difficultyOptionNames = new Text[DifficultySettings.Names.Length];

            for (int i = 0; i < DifficultySettings.Names.Length; i++)
            {
                int index = i;
                var row = UIFactory.CreateButton(card.rectTransform, "DifficultyOption" + i, string.Empty,
                    new Color(0.1f, 0.14f, 0.17f), Accent, () => SelectDifficultyOption(index), 20);
                UIFactory.Place(row.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(40f, -130f - i * 80f), new Vector2(680f, 70f));

                difficultyOptionBackgrounds[i] = row.GetComponent<Image>();

                var name = UIFactory.CreateText(row.transform, "DifficultyName", DifficultySettings.Names[i],
                    24, TextColor, TextAnchor.MiddleLeft);
                UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(24f, 0f), new Vector2(150f, 44f));
                difficultyOptionNames[i] = name;

                var desc = UIFactory.CreateText(row.transform, "DifficultyDesc", DifficultyDescriptions[i],
                    15, Muted, TextAnchor.MiddleLeft);
                UIFactory.Place(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(184f, 0f), new Vector2(470f, 44f));
            }

            var start = UIFactory.CreateButton(card.rectTransform, "DifficultyStart", "开始游戏", Accent,
                new Color(0.03f, 0.09f, 0.07f), ConfirmDifficultyAndStart, 24);
            UIFactory.Place(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-110f, 34f), new Vector2(200f, 56f));

            var back = UIFactory.CreateButton(card.rectTransform, "DifficultyBack", "返回",
                new Color(0.11f, 0.15f, 0.18f), Accent, CloseDifficultySelect, 24);
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(110f, 34f), new Vector2(200f, 56f));

            difficultyOverlay.gameObject.SetActive(false);
        }

        /// <summary>
        /// 开始游戏：关闭主界面、重置计时并锁定鼠标。
        /// showWelcome 为 false 时用于读档，避免显示新游戏开场提示。
        /// </summary>
        public void StartGame(bool showWelcome = true)
        {
            if (!mainMenuOpen)
            {
                return;
            }

            GameMap map = GameMapCatalog.Current;
            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(map.SceneName) && activeScene != map.SceneName)
            {
                // 选中的地图在另一个场景：切换场景后由 UIRoot.Start 自动开始，避免再次弹主界面。
                GameSession.AutoStartNewGame = showWelcome;
                mainMenuOpen = false;
                settingsOpen = false;
                RefreshModalState();
                AudioManager.Instance?.PlayClick();
                SceneManager.LoadScene(map.SceneName);
                return;
            }

            mainMenuOpen = false;
            settingsOpen = false;

            if (mainMenuOverlay != null)
            {
                mainMenuOverlay.gameObject.SetActive(false);
            }

            GameManager.Instance?.StartTimer();
            RefreshModalState();
            AudioManager.Instance?.PlayClick();
            if (showWelcome)
            {
                SaveGameService.SelectSlotForNewGame();
                GameManager.Instance?.Toast("你在庄园书房中醒来。找到四枚徽记，打开西侧封印大门。");
                TutorialUI.Instance?.ShowIfNeeded();
            }
        }

        public void ContinueGame()
        {
            if (!SaveGameService.HasAnySave())
            {
                GameManager.Instance?.Toast("没有可读取的存档。");
                RefreshContinueButton();
                return;
            }

            SaveSlotUI.Instance?.Show(true);
        }

        private void RefreshContinueButton()
        {
            if (mainMenuContinueButton == null)
            {
                return;
            }

            bool hasSave = SaveGameService.HasAnySave();
            mainMenuContinueButton.interactable = hasSave;
            Text label = mainMenuContinueButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = hasSave ? Accent : Muted;
            }
        }

        /// <summary>
        /// 主界面重新显示（例如通关后回到主界面时使用）。
        /// </summary>
        public void ShowMainMenu()
        {
            mainMenuOpen = true;
            settingsOpen = false;
            clueOpen = false;
            backpackOpen = false;

            if (settingsOverlay != null)
            {
                settingsOverlay.gameObject.SetActive(false);
            }

            if (cluePanel != null)
            {
                cluePanel.gameObject.SetActive(false);
            }

            if (backpackOverlay != null)
            {
                backpackOverlay.gameObject.SetActive(false);
            }

            if (mainMenuOverlay != null)
            {
                mainMenuOverlay.gameObject.SetActive(true);
            }

            ApplySelectedMap();
            RefreshContinueButton();
            RefreshModalState();
        }

        /// <summary>全屏黑幕：跳杀结束后遮挡传送过程。</summary>
        private void BuildFade()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "FadeOverlay",
                new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = false;
            fadeOverlay = overlay;

            fadeLabel = UIFactory.CreateText(overlay.rectTransform, "FadeLabel", string.Empty,
                40, new Color(0.86f, 0.9f, 0.93f, 1f));
            UIFactory.Place(fadeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 120f));
            fadeLabel.raycastTarget = false;

            overlay.gameObject.SetActive(false);
        }

        /// <summary>设置黑幕透明度（0 全透明，1 全黑）。</summary>
        public void SetFadeAlpha(float alpha)
        {
            if (fadeOverlay == null)
            {
                return;
            }

            fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            fadeOverlay.gameObject.SetActive(alpha > 0.001f);
        }

        /// <summary>黑幕中央的提示文字，例如「你被抓住了 · 剩余机会 2」。</summary>
        public void SetFadeLabel(string text)
        {
            if (fadeLabel != null)
            {
                fadeLabel.text = text ?? string.Empty;
            }
        }

        /// <summary>被怪物抓住三次后的失败结算面板。</summary>
        private void BuildLosePanel()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "LoseOverlay",
                new Color(0.03f, 0.01f, 0.012f, 0.96f), true);
            UIFactory.Stretch(overlay.rectTransform);
            losePanel = overlay.rectTransform;

            var card = UIFactory.CreateImage(losePanel, "LoseCard",
                new Color(0.055f, 0.03f, 0.035f, 0.98f), true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(700f, 400f));

            var accentBar = UIFactory.CreateImage(card.rectTransform, "LoseAccentBar", Danger);
            accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "LoseTitle", "你被抓住了", 60, Danger);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 96f), new Vector2(600f, 90f));

            var subtitle = UIFactory.CreateText(card.rectTransform, "LoseSubtitle",
                "第三次失手，走廊的灯彻底熄灭，庄园重新安静下来。", 21, Muted);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 26f), new Vector2(620f, 34f));

            loseStatsText = UIFactory.CreateText(card.rectTransform, "LoseStats", string.Empty, 21, TextColor);
            UIFactory.Place(loseStatsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(420f, 34f));

            var restart = UIFactory.CreateButton(card.rectTransform, "LoseRestartButton", "重新开始",
                new Color(0.14f, 0.09f, 0.1f), Danger, Restart, 23);
            UIFactory.Place(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(240f, 56f));

            losePanel.gameObject.SetActive(false);
        }

        private void HandleLost()
        {
            loseOpen = true;
            winOpen = false;

            if (loseStatsText != null && GameManager.Instance != null)
            {
                loseStatsText.text = "用时 " + FormatElapsed(GameManager.Instance.FinalElapsed) +
                                     "  ·  被抓 " + GameManager.Instance.CaughtCount + " 次";
            }

            if (losePanel != null)
            {
                losePanel.gameObject.SetActive(true);
            }

            HidePrompt();
            RefreshModalState();
            Debug.Log("[PuzzleEscape] 失败结算：被怪物抓住 " +
                      (GameManager.Instance != null ? GameManager.Instance.CaughtCount : 0) + " 次。");
        }

        private void BuildSettings()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "SettingsOverlay",
                new Color(0f, 0f, 0f, 0.72f), true);
            UIFactory.Stretch(overlay.rectTransform);
            settingsOverlay = overlay.rectTransform;

            var card = UIFactory.CreateImage(settingsOverlay, "SettingsCard", PanelColor, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 880f));

            var accentBar = UIFactory.CreateImage(card.rectTransform, "SettingsAccentBar", Accent);
            accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "SettingsTitle", "设置", 46,
                TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -28f), new Vector2(260f, 60f));

            var subtitle = UIFactory.CreateText(card.rectTransform, "SettingsSubtitle",
                "Esc 返回游戏", 18, Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(42f, -92f), new Vector2(400f, 30f));
            settingsSubtitleText = subtitle;

            // 手机端专用：进入自定义控制布局。
            var customControlsButton = UIFactory.CreateButton(card.rectTransform, "SettingsCustomControls",
                "自定义控制", new Color(0.1f, 0.15f, 0.18f), Accent, BeginCustomControls, 18);
            UIFactory.Place(customControlsButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-40f, -28f), new Vector2(190f, 52f));
            customControlsButton.gameObject.SetActive(IsMobileUI);

            CreateSettingRow(card.rectTransform, "主音量", -170f, out settingsVolumeValue,
                DecreaseVolume, IncreaseVolume);
            CreateSettingRow(card.rectTransform, "鼠标灵敏度", -240f, out settingsSensitivityValue,
                DecreaseSensitivity, IncreaseSensitivity);
            CreateSettingToggle(card.rectTransform, "反转 Y 轴", -310f, out settingsInvertValue,
                ToggleInvertY);
            CreateSettingToggle(card.rectTransform, "显示模式", -380f, out settingsFullscreenValue,
                ToggleFullscreen);
            CreateQualityRow(card.rectTransform, -450f);
            CreateSettingRow(card.rectTransform, "帧率", -520f, out settingsFrameRateValue,
                DecreaseFrameRate, IncreaseFrameRate);
            CreateSettingRow(card.rectTransform, "视野", -590f, out settingsFieldOfViewValue,
                DecreaseFieldOfView, IncreaseFieldOfView);
            CreateSettingRow(card.rectTransform, "界面缩放", -660f, out settingsUiScaleValue,
                DecreaseUiScale, IncreaseUiScale);
            CreateSettingInfo(card.rectTransform, "难度", -730f, out settingsDifficultyValue, "本局固定");

            var save = UIFactory.CreateButton(card.rectTransform, "SettingsSave", "保存游戏",
                new Color(0.1f, 0.15f, 0.18f), Accent, SaveGame, 18);
            UIFactory.Place(save.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-270f, 32f), new Vector2(150f, 52f));

            var resume = UIFactory.CreateButton(card.rectTransform, "SettingsResume", "继续游戏",
                new Color(0.1f, 0.15f, 0.18f), Accent, CloseSettings, 18);
            UIFactory.Place(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-90f, 32f), new Vector2(150f, 52f));
            settingsResumeText = resume.GetComponentInChildren<Text>();

            var restart = UIFactory.CreateButton(card.rectTransform, "SettingsRestart", "重新开始",
                new Color(0.11f, 0.13f, 0.16f), TextColor, Restart, 18);
            UIFactory.Place(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(90f, 32f), new Vector2(150f, 52f));

            var quit = UIFactory.CreateButton(card.rectTransform, "SettingsQuit", "退出游戏",
                new Color(0.14f, 0.1f, 0.1f), Danger, QuitGame, 18);
            UIFactory.Place(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(270f, 32f), new Vector2(150f, 52f));

            settingsOverlay.gameObject.SetActive(false);
            SyncSettingsDisplay();
        }

        private void CreateSettingRow(RectTransform card, string label, float y, out Text valueText,
            UnityAction minus, UnityAction plus)
        {
            var labelText = UIFactory.CreateText(card, "SettingLabel", label, 22, TextColor,
                TextAnchor.MiddleLeft);
            UIFactory.Place(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, y), new Vector2(320f, 44f));

            var minusButton = UIFactory.CreateButton(card, "SettingMinus", "-",
                new Color(0.1f, 0.14f, 0.17f), Accent, minus, 25);
            UIFactory.Place(minusButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(400f, y + 2f), new Vector2(56f, 44f));

            valueText = UIFactory.CreateText(card, "SettingValue", "—", 20, TextColor);
            UIFactory.Place(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(470f, y), new Vector2(120f, 44f));

            var plusButton = UIFactory.CreateButton(card, "SettingPlus", "+",
                new Color(0.1f, 0.14f, 0.17f), Accent, plus, 25);
            UIFactory.Place(plusButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(600f, y + 2f), new Vector2(56f, 44f));
        }

        private void CreateSettingToggle(RectTransform card, string label, float y, out Text valueText,
            UnityAction toggle)
        {
            var labelText = UIFactory.CreateText(card, "SettingLabel", label, 22, TextColor,
                TextAnchor.MiddleLeft);
            UIFactory.Place(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, y), new Vector2(320f, 44f));

            valueText = UIFactory.CreateText(card, "SettingValue", "—", 20, TextColor);
            UIFactory.Place(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(470f, y), new Vector2(100f, 44f));

            var toggleButton = UIFactory.CreateButton(card, "SettingToggle", "切换",
                new Color(0.1f, 0.14f, 0.17f), Accent, toggle, 20);
            UIFactory.Place(toggleButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(582f, y + 2f), new Vector2(100f, 44f));
        }

        /// <summary>只读设置行：难度在开始新游戏时选定，游戏内不可修改。</summary>
        private void CreateSettingInfo(RectTransform card, string label, float y, out Text valueText, string note)
        {
            var labelText = UIFactory.CreateText(card, "SettingLabel", label, 22, TextColor,
                TextAnchor.MiddleLeft);
            UIFactory.Place(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, y), new Vector2(320f, 44f));

            valueText = UIFactory.CreateText(card, "SettingValue", "—", 20, TextColor,
                TextAnchor.MiddleLeft);
            UIFactory.Place(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(470f, y), new Vector2(140f, 44f));

            var noteText = UIFactory.CreateText(card, "SettingNote", note, 14, Muted,
                TextAnchor.MiddleLeft);
            UIFactory.Place(noteText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(608f, y), new Vector2(100f, 44f));
        }

        /// <summary>
        /// 光影质量三挡按钮（低 / 中 / 高），当前挡位高亮。
        /// </summary>
        private void CreateQualityRow(RectTransform card, float y)
        {
            var labelText = UIFactory.CreateText(card, "SettingLabel", "光影质量", 22, TextColor,
                TextAnchor.MiddleLeft);
            UIFactory.Place(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, y), new Vector2(320f, 44f));

            qualityButtonImages = new Image[3];
            qualityButtonLabels = new Text[3];

            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;
                var button = UIFactory.CreateButton(card, "QualityButton" + level,
                    LightingQuality.LevelNames[i], new Color(0.1f, 0.14f, 0.17f), Accent,
                    () => SetLightingQuality(level), 20);
                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(400f + i * 84f, y + 2f), new Vector2(76f, 44f));

                qualityButtonImages[i] = button.GetComponent<Image>();
                qualityButtonLabels[i] = button.GetComponentInChildren<Text>();
            }
        }

        private void SetLightingQuality(int level)
        {
            level = Mathf.Clamp(level, 1, 3);
            LightingQuality.Instance?.SetLevel(level);
            RefreshQualityButtons();
            AudioManager.Instance?.PlayClick();
            GameManager.Instance?.Toast("光影质量：" + LightingQuality.LevelNames[level - 1]);
        }

        private void RefreshQualityButtons()
        {
            if (qualityButtonImages == null || qualityButtonLabels == null)
            {
                return;
            }

            int active = LightingQuality.Instance != null ? LightingQuality.Instance.Level : 1;

            for (int i = 0; i < qualityButtonImages.Length; i++)
            {
                bool on = i + 1 == active;
                qualityButtonImages[i].color = on
                    ? Accent
                    : new Color(0.1f, 0.14f, 0.17f);
                qualityButtonLabels[i].color = on
                    ? new Color(0.03f, 0.09f, 0.07f)
                    : Accent;
            }
        }

        public void OpenSettings()
        {
            if (winOpen)
            {
                return;
            }

            clueOpen = false;
            backpackOpen = false;

            if (cluePanel != null)
            {
                cluePanel.gameObject.SetActive(false);
            }

            if (backpackOverlay != null)
            {
                backpackOverlay.gameObject.SetActive(false);
            }

            if (keypadUI != null && keypadUI.IsOpen)
            {
                keypadUI.Close();
            }

            settingsOpen = true;
            SyncSettingsDisplay();

            // 从主界面进入设置时，返回按钮回到主界面而不是游戏内。
            if (settingsResumeText != null)
            {
                settingsResumeText.text = mainMenuOpen ? "返回主界面" : "继续游戏";
            }

            if (settingsSubtitleText != null)
            {
                settingsSubtitleText.text = mainMenuOpen ? "Esc 返回主界面" : "Esc 返回游戏";
            }

            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        public void CloseSettings()
        {
            settingsOpen = false;
            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        /// <summary>手机端进入自定义控制布局：拖动屏幕上的摇杆与交互按钮。</summary>
        private void BeginCustomControls()
        {
            if (customizingControls || mobileJoystickRect == null || mobileInteractRect == null)
            {
                return;
            }

            if (settingsOpen)
            {
                CloseSettings();
            }

            customizingControls = true;

            joystickSiblingIndex = mobileJoystickRect.GetSiblingIndex();
            interactSiblingIndex = mobileInteractRect.GetSiblingIndex();
            runSiblingIndex = mobileRunRect != null ? mobileRunRect.GetSiblingIndex() : 0;
            dropSiblingIndex = mobileDropRect != null ? mobileDropRect.GetSiblingIndex() : 0;

            BuildCustomControlsOverlay();

            // 控件提到最上层，越过遮罩直接接收拖拽。
            mobileJoystickRect.SetAsLastSibling();
            mobileInteractRect.SetAsLastSibling();
            if (mobileRunRect != null)
            {
                mobileRunRect.SetAsLastSibling();
            }

            if (mobileDropRect != null)
            {
                mobileDropRect.SetAsLastSibling();
            }

            AttachDragHandle(mobileJoystickRect);
            AttachDragHandle(mobileInteractRect);
            if (mobileRunRect != null)
            {
                AttachDragHandle(mobileRunRect);
            }

            if (mobileDropRect != null)
            {
                AttachDragHandle(mobileDropRect);
            }

            if (mobileJoystick != null)
            {
                mobileJoystick.enabled = false;
            }

            if (mobileLookPad != null)
            {
                mobileLookPad.enabled = false;
            }

            Button interact = mobileInteractRect.GetComponent<Button>();
            if (interact != null)
            {
                interact.enabled = false;
            }

            if (mobileRunButton != null)
            {
                mobileRunButton.enabled = false;
            }

            if (mobileDropButton != null)
            {
                mobileDropButton.enabled = false;
            }

            MobileInput.Reset();
            RefreshModalState();
            AudioManager.Instance?.PlayClick();
            Debug.Log("[PuzzleEscape] 进入自定义控制布局。");
        }

        private void BuildCustomControlsOverlay()
        {
            var overlay = UIFactory.CreateImage(canvas.transform, "CustomControlsOverlay",
                new Color(0.02f, 0.03f, 0.04f, 0.72f), true);
            UIFactory.Stretch(overlay.rectTransform);
            customControlsOverlay = overlay.gameObject;

            var title = UIFactory.CreateText(overlay.rectTransform, "CustomControlsTitle",
                "拖动摇杆、交互、奔跑和丢弃按钮，放到顺手的位置", 26, TextColor);
            title.raycastTarget = false;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(820f, 80f));

            var hint = UIFactory.CreateText(overlay.rectTransform, "CustomControlsHint",
                "完成后自动保存，下次进入游戏依然生效", 18, Muted);
            hint.raycastTarget = false;
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -118f), new Vector2(820f, 40f));

            var reset = UIFactory.CreateButton(overlay.rectTransform, "CustomControlsReset", "恢复默认",
                new Color(0.14f, 0.1f, 0.1f), Danger, ResetCustomControls, 20);
            UIFactory.Place(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-105f, -172f), new Vector2(180f, 54f));

            var done = UIFactory.CreateButton(overlay.rectTransform, "CustomControlsDone", "完成",
                new Color(0.1f, 0.15f, 0.18f), Accent, FinishCustomControls, 20);
            UIFactory.Place(done.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(105f, -172f), new Vector2(180f, 54f));
        }

        private void FinishCustomControls()
        {
            if (!customizingControls)
            {
                return;
            }

            customizingControls = false;
            SaveControlPositions();

            if (customControlsOverlay != null)
            {
                Destroy(customControlsOverlay);
                customControlsOverlay = null;
            }

            DetachDragHandle(mobileJoystickRect);
            DetachDragHandle(mobileInteractRect);
            DetachDragHandle(mobileRunRect);
            DetachDragHandle(mobileDropRect);

            if (mobileJoystick != null)
            {
                mobileJoystick.enabled = true;
            }

            if (mobileLookPad != null)
            {
                mobileLookPad.enabled = true;
            }

            if (mobileRunButton != null)
            {
                mobileRunButton.enabled = true;
            }

            if (mobileDropButton != null)
            {
                mobileDropButton.enabled = true;
            }

            if (mobileRunRect != null)
            {
                mobileRunRect.SetSiblingIndex(Mathf.Clamp(
                    runSiblingIndex, 0, mobileRunRect.parent.childCount - 1));
            }

            if (mobileDropRect != null)
            {
                mobileDropRect.SetSiblingIndex(Mathf.Clamp(
                    dropSiblingIndex, 0, mobileDropRect.parent.childCount - 1));
            }

            if (mobileInteractRect != null)
            {
                Button interact = mobileInteractRect.GetComponent<Button>();
                if (interact != null)
                {
                    interact.enabled = true;
                }

                mobileInteractRect.SetSiblingIndex(Mathf.Clamp(
                    interactSiblingIndex, 0, mobileInteractRect.parent.childCount - 1));
            }

            if (mobileJoystickRect != null)
            {
                mobileJoystickRect.SetSiblingIndex(Mathf.Clamp(
                    joystickSiblingIndex, 0, mobileJoystickRect.parent.childCount - 1));
            }

            RefreshModalState();
            AudioManager.Instance?.PlayClick();

            Vector2 joystick = mobileJoystickRect != null
                ? mobileJoystickRect.anchoredPosition
                : Vector2.zero;
            Vector2 interactPos = mobileInteractRect != null
                ? mobileInteractRect.anchoredPosition
                : Vector2.zero;
            Vector2 runPos = mobileRunRect != null
                ? mobileRunRect.anchoredPosition
                : Vector2.zero;
            Vector2 dropPos = mobileDropRect != null
                ? mobileDropRect.anchoredPosition
                : Vector2.zero;
            Debug.Log("[PuzzleEscape] 自定义控制已保存：摇杆 " + joystick +
                      "，交互按钮 " + interactPos + "，奔跑按钮 " + runPos +
                      "，丢弃按钮 " + dropPos);
        }

        private void ResetCustomControls()
        {
            if (mobileJoystickRect != null)
            {
                mobileJoystickRect.anchoredPosition = JoystickDefaultPosition;
            }

            if (mobileInteractRect != null)
            {
                mobileInteractRect.anchoredPosition = InteractDefaultPosition;
            }

            if (mobileRunRect != null)
            {
                mobileRunRect.anchoredPosition = RunDefaultPosition;
            }

            if (mobileDropRect != null)
            {
                mobileDropRect.anchoredPosition = DropDefaultPosition;
            }
        }

        private void SaveControlPositions()
        {
            if (mobileJoystickRect != null)
            {
                Vector2 position = mobileJoystickRect.anchoredPosition;
                PlayerPrefs.SetFloat(JoystickXPref, position.x);
                PlayerPrefs.SetFloat(JoystickYPref, position.y);
            }

            if (mobileInteractRect != null)
            {
                Vector2 position = mobileInteractRect.anchoredPosition;
                PlayerPrefs.SetFloat(InteractXPref, position.x);
                PlayerPrefs.SetFloat(InteractYPref, position.y);
            }

            if (mobileRunRect != null)
            {
                Vector2 position = mobileRunRect.anchoredPosition;
                PlayerPrefs.SetFloat(RunXPref, position.x);
                PlayerPrefs.SetFloat(RunYPref, position.y);
            }

            if (mobileDropRect != null)
            {
                Vector2 position = mobileDropRect.anchoredPosition;
                PlayerPrefs.SetFloat(DropXPref, position.x);
                PlayerPrefs.SetFloat(DropYPref, position.y);
            }

            PlayerPrefs.Save();
        }

        private static Vector2 LoadControlPosition(string xKey, string yKey, Vector2 fallback)
        {
            return new Vector2(
                PlayerPrefs.GetFloat(xKey, fallback.x),
                PlayerPrefs.GetFloat(yKey, fallback.y));
        }

        private static void AttachDragHandle(RectTransform target)
        {
            UIDragHandle handle = target.GetComponent<UIDragHandle>();
            if (handle == null)
            {
                handle = target.gameObject.AddComponent<UIDragHandle>();
            }

            handle.Target = target;
            handle.enabled = true;
        }

        private static void DetachDragHandle(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            UIDragHandle handle = target.GetComponent<UIDragHandle>();
            if (handle != null)
            {
                handle.enabled = false;
            }
        }

        public void ToggleSettings()
        {
            if (settingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        private void SyncSettingsDisplay()
        {
            if (settingsVolumeValue != null)
            {
                settingsVolumeValue.text = Mathf.RoundToInt(masterVolume * 100f) + "%";
            }

            if (settingsSensitivityValue != null)
            {
                float sensitivity = boundController != null
                    ? boundController.MouseSensitivity
                    : PlayerPrefs.GetFloat("PuzzleEscape.MouseSensitivity", 2.1f);
                settingsSensitivityValue.text = sensitivity.ToString("0.0");
            }

            if (settingsInvertValue != null)
            {
                bool invert = boundController != null
                    ? boundController.InvertY
                    : PlayerPrefs.GetInt("PuzzleEscape.InvertY", 0) == 1;
                settingsInvertValue.text = invert ? "开启" : "关闭";
            }

            if (settingsFullscreenValue != null)
            {
                settingsFullscreenValue.text = Screen.fullScreen ? "全屏" : "窗口";
            }

            if (settingsFrameRateValue != null)
            {
                settingsFrameRateValue.text = FrameRateSettings.Load() + " FPS";
            }

            if (settingsFieldOfViewValue != null)
            {
                settingsFieldOfViewValue.text = Mathf.RoundToInt(fieldOfView) + "°";
            }

            if (settingsUiScaleValue != null)
            {
                settingsUiScaleValue.text = UIScaleSettings.Percentage + "%";
            }

            if (settingsDifficultyValue != null)
            {
                settingsDifficultyValue.text = DifficultySettings.Names[(int)DifficultySettings.Current];
            }

            RefreshQualityButtons();
        }

        private void DecreaseVolume()
        {
            AdjustVolume(-0.1f);
        }

        private void IncreaseVolume()
        {
            AdjustVolume(0.1f);
        }

        private void AdjustVolume(float delta)
        {
            masterVolume = Mathf.Clamp01(masterVolume + delta);
            AudioManager.Instance?.SetMasterVolume(masterVolume);
            PlayerPrefs.SetFloat("PuzzleEscape.MasterVolume", masterVolume);
            PlayerPrefs.Save();
            SyncSettingsDisplay();
            AudioManager.Instance?.PlayClick();
        }

        private void DecreaseSensitivity()
        {
            AdjustSensitivity(-0.2f);
        }

        private void IncreaseSensitivity()
        {
            AdjustSensitivity(0.2f);
        }

        private void AdjustSensitivity(float delta)
        {
            float sensitivity = boundController != null
                ? boundController.MouseSensitivity
                : PlayerPrefs.GetFloat("PuzzleEscape.MouseSensitivity", 2.1f);
            sensitivity = Mathf.Clamp(sensitivity + delta, 0.4f, 5f);

            if (boundController != null)
            {
                boundController.MouseSensitivity = sensitivity;
            }

            PlayerPrefs.SetFloat("PuzzleEscape.MouseSensitivity", sensitivity);
            PlayerPrefs.Save();
            SyncSettingsDisplay();
            AudioManager.Instance?.PlayClick();
        }

        private void ToggleInvertY()
        {
            bool invert = boundController != null
                ? boundController.InvertY
                : PlayerPrefs.GetInt("PuzzleEscape.InvertY", 0) == 1;
            invert = !invert;

            if (boundController != null)
            {
                boundController.InvertY = invert;
            }

            PlayerPrefs.SetInt("PuzzleEscape.InvertY", invert ? 1 : 0);
            PlayerPrefs.Save();
            SyncSettingsDisplay();
            AudioManager.Instance?.PlayClick();
        }

        private void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            SyncSettingsDisplay();
            AudioManager.Instance?.PlayClick();
        }

        private void DecreaseFrameRate()
        {
            AdjustFrameRate(-1);
        }

        private void IncreaseFrameRate()
        {
            AdjustFrameRate(1);
        }

        private void AdjustFrameRate(int direction)
        {
            int fps = FrameRateSettings.Step(direction);
            SyncSettingsDisplay();
            AudioManager.Instance?.PlayClick();
            GameManager.Instance?.Toast("帧率：" + fps + " FPS");
        }

        private void DecreaseUiScale()
        {
            AdjustUiScale(-1);
        }

        private void IncreaseUiScale()
        {
            AdjustUiScale(1);
        }

        private void AdjustUiScale(int direction)
        {
            UIScaleSettings.Step(direction);
            SyncSettingsDisplay();
            AudioManager.Instance?.PlayClick();
            GameManager.Instance?.Toast("界面缩放：" + UIScaleSettings.Percentage + "%");
        }

        private void DecreaseFieldOfView()
        {
            AdjustFieldOfView(-5f);
        }

        private void IncreaseFieldOfView()
        {
            AdjustFieldOfView(5f);
        }

        private void AdjustFieldOfView(float delta)
        {
            fieldOfView = Mathf.Clamp(fieldOfView + delta, 60f, 90f);
            PlayerPrefs.SetFloat("PuzzleEscape.FieldOfView", fieldOfView);
            PlayerPrefs.Save();
            ApplyFieldOfView();
            SyncSettingsDisplay();
            AudioManager.Instance?.PlayClick();
            GameManager.Instance?.Toast("视野：" + Mathf.RoundToInt(fieldOfView) + "°");
        }

        private void ApplyFieldOfView()
        {
            Camera targetCamera = Camera.main;

            if (targetCamera == null && boundController != null)
            {
                targetCamera = boundController.GetComponentInChildren<Camera>(true);
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = fieldOfView;
            }
        }

        private void QuitGame()
        {
            AudioManager.Instance?.PlayClick();
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void BindPlayer(PlayerRig rig)
        {
            if (rig == null)
            {
                return;
            }

            if (boundInventory != null)
            {
                boundInventory.Changed -= RefreshInventory;
            }

            boundInventory = rig.Inventory;
            boundInteractor = rig.Interactor;
            boundController = rig.Controller;

            if (boundInventory != null)
            {
                boundInventory.Changed += RefreshInventory;
            }

            RefreshInventory();
            ApplyFieldOfView();
            SyncSettingsDisplay();
        }

        public void BindInventory(PlayerInventory inventory)
        {
            if (boundInventory != null)
            {
                boundInventory.Changed -= RefreshInventory;
            }

            boundInventory = inventory;

            if (boundInventory != null)
            {
                boundInventory.Changed += RefreshInventory;
            }

            RefreshInventory();
        }

        private void RefreshInventory()
        {
            int count = boundInventory != null ? boundInventory.Count : 0;
            if (quickSlots != null)
            {
                UpdateSlots(quickSlots, 8);
            }

            if (bagSlots != null)
            {
                UpdateSlots(bagSlots, 32);
            }

            if (backpackCountText != null)
            {
                backpackCountText.text = "已收集 " + count + " / 32";
            }
        }

        private void OpenItemDetail(int index)
        {
            ItemDefinition item = boundInventory != null ? boundInventory.ItemAt(index) : null;
            if (item != null)
            {
                ItemDetailUI.Instance?.Show(item);
            }
        }

        private void UpdateSlots(SlotView[] slots, int max)
        {
            for (int i = 0; i < slots.Length && i < max; i++)
            {
                ItemDefinition item = boundInventory != null ? boundInventory.ItemAt(i) : null;
                SlotView view = slots[i];
                bool selected = i == selectedSlot && (slots == quickSlots || i < 8);

                if (item == null)
                {
                    view.Background.color = selected ? SlotSelected : SlotEmpty;
                    view.Icon.sprite = null;
                    view.Icon.gameObject.SetActive(false);
                    view.Label.text = "—";
                    view.Label.color = selected ? Accent : Muted;
                }
                else
                {
                    Sprite icon = ItemIconLibrary.Get(item.Id);
                    view.Background.color = selected ? SlotSelected : SlotFilled;
                    view.Icon.sprite = icon;
                    view.Icon.color = icon != null ? Color.white : item.Color;
                    view.Icon.gameObject.SetActive(true);
                    view.Label.text = item.DisplayName;
                    view.Label.color = selected ? Accent : TextColor;
                }
            }
        }

        private void MakeQuickSlotsInteractive()
        {
            if (quickSlots == null)
            {
                return;
            }

            for (int i = 0; i < quickSlots.Length; i++)
            {
                int captured = i;
                Image background = quickSlots[i].Background;
                background.raycastTarget = true;

                var button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SelectSlot(captured));
            }
        }

        public void SelectSlot(int index)
        {
            selectedSlot = Mathf.Clamp(index, 0, 7);
            RefreshInventory();
        }

        /// <summary>滚轮在已经放有物品的快捷栏格子之间循环选择。</summary>
        private void CycleSelection(int direction)
        {
            var occupied = new System.Collections.Generic.List<int>(8);
            for (int i = 0; i < 8; i++)
            {
                if (boundInventory != null && boundInventory.ItemAt(i) != null)
                {
                    occupied.Add(i);
                }
            }

            if (occupied.Count == 0)
            {
                selectedSlot = 0;
                RefreshInventory();
                return;
            }

            int position = occupied.IndexOf(selectedSlot);
            if (position < 0)
            {
                position = direction > 0 ? -1 : 0;
            }

            position = (position + direction + occupied.Count) % occupied.Count;
            SelectSlot(occupied[position]);
        }

        public bool BeginSlotDrag(int index, Vector2 screenPosition)
        {
            if (boundInventory == null)
            {
                return false;
            }

            ItemDefinition item = boundInventory.ItemAt(index);
            if (item == null)
            {
                return false;
            }

            dragSourceIndex = index;

            if (dragGhost != null)
            {
                Destroy(dragGhost.gameObject);
            }

            Image ghost = UIFactory.CreateImage(canvas.transform, "InventoryDragGhost", Color.white, false);
            Sprite icon = ItemIconLibrary.Get(item.Id);
            ghost.sprite = icon;
            ghost.preserveAspect = true;
            if (icon == null)
            {
                ghost.color = item.Color;
            }

            UIFactory.Place(ghost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(72f, 72f));
            ghost.rectTransform.SetAsLastSibling();
            dragGhost = ghost.rectTransform;
            UpdateSlotDrag(screenPosition);
            return true;
        }

        public void UpdateSlotDrag(Vector2 screenPosition)
        {
            if (dragGhost == null)
            {
                return;
            }

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, null, out Vector2 local))
            {
                dragGhost.anchoredPosition = local;
            }
        }

        public void EndSlotDrag(Vector2 screenPosition)
        {
            if (dragSourceIndex >= 0 && boundInventory != null)
            {
                int target = FindSlotIndexAt(screenPosition);
                if (target >= 0 && target != dragSourceIndex)
                {
                    boundInventory.Swap(dragSourceIndex, target);
                }
            }

            dragSourceIndex = -1;

            if (dragGhost != null)
            {
                Destroy(dragGhost.gameObject);
                dragGhost = null;
            }
        }

        public void ClickSlot(int index)
        {
            OpenItemDetail(index);
        }

        private int FindSlotIndexAt(Vector2 screenPosition)
        {
            if (bagSlots != null && backpackOpen)
            {
                for (int i = 0; i < bagSlots.Length; i++)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(
                            bagSlots[i].Background.rectTransform, screenPosition, null))
                    {
                        return i;
                    }
                }
            }

            if (quickSlots != null)
            {
                for (int i = 0; i < quickSlots.Length; i++)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(
                            quickSlots[i].Background.rectTransform, screenPosition, null))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>丢弃当前选中的快捷栏物品：先向前抛出，再受重力落到地面。</summary>
        public void DropSelectedItem()
        {
            if (boundInventory == null || IsModalOpen)
            {
                return;
            }

            ItemDefinition item = boundInventory.ItemAt(selectedSlot);
            if (item == null)
            {
                ShowToast("没有可丢弃的物品。");
                return;
            }

            Camera camera = Camera.main;
            Vector3 forward = camera != null ? camera.transform.forward : transform.forward;
            Vector3 origin = (camera != null ? camera.transform.position : transform.position) +
                             forward * 0.6f + Vector3.down * 0.1f;

            if (!boundInventory.RemoveAt(selectedSlot))
            {
                return;
            }

            ItemSpawnPoint.CreateDroppedItem(item.Id, origin, Vector3.zero,
                forward * 2.8f + Vector3.up * 1.4f, true);

            // 落地声：既是反馈，也是怪物能听见的线索。
            AudioManager.Instance?.PlayDrop();
            MonsterNpc.ReportNoise(origin);

            ShowToast("已丢弃 · " + item.DisplayName);
            AudioManager.Instance?.PlayClick();
        }

        public void SetObjective(string objective)
        {
            if (objectiveText != null)
            {
                objectiveText.text = "<color=#7FE3C4>目标</color>   " + objective;
            }
        }

        public void ShowPrompt(string message)
        {
            if (promptRoot == null)
            {
                return;
            }

            promptRoot.gameObject.SetActive(true);
            promptText.text = message;
        }

        public void HidePrompt()
        {
            if (promptRoot != null)
            {
                promptRoot.gameObject.SetActive(false);
            }
        }

        public void ShowToast(string message)
        {
            if (toastText == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (toastRoutine != null)
            {
                StopCoroutine(toastRoutine);
            }

            toastText.text = message;
            toastGroup.alpha = 1f;
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            yield return new WaitForSecondsRealtime(2.4f);

            float fade = 0.45f;
            for (float elapsed = 0f; elapsed < fade; elapsed += Time.unscaledDeltaTime)
            {
                toastGroup.alpha = 1f - elapsed / fade;
                yield return null;
            }

            toastGroup.alpha = 0f;
            toastRoutine = null;
        }

        public void ShowClue(string title, string body)
        {
            if (winOpen)
            {
                return;
            }

            backpackOpen = false;
            backpackOverlay.gameObject.SetActive(false);

            if (keypadUI != null && keypadUI.IsOpen)
            {
                keypadUI.Close();
            }

            clueOpen = true;
            clueTitle.text = title;
            clueBody.text = body;
            cluePanel.gameObject.SetActive(true);
            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        public void CloseClue()
        {
            clueOpen = false;
            cluePanel.gameObject.SetActive(false);
            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        public void OpenKeypad(KeypadPuzzle puzzle)
        {
            if (winOpen || puzzle == null)
            {
                return;
            }

            backpackOpen = false;
            backpackOverlay.gameObject.SetActive(false);
            clueOpen = false;
            cluePanel.gameObject.SetActive(false);

            keypadUI.Open(puzzle);
            RefreshModalState();
        }

        public void CloseKeypad()
        {
            if (keypadUI != null)
            {
                keypadUI.Close();
            }

            RefreshModalState();
        }

        public void ToggleBackpack()
        {
            if (winOpen || settingsOpen)
            {
                return;
            }

            if (keypadUI != null && keypadUI.IsOpen)
            {
                return;
            }

            if (clueOpen)
            {
                return;
            }

            backpackOpen = !backpackOpen;
            if (!backpackOpen)
            {
                CancelSlotDrag();
            }

            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        public void CloseBackpack()
        {
            backpackOpen = false;
            CancelSlotDrag();
            RefreshModalState();
            AudioManager.Instance?.PlayClick();
        }

        private void CancelSlotDrag()
        {
            dragSourceIndex = -1;

            if (dragGhost != null)
            {
                Destroy(dragGhost.gameObject);
                dragGhost = null;
            }
        }

        private void OnInteractPressed()
        {
            boundInteractor?.RequestInteract();
        }

        /// <summary>走路 / 奔跑模式切换，按钮文字显示下次点击会切换到哪个模式。</summary>
        private void ToggleRunMode()
        {
            MobileInput.Sprint = !MobileInput.Sprint;
            RefreshRunButton();
            AudioManager.Instance?.PlayClick();
        }

        /// <summary>体力耗尽时由玩家控制器调用，强制切回走路模式。</summary>
        public void SetSprintMode(bool sprint)
        {
            MobileInput.Sprint = sprint;
            RefreshRunButton();
        }

        private void RefreshRunButton()
        {
            if (mobileRunLabel != null)
            {
                mobileRunLabel.text = MobileInput.Sprint ? "走路" : "奔跑";
                mobileRunLabel.color = MobileInput.Sprint ? Accent : TextColor;
            }
        }

        private void RefreshModalState()
        {
            bool documentModal = clueOpen || (keypadUI != null && keypadUI.IsOpen);

            if (modalLayer != null)
            {
                modalLayer.gameObject.SetActive(documentModal);
            }

            if (backpackOverlay != null)
            {
                backpackOverlay.gameObject.SetActive(backpackOpen);
            }

            if (settingsOverlay != null)
            {
                settingsOverlay.gameObject.SetActive(settingsOpen);
            }

            bool modal = IsModalOpen;

            if (modal)
            {
                MobileInput.Reset();
                HidePrompt();
            }

            if (IsMobileUI)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (winOpen || modal)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// 供案件档案等外部模态面板在打开/关闭后刷新光标与 HUD 状态。
        /// </summary>
        public void NotifyExternalModalChanged()
        {
            RefreshModalState();
        }

        private void HandleWon()
        {
            winOpen = true;
            HidePrompt();
            settingsOpen = false;

            if (keypadUI != null)
            {
                keypadUI.Close();
            }

            clueOpen = false;
            backpackOpen = false;

            if (settingsOverlay != null)
            {
                settingsOverlay.gameObject.SetActive(false);
            }

            if (cluePanel != null)
            {
                cluePanel.gameObject.SetActive(false);
            }

            if (modalLayer != null)
            {
                modalLayer.gameObject.SetActive(false);
            }

            if (backpackOverlay != null)
            {
                backpackOverlay.gameObject.SetActive(false);
            }

            winPanel.gameObject.SetActive(true);

            GameManager game = GameManager.Instance;
            if (winTitleText != null)
            {
                winTitleText.text = game != null ? game.EndingTitle : "逃出庄园";
            }

            if (winSubtitleText != null)
            {
                winSubtitleText.text = game != null
                    ? game.EndingSubtitle
                    : "封印大门在你身后缓缓合拢，走廊尽头是清晨的光。";
            }

            if (winTimeText != null)
            {
                float elapsed = game != null ? game.FinalElapsed : 0f;
                winTimeText.text = "逃脱用时  " + FormatElapsed(elapsed);
            }

            if (winRankText != null)
            {
                string rank = game != null ? game.EndingRank : "C";
                winRankText.text = "评级 " + rank + "    ·    F9 可截图";
            }

            if (IsMobileUI)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void SaveGame()
        {
            SaveSlotUI.Instance?.Show(false);
        }

        public void Restart()
        {
            AudioManager.Instance?.PlayClick();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(activeScene.name);
            }
        }

        private void UpdateTimer()
        {
            if (timerText == null || winOpen || GameManager.Instance == null)
            {
                return;
            }

            float elapsed = Mathf.Max(0f, Time.time - GameManager.Instance.StartedAt);
            timerText.text = FormatElapsed(elapsed);
        }

        private void UpdateStamina()
        {
            if (staminaFill == null || boundController == null)
            {
                return;
            }

            float stamina = Mathf.Clamp(boundController.Stamina, 0f, FirstPersonController.MaxStamina);
            float ratio = stamina / FirstPersonController.MaxStamina;

            staminaFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            staminaFill.rectTransform.offsetMin = Vector2.zero;
            staminaFill.rectTransform.offsetMax = Vector2.zero;
            staminaFill.color = boundController.IsExhausted ? Danger : Accent;

            int shown = Mathf.CeilToInt(stamina);
            if (shown != lastStaminaShown && staminaLabel != null)
            {
                lastStaminaShown = shown;
                staminaLabel.text = "体力 " + shown + " / " +
                                    Mathf.RoundToInt(FirstPersonController.MaxStamina);
            }
        }

        private void UpdateCursorHint()
        {
            if (cursorLockHint == null)
            {
                return;
            }

            bool show = !IsMobileUI && !IsModalOpen && Cursor.lockState != CursorLockMode.Locked;

            if (cursorLockHint.gameObject.activeSelf != show)
            {
                cursorLockHint.gameObject.SetActive(show);
            }
        }

        private static string FormatElapsed(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return string.Format("{0:00}:{1:00}", total / 60, total % 60);
        }

        private static void AddShadow(GameObject target, Vector2 distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = distance;
        }

        private static Sprite GetBuiltinSprite(string path)
        {
            try
            {
                return Resources.GetBuiltinResource<Sprite>(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
