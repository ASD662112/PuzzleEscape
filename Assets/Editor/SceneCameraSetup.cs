#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 在场景中放置一个持久化的玩家相机。运行时 PlayerFactory 会复用它，
    /// 并将其挂到玩家根节点下，避免重复创建 Camera 与 AudioListener。
    /// </summary>
    public static class SceneCameraSetup
    {
        [MenuItem("PuzzleEscape/添加玩家相机", false, 13)]
        public static void Ensure()
        {
            GameObject cameraGo = FindExistingCamera();

            if (cameraGo == null)
            {
                cameraGo = new GameObject("PlayerCamera");
            }

            if (cameraGo.transform.parent != null)
            {
                cameraGo.transform.SetParent(null, true);
            }

            cameraGo.name = "PlayerCamera";
            cameraGo.tag = "MainCamera";
            cameraGo.layer = 2;
            cameraGo.transform.position = new Vector3(-9f, 1.63f, 7.2f);
            cameraGo.transform.rotation = Quaternion.identity;

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
            camera.fieldOfView = 70f;

            if (cameraGo.GetComponent<AudioListener>() == null &&
                Object.FindObjectsOfType<AudioListener>().Length == 0)
            {
                cameraGo.AddComponent<AudioListener>();
            }

            EditorSceneManager.MarkSceneDirty(cameraGo.scene);
        }

        private static GameObject FindExistingCamera()
        {
            GameObject found = GameObject.Find("PlayerCamera");
            if (found != null)
            {
                return found;
            }

            if (Camera.main != null)
            {
                return Camera.main.gameObject;
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
#endif
