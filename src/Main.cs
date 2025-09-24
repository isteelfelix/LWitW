using MelonLoader;
using UnityEngine;
using HarmonyLib;
using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(LWitWMod.Main), "LWitWMod", "1.0.0", "Chieftain51")]
[assembly: MelonGame("SunnySideUp", "Little Witch In The Woods")]

namespace LWitWMod
{
    public class Main : MelonMod
    {
        internal static Translation Translation;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[LWitWMod] LWitWMod loaded!");

            // Load ru.json translations first (so Translate is available)
            try
            {
                string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string ruJsonPath = Path.Combine(modPath, "ru.json");
                Translation = new Translation(ruJsonPath);
                MelonLogger.Msg($"Loaded translations: {Translation.Count} entries");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load translations: {ex}");
            }

            // Try to load user's Cyrillic AssetBundle (if present)
            try
            {
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string gameRoot = dllDir;
                if (string.Equals(Path.GetFileName(dllDir), "Mods", StringComparison.OrdinalIgnoreCase))
                    gameRoot = Path.GetDirectoryName(dllDir) ?? dllDir;

                string bundlePath = Path.Combine(gameRoot, "Mods", "font", "cyrillicfont.bundle");
                if (!File.Exists(bundlePath))
                {
                    MelonLogger.Msg($"AssetBundle not found at: {bundlePath}");
                    return;
                }

                MelonLogger.Msg($"Loading AssetBundle from: {bundlePath}");
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    MelonLogger.Warning("Failed to load AssetBundle");
                    return;
                }

                MelonLogger.Msg("AssetBundle loaded successfully");
                var names = bundle.GetAllAssetNames();
                MelonLogger.Msg("Assets in bundle: " + string.Join(", ", names.Select(n => Path.GetFileName(n))));

