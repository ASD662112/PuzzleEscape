using PuzzleEscape.Interaction;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 藏书室地毯下的隐藏暗道与地下密室。
    /// 入口和出口都是可交互传送点；首次进入会写入案件档案并解锁成就。
    /// </summary>
    public class SecretPassage : MonoBehaviour
    {
        private const float EntryX = -32f;
        private const float EntryZ = 4.2f;
        private const float ChamberY = -10f;
        private const float EntryDestinationY = -9.95f;
        private const float ReturnDestinationY = 0.08f;

        private void Awake()
        {
            BuildPassage();
        }

        private void BuildPassage()
        {
            GameObject environment = GameObject.Find("Environment");
            Transform parent = environment != null ? environment.transform : transform;
            Material wood = CreateMaterial("HiddenPassageWood", new Color(0.24f, 0.16f, 0.10f), 0.18f);
            Material stone = CreateMaterial("HiddenPassageStone", new Color(0.20f, 0.19f, 0.17f), 0.12f);
            Material floor = CreateMaterial("HiddenPassageFloor", new Color(0.13f, 0.10f, 0.08f), 0.10f);
            Material rug = CreateMaterial("HiddenPassageRug", new Color(0.28f, 0.08f, 0.09f), 0.08f);
            Material paper = CreateMaterial("HiddenPassagePaper", new Color(0.78f, 0.71f, 0.55f), 0.05f);
            Material wax = CreateMaterial("HiddenPassageWax", new Color(0.86f, 0.80f, 0.64f), 0.15f);
            Material flame = CreateMaterial("HiddenPassageFlame", new Color(1f, 0.72f, 0.32f), 0.1f);
            if (flame.HasProperty("_EmissionColor"))
            {
                flame.EnableKeyword("_EMISSION");
                flame.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.18f) * 2f);
            }

            BuildEntry(parent, wood);
            Transform chamber = BuildChamber(parent, wood, stone, floor, rug, paper, wax, flame);
            ExtendLibraryClue();

            Debug.Log("[PuzzleEscape] 隐藏暗道生成：藏书室地毯 → 地下密室。");
        }

        private void BuildEntry(Transform parent, Material wood)
        {
            var entry = new GameObject("SecretPassageEntry");
            entry.transform.SetParent(parent, false);
            entry.transform.position = new Vector3(EntryX, 0.02f, EntryZ);

            var collider = entry.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.06f, 0f);
            collider.size = new Vector3(1.3f, 0.16f, 1.1f);

            CreateBox(entry.transform, "LooseFloorboard", new Vector3(0f, 0.02f, 0f),
                new Vector3(1.16f, 0.04f, 0.9f), wood, false, Quaternion.Euler(0f, 8f, 0f));

            var hatch = entry.AddComponent<PassageHatch>();
            hatch.Label = "掀开地毯下的暗门";
            hatch.Destination = new Vector3(0f, EntryDestinationY, -0.5f);
            hatch.DestinationYaw = 0f;
            hatch.SecretId = "secret_passage";
            hatch.SecretTitle = "地毯下的暗道";
            hatch.SecretBody = "藏书室的地毯中央藏着一块松动的木板。\n下面是一段向地下延伸的窄梯，尽头有一间没有窗户的密室。";
            hatch.DiscoveryToast = "发现隐藏暗道：藏书室地毯下的密室。";
        }

        private Transform BuildChamber(Transform parent, Material wood, Material stone,
            Material floor, Material rug, Material paper, Material wax, Material flame)
        {
            var chamber = new GameObject("HiddenChamber");
            chamber.transform.SetParent(parent, false);
            chamber.transform.position = new Vector3(0f, ChamberY, 0f);

            CreateBox(chamber.transform, "HiddenFloor", new Vector3(0f, -0.1f, 0f),
                new Vector3(8f, 0.2f, 6.4f), floor, true);
            CreateBox(chamber.transform, "HiddenCeiling", new Vector3(0f, 2.9f, 0f),
                new Vector3(8f, 0.2f, 6.4f), stone, true);
            CreateBox(chamber.transform, "HiddenWallWest", new Vector3(-4f, 1.4f, 0f),
                new Vector3(0.2f, 3f, 6.4f), stone, true);
            CreateBox(chamber.transform, "HiddenWallEast", new Vector3(4f, 1.4f, 0f),
                new Vector3(0.2f, 3f, 6.4f), stone, true);
            CreateBox(chamber.transform, "HiddenWallSouth", new Vector3(0f, 1.4f, -3.2f),
                new Vector3(8f, 3f, 0.2f), stone, true);
            CreateBox(chamber.transform, "HiddenWallNorth", new Vector3(0f, 1.4f, 3.2f),
                new Vector3(8f, 3f, 0.2f), stone, true);

            CreateBox(chamber.transform, "HiddenRug", new Vector3(0f, 0.02f, 0.4f),
                new Vector3(3.2f, 0.02f, 2.6f), rug, false);

            CreateBox(chamber.transform, "HiddenTableTop", new Vector3(0f, 0.78f, 1.5f),
                new Vector3(1.2f, 0.08f, 0.7f), wood, true);
            CreateBox(chamber.transform, "HiddenTableLegA", new Vector3(-0.45f, 0.36f, 1.35f),
                new Vector3(0.1f, 0.72f, 0.1f), wood, false);
            CreateBox(chamber.transform, "HiddenTableLegB", new Vector3(0.45f, 0.36f, 1.35f),
                new Vector3(0.1f, 0.72f, 0.1f), wood, false);

            CreateBox(chamber.transform, "HiddenCandle", new Vector3(0.46f, 0.92f, 1.5f),
                new Vector3(0.09f, 0.16f, 0.09f), wax, false);
            CreateBox(chamber.transform, "HiddenFlame", new Vector3(0.46f, 1.04f, 1.5f),
                new Vector3(0.035f, 0.06f, 0.035f), flame, false);

            CreateBox(chamber.transform, "HiddenCrateA", new Vector3(-2.2f, 0.35f, -2.3f),
                new Vector3(0.7f, 0.7f, 0.7f), wood, true, Quaternion.Euler(0f, 18f, 0f));
            CreateBox(chamber.transform, "HiddenCrateB", new Vector3(-1.4f, 0.28f, -2.65f),
                new Vector3(0.55f, 0.55f, 0.55f), wood, true, Quaternion.Euler(0f, -12f, 0f));

            var noteRoot = new GameObject("SecretChamberNote");
            noteRoot.transform.SetParent(chamber.transform, false);
            noteRoot.transform.localPosition = new Vector3(0f, 0.86f, 1.5f);
            var noteCollider = noteRoot.AddComponent<BoxCollider>();
            noteCollider.size = new Vector3(0.44f, 0.22f, 0.36f);
            CreateBox(noteRoot.transform, "Paper", Vector3.zero,
                new Vector3(0.36f, 0.014f, 0.28f), paper, false, Quaternion.Euler(0f, -12f, 0f));

            var clue = noteRoot.AddComponent<ClueInteractable>();
            clue.Title = "隐藏密室手记";
            clue.Body = "墙上的划痕记录着每一次开门。\n最后一行写着：\"四枚徽记不是钥匙，是四个证词。\"\n桌角压着一张 1921 年的酒窖收据。";
            clue.Label = "阅读密室手记";
            clue.ClueId = "secret_chamber";

            CreateLight(chamber.transform, "HiddenChamberLightA", new Vector3(0f, 2.6f, 0f),
                new Color(1f, 0.82f, 0.62f), 3.2f, 6.5f, true);
            CreateLight(chamber.transform, "HiddenChamberLightB", new Vector3(0f, 2.5f, -2f),
                new Color(0.72f, 0.64f, 0.92f), 1.4f, 5f, false);
            CreateLight(chamber.transform, "HiddenCandleLight", new Vector3(0.46f, 1.2f, 1.5f),
                new Color(1f, 0.66f, 0.3f), 1f, 3.5f, false);
            CreateLight(chamber.transform, "HiddenExitLight", new Vector3(0f, 1.4f, -2.4f),
                new Color(1f, 0.78f, 0.52f), 0.9f, 4f, false);

            var exit = new GameObject("SecretPassageExit");
            exit.transform.SetParent(chamber.transform, false);
            exit.transform.localPosition = new Vector3(0f, 0.06f, -2.55f);
            var exitCollider = exit.AddComponent<BoxCollider>();
            exitCollider.center = new Vector3(0f, 0.08f, 0f);
            exitCollider.size = new Vector3(1.1f, 0.2f, 0.9f);

            CreateBox(exit.transform, "ReturnHatch", Vector3.zero,
                new Vector3(0.95f, 0.05f, 0.75f), wood, false);

            var back = exit.AddComponent<PassageHatch>();
            back.Label = "爬回藏书室";
            back.Destination = new Vector3(EntryX, ReturnDestinationY, EntryZ);
            back.DestinationYaw = 0f;

            return chamber.transform;
        }

        private static void ExtendLibraryClue()
        {
            ClueInteractable[] clues = Object.FindObjectsOfType<ClueInteractable>(true);
            for (int i = 0; i < clues.Length; i++)
            {
                if (clues[i] == null || clues[i].ClueId != "library")
                {
                    continue;
                }

                const string addition = "地毯中央有一块松动的木板，下面似乎有冷风。";
                if (!clues[i].Body.Contains(addition))
                {
                    clues[i].Body = clues[i].Body + "\n\n" + addition;
                }

                return;
            }
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition,
            Vector3 scale, Material material, bool keepCollider,
            Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.transform.localRotation = rotation ?? Quaternion.identity;

            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return go;
        }

        private static void CreateLight(Transform parent, string name, Vector3 localPosition,
            Color color, float intensity, float range, bool shadows)
        {
            var lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = localPosition;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            var material = new Material(shader);
            material.name = name;
            material.color = color;
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            return material;
        }
    }
}
