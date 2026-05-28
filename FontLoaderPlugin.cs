// cu-font-loader - External font loader for Casualties: Unknown
// Licensed under MIT License
// https://github.com/FenChen0211/cu-font-loader
//
// 独立 BepInEx 插件: 扫描 fonts/ 目录中的 .ttf/.otf 文件，
// 自动注入 TextMeshPro 全局回退字体表。
// 不依赖任何游戏特定代码。

using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace CuFontLoader
{
    [BepInPlugin("fenchen.cu-font-loader", "CU Font Loader", "1.0.0")]
    public class FontLoaderPlugin : BaseUnityPlugin
    {
        private static ManualLogSource Log;
        private static List<TMP_FontAsset> s_loadedFonts = new List<TMP_FontAsset>();

        private void Awake()
        {
            Log = Logger;

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

            if (s_loadedFonts.Count > 0)
            {
                Log.LogInfo("Loaded " + s_loadedFonts.Count + " font(s), registering callback");
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var fallback = TMP_Settings.fallbackFontAssets;
            foreach (var fa in s_loadedFonts)
            {
                if (!fallback.Contains(fa))
                {
                    fallback.Add(fa);
                    Log.LogInfo("  injected: " + fa.name + " (scene: " + scene.name + ")");
                }
            }
        }
    }
}
