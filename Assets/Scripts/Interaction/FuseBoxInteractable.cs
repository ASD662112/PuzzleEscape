using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// B1 配电间的总闸：一共缺三枚保险丝，逐枚装进去，
    /// 三枚集齐后恢复疗养院供电，一层正门与车库卷帘门才工作。
    /// </summary>
    public class FuseBoxInteractable : Interactable
    {
        public string MissingHint = "保险丝座还是空的，铜片被烧黑了。";

        private bool IsPowered => GameManager.Instance != null && GameManager.Instance.PowerOn;

        public override string Prompt => IsPowered ? "配电箱（已通电）" : "往总闸里装保险丝";

        public override bool CanInteract => !IsPowered;

        public override void Interact(PlayerInteractor interactor)
        {
            GameManager manager = GameManager.Instance;

            if (IsPowered)
            {
                manager?.Toast("总闸已经合上了，指示灯是绿的。");
                return;
            }

            PlayerInventory inventory = interactor != null
                ? interactor.GetComponent<PlayerInventory>()
                : null;

            if (inventory == null)
            {
                return;
            }

            foreach (string fuseId in ItemIds.BreakerFuses)
            {
                if (!inventory.Contains(fuseId))
                {
                    continue;
                }

                inventory.Remove(fuseId);
                manager?.InstallFuse(fuseId);
                AudioManager.Instance?.PlayDoor();
                return;
            }

            int installed = manager != null ? manager.InstalledFuses : 0;
            manager?.Toast(MissingHint + "（已装 " + installed + "/" + GameManager.RequiredFuses +
                                  "，还缺 " + (GameManager.RequiredFuses - installed) + " 枚）");
            AudioManager.Instance?.PlayError();
        }
    }
}
