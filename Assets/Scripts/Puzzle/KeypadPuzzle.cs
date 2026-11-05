using System;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Interaction;
using PuzzleEscape.Player;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Puzzle
{
    /// <summary>
    /// 4 位密码锁逻辑。只有通电后才能打开输入面板。
    /// </summary>
    public class KeypadPuzzle : Interactable
    {
        public string Solution = "0427";
        public Renderer Indicator;
        public HingedDoor LinkedDoor;
        public bool UnlocksArchive;
        public bool RequiresPower = true;
        public string PuzzleId;
        public string FooterHint = "线索提示：密码与房间里的时间有关";
        public string RewardId;
        public string RewardTitle;
        [TextArea] public string RewardBody;

        private string entry = string.Empty;

        public string Entry => entry;
        public int CodeLength => Solution.Length;
        public bool IsSolved { get; private set; }

        public event Action Solved;

        public bool IsPowered => !RequiresPower || (GameManager.Instance != null && GameManager.Instance.PowerOn);

        public override string Prompt
        {
            get
            {
                if (IsSolved)
                {
                    return "密码键盘（已解锁）";
                }

                if (!RequiresPower)
                {
                    return "使用密码锁";
                }

                return IsPowered ? "使用密码键盘" : "检查密码键盘";
            }
        }

        public override bool CanInteract => !IsSolved;

        public override void Interact(PlayerInteractor interactor)
        {
            if (IsSolved)
            {
                return;
            }

            if (!IsPowered)
            {
                GameManager.Instance?.Toast("键盘没有反应，屏幕一片漆黑。也许该先找到电源。");
                AudioManager.Instance?.PlayError();
                return;
            }

            UIRoot.Instance?.OpenKeypad(this);
        }

        public void PressDigit(string digit)
        {
            if (IsSolved || string.IsNullOrEmpty(digit) || digit.Length != 1)
            {
                return;
            }

            if (entry.Length >= CodeLength)
            {
                return;
            }

            entry += digit;
            AudioManager.Instance?.PlayClick();
        }

        public void Backspace()
        {
            if (IsSolved || entry.Length == 0)
            {
                return;
            }

            entry = entry.Substring(0, entry.Length - 1);
            AudioManager.Instance?.PlayClick();
        }

        public void Clear()
        {
            entry = string.Empty;
        }

        /// <summary>
        /// 提交输入。正确返回 true；错误会清空输入并返回 false。
        /// </summary>
        public bool Submit()
        {
            if (IsSolved)
            {
                return false;
            }

            if (entry == Solution)
            {
                IsSolved = true;
                SetIndicator(new Color(0.35f, 1f, 0.55f, 1f));
                LinkedDoor?.Open();

                if (UnlocksArchive)
                {
                    GameManager.Instance?.NotifyArchiveUnlocked();
                }

                if (!string.IsNullOrEmpty(PuzzleId))
                {
                    CaseFileSystem.RecordPuzzle(PuzzleId);
                }

                if (!string.IsNullOrEmpty(RewardTitle))
                {
                    CaseFileSystem.RecordSecret(RewardId, RewardTitle, RewardBody);
                    UIRoot.Instance?.ShowClue(RewardTitle, RewardBody);
                    GameManager.Instance?.Toast("密码正确，找到了一份隐藏记录。");
                }

                AchievementSystem.Instance?.NotifyKeypadSolved();
                Solved?.Invoke();
                return true;
            }

            entry = string.Empty;
            return false;
        }

        public void SetPowered()
        {
            if (!IsSolved)
            {
                SetIndicator(new Color(1f, 0.75f, 0.35f, 1f));
            }
        }

        /// <summary>读取存档时恢复密码锁已解开状态。</summary>
        public void RestoreSolved(bool solved)
        {
            IsSolved = solved;
            entry = string.Empty;

            if (solved)
            {
                SetIndicator(new Color(0.35f, 1f, 0.55f, 1f));
            }
        }

        private void SetIndicator(Color color)
        {
            if (Indicator == null)
            {
                return;
            }

            Material material = Indicator.material;
            material.color = color;

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.9f);
            }
        }
    }
}
