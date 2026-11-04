using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 备用逃生路线一：B1 车库卷帘门。
    /// 需要「卷帘门曲柄」装进卷扬机，且全楼已经通电，摇起卷帘门即通关。
    /// </summary>
    public class GarageShutterInteractable : Interactable
    {
        public Transform Shutter;
        public float OpenHeight = 3.2f;
        public string RequiredItemId = "gate_crank";
        public string RequiredItemName = "卷帘门曲柄";
        public bool RequirePower = true;

        private bool opened;

        public override string Prompt => opened ? "车库卷帘门（已打开）" : "检查卷帘门卷扬机";

        public override bool CanInteract => !opened;

        public override void Interact(PlayerInteractor interactor)
        {
            if (opened)
            {
                return;
            }

            GameManager manager = GameManager.Instance;

            if (RequirePower && manager != null && !manager.PowerOn)
            {
                manager.Toast("卷扬机没有电，指示灯是灭的。得先合上 B1 配电间的总闸。");
                AudioManager.Instance?.PlayError();
                return;
            }

            PlayerInventory inventory = interactor != null
                ? interactor.GetComponent<PlayerInventory>()
                : null;

            if (inventory == null || !inventory.Contains(RequiredItemId))
            {
                manager?.Toast("卷扬机的摇柄插槽是空的。（需要：" + RequiredItemName + "）");
                AudioManager.Instance?.PlayError();
                return;
            }

            inventory.Remove(RequiredItemId);
            opened = true;

            if (Shutter != null)
            {
                Shutter.localPosition += Vector3.up * OpenHeight;

                Collider collider = Shutter.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            AudioManager.Instance?.PlayDoor();
            manager?.Win("从车库离开",
                "卷帘门在刺耳的摩擦声里升起，冷雾灌进车库，你没有再回头。");
        }
    }

    /// <summary>
    /// 备用逃生路线二：4F 东侧消防连廊。
    /// 用消防斧撬开锈死的梯口盖板，顺着外挂消防梯下到地面即通关。
    /// </summary>
    public class FireEscapeInteractable : Interactable
    {
        public Transform Hatch;
        public string RequiredItemId = "fire_axe";
        public string RequiredItemName = "消防斧";
        public bool RequirePower = false;

        private bool opened;

        public override string Prompt => opened ? "消防梯（已打开）" : "撬开消防梯盖板";

        public override bool CanInteract => !opened;

        public override void Interact(PlayerInteractor interactor)
        {
            if (opened)
            {
                return;
            }

            GameManager manager = GameManager.Instance;

            if (RequirePower && manager != null && !manager.PowerOn)
            {
                manager.Toast("连廊应急灯没亮，梯口一片漆黑，看不清脚下。");
                AudioManager.Instance?.PlayError();
                return;
            }

            PlayerInventory inventory = interactor != null
                ? interactor.GetComponent<PlayerInventory>()
                : null;

            if (inventory == null || !inventory.Contains(RequiredItemId))
            {
                manager?.Toast("盖板的合页锈死了，徒手撬不开。（需要：" + RequiredItemName + "）");
                AudioManager.Instance?.PlayError();
                return;
            }

            opened = true;

            if (Hatch != null)
            {
                Hatch.localRotation *= Quaternion.Euler(0f, -80f, 0f);
            }

            AudioManager.Instance?.PlayDoor();
            manager?.Win("从消防连廊离开",
                "盖板被撬开，外挂消防梯在夜风里轻响。你一级一级往下，直到脚踩到湿冷的草地。");
        }
    }
}
