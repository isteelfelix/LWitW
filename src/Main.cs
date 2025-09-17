using MelonLoader;
using UnityEngine;
using System.Reflection;
using System.IO;
using System.Linq;

[assembly: MelonInfo(typeof(LWitWMod.Main), "LWitWMod", "1.0.0", "YourName")]
[assembly: MelonGame("Akinori", "Little Witch in the Woods")] // Replace with actual game details

namespace LWitWMod
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("LWitWMod loaded!");

            // Load embedded JSON resources
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames().Where(name => name.Contains("res.") && name.EndsWith(".json"));
            foreach (var resourceName in resourceNames)
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var json = reader.ReadToEnd();
                            LoggerInstance.Msg($"Loaded resource: {resourceName}, Length: {json.Length}");
                            // Here you can parse the JSON and apply modifications to the game
                        }
                    }
                }
            }
        }

        // Add your mod logic here
    }
}
