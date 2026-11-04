#if UNITY_EDITOR
using PuzzleEscape.Core;
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 删除旧房门并按修正后的门框配对规则重新生成全部房门。
    /// 菜单：PuzzleEscape → 重建所有房门
    /// </summary>
    public static class DoorRebuildRepair
    {
        [MenuItem("PuzzleEscape/重建所有房门")]
        public static void RebuildAllDoors()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[PuzzleEscape] 当前没有可修复的场景。");
                return;
            }

            GameObject environment = GameObject.Find("Environment");
            if (environment == null)
            {
                Debug.LogWarning("[PuzzleEscape] 找不到 Environment 根节点。");
                return;
            }

            SimpleDoorInteractable[] oldDoors =
                environment.GetComponentsInChildren<SimpleDoorInteractable>(true);
            for (int i = 0; i < oldDoors.Length; i++)
            {
                if (oldDoors[i] != null)
                {
                    Object.DestroyImmediate(oldDoors[i].gameObject);
                }
            }

            // 清理残留的同名空节点，避免 AddDoorsToDoorways 统计到旧门。
            Transform[] transforms = environment.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name.StartsWith("RoomDoor_"))
                {
                    Object.DestroyImmediate(transforms[i].gameObject);
                }
            }

            MansionLevelBuilder.AddDoorsToDoorways(environment.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            int rebuilt = environment.GetComponentsInChildren<SimpleDoorInteractable>(true).Length;
            Debug.Log("[PuzzleEscape] 房门重建完成：" + rebuilt + " 扇。");
        }
    }
}
#endif
