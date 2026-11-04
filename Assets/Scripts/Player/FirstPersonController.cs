using PuzzleEscape.Core;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Player
{
    /// <summary>
    /// 第一人称移动与视角，同时支持 PC 键鼠和手机虚拟摇杆/拖拽。
    /// </summary>
    public class FirstPersonController : MonoBehaviour
    {
        public float WalkSpeed = 3.3f;
        public float SprintSpeed = 5.2f;
        public float MouseSensitivity = 2.1f;
        public float TouchSensitivity = 0.16f;
        public bool InvertY;

        // 体力：满值 100，奔跑每秒 -5，走路每秒 +3；耗尽后回到 20 才能再次奔跑。
        public const float MaxStamina = 100f;
        public const float SprintDrainPerSecond = 5f;
        public const float WalkRegenPerSecond = 3f;
        public const float SprintResumeThreshold = 20f;

        public float Stamina { get; private set; } = MaxStamina;

        /// <summary>体力耗尽后强制走路，直到回复到 20 才能再次奔跑。</summary>
        public bool IsExhausted { get; private set; }

        private CharacterController controller;
        private Transform cameraTransform;
        private float pitch;
        private float verticalVelocity;

        /// <summary>躲进柜子等藏身点时锁定移动，但视角仍可转动。</summary>
        public bool MovementFrozen { get; set; }

        public void Initialize(Transform camera)
        {
            cameraTransform = camera;
            controller = GetComponent<CharacterController>();
        }

        public float Pitch => pitch;

        public void SetPitch(float value)
        {
            pitch = Mathf.Clamp(value, -85f, 85f);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void Start()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            MobileInput.Reset();

            MouseSensitivity = PlayerPrefs.GetFloat("PuzzleEscape.MouseSensitivity", MouseSensitivity);
            InvertY = PlayerPrefs.GetInt("PuzzleEscape.InvertY", InvertY ? 1 : 0) == 1;

            // 主界面（或其它模态面板）打开时保持鼠标可见，光标状态由 UIRoot 统一管理。
            if (!IsMobile && (UIRoot.Instance == null || !UIRoot.Instance.IsModalOpen))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private static bool IsMobile => UIRoot.Instance != null && UIRoot.Instance.IsMobileUI;

        private void OnApplicationFocus(bool hasFocus)
        {
            if (IsMobile)
            {
                return;
            }

            if (!hasFocus)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (UIRoot.Instance == null || !UIRoot.Instance.IsModalOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            if (UIRoot.Instance != null && UIRoot.Instance.IsModalOpen)
            {
                return;
            }

            if (GameManager.Instance != null &&
                (GameManager.Instance.IsWon || GameManager.Instance.IsLost))
            {
                return;
            }

            if (controller == null || cameraTransform == null)
            {
                return;
            }

            // Game View 刚获得焦点时鼠标可能仍未锁定；点击画面后重新锁定。
            if (!IsMobile && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            UpdateLook();
            UpdateMove();
        }

        /// <summary>
        /// 兜底：若相机被其他脚本或场景操作从玩家身上移开，重新挂回玩家头部。
        /// </summary>
        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }

                cameraTransform = main.transform;
            }

            if (cameraTransform.parent == transform)
            {
                return;
            }

            cameraTransform.SetParent(transform, false);
            cameraTransform.localPosition = new Vector3(0f, 1.58f, 0f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateLook()
        {
            Vector2 look;

            if (IsMobile)
            {
                look = MobileInput.ConsumeLookDelta() * TouchSensitivity;
            }
            else
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    return;
                }

                look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

                if (InvertY)
                {
                    look.y = -look.y;
                }

                look *= MouseSensitivity;
            }

            if (look.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMove()
        {
            if (MovementFrozen)
            {
                // 躲进柜子后站着不动，按走路速度继续回复体力（不能奔跑，所以只回不扣）。
                UpdateStamina(false);
                return;
            }

            Vector2 input;
            bool wantsSprint;

            if (IsMobile)
            {
                input = MobileInput.Move;
                wantsSprint = MobileInput.Sprint;
            }
            else
            {
                float horizontal = 0f;
                float vertical = 0f;

                if (Input.GetKey(KeyCode.D))
                {
                    horizontal += 1f;
                }

                if (Input.GetKey(KeyCode.A))
                {
                    horizontal -= 1f;
                }

                if (Input.GetKey(KeyCode.W))
                {
                    vertical += 1f;
                }

                if (Input.GetKey(KeyCode.S))
                {
                    vertical -= 1f;
                }

                input = new Vector2(horizontal, vertical);
                wantsSprint = Input.GetKey(KeyCode.LeftShift);
            }

            float magnitude = Mathf.Clamp01(input.magnitude);
            bool moving = magnitude > 0.01f;
            bool sprinting = wantsSprint && moving && !IsExhausted && Stamina > 0f;

            sprinting = UpdateStamina(sprinting);

            float speed = sprinting ? SprintSpeed : WalkSpeed;
            Vector3 direction = transform.right * input.x + transform.forward * input.y;

            if (magnitude <= 0.01f)
            {
                direction = Vector3.zero;
            }
            else
            {
                direction.Normalize();
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            Vector3 velocity = direction * (speed * magnitude) + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// 结算体力：奔跑扣 5/秒，其余状态（走路、站定、躲藏）回 3/秒。
        /// 返回本帧是否真的在奔跑——体力刚耗尽时会强制改为 false。
        /// </summary>
        private bool UpdateStamina(bool sprinting)
        {
            if (sprinting)
            {
                Stamina -= SprintDrainPerSecond * Time.deltaTime;

                if (Stamina <= 0f)
                {
                    Stamina = 0f;
                    IsExhausted = true;
                    sprinting = false;

                    if (IsMobile)
                    {
                        UIRoot.Instance?.SetSprintMode(false);
                    }
                }

                return sprinting;
            }

            Stamina = Mathf.Min(MaxStamina, Stamina + WalkRegenPerSecond * Time.deltaTime);

            if (IsExhausted && Stamina >= SprintResumeThreshold)
            {
                IsExhausted = false;
            }

            return false;
        }

        /// <summary>
        /// 电梯等场景使用：绕过 CharacterController 的位置锁定。
        /// </summary>
        public void Teleport(Vector3 destination, float yaw)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }

            transform.SetPositionAndRotation(destination, Quaternion.Euler(0f, yaw, 0f));

            pitch = 0f;
            verticalVelocity = 0f;

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.identity;
            }

            if (controller != null)
            {
                controller.enabled = true;
            }

            MobileInput.Reset();

            if (!IsMobile)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
