using PuzzleEscape.Core;
using PuzzleEscape.Interaction;
using UnityEngine;

namespace PuzzleEscape.Player
{
    /// <summary>
    /// 玩家的躲藏状态：躲进柜子后禁止移动与拾取，怪物看不见玩家。
    /// 再按一次交互键退出。
    /// </summary>
    public class PlayerHiding : MonoBehaviour
    {
        public static PlayerHiding Instance { get; private set; }

        public bool IsHidden { get; private set; }

        public HidingSpot Spot { get; private set; }

        private FirstPersonController controller;
        private Vector3 exitPosition;
        private bool hasExitPosition;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Initialize(FirstPersonController value)
        {
            controller = value;
        }

        public void Enter(HidingSpot spot, string displayName, Vector3? hidePosition = null)
        {
            if (IsHidden || controller == null || spot == null)
            {
                return;
            }

            IsHidden = true;
            Spot = spot;

            // 记录进入前的位置，出来时送回原处。
            exitPosition = transform.position;
            hasExitPosition = true;

            if (hidePosition.HasValue)
            {
                Teleport(hidePosition.Value);
            }

            controller.MovementFrozen = true;
            GameManager.Instance?.Toast("你躲进了" + displayName + "（按 F 出来）");
        }

        public void Exit()
        {
            if (!IsHidden)
            {
                return;
            }

            HidingSpot spot = Spot;
            IsHidden = false;
            Spot = null;

            if (controller != null)
            {
                controller.MovementFrozen = false;
            }

            if (hasExitPosition)
            {
                Teleport(exitPosition);
            }

            if (spot != null)
            {
                spot.ForceExit();
            }

            GameManager.Instance?.Toast("你从藏身处出来。");
        }

        /// <summary>绕过 CharacterController 直接改位置，避免被柜体碰撞顶开。</summary>
        private void Teleport(Vector3 position)
        {
            CharacterController character = GetComponent<CharacterController>();
            bool wasEnabled = character != null && character.enabled;

            if (wasEnabled)
            {
                character.enabled = false;
            }

            transform.position = position;

            if (wasEnabled)
            {
                character.enabled = true;
            }
        }
    }
}
