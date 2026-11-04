using PuzzleEscape.Audio;
using UnityEngine;

namespace PuzzleEscape.Player
{
    /// <summary>
    /// 脚步录音跟随移动状态：移动时循环播放，停下时暂停（保留进度），
    /// 再次移动从暂停处继续，播到结尾后自动从头循环。
    /// </summary>
    public class FootstepAudio : MonoBehaviour
    {
        [Tooltip("水平速度超过该值视为正在移动。")]
        public float MoveThreshold = 0.45f;

        private CharacterController controller;
        private FirstPersonController movement;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            movement = GetComponent<FirstPersonController>();
        }

        private void Update()
        {
            if (controller == null || !controller.enabled || (movement != null && !movement.enabled))
            {
                AudioManager.Instance?.SetFootstepsMoving(false);
                return;
            }

            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            AudioManager.Instance?.SetFootstepsMoving(
                controller.isGrounded && velocity.magnitude > MoveThreshold);
        }
    }
}
