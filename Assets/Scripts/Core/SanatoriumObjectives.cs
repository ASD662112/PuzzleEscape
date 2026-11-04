using PuzzleEscape.Data;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 疗养院的通关引导。共三条互相独立的逃生路线：
    /// 主线「一层正门」、支线「地下车库卷帘门」、支线「四层东侧消防连廊」。
    /// 目标文案按「通行卡 → 总闸供电 → 钥匙 → 下一张通行卡 → 出口」实时推进。
    /// </summary>
    public class SanatoriumObjectives : MonoBehaviour
    {
        private enum EscapeRoute
        {
            MainGate,
            Garage,
            FireEscape
        }

        private const float RefreshInterval = 0.4f;

        private PlayerInventory inventory;
        private float timer;
        private EscapeRoute route = EscapeRoute.MainGate;

        private void Update()
        {
            timer += Time.deltaTime;

            if (timer < RefreshInterval)
            {
                return;
            }

            timer = 0f;
            Refresh();
        }

        public void Refresh()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.IsWon)
            {
                return;
            }

            if (inventory == null)
            {
                inventory = FindObjectOfType<PlayerInventory>();
            }

            if (inventory == null)
            {
                return;
            }

            EscapeRoute previous = route;
            UpdateRoute();

            if (route != previous)
            {
                AnnounceRoute(manager);
            }

            string objective = BuildObjective(manager);

            if (objective == manager.CurrentObjective)
            {
                return;
            }

            manager.SetMapObjective(objective);
        }

        private void UpdateRoute()
        {
            if (inventory.Contains(ItemIds.GateCrank) || inventory.Contains(ItemIds.GarageKey))
            {
                route = EscapeRoute.Garage;
                return;
            }

            if (inventory.Contains(ItemIds.FireKey) || inventory.Contains(ItemIds.FireAxe))
            {
                route = EscapeRoute.FireEscape;
                return;
            }

            route = EscapeRoute.MainGate;
        }

        private void AnnounceRoute(GameManager manager)
        {
            if (route == EscapeRoute.Garage)
            {
                manager.Toast("备用出路：地下车库的卷帘门。");
            }
            else if (route == EscapeRoute.FireEscape)
            {
                manager.Toast("备用出路：四层东侧的消防连廊。");
            }
        }

        // ------------------------------------------------------------------
        // 目标文案
        // ------------------------------------------------------------------
        private string BuildObjective(GameManager manager)
        {
            if (!inventory.Contains(ItemIds.Card2F))
            {
                return "在 1F 南侧 1 号病房的柜子里找到二层通行卡";
            }

            // 正门与车库卷帘门都要电；消防连廊是机械逃生梯，不需要供电
            switch (route)
            {
                case EscapeRoute.Garage:
                    return manager.PowerOn ? GarageObjective() : PowerObjective(manager);
                case EscapeRoute.FireEscape:
                    return FireObjective(manager);
                default:
                    return manager.PowerOn ? MainObjective(manager) : PowerObjective(manager);
            }
        }

        /// <summary>总闸的三枚保险丝：先装手上的，没有就按 甲→乙→丙 找下一枚。</summary>
        private string PowerObjective(GameManager manager)
        {
            foreach (string fuseId in ItemIds.BreakerFuses)
            {
                if (inventory.Contains(fuseId))
                {
                    return "回 B1 配电间装保险丝（已装 " + manager.InstalledFuses + "/" +
                           GameManager.RequiredFuses + "）";
                }
            }

            foreach (string fuseId in ItemIds.BreakerFuses)
            {
                if (manager.HasInstalledFuse(fuseId))
                {
                    continue;
                }

                return DescribeFuseLocation(fuseId);
            }

            return "回 B1 配电间，把保险丝装进总闸";
        }

        private string DescribeFuseLocation(string fuseId)
        {
            if (fuseId == ItemIds.FuseA)
            {
                return inventory.Contains(ItemIds.LockerKey)
                    ? "用库房钥匙打开 B1 北侧 2 号储藏室，柜子里有保险丝"
                    : "下到 B1，在北侧 1 号设备间（洗衣房）抽屉里找库房钥匙";
            }

            if (fuseId == ItemIds.FuseB)
            {
                return inventory.Contains(ItemIds.ClerkKey)
                    ? "用职员钥匙打开 B1 南侧 5 号档案室，柜子里有保险丝"
                    : "去 B1 南侧 4 号值班室，抽屉里有职员钥匙";
            }

            return "去 B1 南侧 1 号储藏室，储物柜里还有一枚保险丝";
        }

        /// <summary>
        /// 拿到四层通行卡之前的公共链路（主线与消防连廊支线都要走）：
        /// 2F 药库钥匙 → 三层卡 → 3F 手术区钥匙 → 四层卡。
        /// </summary>
        private string CardChainObjective(GameManager manager)
        {
            if (!inventory.Contains(ItemIds.StorageKey))
            {
                return "上到 2F，在南侧 2 号值班室抽屉里找药库钥匙";
            }

            if (!inventory.Contains(ItemIds.Card3F))
            {
                return "用钥匙打开 2F 药房，柜子里有三层通行卡";
            }

            if (!inventory.Contains(ItemIds.SurgeryKey))
            {
                return "上到 3F，在南侧 2 号值班室抽屉里找手术区钥匙";
            }

            if (!inventory.Contains(ItemIds.Card4F))
            {
                return "用钥匙打开 3F 手术室，柜子里有四层通行卡";
            }

            return null;
        }

        private string MainObjective(GameManager manager)
        {
            // 主线额外需要 1F 的病房钥匙 / 物品柜钥匙，用来开 4F 的病房
            if (!inventory.Contains(ItemIds.WardKey))
            {
                return "去 1F 南侧 2 号值班室，抽屉里有病房钥匙";
            }

            if (!inventory.Contains(ItemIds.PatientKey))
            {
                return "用病房钥匙打开 1F 南侧 4 号治疗室，柜子里有物品柜钥匙";
            }

            string cards = CardChainObjective(manager);
            if (cards != null)
            {
                return cards;
            }

            if (!inventory.Contains(ItemIds.StaffKey))
            {
                return "用物品柜钥匙打开 4F 南侧 3 号病房，柜子里有值班钥匙";
            }

            if (!inventory.Contains(ItemIds.DirectorKey))
            {
                return "用值班钥匙打开 4F 南侧 2 号值班室，抽屉里有院长室钥匙";
            }

            if (!inventory.Contains(ItemIds.MainKey))
            {
                return "用院长室钥匙打开 4F 北侧 1 号档案室，柜子里有主门钥匙";
            }

            return "返回 1F 门厅，用主门钥匙打开正门离开";
        }

        private string GarageObjective()
        {
            if (!inventory.Contains(ItemIds.GarageKey))
            {
                return "在 B1 北侧 3 号储藏室的柜子里找车库钥匙";
            }

            if (!inventory.Contains(ItemIds.GateCrank))
            {
                return "用车库钥匙打开 B1 南侧 6 号车库，工具柜里有卷帘门曲柄";
            }

            return "在车库卷扬机上装好曲柄，摇起卷帘门离开";
        }

        private string FireObjective(GameManager manager)
        {
            string cards = CardChainObjective(manager);
            if (cards != null)
            {
                return cards;
            }

            if (!inventory.Contains(ItemIds.FireKey))
            {
                return "在 4F 北侧 2 号储藏室的柜子里找消防连廊钥匙";
            }

            if (!inventory.Contains(ItemIds.FireAxe))
            {
                return "在 4F 南侧 4 号治疗室的柜子里找消防斧";
            }

            return "用消防连廊钥匙打开 4F 东侧的门，再用消防斧撬开消防梯口";
        }
    }
}
