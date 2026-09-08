// cu-font-loader - External font loader for Casualties: Unknown
// Licensed under MIT License
// https://github.com/FenChen0211/cu-font-loader

using System;
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
    [BepInPlugin("fenchen.cu-font-loader", "CU Font Loader", "1.4.0-pre.1")]
    public class FontLoaderPlugin : BaseUnityPlugin
    {
        private static ManualLogSource Log;
        private static readonly List<TMP_FontAsset> LoadedFonts = new List<TMP_FontAsset>();
        private static readonly List<Font> LoadedUnityFonts = new List<Font>();
        private static TMP_FontAsset PrimaryFontAsset;
        private static Font PrimaryUnityFont;
        private static readonly HashSet<TMP_Text> SeenTexts = new HashSet<TMP_Text>();
        private static readonly HashSet<UnityEngine.UI.Text> SeenLegacyTexts = new HashSet<UnityEngine.UI.Text>();
        private static readonly List<string> ExcludedObjectPatterns = new List<string>();
        private static readonly List<string> ExcludedFontPatterns = new List<string>();
        private static readonly List<string> ExcludedMaterialPatterns = new List<string>();
        private static readonly Dictionary<UnityEngine.Object, string> DecisionLogCache = new Dictionary<UnityEngine.Object, string>();
        private static int decisionLogCount;
        private static bool decisionLogCapReached;
        private static int scanPassCount;

        internal static ConfigEntry<bool> ReplaceAllText;
        internal static ConfigEntry<FontReplaceMode> ReplaceMode;
        internal static ConfigEntry<float> ScanIntervalSeconds;
        internal static ConfigEntry<string> PrimaryFont;
        internal static ConfigEntry<int> AtlasSize;
        internal static ConfigEntry<int> SamplingPointSize;
        internal static ConfigEntry<int> AtlasPadding;
        internal static ConfigEntry<bool> PreserveCustomMaterials;
        internal static ConfigEntry<string> ExcludedFontNames;
        internal static ConfigEntry<string> ExcludedMaterialNames;
        internal static ConfigEntry<string> ExcludedObjectNames;
        internal static ConfigEntry<bool> LogTextDetails;
        internal static ConfigEntry<int> MaxLoggedTexts;
        internal static ConfigEntry<bool> CompatibilityLog;
        internal static ConfigEntry<int> MaxCompatibilityLogs;

        private static float s_nextScanTime;

        public enum FontReplaceMode
        {
            FallbackOnly,
            ReplaceOnce,
            Persistent,
            Safe,
            Normal,
            Aggressive
        }

        private struct TextDecision
        {
            public bool Replace;
            public string SkipReason;
        }

        private void Awake()
        {
            Log = Logger;

            ReplaceAllText = Config.Bind(
                "General",
                "ReplaceAllText",
                false,
                "已废弃。这是旧版本的开关，现在不再产生任何作用，保留只是为了不破坏旧配置文件。请使用 ReplaceMode。"
            );

            ReplaceMode = Config.Bind(
                "General",
                "ReplaceMode",
                FontReplaceMode.Normal,
                "FallbackOnly = 只把外部字体加入 TMP fallback，不主动替换游戏字体（最安全）。 " +
                "Safe = 只替换普通文本，跳过明显使用 Sprite、特殊字体或自定义材质的文本。 " +
                "Normal = 替换大多数文本，同时保留其他 Mod 的自定义 UI 和材质。 " +
                "Aggressive = 接近旧版 Persistent 行为，反复强制替换所有文本。 " +
                "旧版 ReplaceOnce / Persistent 会在首次启动时自动迁移为 Normal / Aggressive。"
            );

            ScanIntervalSeconds = Config.Bind(
                "General",
                "ScanIntervalSeconds",
                1.0f,
                "Safe / Normal / Aggressive 模式下，扫描新 TMP 文本的间隔秒数。不建议低于 0.25。"
            );

            PrimaryFont = Config.Bind(
                "General",
                "PrimaryFont",
                "",
                "加载了多个字体时，用哪一个作为主替换字体（按文件名匹配，不含扩展名，不区分大小写）。" +
                "留空则使用按文件名排序后的第一个字体。其余字体只作为 fallback 使用。"
            );

            AtlasSize = Config.Bind(
                "Font",
                "AtlasSize",
                4096,
                "外部 TMP 动态字体图集大小。中文字符较多，较大的图集可以减少回退到游戏原字体的情况。"
            );

            SamplingPointSize = Config.Bind(
                "Font",
                "SamplingPointSize",
                36,
                "创建 TMP 字体资产时使用的采样字号。"
            );

            AtlasPadding = Config.Bind(
                "Font",
                "AtlasPadding",
                5,
                "创建 TMP 字体资产时使用的字形间距。"
            );

            PreserveCustomMaterials = Config.Bind(
                "Compatibility",
                "PreserveCustomMaterials",
                true,
                "保护其他 Mod 的自定义材质。开启时，使用非默认材质（描边、辉光、自定义 Shader 等）的文本会被整体跳过，不会替换其字体。" +
                "关闭后恢复旧版行为：这些文本的字体和材质会被一起替换。"
            );

            ExcludedFontNames = Config.Bind(
                "Compatibility",
                "ExcludedFontNames",
                "",
                "不替换使用这些字体（FontAsset 名称）的文本。逗号分隔，不区分大小写，名称包含关键字即命中。"
            );

            ExcludedMaterialNames = Config.Bind(
                "Compatibility",
                "ExcludedMaterialNames",
                "",
                "不替换使用这些材质（材质名称）的文本。逗号分隔，不区分大小写，名称包含关键字即命中。"
            );

            ExcludedObjectNames = Config.Bind(
                "Compatibility",
                "ExcludedObjectNames",
                "",
                "不替换这些 GameObject 名称的 TMP / 旧版文本。逗号分隔，不区分大小写，名称包含关键字即命中。"
            );

            ExcludedFontNames.SettingChanged += OnExclusionSettingChanged;
            ExcludedMaterialNames.SettingChanged += OnExclusionSettingChanged;
            ExcludedObjectNames.SettingChanged += OnExclusionSettingChanged;
            RefreshExclusionCache();

            LogTextDetails = Config.Bind(
                "Debug",
                "LogTextDetails",
                false,
                "调试用。开启后会在日志中输出 TMP 文本对象名称、激活状态、字体、材质和文本片段。"
            );

            MaxLoggedTexts = Config.Bind(
                "Debug",
                "MaxLoggedTexts",
                80,
                "LogTextDetails 开启时，每次字体扫描最多输出多少个 TMP 文本对象。"
            );

            CompatibilityLog = Config.Bind(
                "Debug",
                "CompatibilityLog",
                false,
                "兼容性调试日志。开启后，文本被替换或跳过时会输出一行决策记录（场景、对象名、字体、材质、原因）。" +
                "同一对象的同一决策只记录一次，不会持续刷屏。排查其他 Mod UI 被误伤时开启。"
            );

            MaxCompatibilityLogs = Config.Bind(
                "Debug",
                "MaxCompatibilityLogs",
                200,
                "CompatibilityLog 开启时，本次游戏会话最多输出多少条决策记录，防止日志过大。"
            );

            MigrateLegacyReplaceMode();

            string fontsDir = Path.Combine(Paths.PluginPath, "cu-font-loader", "fonts");
            if (!Directory.Exists(fontsDir))
            {
                Directory.CreateDirectory(fontsDir);
                Log.LogInfo("fonts/ dir missing, created: " + fontsDir);
            }

            List<string> fontPaths = new List<string>();
            foreach (string pattern in new[] { "*.ttf", "*.otf" })
            {
                fontPaths.AddRange(Directory.GetFiles(fontsDir, pattern));
            }

            fontPaths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string fontPath in fontPaths)
            {
                LoadFont(fontPath);
            }

            if (LoadedFonts.Count == 0)
            {
                return;
            }

            ResolvePrimaryFont();
            Log.LogInfo("Loaded " + LoadedFonts.Count + " font(s), primary: " + PrimaryFontAsset.name);
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToLoadedText(true);
            ApplyToLoadedLegacyText(true);
        }

        private void Update()
        {
            if (LoadedFonts.Count == 0 || GetEffectiveMode() == FontReplaceMode.FallbackOnly)
            {
                return;
            }

            float interval = Mathf.Max(0.25f, ScanIntervalSeconds.Value);
            if (Time.unscaledTime < s_nextScanTime)
            {
                return;
            }

            s_nextScanTime = Time.unscaledTime + interval;
            ApplyToLoadedText(false);
            ApplyToLoadedLegacyText(false);
        }

        private static FontReplaceMode GetEffectiveMode()
        {
            switch (ReplaceMode.Value)
            {
                case FontReplaceMode.ReplaceOnce:
                    return FontReplaceMode.Normal;
                case FontReplaceMode.Persistent:
                    return FontReplaceMode.Aggressive;
                default:
                    return ReplaceMode.Value;
            }
        }

        private void MigrateLegacyReplaceMode()
        {
            switch (ReplaceMode.Value)
            {
                case FontReplaceMode.ReplaceOnce:
                    Log.LogInfo("Migrating legacy ReplaceMode: ReplaceOnce -> Normal");
                    ReplaceMode.Value = FontReplaceMode.Normal;
                    Config.Save();
                    break;
                case FontReplaceMode.Persistent:
                    Log.LogInfo("Migrating legacy ReplaceMode: Persistent -> Aggressive");
                    ReplaceMode.Value = FontReplaceMode.Aggressive;
                    Config.Save();
                    break;
            }
        }

        private static TextDecision EvaluateTMPText(TMP_Text text)
        {
            TextDecision decision = default(TextDecision);
            decision.Replace = true;

            TMP_FontAsset font = text.font;
            if (font == null)
            {
                decision.Replace = false;
                decision.SkipReason = "FontMissing";
                return decision;
            }

            TMP_FontAsset primary = GetPrimaryFontAsset();
            if (primary != null && font == primary)
            {
                decision.Replace = false;
                return decision;
            }

            if (ExcludedObjectPatterns.Count > 0 &&
                MatchesExclusion(text.gameObject.name, ExcludedObjectPatterns))
            {
                decision.Replace = false;
                decision.SkipReason = "Excluded";
                return decision;
            }

            if (ExcludedFontPatterns.Count > 0 &&
                MatchesExclusion(font.name, ExcludedFontPatterns))
            {
                decision.Replace = false;
                decision.SkipReason = "Excluded";
                return decision;
            }

            Material material = text.fontSharedMaterial;
            if (material == null)
            {
                decision.Replace = false;
                decision.SkipReason = "MaterialMissing";
                return decision;
            }

            if (ExcludedMaterialPatterns.Count > 0 &&
                MatchesExclusion(material.name, ExcludedMaterialPatterns))
            {
                decision.Replace = false;
                decision.SkipReason = "ExcludedMaterial";
                return decision;
            }

            FontReplaceMode mode = GetEffectiveMode();
            if (mode != FontReplaceMode.Aggressive && !IsTmpShader(material.shader))
            {
                decision.Replace = false;
                decision.SkipReason = "CustomShader";
                return decision;
            }

            if (material != font.material && PreserveCustomMaterials.Value)
            {
                decision.Replace = false;
                decision.SkipReason = "CustomMaterial";
                return decision;
            }

            if (mode == FontReplaceMode.Safe)
            {
                string content = text.text;
                if (content != null && content.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    decision.Replace = false;
                    decision.SkipReason = "SpriteTag";
                    return decision;
                }
            }

            return decision;
        }

        private static bool IsTmpShader(Shader shader)
        {
            if (shader == null)
            {
                return false;
            }

            string shaderName = shader.name;
            return shaderName != null &&
                (shaderName.StartsWith("TextMeshPro/", StringComparison.OrdinalIgnoreCase) ||
                 shaderName.StartsWith("Text Mesh Pro/", StringComparison.OrdinalIgnoreCase));
        }

        private static void OnExclusionSettingChanged(object sender, EventArgs e)
        {
            RefreshExclusionCache();
        }

        private static void RefreshExclusionCache()
        {
            ParseExclusions(ExcludedObjectNames.Value, ExcludedObjectPatterns);
            ParseExclusions(ExcludedFontNames.Value, ExcludedFontPatterns);
            ParseExclusions(ExcludedMaterialNames.Value, ExcludedMaterialPatterns);
        }

        private static void ParseExclusions(string raw, List<string> target)
        {
            target.Clear();
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length > 0)
                {
                    target.Add(part);
                }
            }
        }

        private static bool MatchesExclusion(string value, List<string> patterns)
        {
            if (value == null || patterns.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < patterns.Count; i++)
            {
                if (value.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBeginDecisionLog(UnityEngine.Object key, string signature)
        {
            if (!CompatibilityLog.Value || decisionLogCapReached)
            {
                return false;
            }

            string existing;
            if (DecisionLogCache.TryGetValue(key, out existing) && existing == signature)
            {
                return false;
            }

            int cap = Mathf.Clamp(MaxCompatibilityLogs.Value, 1, 100000);
            if (decisionLogCount >= cap)
            {
                decisionLogCapReached = true;
                Log.LogInfo("[Compat] decision log cap reached (" + cap + "), further records are suppressed");
                return false;
            }

            DecisionLogCache[key] = signature;
            decisionLogCount++;
            return true;
        }

        private static void LogReplaced(TMP_Text text, TMP_FontAsset previousFont)
        {
            if (!CompatibilityLog.Value || decisionLogCapReached)
            {
                return;
            }

            string from = previousFont != null ? previousFont.name : "<null>";
            if (!TryBeginDecisionLog(text, "R|" + from))
            {
                return;
            }

            string materialName = text.fontSharedMaterial != null ? text.fontSharedMaterial.name : "<null>";
            Log.LogInfo(
                "[Compat] Scene=" + SceneManager.GetActiveScene().name +
                " Object=" + text.gameObject.name +
                " Font=" + text.font.name +
                " Material=" + materialName +
                " Action=Replaced" +
                " From=" + from);
        }

        private static void LogSkipped(TMP_Text text, string reason)
        {
            if (!CompatibilityLog.Value || decisionLogCapReached)
            {
                return;
            }

            string fontName = text.font != null ? text.font.name : "<null>";
            if (!TryBeginDecisionLog(text, "S|" + reason + "|" + fontName))
            {
                return;
            }

            string materialName = text.fontSharedMaterial != null ? text.fontSharedMaterial.name : "<null>";
            Log.LogInfo(
                "[Compat] Scene=" + SceneManager.GetActiveScene().name +
                " Object=" + text.gameObject.name +
                " Font=" + fontName +
                " Material=" + materialName +
                " Action=Skipped" +
                " Reason=" + reason);
        }

        private static void LogLegacyReplaced(UnityEngine.UI.Text text, Font previousFont)
        {
            if (!CompatibilityLog.Value || decisionLogCapReached)
            {
                return;
            }

            string from = previousFont != null ? previousFont.name : "<null>";
            if (!TryBeginDecisionLog(text, "R|" + from))
            {
                return;
            }

            Log.LogInfo(
                "[Compat] Scene=" + SceneManager.GetActiveScene().name +
                " Object=" + text.gameObject.name +
                " Font=" + text.font.name +
                " Action=Replaced" +
                " From=" + from);
        }

        private static void LogLegacySkipped(UnityEngine.UI.Text text, string reason)
        {
            if (!CompatibilityLog.Value || decisionLogCapReached)
            {
                return;
            }

            string fontName = text.font != null ? text.font.name : "<null>";
            if (!TryBeginDecisionLog(text, "S|" + reason + "|" + fontName))
            {
                return;
            }

            Log.LogInfo(
                "[Compat] Scene=" + SceneManager.GetActiveScene().name +
                " Object=" + text.gameObject.name +
                " Font=" + fontName +
                " Action=Skipped" +
                " Reason=" + reason);
        }

        private static void PruneDestroyed()
        {
            SeenTexts.RemoveWhere(t => t == null);
            SeenLegacyTexts.RemoveWhere(t => t == null);

            List<UnityEngine.Object> deadKeys = null;
            foreach (KeyValuePair<UnityEngine.Object, string> pair in DecisionLogCache)
            {
                if (pair.Key == null)
                {
                    if (deadKeys == null)
                    {
                        deadKeys = new List<UnityEngine.Object>();
                    }

                    deadKeys.Add(pair.Key);
                }
            }

            if (deadKeys != null)
            {
                for (int i = 0; i < deadKeys.Count; i++)
                {
                    DecisionLogCache.Remove(deadKeys[i]);
                }
            }
        }

        private static void LoadFont(string fontPath)
        {
            try
            {
                Log.LogInfo("Loading: " + Path.GetFileName(fontPath));
                Font dynamicFont = new Font(fontPath);
                int atlasSize = Mathf.Clamp(AtlasSize.Value, 512, 8192);
                int samplingPointSize = Mathf.Clamp(SamplingPointSize.Value, 8, 128);
                int atlasPadding = Mathf.Clamp(AtlasPadding.Value, 1, 16);
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                    dynamicFont,
                    samplingPointSize,
                    atlasPadding,
                    GlyphRenderMode.SDFAA,
                    atlasSize,
                    atlasSize,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (fontAsset == null)
                {
                    return;
                }

                fontAsset.name = Path.GetFileNameWithoutExtension(fontPath);
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.isMultiAtlasTexturesEnabled = true;
                LoadedFonts.Add(fontAsset);
                LoadedUnityFonts.Add(dynamicFont);
                Log.LogInfo("  OK: " + fontAsset.name + " (" + atlasSize + "x" + atlasSize + " dynamic atlas)");
            }
            catch (System.Exception ex)
            {
                Log.LogError("  FAIL: " + ex.Message);
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SeenTexts.Clear();
            SeenLegacyTexts.Clear();
            DecisionLogCache.Clear();
            ApplyToLoadedText(true);
            ApplyToLoadedLegacyText(true);
        }

        private static void ApplyToLoadedText(bool logResult)
        {
            RegisterGlobalFallbacks();

            TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            int replaced = 0;
            int skipped = 0;
            int patchedFallback = 0;
            FontReplaceMode mode = GetEffectiveMode();
            bool enforceRepeatedly = mode == FontReplaceMode.Aggressive || mode == FontReplaceMode.Persistent;

            scanPassCount++;
            if (scanPassCount % 32 == 0)
            {
                PruneDestroyed();
            }

            foreach (TMP_Text text in allTexts)
            {
                if (text == null)
                {
                    continue;
                }

                if (PatchExternalFallbacks(text.font))
                {
                    patchedFallback++;
                }

                if (mode == FontReplaceMode.FallbackOnly)
                {
                    continue;
                }

                TextDecision decision = EvaluateTMPText(text);
                if (!decision.Replace)
                {
                    if (decision.SkipReason != null)
                    {
                        skipped++;
                        LogSkipped(text, decision.SkipReason);
                    }

                    continue;
                }

                bool firstSeen = !SeenTexts.Contains(text);
                if (!enforceRepeatedly && !firstSeen)
                {
                    continue;
                }

                TMP_FontAsset previousFont = text.font;
                if (ReplaceTextFont(text))
                {
                    SeenTexts.Add(text);
                    replaced++;
                    LogReplaced(text, previousFont);
                }
            }

            if (LogTextDetails.Value)
            {
                LogTextSnapshot(allTexts);
            }

            int patchedControls = PatchTmpControls();

            if (logResult || replaced > 0)
            {
                Log.LogInfo(
                    "Font pass: scanned " + allTexts.Length +
                    ", replaced " + replaced +
                    ", skipped " + skipped +
                    ", patched fallback " + patchedFallback +
                    ", patched controls " + patchedControls);
            }
        }

        private static void LogTextSnapshot(TMP_Text[] allTexts)
        {
            int limit = Mathf.Clamp(MaxLoggedTexts.Value, 1, 500);
            int logged = 0;
            foreach (TMP_Text text in allTexts)
            {
                if (text == null)
                {
                    continue;
                }

                GameObject obj = text.gameObject;
                string sample = text.text;
                if (sample == null)
                {
                    sample = string.Empty;
                }
                sample = sample.Replace("\r", " ").Replace("\n", " ");
                if (sample.Length > 32)
                {
                    sample = sample.Substring(0, 32);
                }

                string fontName = text.font != null ? text.font.name : "<null>";
                string materialName = text.fontSharedMaterial != null ? text.fontSharedMaterial.name : "<null>";
                Log.LogInfo(
                    "Text detail: active=" + obj.activeInHierarchy +
                    ", name=" + obj.name +
                    ", type=" + text.GetType().Name +
                    ", font=" + fontName +
                    ", material=" + materialName +
                    ", text=\"" + sample + "\"");

                logged++;
                if (logged >= limit)
                {
                    break;
                }
            }
        }

        private static int PatchTmpControls()
        {
            if (LoadedFonts.Count == 0 || GetEffectiveMode() == FontReplaceMode.FallbackOnly)
            {
                return 0;
            }

            TMP_FontAsset primary = GetPrimaryFontAsset();
            if (primary == null)
            {
                return 0;
            }

            int patched = 0;

            TMP_InputField[] inputFields = Resources.FindObjectsOfTypeAll<TMP_InputField>();
            foreach (TMP_InputField inputField in inputFields)
            {
                if (inputField == null)
                {
                    continue;
                }

                TMP_Text textComponent = inputField.textComponent;
                if (textComponent == null || textComponent.font != primary)
                {
                    continue;
                }

                if (inputField.fontAsset != primary)
                {
                    inputField.fontAsset = primary;
                    patched++;
                }
            }

            return patched;
        }

        private static void ApplyToLoadedLegacyText(bool logResult)
        {
            if (LoadedUnityFonts.Count == 0 || GetEffectiveMode() == FontReplaceMode.FallbackOnly)
            {
                return;
            }

            if (PrimaryUnityFont == null)
            {
                return;
            }

            FontReplaceMode mode = GetEffectiveMode();
            bool enforceRepeatedly = mode == FontReplaceMode.Aggressive || mode == FontReplaceMode.Persistent;

            UnityEngine.UI.Text[] allTexts = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>();
            int replaced = 0;

            foreach (UnityEngine.UI.Text text in allTexts)
            {
                if (text == null)
                {
                    continue;
                }

                if (text.font == null)
                {
                    continue;
                }

                if ((ExcludedObjectPatterns.Count > 0 &&
                        MatchesExclusion(text.gameObject.name, ExcludedObjectPatterns)) ||
                    (ExcludedFontPatterns.Count > 0 &&
                        MatchesExclusion(text.font.name, ExcludedFontPatterns)))
                {
                    LogLegacySkipped(text, "Excluded");
                    continue;
                }

                bool firstSeen = !SeenLegacyTexts.Contains(text);
                if (!enforceRepeatedly && !firstSeen)
                {
                    continue;
                }

                Font previousFont = text.font;
                if (text.font != PrimaryUnityFont)
                {
                    text.font = PrimaryUnityFont;
                    SeenLegacyTexts.Add(text);
                    replaced++;
                    LogLegacyReplaced(text, previousFont);
                }
            }

            if (logResult || replaced > 0)
            {
                Log.LogInfo("Legacy UI.Text pass: scanned " + allTexts.Length + ", replaced " + replaced);
            }
        }

        private static void RegisterGlobalFallbacks()
        {
            List<TMP_FontAsset> globalFallbacks = TMP_Settings.fallbackFontAssets;
            if (globalFallbacks == null)
            {
                return;
            }

            foreach (TMP_FontAsset fontAsset in LoadedFonts)
            {
                if (!globalFallbacks.Contains(fontAsset))
                {
                    globalFallbacks.Add(fontAsset);
                }
            }
        }

        private static bool PatchExternalFallbacks(TMP_FontAsset originalFont)
        {
            if (originalFont == null)
            {
                return false;
            }

            bool changed = false;
            foreach (TMP_FontAsset externalFont in LoadedFonts)
            {
                if (originalFont == externalFont)
                {
                    continue;
                }

                if (externalFont.fallbackFontAssetTable == null)
                {
                    externalFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                }

                List<TMP_FontAsset> fallbacks = externalFont.fallbackFontAssetTable;
                if (!fallbacks.Contains(originalFont))
                {
                    fallbacks.Add(originalFont);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool ReplaceTextFont(TMP_Text text)
        {
            if (text == null)
            {
                return false;
            }

            TMP_FontAsset replacement = GetPrimaryFontAsset();
            if (replacement == null)
            {
                return false;
            }

            TMP_FontAsset currentFont = text.font;
            if (currentFont == null || currentFont == replacement)
            {
                return false;
            }

            Material currentMaterial = text.fontSharedMaterial;
            bool replaceMaterial =
                currentMaterial == null ||
                currentMaterial == currentFont.material ||
                !PreserveCustomMaterials.Value;

            text.font = replacement;

            if (replaceMaterial && text.fontSharedMaterial != replacement.material)
            {
                text.fontSharedMaterial = replacement.material;
            }

            text.SetAllDirty();
            return true;
        }

        private static void ResolvePrimaryFont()
        {
            PrimaryFontAsset = null;
            PrimaryUnityFont = null;

            if (LoadedFonts.Count == 0)
            {
                return;
            }

            int index = 0;
            string wanted = PrimaryFont.Value;
            if (!string.IsNullOrEmpty(wanted))
            {
                for (int i = 0; i < LoadedFonts.Count; i++)
                {
                    if (string.Equals(LoadedFonts[i].name, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }

                if (index == 0 && !string.Equals(LoadedFonts[0].name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogWarning("PrimaryFont '" + wanted + "' not found, using '" + LoadedFonts[0].name + "'");
                }
            }

            PrimaryFontAsset = LoadedFonts[index];
            if (index < LoadedUnityFonts.Count)
            {
                PrimaryUnityFont = LoadedUnityFonts[index];
            }
        }

        private static TMP_FontAsset GetPrimaryFontAsset()
        {
            if (PrimaryFontAsset == null && LoadedFonts.Count > 0)
            {
                ResolvePrimaryFont();
            }

            return PrimaryFontAsset;
        }
    }
}
