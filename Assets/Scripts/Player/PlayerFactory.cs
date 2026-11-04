using UnityEngine;

namespace PuzzleEscape.Player
{
    public class PlayerRig
    {
        public GameObject Root;
        public FirstPersonController Controller;
        public PlayerInventory Inventory;
        public PlayerInteractor Interactor;
    }

    public static class PlayerFactory
    {
        public static PlayerRig Create(Vector3 position, float yaw)
        {
            var root = new GameObject("Player");
            root.tag = "Player";
            root.layer = 2; // Ignore Raycast：避免交互射线击中玩家自己的胶囊体
            root.transform.SetPositionAndRotation(FindFreePosition(position), Quaternion.Euler(0f, yaw, 0f));

            var character = root.AddComponent<CharacterController>();
            character.height = 1.7f;
            character.radius = 0.3f;
            character.center = new Vector3(0f, 0.85f, 0f);
            character.slopeLimit = 45f;
            character.stepOffset = 0.2f;
            character.skinWidth = 0.05f;

            var inventory = root.AddComponent<PlayerInventory>();
            var controller = root.AddComponent<FirstPersonController>();
            root.AddComponent<FootstepAudio>();

            GameObject cameraGo = FindExistingSceneCamera();

            if (cameraGo == null)
            {
                cameraGo = new GameObject("PlayerCamera");
            }

            cameraGo.name = "PlayerCamera";
            cameraGo.tag = "MainCamera";
            cameraGo.layer = 2;
            cameraGo.transform.SetParent(root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            cameraGo.transform.localRotation = Quaternion.identity;

            var camera = cameraGo.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraGo.AddComponent<Camera>();
            }

            camera.enabled = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.018f, 0.028f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.useOcclusionCulling = true;
            camera.fieldOfView = Mathf.Clamp(
                PlayerPrefs.GetFloat("PuzzleEscape.FieldOfView", 70f), 60f, 90f);

            if (cameraGo.GetComponent<AudioListener>() == null &&
                Object.FindObjectsOfType<AudioListener>().Length == 0)
            {
                cameraGo.AddComponent<AudioListener>();
            }

            var lanternGo = new GameObject("PlayerLanternLight");
            lanternGo.transform.SetParent(cameraGo.transform, false);
            lanternGo.transform.localPosition = new Vector3(0.16f, -0.12f, 0.08f);
            lanternGo.transform.localRotation = Quaternion.identity;

            var lanternLight = lanternGo.AddComponent<Light>();
            lanternLight.type = LightType.Spot;
            lanternLight.color = new Color(1f, 0.97f, 0.92f);
            lanternLight.range = 18f;
            lanternLight.spotAngle = 55f;
            lanternLight.innerSpotAngle = 32f;
            lanternLight.intensity = 4.2f;
            lanternLight.shadows = LightShadows.Soft;
            lanternLight.shadowStrength = 0.9f;
            lanternLight.enabled = false;

            var lantern = root.AddComponent<PlayerLantern>();
            lantern.Initialize(inventory, lanternLight, CreateCandleLight(root.transform));

            controller.Initialize(cameraGo.transform);

            var hiding = root.AddComponent<PlayerHiding>();
            hiding.Initialize(controller);

            var interactor = root.AddComponent<PlayerInteractor>();
            interactor.Initialize(cameraGo.transform);

            return new PlayerRig
            {
                Root = root,
                Controller = controller,
                Inventory = inventory,
                Interactor = interactor
            };
        }

        /// <summary>
        /// 半截蜡烛的照明：挂在玩家身上的暖色点光，范围 5 米，拿到蜡烛后由 PlayerLantern 打开。
        /// </summary>
        private static Light CreateCandleLight(Transform player)
        {
            var candleGo = new GameObject("PlayerCandleLight");
            candleGo.transform.SetParent(player, false);
            candleGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);

            var candleLight = candleGo.AddComponent<Light>();
            candleLight.type = LightType.Point;
            candleLight.color = new Color(1f, 0.63f, 0.36f);
            candleLight.range = 5f;
            candleLight.intensity = 1.8f;
            candleLight.shadows = LightShadows.None;
            candleLight.enabled = false;
            return candleLight;
        }

        /// <summary>
        /// 出生点若被家具/墙体占住，就近找一个能站人的位置，
        /// 否则角色会被碰撞体顶起来并卡住（表现为视角升到空中、无法移动）。
        /// </summary>
        private static Vector3 FindFreePosition(Vector3 position)
        {
            if (IsFree(position))
            {
                return position;
            }

            var builder = new System.Text.StringBuilder();
            Vector3 bottom = position + Vector3.up * 0.35f;
            Vector3 top = position + Vector3.up * 1.5f;
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (overlap != null)
                {
                    builder.Append(overlap.name).Append(" / ");
                }
            }

            Debug.LogWarning("[PuzzleEscape] 出生点被占住，重叠碰撞体：" + builder);

            for (int radius = 1; radius <= 4; radius++)
            {
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI * 2f / 16f;
                    Vector3 candidate = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (IsFree(candidate))
                    {
                        Debug.LogWarning("[PuzzleEscape] 出生点被占住，已改到附近空位：" + candidate);
                        return candidate;
                    }
                }
            }

            Debug.LogWarning("[PuzzleEscape] 出生点附近没有空位，保持原位置。");
            return position;
        }

        private static bool IsFree(Vector3 position)
        {
            Vector3 bottom = position + Vector3.up * 0.35f;
            Vector3 top = position + Vector3.up * 1.5f;
            return !Physics.CheckCapsule(bottom, top, 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static GameObject FindExistingSceneCamera()
        {
            Camera main = Camera.main;
            if (main != null)
            {
                return main.gameObject;
            }

            foreach (Camera camera in Object.FindObjectsOfType<Camera>())
            {
                if (camera.gameObject.scene.IsValid())
                {
                    return camera.gameObject;
                }
            }

            return null;
        }
    }
}
