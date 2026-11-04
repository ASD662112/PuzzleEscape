using System.Collections.Generic;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Interaction;
using PuzzleEscape.UI;
using PuzzleEscape.Audio;
using UnityEngine;

namespace PuzzleEscape.Player
{
    /// <summary>
    /// 玩家手电筒：获得后默认打开，按 E 切换开关；手机端保持常亮。
    /// 灯光挂在相机上，照射方向跟随视线。
    /// 同时管理半截蜡烛：只有选中蜡烛所在的快捷栏格才会照明，
    /// 每根蜡烛可燃烧 60 秒，未选中时暂停并保留剩余时间。
    /// </summary>
    public class PlayerLantern : MonoBehaviour
    {
        private const float CandleBurnSeconds = 60f;

        public Light Lamp;

        /// <summary>半截蜡烛的随身点光，由 PlayerFactory 创建。</summary>
        public Light CandleLamp;

        public bool IsOn { get; private set; }

        private PlayerInventory inventory;
        private bool wasOwned;

        // 每根蜡烛按所在槽位记录剩余燃烧时间；拖动物品交换槽位时一起交换。
        private readonly Dictionary<int, float> candleFuel = new Dictionary<int, float>();

        public void Initialize(PlayerInventory playerInventory, Light lamp, Light candleLamp = null)
        {
            inventory = playerInventory;
            Lamp = lamp;
            CandleLamp = candleLamp;

            if (inventory != null)
            {
                inventory.Changed += HandleInventoryChanged;
                inventory.Swapped += HandleSlotsSwapped;
                inventory.SlotCleared += HandleSlotCleared;
                EnsureCandleFuel();
            }

            if (CandleLamp != null)
            {
                CandleLamp.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= HandleInventoryChanged;
                inventory.Swapped -= HandleSlotsSwapped;
                inventory.SlotCleared -= HandleSlotCleared;
            }
        }

        private void Update()
        {
            if (inventory == null)
            {
                return;
            }

            UpdateCandle();
            TryPlaceCandle();

            if (Lamp == null)
            {
                return;
            }

            bool owned = inventory.Contains(ItemIds.Lantern);
            bool mobile = UIRoot.Instance != null && UIRoot.Instance.IsMobileUI;
            bool canToggle = UIRoot.Instance == null || !UIRoot.Instance.IsModalOpen;

            // 首次拿到手电筒时自动点亮，避免玩家在黑暗里摸索开关。
            if (owned && !wasOwned)
            {
                IsOn = true;
            }

            if (!owned)
            {
                IsOn = false;
            }

            wasOwned = owned;

            if (owned && !mobile && canToggle && Input.GetKeyDown(KeyCode.E))
            {
                Toggle();
            }

            Lamp.enabled = owned && IsOn;
        }

        private void UpdateCandle()
        {
            if (CandleLamp == null || UIRoot.Instance == null)
            {
                return;
            }

            int selected = UIRoot.Instance.SelectedSlotIndex;
            ItemDefinition item = inventory.ItemAt(selected);
            float fuel = 0f;

            bool candleSelected = item != null && item.Id == ItemIds.Candle &&
                                  candleFuel.TryGetValue(selected, out fuel) && fuel > 0f;

            if (candleSelected && !UIRoot.Instance.IsModalOpen)
            {
                fuel -= Time.deltaTime;

                if (fuel <= 0f)
                {
                    candleFuel.Remove(selected);
                    inventory.RemoveAt(selected);
                    GameManager.Instance?.Toast("蜡烛烧尽了。");
                    candleSelected = false;
                }
                else
                {
                    candleFuel[selected] = fuel;
                }
            }

            CandleLamp.enabled = candleSelected;
        }

        private void HandleInventoryChanged()
        {
            EnsureCandleFuel();
        }

        private void HandleSlotsSwapped(int from, int to)
        {
            bool fromHas = candleFuel.TryGetValue(from, out float fromFuel);
            bool toHas = candleFuel.TryGetValue(to, out float toFuel);

            if (toHas)
            {
                candleFuel[from] = toFuel;
            }
            else
            {
                candleFuel.Remove(from);
            }

            if (fromHas)
            {
                candleFuel[to] = fromFuel;
            }
            else
            {
                candleFuel.Remove(to);
            }
        }

        private void HandleSlotCleared(int index)
        {
            candleFuel.Remove(index);
        }

        /// <summary>为新拿到的蜡烛初始化 60 秒燃烧时间；已经有计时的槽位保持不变。</summary>
        private void EnsureCandleFuel()
        {
            if (inventory == null)
            {
                return;
            }

            for (int i = 0; i < PlayerInventory.Capacity; i++)
            {
                ItemDefinition item = inventory.ItemAt(i);
                if (item != null && item.Id == ItemIds.Candle && !candleFuel.ContainsKey(i))
                {
                    candleFuel[i] = CandleBurnSeconds;
                }
            }
        }

        /// <summary>切换手电筒开关（E 键 / 手机端按钮均可调用）。</summary>
        public void Toggle()
        {
            IsOn = !IsOn;
            AudioManager.Instance?.PlayClick();
        }

        /// <summary>
        /// Windows 端：选中半截蜡烛后按鼠标右键，把蜡烛放在身前的地面上。
        /// 放下的蜡烛沿用这根蜡烛剩余的燃烧时间。
        /// </summary>
        private void TryPlaceCandle()
        {
            if (inventory == null || UIRoot.Instance == null)
            {
                return;
            }

            if (UIRoot.Instance.IsMobileUI || UIRoot.Instance.IsModalOpen)
            {
                return;
            }

            if (PlayerHiding.Instance != null && PlayerHiding.Instance.IsHidden)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(1))
            {
                return;
            }

            int selected = UIRoot.Instance.SelectedSlotIndex;
            ItemDefinition item = inventory.ItemAt(selected);
            if (item == null || item.Id != ItemIds.Candle)
            {
                return;
            }

            float fuel = candleFuel.TryGetValue(selected, out float remaining) ? remaining : CandleBurnSeconds;
            Vector3 position = FindCandlePlacement();

            inventory.RemoveAt(selected);
            PlacedCandle.Create(position, fuel);
            AudioManager.Instance?.PlayDrop();
            GameManager.Instance?.Toast("你把蜡烛放在了地上。");
        }

        /// <summary>在身前一米左右找落点：优先落到地面/桌面上，前方有墙时贴近墙根。</summary>
        private Vector3 FindCandlePlacement()
        {
            Transform eye = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = eye.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }

            forward.Normalize();

            Vector3 origin = eye.position + forward * 1.1f;

            // 前方 1 米内有墙/家具，把落点收回到障碍物前面，避免蜡烛嵌进模型。
            if (Physics.Raycast(eye.position, forward, out RaycastHit wall, 1.1f, ~0,
                    QueryTriggerInteraction.Ignore) && !IsPlayerCollider(wall.collider))
            {
                origin = wall.point - forward * 0.25f;
            }

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, 3f, ~0,
                    QueryTriggerInteraction.Ignore) && !IsPlayerCollider(ground.collider))
            {
                return ground.point + Vector3.up * 0.02f;
            }

            return new Vector3(origin.x, transform.position.y + 0.02f, origin.z);
        }

        private bool IsPlayerCollider(Collider other)
        {
            return other != null && (other.transform == transform || other.transform.IsChildOf(transform));
        }
    }
}
