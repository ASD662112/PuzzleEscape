using System.Collections.Generic;
using PuzzleEscape.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 运行时构建 uGUI 的轻量工厂，避免依赖预制体与场景中的 UI 资源。
    /// </summary>
    public static class UIFactory
    {
        private static readonly Dictionary<GameLanguage, Font> FontCache =
            new Dictionary<GameLanguage, Font>();

        public static Font Font
        {
            get
            {
                GameLanguage language = Localization.Current;
                if (FontCache.TryGetValue(language, out Font cached) && cached != null)
                {
                    return cached;
                }

                Font font = CreateFont(language);
                FontCache[language] = font;
                return font;
            }
        }

        private static Font CreateFont(GameLanguage language)
        {
            string[] families;

            switch (language)
            {
                case GameLanguage.Japanese:
                    families = new[]
                    {
                        "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic",
                        "Microsoft YaHei", "Arial Unicode MS", "Arial"
                    };
                    break;
                case GameLanguage.Korean:
                    families = new[]
                    {
                        "Malgun Gothic", "Gulim", "Batang",
                        "Microsoft YaHei", "Arial Unicode MS", "Arial"
                    };
                    break;
                case GameLanguage.ChineseTraditional:
                    families = new[]
                    {
                        "Microsoft JhengHei", "Microsoft YaHei", "PingFang TC",
                        "Arial Unicode MS", "Arial"
                    };
                    break;
                default:
                    families = new[]
                    {
                        "Microsoft YaHei", "PingFang SC", "SimHei",
                        "Arial Unicode MS", "Arial"
                    };
                    break;
            }

            Font font = UnityEngine.Font.CreateDynamicFontFromOSFont(families, 20);
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public static Image CreateImage(Transform parent, string name, Color color, bool raycastTarget = false)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(parent, name);

            // 用 LocalizedText 代替原生 Text：给 text 赋值时自动按当前语言翻译，
            // 切换语言时也能统一刷新已经创建好的界面。
            var text = rect.gameObject.AddComponent<LocalizedText>();
            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color background,
            Color labelColor, UnityAction onClick, int fontSize = 24)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.97f, 0.95f, 1f);
            colors.pressedColor = new Color(0.76f, 0.83f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var text = CreateText(rect, "Label", label, fontSize, labelColor);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(6f, 4f);
            text.rectTransform.offsetMax = new Vector2(-6f, -4f);
            return button;
        }
    }
}
