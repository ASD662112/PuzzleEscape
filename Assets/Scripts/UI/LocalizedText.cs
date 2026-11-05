using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 会跟随语言切换自动更新的 Text。所有界面文字都用它创建：
    /// 直接赋值 text 时会按当前语言翻译，RefreshAll 会把已有文本重新翻译。
    /// </summary>
    public class LocalizedText : Text
    {
        private string sourceText;
        private string formatTemplate;
        private object[] formatArgs;

        public override string text
        {
            get { return base.text; }
            set
            {
                sourceText = value;
                formatTemplate = null;
                formatArgs = null;
                base.text = Core.Localization.T(value);
            }
        }

        /// <summary>带数字/名字的动态文本：翻译模板后再格式化，切换语言时可以按同一组参数重新生成。</summary>
        public void SetFormatted(string template, params object[] args)
        {
            sourceText = template;
            formatTemplate = template;
            formatArgs = args;
            base.text = Core.Localization.Format(template, args);
        }

        public void RefreshText()
        {
            Font font = UIFactory.Font;
            if (font != null && this.font != font)
            {
                this.font = font;
            }

            if (formatTemplate != null)
            {
                base.text = Core.Localization.Format(formatTemplate, formatArgs);
            }
            else
            {
                base.text = Core.Localization.T(sourceText);
            }
        }

        /// <summary>语言切换后刷新场景里所有已经创建的文本。</summary>
        public static void RefreshAll()
        {
            LocalizedText[] texts = Resources.FindObjectsOfTypeAll<LocalizedText>();
            for (int i = 0; i < texts.Length; i++)
            {
                LocalizedText text = texts[i];
                if (text == null || text.gameObject.scene.IsValid() == false)
                {
                    continue;
                }

                text.RefreshText();
            }
        }
    }
}
