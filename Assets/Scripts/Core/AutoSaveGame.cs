using PuzzleEscape.Core;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>每 60 秒自动保存一次，并在切后台/退出时补存。</summary>
    public class AutoSaveGame : MonoBehaviour
    {
        private float nextSaveAt;

        private void Start()
        {
            nextSaveAt = Time.time + 60f;
        }

        private void Update()
        {
            if (Time.time < nextSaveAt)
            {
                return;
            }

            nextSaveAt = Time.time + 60f;
            SaveGameService.SaveGame(false);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveGameService.SaveGame(false);
            }
        }

        private void OnApplicationQuit()
        {
            SaveGameService.SaveGame(false);
        }
    }
}
