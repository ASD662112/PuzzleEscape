using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 地下室等黑暗区域的照明切换。玩家进入触发区后降低环境光并加重雾效，
    /// 离开后恢复地表设置。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LightingZone : MonoBehaviour
    {
        public Color DarkAmbient = new Color(0.006f, 0.007f, 0.01f);
        public Color DarkFogColor = new Color(0.002f, 0.0025f, 0.004f);
        public float DarkFogDensity = 0.05f;

        private bool inside;
        private bool saved;
        private Color previousAmbient;
        private bool previousFog;
        private Color previousFogColor;
        private float previousFogDensity;
        private Collider zoneCollider;
        private GameObject player;

        private void Reset()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (zoneCollider == null)
            {
                return;
            }

            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            if (player == null)
            {
                return;
            }

            Bounds bounds = zoneCollider.bounds;
            bounds.Expand(0.6f);
            bool shouldBeInside = bounds.Contains(player.transform.position);

            if (shouldBeInside && !inside)
            {
                Enter();
            }
            else if (!shouldBeInside && inside)
            {
                Exit();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                player = other.gameObject;
                Enter();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Exit();
            }
        }

        public void Enter()
        {
            if (inside)
            {
                return;
            }

            inside = true;

            // 交由光影质量总控记录“黑暗区域”状态，挡位变化时不会覆盖地下室的暗环境。
            if (LightingQuality.Instance != null)
            {
                LightingQuality.Instance.EnterDarkZone(DarkAmbient, DarkFogColor, DarkFogDensity);
                return;
            }

            if (!saved)
            {
                previousAmbient = RenderSettings.ambientLight;
                previousFog = RenderSettings.fog;
                previousFogColor = RenderSettings.fogColor;
                previousFogDensity = RenderSettings.fogDensity;
                saved = true;
            }

            RenderSettings.ambientLight = DarkAmbient;
            RenderSettings.fog = true;
            RenderSettings.fogColor = DarkFogColor;
            RenderSettings.fogDensity = DarkFogDensity;
        }

        public void Exit()
        {
            if (!inside)
            {
                return;
            }

            inside = false;

            if (LightingQuality.Instance != null)
            {
                LightingQuality.Instance.ExitDarkZone();
                return;
            }

            if (!saved)
            {
                return;
            }

            RenderSettings.ambientLight = previousAmbient;
            RenderSettings.fog = previousFog;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogDensity = previousFogDensity;
        }

        private void OnDisable()
        {
            Exit();
        }
    }
}
