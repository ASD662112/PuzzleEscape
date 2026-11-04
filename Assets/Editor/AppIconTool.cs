#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 把 Assets/Art/UI 下的图标资源应用到 Windows 与 Android（含自适应图标）。
    /// 菜单：PuzzleEscape → 设置应用图标（Windows + Android）/ 检查应用图标设置
    /// </summary>
    public static class AppIconTool
    {
        private const string IconPath = "Assets/Art/UI/AppIcon.png";
        private const string AndroidBackgroundPath = "Assets/Art/UI/AppIconAndroidBackground.png";
        private const string AndroidForegroundPath = "Assets/Art/UI/AppIconAndroidForeground.png";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        private const int KindAdaptive = 2;

        [MenuItem("PuzzleEscape/设置应用图标（Windows + Android）", false, 40)]
        public static void ApplyIcons()
        {
            // 先改导入设置，改完再重新加载，避免拿到被重导入替换掉的旧引用。
            ConfigureImport(IconPath, false);
            ConfigureImport(AndroidBackgroundPath, false);
            ConfigureImport(AndroidForegroundPath, true);
            AssetDatabase.Refresh();

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidBackgroundPath);
            Texture2D foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidForegroundPath);

            if (icon == null)
            {
                Debug.LogError("[PuzzleEscape] 没找到图标：" + IconPath);
                return;
            }

            // 自适应图标走序列化写入，放在其它设置之前，避免被 PlayerSettings 的写回覆盖。
            bool adaptive = background != null && foreground != null &&
                            ApplyAdaptiveIcons(background, foreground);

            bool windows = ApplyWindowsIcon(icon);
            bool android = ApplyPlatformIcon(BuildTargetGroup.Android, icon);

            AssetDatabase.SaveAssets();

            Debug.Log("[PuzzleEscape] 应用图标设置结果：Windows " + (windows ? "成功" : "失败") +
                      "，Android 默认图标 " + (android ? "成功" : "失败") +
                      "，Android 自适应图标 " + (adaptive ? "成功" : "失败") + "。重新构建后生效。");

            if (!windows)
            {
                Debug.LogWarning("[PuzzleEscape] Windows 图标写入失败：请在 Project Settings → Player → " +
                                 "Windows, Mac, Linux → Icon 里手动把 AppIcon.png 拖到 Default Icon。");
            }

            if (!adaptive)
            {
                Debug.LogWarning("[PuzzleEscape] Android 自适应图标写入失败：请在 Project Settings → Player → " +
                                 "Android → Icon 里手动把 AppIconAndroidBackground 拖到 Background、" +
                                 "AppIconAndroidForeground 拖到 Foreground。");
            }
        }

        [MenuItem("PuzzleEscape/检查应用图标设置", false, 41)]
        public static void InspectIcons()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[PuzzleEscape] 读不到 ProjectSettings.asset。");
                return;
            }

            var settings = new SerializedObject(assets[0]);

            SerializedProperty legacy = settings.FindProperty("m_BuildTargetIcons");
            int standalone = 0;
            if (legacy != null)
            {
                for (int i = 0; i < legacy.arraySize; i++)
                {
                    SerializedProperty platform = legacy.GetArrayElementAtIndex(i);
                    SerializedProperty target = platform.FindPropertyRelative("m_BuildTarget");
                    SerializedProperty icons = platform.FindPropertyRelative("m_Icons");
                    int filled = 0;
                    for (int j = 0; j < icons.arraySize; j++)
                    {
                        if (IsIconSlotFilled(icons.GetArrayElementAtIndex(j)))
                        {
                            filled++;
                        }
                    }

                    Debug.Log("[PuzzleEscape] 图标组 " + target.stringValue + "：共 " + icons.arraySize +
                              " 个槽位，已填 " + filled + " 个。");
                    if (target.stringValue == "Standalone")
                    {
                        standalone = filled;
                    }
                }
            }

            SerializedProperty platforms = settings.FindProperty("m_BuildTargetPlatformIcons");
            if (platforms != null)
            {
                for (int i = 0; i < platforms.arraySize; i++)
                {
                    SerializedProperty platform = platforms.GetArrayElementAtIndex(i);
                    SerializedProperty target = platform.FindPropertyRelative("m_BuildTarget");
                    if (target.stringValue != "Android")
                    {
                        continue;
                    }

                    SerializedProperty icons = platform.FindPropertyRelative("m_Icons");
                    int adaptiveFilled = 0;
                    int legacyFilled = 0;
                    for (int j = 0; j < icons.arraySize; j++)
                    {
                        SerializedProperty entry = icons.GetArrayElementAtIndex(j);
                        SerializedProperty kind = entry.FindPropertyRelative("m_Kind");
                        SerializedProperty textures = entry.FindPropertyRelative("m_Textures");
                        if (textures == null || textures.arraySize == 0)
                        {
                            continue;
                        }

                        if (kind.intValue == KindAdaptive)
                        {
                            adaptiveFilled++;
                        }
                        else
                        {
                            legacyFilled++;
                        }
                    }

                    Debug.Log("[PuzzleEscape] Android：自适应槽位已填 " + adaptiveFilled +
                              " 个，普通/圆形槽位已填 " + legacyFilled + " 个。");
                }
            }

            Debug.Log("[PuzzleEscape] Windows（Standalone）已填图标槽位：" + standalone +
                      "。为 0 表示 .exe 不会带自定义图标。");
        }

        /// <summary>
        /// Windows 图标：Standalone 的图标槽固定是 8 个尺寸（16/24/32/48/64/96/128/256），
        /// 必须一次传入 8 张纹理，少于 8 张 Unity 会直接拒绝。这里每个槽都填同一张图，
        /// 构建时由 Unity 缩放到对应尺寸。
        /// </summary>
        private static bool ApplyWindowsIcon(Texture2D icon)
        {
            if (CountFilledIcons("m_BuildTargetIcons", "Standalone") > 0)
            {
                return true;
            }

            var icons = new Texture2D[StandaloneIconCount];
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i] = icon;
            }

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons, IconKind.Any);
            if (CountFilledIcons("m_BuildTargetIcons", "Standalone") > 0)
            {
                Debug.Log("[PuzzleEscape] Windows 图标已通过 IconKind.Any 写入 " + StandaloneIconCount + " 个尺寸。");
                return true;
            }

            // 兜底：直接按序列化格式写入 Standalone 的 8 个纹理槽。
            return WriteStandaloneIconSerialized(icon);
        }

        /// <summary>Standalone 图标槽数量：16/24/32/48/64/96/128/256。</summary>
        private const int StandaloneIconCount = 8;

        private static bool ApplyPlatformIcon(BuildTargetGroup group, Texture2D icon)
        {
            PlayerSettings.SetIconsForTargetGroup(group, new[] { icon }, IconKind.Any);
            return true;
        }

        private static int CountFilledIcons(string propertyName, string buildTarget)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath);
            if (assets == null || assets.Length == 0)
            {
                return 0;
            }

            var settings = new SerializedObject(assets[0]);
            SerializedProperty platforms = settings.FindProperty(propertyName);
            if (platforms == null)
            {
                return 0;
            }

            int filled = 0;
            for (int i = 0; i < platforms.arraySize; i++)
            {
                SerializedProperty platform = platforms.GetArrayElementAtIndex(i);
                SerializedProperty target = platform.FindPropertyRelative("m_BuildTarget");
                if (target == null || target.stringValue != buildTarget)
                {
                    continue;
                }

                SerializedProperty icons = platform.FindPropertyRelative("m_Icons");
                if (icons == null)
                {
                    continue;
                }

                for (int j = 0; j < icons.arraySize; j++)
                {
                    if (IsIconSlotFilled(icons.GetArrayElementAtIndex(j)))
                    {
                        filled++;
                    }
                }
            }

            return filled;
        }

        /// <summary>
        /// Standalone 的槽位结构是 { m_Icon, m_Width, m_Height, m_Kind }；
        /// Android 平台图标是 { m_Textures, m_Width, m_Height, m_Kind }。
        /// 两种都识别，避免明明写成功却被判定为失败。
        /// </summary>
        private static bool IsIconSlotFilled(SerializedProperty slot)
        {
            if (slot.propertyType == SerializedPropertyType.ObjectReference)
            {
                return slot.objectReferenceValue != null;
            }

            SerializedProperty icon = slot.FindPropertyRelative("m_Icon");
            if (icon != null)
            {
                return icon.objectReferenceValue != null;
            }

            SerializedProperty textures = slot.FindPropertyRelative("m_Textures");
            return textures != null && textures.arraySize > 0;
        }

        /// <summary>Windows 兜底：在 m_BuildTargetIcons 里为 Standalone 建一个含纹理的槽位。</summary>
        private static bool WriteStandaloneIconSerialized(Texture2D icon)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath);
            if (assets == null || assets.Length == 0)
            {
                return false;
            }

            var settings = new SerializedObject(assets[0]);
            SerializedProperty platforms = settings.FindProperty("m_BuildTargetIcons");
            if (platforms == null)
            {
                return false;
            }

            SerializedProperty standalone = null;
            for (int i = 0; i < platforms.arraySize; i++)
            {
                SerializedProperty platform = platforms.GetArrayElementAtIndex(i);
                SerializedProperty target = platform.FindPropertyRelative("m_BuildTarget");
                if (target != null && target.stringValue == "Standalone")
                {
                    standalone = platform;
                    break;
                }
            }

            if (standalone == null)
            {
                platforms.arraySize++;
                standalone = platforms.GetArrayElementAtIndex(platforms.arraySize - 1);
                standalone.FindPropertyRelative("m_BuildTarget").stringValue = "Standalone";
            }

            SerializedProperty icons = standalone.FindPropertyRelative("m_Icons");
            if (icons == null)
            {
                return false;
            }

            // Standalone 的 m_Icons 是 8 个纹理引用（16/24/32/48/64/96/128/256）。
            icons.arraySize = StandaloneIconCount;

            for (int i = 0; i < icons.arraySize; i++)
            {
                SerializedProperty slot = icons.GetArrayElementAtIndex(i);
                if (slot.propertyType == SerializedPropertyType.ObjectReference)
                {
                    slot.objectReferenceValue = icon;
                    continue;
                }

                SerializedProperty single = slot.FindPropertyRelative("m_Icon");
                if (single != null)
                {
                    single.objectReferenceValue = icon;
                    continue;
                }

                SerializedProperty textures = slot.FindPropertyRelative("m_Textures");
                if (textures == null)
                {
                    return false;
                }

                textures.arraySize = 1;
                textures.GetArrayElementAtIndex(0).objectReferenceValue = icon;
            }

            settings.ApplyModifiedProperties();
            settings.Update();
            return CountFilledIcons("m_BuildTargetIcons", "Standalone") > 0;
        }

        /// <summary>
        /// Unity 没有公开 Android 自适应图标的 API，这里直接改 PlayerSettings 里
        /// m_BuildTargetPlatformIcons 中 m_Kind == 2（Adaptive）的条目：
        /// m_Textures 第一个是背景、第二个是前景。
        /// </summary>
        private static bool ApplyAdaptiveIcons(Texture2D background, Texture2D foreground)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath);
            if (assets == null || assets.Length == 0)
            {
                return false;
            }

            var settings = new SerializedObject(assets[0]);
            SerializedProperty platforms = settings.FindProperty("m_BuildTargetPlatformIcons");
            if (platforms == null || !platforms.isArray)
            {
                return false;
            }

            bool applied = false;

            for (int i = 0; i < platforms.arraySize; i++)
            {
                SerializedProperty platform = platforms.GetArrayElementAtIndex(i);
                SerializedProperty target = platform.FindPropertyRelative("m_BuildTarget");
                if (target == null || target.stringValue != "Android")
                {
                    continue;
                }

                SerializedProperty icons = platform.FindPropertyRelative("m_Icons");
                if (icons == null || !icons.isArray)
                {
                    continue;
                }

                for (int j = 0; j < icons.arraySize; j++)
                {
                    SerializedProperty entry = icons.GetArrayElementAtIndex(j);
                    SerializedProperty kind = entry.FindPropertyRelative("m_Kind");
                    if (kind == null || kind.intValue != KindAdaptive)
                    {
                        continue;
                    }

                    SerializedProperty textures = entry.FindPropertyRelative("m_Textures");
                    if (textures == null)
                    {
                        continue;
                    }

                    textures.arraySize = 2;
                    textures.GetArrayElementAtIndex(0).objectReferenceValue = background;
                    textures.GetArrayElementAtIndex(1).objectReferenceValue = foreground;
                    applied = true;
                }
            }

            if (applied)
            {
                settings.ApplyModifiedProperties();
                settings.Update();
            }

            return applied;
        }

        /// <summary>
        /// 图标需要不压缩、无 mipmap、保持方形尺寸，否则缩小后会发虚。
        /// 前景图必须保留透明通道。
        /// </summary>
        private static void ConfigureImport(string path, bool keepAlpha)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaIsTransparency = keepAlpha;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }
    }
}
#endif
