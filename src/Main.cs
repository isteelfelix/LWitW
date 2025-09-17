using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(LWitWMod.Main), "LWitWMod", "1.0.0", "YourName")]
[assembly: MelonGame("DeveloperName", "GameName")] // Replace with actual game details

namespace LWitWMod
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("LWitWMod loaded!");
        }

        // Add your mod logic here
    }
}
