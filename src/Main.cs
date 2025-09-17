using MelonLoader;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(LWitWMod.Main), "LWitWMod", "1.0.0", "Chieftain51")]
[assembly: MelonGame("SunnySideUp", "Little Witch In The Woods")]

namespace LWitWMod
{
    public class Main : MelonMod
    {
        internal static Translation Translation;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("LWitWMod loaded!");

            // Load ru.json from Mods folder
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string ruJsonPath = Path.Combine(modPath, "ru.json");
            Translation = new Translation(ruJsonPath);

            LoggerInstance.Msg($"Loaded translations: {Translation.Count} entries");

            // Harmony patches will be applied automatically via [HarmonyPatch]
        }
    }

    // TMP_Text.SetText(string)
    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), new System.Type[] { typeof(string) })]
    internal static class Patch_TMP_SetText_1
    {
        static void Prefix([HarmonyArgument("sourceText")] ref string txt)
        {
            if (Main.Translation.TryTranslate(txt, out var tr))
                txt = tr;
        }
    }

    // TMP_Text.SetText(string, bool)
    [HarmonyPatch]
    internal static class Patch_TMP_SetText_2
    {
        static MethodBase TargetMethod()
        {
            return typeof(TMP_Text).GetMethod(
                "SetText",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new System.Type[] { typeof(string), typeof(bool) },
                null
            );
        }

        static void Prefix([HarmonyArgument("sourceText")] ref string txt)
        {
            if (Main.Translation.TryTranslate(txt, out var tr))
                txt = tr;
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

        static void Prefix(ref string value)
        {
            if (Main.Translation.TryTranslate(value, out var tr))
                value = tr;
        }
    }

    // UnityEngine.UI.Text.text setter
    [HarmonyPatch]
    internal static class Patch_UIText_set_text
    {
        static MethodBase TargetMethod()
        {
            var prop = typeof(Text).GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetSetMethod();
        }

        static void Prefix(ref string value)
        {
            if (Main.Translation.TryTranslate(value, out var tr))
                value = tr;
        }
    }
}
