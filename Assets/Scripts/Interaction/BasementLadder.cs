using PuzzleEscape.Core;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 地下室里的回程梯子：把玩家送回锅炉房的地窖入口。
    /// </summary>
    public class BasementLadder : Interactable
    {
        public Vector3 Destination;
        public float DestinationYaw;
        public LightingZone Zone;

        public override string Prompt => "攀爬梯子返回锅炉房";

        public override void Interact(PlayerInteractor interactor)
        {
            FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
            if (controller == null)
            {
                return;
            }

            controller.Teleport(Destination, DestinationYaw);
            Zone?.Exit();
            GameManager.Instance?.Toast("你顺着梯子回到了锅炉房。");
        }
    }
}
