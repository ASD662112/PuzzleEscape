using PuzzleEscape.Audio;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 根据玩家当前所在房间切换环境音分类。
    /// 地下室、藏书室、温室、厨房与卧室各有独立的循环底噪，其余区域只保留全图低频嗡鸣。
    /// </summary>
    public class RoomAmbienceDirector : MonoBehaviour
    {
        private RoomTone currentTone = RoomTone.None;
        private float nextScan;

        private void Start()
        {
            nextScan = Time.time + 1f;
        }

        private void Update()
        {
            if (Time.time < nextScan)
            {
                return;
            }

            nextScan = Time.time + 0.5f;

            GameManager game = GameManager.Instance;
            MapSystem map = MapSystem.Instance;
            if (game == null || !game.HasStarted || game.IsWon ||
                map == null || map.CurrentRoom == null)
            {
                ApplyTone(RoomTone.None);
                return;
            }

            ApplyTone(Classify(map.CurrentRoom));
        }

        private void ApplyTone(RoomTone tone)
        {
            if (tone == currentTone)
            {
                return;
            }

            currentTone = tone;
            AudioManager.Instance?.SetRoomTone(tone);
        }

        private static RoomTone Classify(MapSystem.RoomInfo room)
        {
            if (room.Floor == -1)
            {
                return RoomTone.Basement;
            }

            string name = room.DisplayName;

            if (name.Contains("温室") || name.Contains("日光房"))
            {
                return RoomTone.Greenhouse;
            }

            if (name.Contains("厨房") || name.Contains("餐厅") ||
                name.Contains("酒窖") || name.Contains("食品储藏") ||
                name.Contains("冰窖"))
            {
                return RoomTone.Kitchen;
            }

            if (name.Contains("书房") || name.Contains("藏书室") ||
                name.Contains("档案室") || name.Contains("实验室") ||
                name.Contains("观测室") || name.Contains("吸烟室"))
            {
                return RoomTone.Library;
            }

            if (name.Contains("卧室") || name.Contains("客房") ||
                name.Contains("仆役") || name.Contains("音乐室"))
            {
                return RoomTone.Bedroom;
            }

            return RoomTone.None;
        }
    }
}