                // Prefer named TMP_FontAsset, then any TMP_FontAsset, then TTF fallback
                TMP_FontAsset fontAsset = null;
                foreach (var n in names.Where(x => x.IndexOf("noto", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("notosans", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    try { fontAsset = bundle.LoadAsset<TMP_FontAsset>(n); } catch { fontAsset = null; }
                    if (fontAsset != null) { MelonLogger.Msg($"Found preferred TMP_FontAsset in bundle: {fontAsset.name}"); break; }
                }

                if (fontAsset == null)
                {
                    foreach (var n in names)
                    {
                        try { fontAsset = bundle.LoadAsset<TMP_FontAsset>(n); } catch { fontAsset = null; }
                        if (fontAsset != null) { MelonLogger.Msg($"Found TMP_FontAsset in bundle: {fontAsset.name}"); break; }
                    }
                }

                Font ttf = null;
                if (fontAsset == null)
                {
                    foreach (var n in names.Where(x => x.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
                    {
                        try { ttf = bundle.LoadAsset<Font>(n); } catch { ttf = null; }
                        if (ttf != null) { MelonLogger.Msg($"Found font file in bundle: {ttf.name}"); break; }
                    }
                }

                // load material & atlas if present
                foreach (var n in names)
                {
                    try
                    {
                        var mat = bundle.LoadAsset<Material>(n);
                        if (mat != null)
                        {
                            RuntimeCache.LoadedCyrillicMaterial = mat;
                            MelonLogger.Msg($"Found Material in bundle: {mat.name}");
                            break;
                        }
                    }
                    catch { }
                }

                // Prefer explicit PNG atlas named similarly to the TMP asset, then any .png, then any Texture2D
                string preferredAtlasName = "NotoSans-Regular SDF Atlas.png";
                // try exact preferred name first (case-insensitive match anywhere in path)
                var exact = names.FirstOrDefault(n => Path.GetFileName(n).Equals(preferredAtlasName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exact))
                {
                    try
                    {
                        var tex = bundle.LoadAsset<Texture2D>(exact);
                        if (tex != null)
                        {
                            RuntimeCache.LoadedCyrillicAtlas = tex;
                            MelonLogger.Msg($"Found preferred PNG atlas in bundle: {tex.name}");
                        }
                    }
                    catch { }
                }

                // if not found, search any .png asset
                if (RuntimeCache.LoadedCyrillicAtlas == null)
                {
                    var pngPath = names.FirstOrDefault(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(pngPath))
                    {
                        try
                        {
                            var tex = bundle.LoadAsset<Texture2D>(pngPath);
                            if (tex != null)
                            {
                                RuntimeCache.LoadedCyrillicAtlas = tex;
                                MelonLogger.Msg($"Found PNG atlas in bundle: {tex.name}");
                            }
                        }
                        catch { }
                    }
                }

                // final fallback: any Texture2D
                if (RuntimeCache.LoadedCyrillicAtlas == null)
                {
                    foreach (var n in names)
                    {
                        try
                        {
                            var tex = bundle.LoadAsset<Texture2D>(n);
                            if (tex != null)
                            {
                                RuntimeCache.LoadedCyrillicAtlas = tex;
                                MelonLogger.Msg($"Found Texture2D in bundle: {tex.name}");
                                break;
                            }
                        }
                        catch { }
                    }
                }

                // Decide on final font asset: prefer usable TMP_FontAsset, otherwise try to make one from TTF
                if (fontAsset != null && FontHelpers.IsFontAssetUsable(fontAsset))
                {
                    RuntimeCache.LoadedCyrillicFont = fontAsset;
                    RuntimeCache.LoadedBundle = bundle;
                }
                else if (ttf != null)
                {
                    try
                    {
                        var created = TMPro.TMP_FontAsset.CreateFontAsset(ttf);
                        if (created != null && FontHelpers.IsFontAssetUsable(created))
                        {
                            RuntimeCache.LoadedCyrillicFont = created;
                            RuntimeCache.LoadedBundle = bundle;
                            MelonLogger.Msg($"Created runtime TMP_FontAsset from {ttf.name}");
                        }
                        else
                        {
                            MelonLogger.Msg("Created TMP_FontAsset from TTF but it appears unusable");
                            bundle.Unload(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Failed to create TMP_FontAsset from TTF in bundle: {ex}");
                        bundle.Unload(false);
                    }
                }
                else if (fontAsset != null)
                {
                    // found a TMP_FontAsset but it seems unusable
                    MelonLogger.Msg($"Found TMP_FontAsset '{fontAsset.name}' but it appears unusable (missing atlas/glyphs)");
                    bundle.Unload(false);
                }
                else
                {
                    MelonLogger.Msg("No TMP_FontAsset or TTF found in bundle");
                    bundle.Unload(false);
                }

                // If we have a loaded font, ensure consistent visuals
                if (RuntimeCache.LoadedCyrillicFont != null)
                {
                    FontHelpers.InjectCyrillicIntoAllFallbacks(RuntimeCache.LoadedCyrillicFont);
                    FontHelpers.ApplyCyrillicGlobally(RuntimeCache.LoadedCyrillicFont);

                    // Ensure all TMP_FontAsset have our material for consistency
                    FontHelpers.ApplyCyrillicMaterialToAllFonts(RuntimeCache.LoadedCyrillicFont.material);

                    // re-apply on scene change
                    SceneManager.activeSceneChanged += (prev, next) =>
                    {
                        FontHelpers.InjectCyrillicIntoAllFallbacks(RuntimeCache.LoadedCyrillicFont);
                        FontHelpers.ApplyCyrillicGlobally(RuntimeCache.LoadedCyrillicFont);
                        FontHelpers.ApplyCyrillicMaterialToAllFonts(RuntimeCache.LoadedCyrillicFont.material);
                    };

                    // Also start a coroutine to periodically re-apply for dynamically created UI
                    MelonCoroutines.Start(PeriodicReapplyCoroutine());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initializing Cyrillic font bundle: {ex}");
            }
        }

        private static System.Collections.IEnumerator PeriodicReapplyCoroutine()
        {
            int count = 0;
            while (true)
            {
                yield return new WaitForSeconds(1f); // re-apply every 1 second
                if (RuntimeCache.LoadedCyrillicFont != null)
                {
                    try
                    {
                        FontHelpers.ApplyCyrillicGlobally(RuntimeCache.LoadedCyrillicFont);
                        FontHelpers.ApplyCyrillicMaterialToAllFonts(RuntimeCache.LoadedCyrillicFont.material);
                        count++;
                        if (count % 10 == 0) // log every 10 times to avoid spam
                            MelonLogger.Msg($"Periodic re-apply #{count}: applied Cyrillic font and material globally");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Periodic re-apply failed: {ex}");
                    }
                }
            }
        }
    }

    internal static class FontHelpers
    {
        // Returns true if the TMP_FontAsset looks initialized and safe to assign directly
        public static bool IsFontAssetUsable(TMP_FontAsset fa)
        {
            if (fa == null) return false;
            try
            {
                // atlasTextures should be present and have at least one non-null texture
                if (fa.atlasTextures == null || fa.atlasTextures.Length == 0) return false;
                if (fa.atlasTextures.Any(t => t == null)) return false;
                // glyph/character tables should be present and non-empty
                if (fa.glyphTable == null || fa.glyphTable.Count == 0) return false;
                if (fa.characterTable == null || fa.characterTable.Count == 0) return false;
                // additional sanity: faceInfo.pointSize should be non-zero (if available)
                try
                {
                    if (fa.faceInfo.pointSize == 0) return false;
                }
                catch { /* some TMP versions don't expose pointSize the same way; ignore */ }
                return true;
            }
            catch { return false; }
        }

        public static void InjectCyrillicIntoAllFallbacks(TMP_FontAsset cyr)
        {
            if (cyr == null) return;
            try
            {
                var allRaw = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
                int injected = 0;
                foreach (var o in allRaw)
                {
                    var fa = o as TMP_FontAsset;
                    if (fa == null) continue;
                    if (fa == cyr) continue;
                    if (fa.fallbackFontAssetTable == null)
                        fa.fallbackFontAssetTable = new List<TMP_FontAsset>();

                    if (!fa.fallbackFontAssetTable.Contains(cyr))
                    {
                        fa.fallbackFontAssetTable.Add(cyr);
                        injected++;
                    }
                }
                MelonLogger.Msg($"Injected Cyrillic fallback into {injected} TMP_FontAsset(s)");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to inject fallbacks: {ex}");
            }
        }

        public static bool HasCyrillic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (var ch in s)
                if (ch >= 0x0400 && ch <= 0x052F) return true;
            return false;
        }

        public static void ApplyCyrillicToText(TMP_Text __instance)
        {
            if (__instance == null) return;
            if (RuntimeCache.LoadedCyrillicFont == null) return;
            try
            {
                // If the loaded font is safe to assign (has atlas & glyphs), set it. Otherwise, only ensure it's in fallbacks.
                if (IsFontAssetUsable(RuntimeCache.LoadedCyrillicFont))
                {
                    __instance.font = RuntimeCache.LoadedCyrillicFont;
                    // Force use the font's material to ensure consistency
                    __instance.fontSharedMaterial = RuntimeCache.LoadedCyrillicFont.material;
                }
                else
                {
                    // don't assign broken font; instead add as fallback to the instance's font asset
                    var current = __instance.font;
                    if (current != null && !current.fallbackFontAssetTable.Contains(RuntimeCache.LoadedCyrillicFont))
                    {
                        try
                        {
                            if (current.fallbackFontAssetTable == null) current.fallbackFontAssetTable = new List<TMP_FontAsset>();
                            current.fallbackFontAssetTable.Add(RuntimeCache.LoadedCyrillicFont);
                            MelonLogger.Msg($"Added LoadedCyrillicFont as fallback to font '{current.name}' for object '{__instance.gameObject.name}'");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Failed to add fallback for '{__instance.gameObject.name}': {ex}");
                        }
                    }
                }

                // Removed material replacement logic to prevent NRE in TMP_SubMeshUI due to incompatible materials
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"ApplyCyrillicToText failed: {ex}");
            }
        }

        public static void ApplyCyrillicGlobally(TMP_FontAsset cyr)
        {
            if (cyr == null) return;
            try
            {
                var existingRaw = Resources.FindObjectsOfTypeAll(typeof(TMP_Text));
                int applied = 0;
                int alreadyCyrillic = 0;
                foreach (var o in existingRaw)
                {
                    var tt = o as TMP_Text;
                    if (tt == null) continue;
                    if (tt.font == cyr)
                    {
                        alreadyCyrillic++;
                        continue;
                    }
                    // Always replace to ensure consistent visuals
                    ApplyCyrillicToText(tt);
                    applied++;
                }
                MelonLogger.Msg($"Globally applied Cyrillic font to {applied} TMP_Text(s), {alreadyCyrillic} already had it, total {existingRaw.Length}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"ApplyCyrillicGlobally failed: {ex}");
            }
        }

        public static void ApplyCyrillicMaterialToAllFonts(Material cyrMat)
        {
            if (cyrMat == null) return;
            try
            {
                var allRaw = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
                int applied = 0;
                foreach (var o in allRaw)
                {
                    var fa = o as TMP_FontAsset;
                    if (fa == null) continue;
                    fa.material = cyrMat;
                    applied++;
                }
                MelonLogger.Msg($"Applied Cyrillic material to {applied} TMP_FontAsset(s)");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to apply material to fonts: {ex}");
            }
        }
    }

    // small runtime cache for loaded assets and material clones
    internal static class RuntimeCache
    {
        public static AssetBundle LoadedBundle;
        public static TMP_FontAsset LoadedCyrillicFont;
        public static Material LoadedCyrillicMaterial;
        public static Texture2D LoadedCyrillicAtlas;
        // cache original material -> cloned material
        public static Dictionary<Material, Material> MaterialCloneCache = new Dictionary<Material, Material>();
    }

    // TMP_Text.text setter
    [HarmonyPatch]
    internal static class Patch_TMP_set_text
    {
        static MethodBase TargetMethod()
        {
            var prop = typeof(TMP_Text).GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetSetMethod();
        }

        static bool Prefix(ref string value, TMP_Text __instance)
        {
            if (LWitWMod.Main.Translation == null) return true;
            if (string.IsNullOrEmpty(value)) return true;

            if (LWitWMod.Main.Translation.TryTranslate(value, out var translated))
            {
                value = translated;
            }

            // If the text contains Cyrillic and we have a loaded font, assign it and a compatible material
            if (FontHelpers.HasCyrillic(value) && RuntimeCache.LoadedCyrillicFont != null)
            {
                try
                {
                    FontHelpers.ApplyCyrillicToText(__instance);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error applying Cyrillic font/material: {ex}");
                }
            }

            return true;
        }
    }

    // LocalizationSetter.SetTextLocale patch to override font after game's localization
    [HarmonyPatch]
    internal static class Patch_LocalizationSetter_SetTextLocale
    {
        static MethodBase TargetMethod()
        {
            // Find the type by full name
            var type = AccessTools.TypeByName("SunnySideUp.LocalizationSetter");
            if (type == null)
            {
                MelonLogger.Warning("Could not find type 'SunnySideUp.LocalizationSetter'");
                return null;
            }
            // Find the method
            var method = AccessTools.Method(type, "SetTextLocale");
            if (method == null)
            {
                MelonLogger.Warning($"Could not find method 'SetTextLocale' in type '{type.FullName}'");
                return null;
            }
            MelonLogger.Msg($"Found SunnySideUp.LocalizationSetter.SetTextLocale: {method}");
            return method;
        }

        static void Postfix()
        {
            // After game's localization sets fonts, re-apply our Cyrillic font globally
            if (RuntimeCache.LoadedCyrillicFont != null)
            {
                try
                {
                    FontHelpers.ApplyCyrillicGlobally(RuntimeCache.LoadedCyrillicFont);
                    MelonLogger.Msg("Re-applied Cyrillic font after LocalizationSetter.SetTextLocale");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to re-apply Cyrillic font after localization: {ex}");
                }
            }
        }
    }

    // TMP_Text.font setter patch to prevent overriding our Cyrillic font
    [HarmonyPatch]
    internal static class Patch_TMP_Text_set_font
    {
        static MethodBase TargetMethod()
        {
            var prop = typeof(TMP_Text).GetProperty("font", BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetSetMethod();
        }

        static bool Prefix(ref TMP_FontAsset value, TMP_Text __instance)
        {
            // If trying to set a font that's not our Cyrillic, and we have Cyrillic loaded, force our font
            if (RuntimeCache.LoadedCyrillicFont != null && FontHelpers.IsFontAssetUsable(RuntimeCache.LoadedCyrillicFont) && value != RuntimeCache.LoadedCyrillicFont)
            {
                value = RuntimeCache.LoadedCyrillicFont;
                MelonLogger.Msg($"Forced Cyrillic font on TMP_Text '{__instance.gameObject.name}' instead of '{value?.name ?? "null"}'");
            }
            return true;
        }
    }

    // TMP_Text.fontSharedMaterial setter patch to prevent overriding our Cyrillic material
    [HarmonyPatch]
    internal static class Patch_TMP_Text_set_fontSharedMaterial
    {
        static MethodBase TargetMethod()
        {
            var prop = typeof(TMP_Text).GetProperty("fontSharedMaterial", BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetSetMethod();
        }

        static bool Prefix(ref Material value, TMP_Text __instance)
        {
            // If trying to set a material that's not our Cyrillic, and we have Cyrillic loaded, force our material
            if (RuntimeCache.LoadedCyrillicFont != null && value != RuntimeCache.LoadedCyrillicFont.material)
            {
                value = RuntimeCache.LoadedCyrillicFont.material;
                MelonLogger.Msg($"Forced Cyrillic material on TMP_Text '{__instance.gameObject.name}' instead of '{value?.name ?? "null"}'");
            }
            return true;
        }
    }
}
