// cu-font-loader - External font loader for Casualties: Unknown
// Licensed under MIT License
// https://github.com/FenChen0211/cu-font-loader
//
// 独立 BepInEx 插件: 扫描 fonts/ 目录中的 .ttf/.otf 文件，
// 注入 TextMeshPro 字体回退表 / 全局替换。
// 不依赖任何游戏特定代码。

using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace CuFontLoader
{
    [BepInPlugin("fenchen.cu-font-loader", "CU Font Loader", "1.2.0")]
    public class FontLoaderPlugin : BaseUnityPlugin
    {
        private static ManualLogSource Log;
        private static List<TMP_FontAsset> s_loadedFonts = new List<TMP_FontAsset>();
        internal static ConfigEntry<bool> ReplaceAllText;

        private void Awake()
        {
            Log = Logger;

            ReplaceAllText = Config.Bind<bool>(
                "General",
                "ReplaceAllText",
                false,
                "true = global replace all TMP text with external font, " +
                "false = fallback only (default)"
            );

            string fontsDir = Path.Combine(Paths.PluginPath, "cu-font-loader", "fonts");

            if (!Directory.Exists(fontsDir))
            {
                Log.LogInfo("fonts/ dir not found, skipping");
                return;
            }

            foreach (string pattern in new[] { "*.ttf", "*.otf" })
            {
                foreach (string fontPath in Directory.GetFiles(fontsDir, pattern))
                {
                    try
                    {
                        Log.LogInfo("Loading: " + Path.GetFileName(fontPath));
                        Font df = new Font(fontPath);
                        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(
                            df, 36, 5, GlyphRenderMode.SDFAA, 512, 512);
                        if (fa != null)
                        {
                            s_loadedFonts.Add(fa);
                            Log.LogInfo("  OK: " + fa.name);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.LogError("  FAIL: " + ex.Message);
                    }
                }
            }

            if (s_loadedFonts.Count == 0) return;

            Log.LogInfo("Loaded " + s_loadedFonts.Count + " font(s)");

            // 始终注册 sceneLoaded（回退 + 全局替换都在这处理，无需 Harmony）
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 1. 回退模式：注入全局 fallback 表
            var fallback = TMP_Settings.fallbackFontAssets;
            foreach (var fa in s_loadedFonts)
            {
                if (!fallback.Contains(fa))
                {
                    fallback.Add(fa);
                }
            }

            // 2. 全局替换模式：遍历场景内所有 TMP_Text，直接设 font
            if (ReplaceAllText.Value && s_loadedFonts.Count > 0)
            {
                var allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                foreach (var text in allTexts)
                {
                    if (text.font != s_loadedFonts[0])
                    {
                        text.font = s_loadedFonts[0];
                    }
                }
                Log.LogInfo("Global replace applied to " + allTexts.Length + " TMP_Text objects");
            }
        }
    }
}
