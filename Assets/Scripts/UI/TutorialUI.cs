using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>首次开始新游戏时的三行操作引导。</summary>
    public class TutorialUI : MonoBehaviour
    {
        public static TutorialUI Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private GameObject overlay;
        private const string TutorialKey = "PuzzleEscape.TutorialSeen";

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
            var canvasGo = new GameObject("TutorialCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 22;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            UIFactory.Stretch((RectTransform)canvasGo.transform);

            var backdrop = UIFactory.CreateImage(canvasGo.transform, "TutorialBackdrop",
                new Color(0f, 0f, 0f, 0.66f), true);
            UIFactory.Stretch(backdrop.rectTransform);
            overlay = backdrop.gameObject;

            var card = UIFactory.CreateImage(backdrop.rectTransform, "TutorialCard",
                new Color(0.04f, 0.06f, 0.075f, 0.99f), true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 460f));

            var accent = UIFactory.CreateImage(card.rectTransform, "TutorialAccent",
                new Color(0.5f, 0.89f, 0.76f, 1f));
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "TutorialTitle", "开始探索", 38,
                new Color(0.91f, 0.94f, 0.96f, 1f), TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -30f), new Vector2(500f, 48f));

            var body = UIFactory.CreateText(card.rectTransform, "TutorialBody",
                UIRoot.Instance != null && UIRoot.Instance.IsMobileUI
                    ? "1. 左侧摇杆移动，右侧拖动转动视角\n2. 点击「奔跑」切换走路 / 奔跑\n3. 靠近物品后点击「交互」\n4. 「背包」查看道具，「档案」查看笔记与提示\n\n阅读便签，打开抽屉，寻找四枚徽记。"
                    : "1. WASD 移动，鼠标观察，Shift 加速\n2. 靠近物品按 F 交互，拾取 / 阅读 / 打开容器\n3. B 打开背包，J 打开案件档案，E 开关手电筒\n\n阅读便签，打开抽屉，寻找四枚徽记。",
                21, new Color(0.78f, 0.82f, 0.86f, 1f), TextAnchor.UpperLeft);
            UIFactory.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -100f), new Vector2(640f, 220f));
            body.lineSpacing = 1.25f;

            var close = UIFactory.CreateButton(card.rectTransform, "TutorialClose", "开始探索",
                new Color(0.5f, 0.89f, 0.76f, 1f), new Color(0.03f, 0.09f, 0.07f), Close, 22);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(220f, 52f));

            overlay.SetActive(false);
        }

        public void ShowIfNeeded()
        {
            if (IsOpen || PlayerPrefs.GetInt(TutorialKey, 0) == 1)
            {
                return;
            }

            if (GameManager.Instance == null || !GameManager.Instance.HasStarted)
            {
                return;
            }

            IsOpen = true;
            overlay.SetActive(true);
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
            overlay.SetActive(false);
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();
            UIRoot.Instance?.NotifyExternalModalChanged();
            AudioManager.Instance?.PlayClick();
        }
    }
}
