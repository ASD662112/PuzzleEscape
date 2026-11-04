using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 房门的检查交互，为玩家提供“需要密码”的方向提示；解锁后可直接推开。
    /// </summary>
    public class DoorInteractable : Interactable
    {
        public HingedDoor Door;

        public override string Prompt
        {
            get
            {
                if (GameManager.Instance != null && GameManager.Instance.DoorUnlocked)
                {
                    return "推开房门";
                }

                return "检查房门";
            }
        }

        public override bool CanInteract => Door != null && !Door.IsOpen;

        public override void Interact(PlayerInteractor interactor)
        {
            if (GameManager.Instance != null && GameManager.Instance.DoorUnlocked)
            {
                Door?.Open();
                return;
            }

            GameManager.Instance?.Toast("房门被电子锁锁住了。门旁有一个密码键盘。");
            AudioManager.Instance?.PlayError();
        }
    }
}
