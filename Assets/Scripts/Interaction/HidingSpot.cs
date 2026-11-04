using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 可躲藏的柜子：按 F 躲进去，再按 F 出来。
    /// 柜门从头到尾保持关闭（外面看不出里面有人），怪物视线也会跳过玩家。
    /// 把自己复制到场景其他地方即可得到更多躲藏点。
    /// </summary>
    public class HidingSpot : Interactable
    {
        public string DisplayName = "柜子";

        [Tooltip("柜门；只用来保证躲藏期间保持关闭，可以留空。")]
        public Transform Door;

        [Tooltip("躲避时玩家被送到这个点；留空则原地站着。")]
        public Transform HidePoint;

        private Quaternion closedRotation;

        public override string Prompt
        {
            get
            {
                PlayerHiding hiding = PlayerHiding.Instance;
                if (hiding != null && hiding.IsHidden && hiding.Spot == this)
                {
                    return "离开" + DisplayName;
                }

                return "躲进" + DisplayName;
            }
        }

        private void Start()
        {
            if (Door != null)
            {
                closedRotation = Door.localRotation;
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            PlayerHiding hiding = ResolveHiding(interactor);
            if (hiding == null)
            {
                Debug.LogWarning("[PuzzleEscape] 找不到 PlayerHiding，无法躲藏：" + name);
                return;
            }

            if (hiding.IsHidden)
            {
                if (hiding.Spot == this)
                {
                    hiding.Exit();
                }

                return;
            }

            // 躲进去时先关掉柜体碰撞，玩家才能站进柜子内部而不被顶出来。
            // 柜门保持关闭：从外面看不出里面有人，出来时才恢复碰撞。
            SetColliderEnabled(false);
            hiding.Enter(this, DisplayName, HidePoint != null ? HidePoint.position : (Vector3?)null);
            SetDoor(false);
            Debug.Log("[PuzzleEscape] 玩家躲进" + DisplayName + "：" + transform.position);
        }

        /// <summary>
        /// 取玩家身上的 PlayerHiding；万一没有（旧场景/预制体缺组件）就现场补一个，
        /// 保证躲藏柜不依赖 PlayerFactory 的版本是否正确。
        /// </summary>
        private static PlayerHiding ResolveHiding(PlayerInteractor interactor)
        {
            if (PlayerHiding.Instance != null)
            {
                return PlayerHiding.Instance;
            }

            PlayerHiding hiding = Object.FindObjectOfType<PlayerHiding>(true);

            if (hiding == null && interactor != null)
            {
                hiding = interactor.GetComponent<PlayerHiding>();
            }

            if (hiding == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    hiding = player.GetComponent<PlayerHiding>();
                    if (hiding == null)
                    {
                        hiding = player.AddComponent<PlayerHiding>();
                    }

                    hiding.Initialize(player.GetComponent<FirstPersonController>());
                }
            }

            return hiding;
        }

        /// <summary>玩家退出藏身时由 PlayerHiding 调用，恢复柜体碰撞。</summary>
        public void ForceExit()
        {
            SetColliderEnabled(true);
            SetDoor(false);
        }

        private void SetColliderEnabled(bool value)
        {
            Collider body = GetComponent<Collider>();

            if (body != null)
            {
                body.enabled = value;
            }
        }

        /// <summary>柜门始终关闭：躲藏期间直接锁在初始角度，不做开合动画。</summary>
        private void SetDoor(bool open)
        {
            if (Door == null || open)
            {
                return;
            }

            Door.localRotation = closedRotation;
        }
    }
}
