using MelonLoader;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

[assembly: MelonInfo(typeof(LWitWMod.Main), "LWitWMod", "1.0.0", "YourName")]
[assembly: MelonGame("Akinori", "Little Witch in the Woods")]

namespace LWitWMod
{
    public class Main : MelonMod
    {
        private static Harmony HarmonyInstance;
        private static Dictionary<string, string> translations = new();

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("LWitWMod loaded!");

            // Load ru.json from Mods folder
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string ruJsonPath = Path.Combine(modPath, "ru.json");
            if (File.Exists(ruJsonPath))
            {
                string json = File.ReadAllText(ruJsonPath);
                translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                LoggerInstance.Msg($"Loaded {translations.Count} translations from ru.json");
            }
            else
            {
                LoggerInstance.Msg("ru.json not found in Mods folder");
            }

            // Initialize Harmony
            HarmonyInstance = new Harmony("LWitWMod");

            // TODO: Patch localization methods
            // Example: Patch(typeof(LocalizationManager).GetMethod("GetString"), new HarmonyMethod(typeof(Main).GetMethod("GetStringPostfix")));
        }

        // Example postfix patch for localization
        public static void GetStringPostfix(ref string __result, string key)
        {
            if (translations.TryGetValue(key, out string translated))
            {
                __result = translated;
            }
        }
    }
}
