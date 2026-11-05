using System.Collections.Generic;
using PuzzleEscape.Interaction;
using PuzzleEscape.Puzzle;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 在现有房间中放置 6 个可选隐藏密码锁。
    /// 每个密码锁的四位密码每局随机生成，线索会追加到对应房间的便签里，
    /// 解开后给出一份新的隐藏记录。
    /// </summary>
    public class ExpansionPuzzles : MonoBehaviour
    {
        private class PuzzleSpec
        {
            public string ClueId;
            public string PuzzleId;

            /// <summary>线索文本模板，{code} 会被替换成当局生成的密码。</summary>
            public string ClueTemplate;

            public string FooterHint;
            public string RewardId;
            public string RewardTitle;
            public string RewardBody;
            public Vector3 Offset;
        }

        private static readonly PuzzleSpec[] Specs =
        {
            new PuzzleSpec
            {
                ClueId = "library",
                PuzzleId = "secret_library",
                ClueTemplate = "便签背面用铅笔写着一串数字：{code}。\n桌边的旧密码锁也许认这串数字。",
                FooterHint = "线索提示：藏书室便签的背面写着四位数字。",
                RewardId = "secret_library_card",
                RewardTitle = "夹在百科全书里的借阅卡",
                RewardBody = "借阅卡上只有一个名字：E. Vane。\n日期是 1921 年 10 月 13 日，和酒窖里最旧的那瓶酒同年。\n背面写着一句话：\"地下室不是储藏间，是入口。\"",
                Offset = new Vector3(1.0f, 0f, 0f)
            },
            new PuzzleSpec
            {
                ClueId = "wine",
                PuzzleId = "secret_wine",
                ClueTemplate = "酒窖账本的封皮内侧，有人用铅笔留下四个数字：{code}。\n酒架旁的旧锁也许认这个数字。",
                FooterHint = "线索提示：酒窖账本封皮内侧写着四位数字。",
                RewardId = "secret_wine_ledger",
                RewardTitle = "酒窖账本的一页",
                RewardBody = "账本记录了 1921 年到 1948 年的每一次封存。\n最后一页被撕掉了，只剩下半句话：\"四枚徽记不能同时离开庄园。\"",
                Offset = new Vector3(1.0f, 0f, 0f)
            },
            new PuzzleSpec
            {
                ClueId = "billiard",
                PuzzleId = "secret_billiard",
                ClueTemplate = "台球记分牌停在 {code}，像有人故意留下的顺序。\n球桌边的密码锁或许在等这组编号。",
                FooterHint = "线索提示：台球室记分牌停在四位数字上。",
                RewardId = "secret_billiard_bet",
                RewardTitle = "台球室的赌局记录",
                RewardBody = "记录上写着：\"如果我没回来，就把那串数字交给下一个进来的人。\"\n落款是一个被涂掉的名字。",
                Offset = new Vector3(1.0f, 0f, 0f)
            },
            new PuzzleSpec
            {
                ClueId = "greenhouse",
                PuzzleId = "secret_greenhouse",
                ClueTemplate = "花盆标签被水泡花了，只剩这四位还看得清：{code}。\n温室里的旧锁一直空着。",
                FooterHint = "线索提示：温室花盆标签上残留四位数字。",
                RewardId = "secret_greenhouse_log",
                RewardTitle = "温室培育日志",
                RewardBody = "日志里夹着一片干枯的黑色花瓣。\n页脚写着：\"这些花不是装饰，它们会记住谁来过。\"",
                Offset = new Vector3(0f, 0f, -1.0f)
            },
            new PuzzleSpec
            {
                ClueId = "laundry",
                PuzzleId = "secret_laundry",
                ClueTemplate = "洗衣清单最下面多了一行手写数字：{code}。\n墨迹很新，不像这里的旧东西。",
                FooterHint = "线索提示：洗衣清单底部有一行手写数字。",
                RewardId = "secret_laundry_list",
                RewardTitle = "仆役洗衣清单",
                RewardBody = "清单上有一行不属于这里的衣物：\n\"小主人的白衬衫，领口有锈色，洗不掉。\"\n日期正好是封印大门关闭的那一天。",
                Offset = new Vector3(-1.0f, 0f, 0f)
            },
            new PuzzleSpec
            {
                ClueId = "observatory",
                PuzzleId = "secret_observatory",
                ClueTemplate = "星图角落被人圈出四个数字：{code}。\n望远镜旁的旧锁似乎就是为它准备的。",
                FooterHint = "线索提示：观测室星图角落圈着四位数字。",
                RewardId = "secret_observatory_chart",
                RewardTitle = "被折起的星图",
                RewardBody = "星图上圈出的不是任何已知星座，而是一座倒映在地下的庄园。\n角落里写着：\"门不是出口，是镜子。\"",
                Offset = new Vector3(-1.0f, 0f, 0f)
            }
        };

        private const string SeedKeyPrefix = "PuzzleEscape.PuzzleSeed.";

        private void Start()
        {
            BuildPuzzles();
        }

        private void BuildPuzzles()
        {
            ClueInteractable[] clues = Object.FindObjectsOfType<ClueInteractable>(true);
            Shader standard = Shader.Find("Standard");
            Material metal = CreateMaterial(standard, new Color(0.16f, 0.18f, 0.2f));
            Material screen = CreateMaterial(standard, new Color(0.12f, 0.42f, 0.36f));

            int seed = ResolveSeed();
            var usedCodes = new HashSet<string>();
            int created = 0;

            foreach (PuzzleSpec spec in Specs)
            {
                ClueInteractable clue = FindClue(clues, spec.ClueId);
                if (clue == null)
                {
                    continue;
                }

                string code = BuildCode(seed, spec.PuzzleId, usedCodes);
                string clueText = spec.ClueTemplate.Replace("{code}", code);

                if (!string.IsNullOrEmpty(clueText) &&
                    !string.IsNullOrEmpty(clue.Body) &&
                    !clue.Body.Contains(clueText))
                {
                    clue.Body = clue.Body + "\n\n" + clueText;
                }

                CreateLock(clue.transform.position, spec, code, metal, screen);
                created++;
                Debug.Log("[PuzzleEscape] 隐藏密码锁 " + spec.PuzzleId + " 本局密码：" + code);
            }

            Debug.Log("[PuzzleEscape] 隐藏密码锁生成：" + created + " 个。");
        }

        /// <summary>
        /// 本局密码种子：读档时沿用存档槽已记录的种子，新开局重新生成，
        /// 让每局密码不同，但存读档前后保持一致。
        /// </summary>
        private static int ResolveSeed()
        {
            string key = SeedKeyPrefix + SaveGameService.ActiveSlot;

            if (SaveGameService.HasPendingLoad && PlayerPrefs.HasKey(key))
            {
                return PlayerPrefs.GetInt(key);
            }

            int seed = Random.Range(1, int.MaxValue);
            PlayerPrefs.SetInt(key, seed);
            PlayerPrefs.Save();
            return seed;
        }

        /// <summary>由种子和密码锁 Id 生成稳定的四位密码，并保证同一局内不重复。</summary>
        private static string BuildCode(int seed, string puzzleId, HashSet<string> used)
        {
            for (int salt = 0; salt < 64; salt++)
            {
                unchecked
                {
                    int hash = seed + salt * 7919;

                    for (int i = 0; i < puzzleId.Length; i++)
                    {
                        hash = hash * 31 + puzzleId[i];
                    }

                    int value = 1000 + ((hash & 0x7fffffff) % 9000);
                    string code = value.ToString("D4");

                    if (!used.Contains(code))
                    {
                        used.Add(code);
                        return code;
                    }
                }
            }

            return "0427";
        }

        private static ClueInteractable FindClue(ClueInteractable[] clues, string clueId)
        {
            for (int i = 0; i < clues.Length; i++)
            {
                if (clues[i] != null && clues[i].ClueId == clueId)
                {
                    return clues[i];
                }
            }

            return null;
        }

        private static void CreateLock(Vector3 notePosition, PuzzleSpec spec, string solution,
            Material metal, Material screen)
        {
            Vector3 position = notePosition + spec.Offset;
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 0.6f, Vector3.down, out hit, 4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y;
            }
            else
            {
                position.y = Mathf.Max(0f, position.y - 0.9f);
            }

            var root = new GameObject("SecretLock_" + spec.PuzzleId);
            root.transform.position = position;

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.55f, 0f);
            collider.size = new Vector3(0.62f, 1.1f, 0.34f);

            CreateBox(root.transform, "LockPedestal", new Vector3(0f, 0.45f, 0f),
                new Vector3(0.5f, 0.9f, 0.26f), metal);
            GameObject screenObject = CreateBox(root.transform, "LockScreen", new Vector3(0f, 0.82f, -0.14f),
                new Vector3(0.34f, 0.2f, 0.025f), screen);
            GameObject indicator = CreateSphere(root.transform, "LockIndicator", new Vector3(0f, 0.98f, -0.14f),
                new Vector3(0.1f, 0.1f, 0.1f), screen);

            var puzzle = root.AddComponent<KeypadPuzzle>();
            puzzle.Solution = solution;
            puzzle.RequiresPower = false;
            puzzle.PuzzleId = spec.PuzzleId;
            puzzle.FooterHint = spec.FooterHint;
            puzzle.RewardId = spec.RewardId;
            puzzle.RewardTitle = spec.RewardTitle;
            puzzle.RewardBody = spec.RewardBody;
            puzzle.Indicator = indicator.GetComponent<Renderer>();

            // 让密码锁屏幕有一点自发光，便于在昏暗房间里辨认。
            Renderer screenRenderer = screenObject.GetComponent<Renderer>();
            Material screenMaterial = new Material(screen);
            screenMaterial.EnableKeyword("_EMISSION");
            screenMaterial.SetColor("_EmissionColor", new Color(0.12f, 0.5f, 0.4f) * 0.35f);
            screenRenderer.sharedMaterial = screenMaterial;
        }

        private static Material CreateMaterial(Shader shader, Color color)
        {
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            var material = new Material(shader);
            material.color = color;
            return material;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition,
            Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            DestroyImmediate(go.GetComponent<Collider>());
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private static GameObject CreateSphere(Transform parent, string name, Vector3 localPosition,
            Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            DestroyImmediate(go.GetComponent<Collider>());
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
