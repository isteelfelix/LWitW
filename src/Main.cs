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

[assembly: MelonInfo(typeof(LWitWMod.Main), "LWitWMod", "1.0.0", "Chieftain51")]
[assembly: MelonGame("SunnySideUp", "Little Witch In The Woods")]

namespace LWitWMod
{
    public class Main : MelonMod
    {
        internal static Translation Translation;
        public static TMP_FontAsset _cyrillicFontAsset;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("LWitWMod loaded!");

            // Load Cyrillic font from AssetBundle
            try
            {
                string bundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "font", "cyrillicfont.bundle");
                if (File.Exists(bundlePath))
                {
                    LoggerInstance.Msg($"Loading AssetBundle from: {bundlePath}");
                    AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                    
                    if (bundle != null)
                    {
                        LoggerInstance.Msg("AssetBundle loaded successfully");
                        
                        // First try to load TMP_FontAsset
                        TMP_FontAsset bundleFontAsset = bundle.LoadAsset<TMP_FontAsset>("NotoSans-Regular SDF");
                        if (bundleFontAsset == null)
                        {
                            string[] possibleNames = { "NotoSans-Regular", "Noto Sans Regular", "NotoSansRegular" };
                            foreach (string name in possibleNames)
                            {
                                bundleFontAsset = bundle.LoadAsset<TMP_FontAsset>(name);
                                if (bundleFontAsset != null) break;
                            }
                        }
                        
                        if (bundleFontAsset != null)
                        {
                            LoggerInstance.Msg($"TMP_FontAsset loaded: {bundleFontAsset.name}");
                            LoggerInstance.Msg($"Atlas: {(bundleFontAsset.atlas != null ? "present" : "null")}");
                            LoggerInstance.Msg($"Material: {(bundleFontAsset.material != null ? "present" : "null")}");
                            LoggerInstance.Msg($"Font: {(bundleFontAsset.sourceFontFile != null ? "present" : "null")}");
                            
                            if (bundleFontAsset.atlas != null)
                            {
                                LoggerInstance.Msg($"TMP_FontAsset with atlas loaded: {bundleFontAsset.name}");
                                _cyrillicFontAsset = bundleFontAsset;
                            }
                            else
                            {
                                LoggerInstance.Msg("TMP_FontAsset has no atlas, trying to load atlas manually...");
                                
                                // Try to load atlas manually
                                Texture2D atlas = bundle.LoadAsset<Texture2D>("NotoSans-Regular SDF Atlas");
                                if (atlas == null)
                                {
                                    string[] atlasNames = { "NotoSans-Regular Atlas", "Noto Sans Regular Atlas", "Font Atlas" };
                                    foreach (string name in atlasNames)
                                    {
                                        atlas = bundle.LoadAsset<Texture2D>(name);
                                        if (atlas != null) break;
                                    }
                                }
                                
                                if (atlas != null)
                                {
                                    bundleFontAsset.atlas = atlas;
                                    LoggerInstance.Msg("Atlas loaded and assigned manually");
                                    _cyrillicFontAsset = bundleFontAsset;
                                }
                                else
                                {
                                    LoggerInstance.Msg("Atlas not found, trying to create from Font...");
                                    
                                    // Load Font from bundle
                                Font font = bundle.LoadAsset<Font>("NotoSans-Regular");
                                if (font == null)
                                {
                                    string[] fontNames = { "Noto Sans Regular", "NotoSansRegular", "NotoSans-Regular.ttf" };
                                    foreach (string name in fontNames)
                                    {
                                        font = bundle.LoadAsset<Font>(name);
                                        if (font != null) break;
                                    }
                                }
                                
                                if (font != null)
                                {
                                    LoggerInstance.Msg($"Creating TMP_FontAsset from Font: {font.name}");
                                    
                                    // Create TMP_FontAsset from static font
                                    _cyrillicFontAsset = TMP_FontAsset.CreateFontAsset(font, 16, 4, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                                    
                                    if (_cyrillicFontAsset != null)
                                    {
                                        _cyrillicFontAsset.name = "NotoSans-Cyrillic-Runtime";
                                        LoggerInstance.Msg("TMP_FontAsset created from Font successfully!");
                                    }
                                    else
                                    {
                                        LoggerInstance.Warning("Failed to create TMP_FontAsset from Font");
                                    }
                                }
                                else
                                {
                                    LoggerInstance.Warning("Neither TMP_FontAsset with atlas nor Font found in bundle");
                                }
                            }
                        }
                        }
                        
                        // Don't unload bundle immediately, as we need the font
                        // bundle.Unload(false); // Keep assets loaded
                    }
                    else
                    {
                        LoggerInstance.Warning("Failed to load AssetBundle");
                    }
                }
                else
                {
                    LoggerInstance.Warning($"AssetBundle not found: {bundlePath}");
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Error($"Error loading AssetBundle: {e.Message}");
            }

            // Load ru.json from Mods folder
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string ruJsonPath = Path.Combine(modPath, "ru.json");
            Translation = new Translation(ruJsonPath);

            LoggerInstance.Msg($"Loaded translations: {Translation.Count} entries");

            // Harmony patches will be applied automatically via [HarmonyPatch]
        }
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

        static void Prefix(ref string value, TMP_Text __instance)
        {
            if (Main.Translation.TryTranslate(value, out var tr))
                value = tr;

            // Replace font with Cyrillic support
            if (Main._cyrillicFontAsset != null && __instance.font != Main._cyrillicFontAsset)
            {
                __instance.font = Main._cyrillicFontAsset;
            }
        }
    }


}
