using System.Collections.Generic;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 实验室三机关顺序谜题。顺序见实验室提示：红 → 蓝 → 绿。
    /// </summary>
    public class LabSequencePuzzle : MonoBehaviour
    {
        public int[] ExpectedOrder = { 1, 3, 2 };
        public SequenceSwitch[] Switches;
        public GameObject[] Blockers;
        public ItemSpawnPoint RewardSpawnPoint;

        private int step;

        public bool IsSolved { get; private set; }

        public void Press(int switchIndex, SequenceSwitch source)
        {
            if (IsSolved || ExpectedOrder.Length == 0)
            {
                return;
            }

            if (ExpectedOrder[step] != switchIndex)
            {
                step = 0;

                if (Switches != null)
                {
                    foreach (SequenceSwitch item in Switches)
                    {
                        item?.SetActivated(false);
                    }
                }

                AudioManager.Instance?.PlayError();
                GameManager.Instance?.Toast("机关顺序错误，三个开关同时复位了。");
                return;
            }

            step++;
            source.SetActivated(true);
            AudioManager.Instance?.PlayClick();

            if (step >= ExpectedOrder.Length)
            {
                Complete();
            }
            else
            {
                GameManager.Instance?.Toast("机关发出了一声轻响……");
            }
        }

        private void Complete()
        {
            IsSolved = true;

            if (Blockers != null)
            {
                foreach (GameObject blocker in Blockers)
                {
                    if (blocker != null)
                    {
                        blocker.SetActive(false);
                    }
                }
            }

            RewardSpawnPoint?.Spawn();
            GameManager.Instance?.NotifyLabSolved();
            AudioManager.Instance?.PlaySuccess();
        }

        /// <summary>读取存档时恢复机关完成状态，不重复刷新奖励。</summary>
        public void RestoreSolved(bool solved)
        {
            IsSolved = solved;
            if (!solved)
            {
                return;
            }

            if (Blockers != null)
            {
                foreach (GameObject blocker in Blockers)
                {
                    if (blocker != null)
                    {
                        blocker.SetActive(false);
                    }
                }
            }

            if (Switches != null)
            {
                foreach (SequenceSwitch item in Switches)
                {
                    item?.SetActivated(true);
                }
            }
        }
    }
}
