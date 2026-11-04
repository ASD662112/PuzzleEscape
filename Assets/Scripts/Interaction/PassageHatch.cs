using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 暗道入口/出口：首次进入隐藏密室时写入案件档案，然后传送玩家。
    /// </summary>
    public class PassageHatch : Interactable
    {
        public string Label = "进入暗门";
        public Vector3 Destination;
        public float DestinationYaw;
        public string SecretId;
        public string SecretTitle;
        [TextArea] public string SecretBody;
        public string DiscoveryToast;

        public override string Prompt => Label;

        public override void Interact(PlayerInteractor interactor)
        {
            if (!string.IsNullOrEmpty(SecretId) && !CaseFileSystem.HasSecret(SecretId))
            {
                CaseFileSystem.RecordSecret(SecretId, SecretTitle, SecretBody);
                if (!string.IsNullOrEmpty(DiscoveryToast))
                {
                    GameManager.Instance?.Toast(DiscoveryToast);
                }
            }

            FirstPersonController controller = interactor != null
                ? interactor.GetComponent<FirstPersonController>()
                : null;

            if (controller != null)
            {
                controller.Teleport(Destination, DestinationYaw);
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    player.transform.SetPositionAndRotation(Destination,
                        Quaternion.Euler(0f, DestinationYaw, 0f));
                }
            }

            AudioManager.Instance?.PlayDoor();
        }
    }
}
