#if UNITY_EDITOR
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 可选的 MCP for Unity 自动连接开关，默认关闭。
    /// 关闭时不启动 HTTP Bridge，避免本地没有 MCP 服务端时反复输出
    /// 「MCP-FOR-UNITY: [WebSocket] Connection failed」。
    /// 需要时用菜单「PuzzleEscape/连接 Unity MCP」手动开启。
    /// </summary>
    [InitializeOnLoad]
    public static class McpAutoConnect
    {
        private const string UseHttpKey = "MCPForUnity.UseHttpTransport";
        private const string AutoStartKey = "MCPForUnity.AutoStartOnLoad";

        /// <summary>本项目自己的开关：默认关闭自动连接。</summary>
        private const string EnabledKey = "PuzzleEscape.McpAutoConnect";

        private const string SessionKey = "PuzzleEscape.McpAutoConnectAttempted.v7";

        static McpAutoConnect()
        {
            // 必须在 delayCall 之前关掉插件自己的自动启动，
            // 否则它的 HttpAutoStartHandler 会抢先去拉起 HTTP bridge 并反复重连。
            if (!EditorPrefs.GetBool(EnabledKey, false))
            {
                EditorPrefs.SetBool(AutoStartKey, false);
            }

            EditorApplication.delayCall += OnLoad;
        }

        private static void OnLoad()
        {
            if (!EditorPrefs.GetBool(EnabledKey, false))
            {
                // 同时关掉插件自身的自动启动，否则它会在编辑器加载时自己拉起 bridge 并不断重连。
                if (EditorPrefs.GetBool(AutoStartKey, false))
                {
                    EditorPrefs.SetBool(AutoStartKey, false);
                }

                return;
            }

            TryConnect();
        }

        [MenuItem("PuzzleEscape/连接 Unity MCP", false, 30)]
        public static void ConnectFromMenu()
        {
            EditorPrefs.SetBool(EnabledKey, true);
            SessionState.SetBool(SessionKey, false);
            TryConnect();
        }

        [MenuItem("PuzzleEscape/关闭 Unity MCP 自动连接", false, 31)]
        public static void DisableFromMenu()
        {
            EditorPrefs.SetBool(EnabledKey, false);
            EditorPrefs.SetBool(AutoStartKey, false);
            Debug.Log("[PuzzleEscape] 已关闭 Unity MCP 自动连接，不再尝试连接 127.0.0.1:8080。");
        }

        private static void TryConnect()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);

            EditorPrefs.SetBool(UseHttpKey, true);
            EditorPrefs.SetBool(AutoStartKey, true);
            EditorConfigurationCache.Instance.Refresh();

            if (MCPServiceLocator.Bridge.IsRunning)
            {
                Debug.Log("[PuzzleEscape] Unity MCP bridge 已在运行。");
                return;
            }

            StartBridgeAsync();
        }

        private static async void StartBridgeAsync()
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                bool started = false;
                try
                {
                    if (attempt == 0)
                    {
                        Debug.Log("[PuzzleEscape] 正在连接 Unity MCP…");
                    }

                    started = await MCPServiceLocator.Bridge.StartAsync();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PuzzleEscape] Unity MCP 连接异常：{ex.Message}");
                }

                if (started)
                {
                    Debug.Log("[PuzzleEscape] Unity MCP bridge 已连接。");
                    return;
                }

                if (attempt < 14)
                {
                    await Task.Delay(1000);
                }
            }

            Debug.LogWarning("[PuzzleEscape] Unity MCP bridge 启动失败，请确认 127.0.0.1:8080 的 MCP 服务端正在运行。");
        }
    }
}
#endif
