using System.IO;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>按 F9 保存当前画面到 persistentDataPath/Screenshots。</summary>
    public class PhotoMode : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                Capture();
            }
        }

        private void Capture()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.HasStarted)
            {
                return;
            }

            string directory = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(directory);

            string file = Path.Combine(directory,
                "PuzzleEscape_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

            ScreenCapture.CaptureScreenshot(file);
            AudioManager.Instance?.PlayClick();
            game.Toast("截图已保存 · " + file);
        }
    }
}
