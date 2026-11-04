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
        private static Font cachedFont;

        public static Font Font
        {
            get
            {
                if (cachedFont != null)
                {
                    return cachedFont;
                }

                cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "PingFang SC", "SimHei", "Arial" }, 20);

                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                return cachedFont;
            }
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
            var text = rect.gameObject.AddComponent<Text>();
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
