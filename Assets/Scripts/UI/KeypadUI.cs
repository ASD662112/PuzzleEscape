using PuzzleEscape.Audio;
using PuzzleEscape.Puzzle;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 可点击 / 可键盘输入的密码面板。
    /// </summary>
    public class KeypadUI : MonoBehaviour
    {
        private static readonly Color Accent = new Color(0.5f, 0.89f, 0.76f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.94f, 0.96f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.6f, 0.66f, 1f);
        private static readonly Color Danger = new Color(0.88f, 0.42f, 0.42f, 1f);
        private static readonly Color PanelColor = new Color(0.055f, 0.07f, 0.09f, 0.98f);

        private UIRoot owner;
        private GameObject panel;
        private Text displayText;
        private Text hintText;
        private Text footerText;
        private KeypadPuzzle puzzle;

        public bool IsOpen { get; private set; }

        public void Initialize(UIRoot uiRoot, Transform parent)
        {
            owner = uiRoot;
            panel = BuildPanel(parent);
            panel.SetActive(false);
        }

        private GameObject BuildPanel(Transform parent)
        {
            var card = UIFactory.CreateImage(parent, "KeypadPanel", PanelColor, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 700f));

            var accentBar = UIFactory.CreateImage(card.rectTransform, "KeypadAccentBar", Accent);
            accentBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentBar.rectTransform.anchoredPosition = Vector2.zero;
            accentBar.rectTransform.sizeDelta = new Vector2(0f, 4f);

            var title = UIFactory.CreateText(card.rectTransform, "KeypadTitle", "电子密码锁", 32, TextColor);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 302f), new Vector2(440f, 44f));

            var subtitle = UIFactory.CreateText(card.rectTransform, "KeypadSubtitle", "SECURITY  KEYPAD", 16, Muted);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 266f), new Vector2(440f, 24f));

            var closeButton = UIFactory.CreateButton(card.rectTransform, "CloseKeypadButton", "关闭",
                new Color(0.11f, 0.15f, 0.18f), Muted, Close, 18);
            UIFactory.Place(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(70f, 36f));

            var displayBox = UIFactory.CreateImage(card.rectTransform, "KeypadDisplayBox",
                new Color(0.015f, 0.025f, 0.03f, 1f), true);
            UIFactory.Place(displayBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 164f), new Vector2(390f, 96f));

            displayText = UIFactory.CreateText(displayBox.rectTransform, "KeypadDisplay", string.Empty, 42,
                Accent);
            UIFactory.Stretch(displayText.rectTransform);
            displayText.rectTransform.offsetMin = new Vector2(10f, 4f);
            displayText.rectTransform.offsetMax = new Vector2(-10f, -4f);

            hintText = UIFactory.CreateText(card.rectTransform, "KeypadHint", "输入 4 位密码，按回车确认", 18, Muted);
            UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 106f), new Vector2(440f, 30f));

            var grid = UIFactory.CreateRect(card.rectTransform, "KeypadGrid");
            UIFactory.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -112f), new Vector2(430f, 390f));

            string[] labels =
            {
                "1", "2", "3",
                "4", "5", "6",
                "7", "8", "9",
                "清除", "0", "确认"
            };

            for (int index = 0; index < labels.Length; index++)
            {
                string label = labels[index];
                int row = index / 3;
                int column = index % 3;

                Color background = new Color(0.13f, 0.16f, 0.19f, 1f);
                Color labelColor = TextColor;

                if (label == "清除")
                {
                    background = new Color(0.25f, 0.17f, 0.17f, 1f);
                    labelColor = new Color(0.9f, 0.63f, 0.63f, 1f);
                }
                else if (label == "确认")
                {
                    background = Accent;
                    labelColor = new Color(0.04f, 0.1f, 0.08f, 1f);
                }

                string captured = label;
                var button = UIFactory.CreateButton(grid, "Key_" + label, label, background, labelColor,
                    () => HandleKey(captured), label.Length > 1 ? 22 : 28);

                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2((column - 1) * 144f, 141f - row * 94f), new Vector2(130f, 80f));
            }

            var footer = UIFactory.CreateText(card.rectTransform, "KeypadFooter",
                "线索提示：密码与房间里的时间有关", 16, Muted);
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(460f, 24f));
            footerText = footer;

            return card.gameObject;
        }

        public void Open(KeypadPuzzle target)
        {
            puzzle = target;
            IsOpen = true;
            panel.SetActive(true);
            hintText.text = "输入 4 位密码，按回车确认";
            hintText.color = Muted;
            if (footerText != null)
            {
                footerText.text = puzzle.FooterHint;
            }
            RefreshDisplay();
            AudioManager.Instance?.PlayClick();
        }

        public void Close()
        {
            IsOpen = false;
            puzzle = null;

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsOpen || puzzle == null)
            {
                return;
            }

            bool changed = false;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Submit();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                puzzle.Backspace();
                changed = true;
            }

            for (int digit = 0; digit <= 9; digit++)
            {
                KeyCode alphaKey = (KeyCode)((int)KeyCode.Alpha0 + digit);
                KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad0 + digit);

                if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
                {
                    puzzle.PressDigit(digit.ToString());
                    changed = true;
                }
            }

            if (changed)
            {
                if (hintText.color == Danger)
                {
                    hintText.text = "输入 4 位密码，按回车确认";
                    hintText.color = Muted;
                }

                RefreshDisplay();
            }
        }

        private void HandleKey(string key)
        {
            if (!IsOpen || puzzle == null)
            {
                return;
            }

            if (key == "清除")
            {
                puzzle.Clear();
                hintText.text = "输入 4 位密码，按回车确认";
                hintText.color = Muted;
                AudioManager.Instance?.PlayClick();
            }
            else if (key == "确认")
            {
                Submit();
                return;
            }
            else
            {
                puzzle.PressDigit(key);
            }

            RefreshDisplay();
        }

        private void Submit()
        {
            if (puzzle == null)
            {
                return;
            }

            if (puzzle.Entry.Length < puzzle.CodeLength)
            {
                hintText.text = "请输入完整的 " + puzzle.CodeLength + " 位密码";
                hintText.color = Danger;
                AudioManager.Instance?.PlayError();
                return;
            }

            bool solved = puzzle.Submit();

            if (solved)
            {
                hintText.text = "密码正确";
                hintText.color = Accent;
                RefreshDisplay();
                AudioManager.Instance?.PlaySuccess();
                owner.CloseKeypad();
            }
            else
            {
                hintText.text = "密码错误，请重新输入";
                hintText.color = Danger;
                RefreshDisplay();
                AudioManager.Instance?.PlayError();
            }
        }

        private void RefreshDisplay()
        {
            if (displayText == null)
            {
                return;
            }

            if (puzzle == null)
            {
                displayText.text = string.Empty;
                return;
            }

            string value = string.Empty;
            for (int i = 0; i < puzzle.CodeLength; i++)
            {
                if (i > 0)
                {
                    value += "   ";
                }

                value += i < puzzle.Entry.Length ? puzzle.Entry[i].ToString() : "·";
            }

            displayText.text = value;
        }
    }
}
