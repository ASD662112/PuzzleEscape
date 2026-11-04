using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 房间灯光总控：拉下电闸后整体提亮。
    /// </summary>
    public class RoomLighting : MonoBehaviour
    {
        public static RoomLighting Instance { get; private set; }

        public Light CeilingLight;
        public Light LampLight;
        public Light WindowLight;
        public Renderer BulbRenderer;

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

        public void SetPowered(bool powered)
        {
            if (CeilingLight != null)
            {
                CeilingLight.intensity = powered ? 1.55f : 0.4f;
            }

            if (LampLight != null)
            {
                LampLight.intensity = powered ? 1f : 0.05f;
            }

            if (WindowLight != null)
            {
                WindowLight.intensity = powered ? 0.8f : 0.25f;
            }

            RenderSettings.ambientLight = powered
                ? new Color(0.17f, 0.18f, 0.21f)
                : new Color(0.07f, 0.08f, 0.11f);

            if (BulbRenderer != null)
            {
                Material material = BulbRenderer.material;
                material.color = powered
                    ? new Color(1f, 0.92f, 0.7f)
                    : new Color(0.42f, 0.39f, 0.32f);

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor",
                        powered ? new Color(1f, 0.8f, 0.45f) * 1.2f : Color.black);
                }
            }
        }
    }
}
