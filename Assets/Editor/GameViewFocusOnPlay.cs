#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 进入 Play 时自动把焦点交给 Game View，
    /// 避免按 Play 后键盘输入仍停留在工具栏或 Scene 视图导致角色无法移动。
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewFocusOnPlay
    {
        static GameViewFocusOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            EditorApplication.delayCall += FocusGameView;
        }

        private static void FocusGameView()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
            {
                return;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView != null)
            {
                gameView.Focus();
            }
        }
    }
}
#endif
