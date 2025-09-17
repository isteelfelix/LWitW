# LWitWMod

This is a base project for a MelonLoader mod for a Unity game.

## Setup

1. Install MelonLoader for your Unity game.
2. Copy the MelonLoader.dll and UnityEngine.dll to your project references or update the .csproj file with correct paths.
3. Build the project to generate the DLL.
4. Place the generated DLL in the Mods folder of your game.

## Building

Use `dotnet build` to build the project.

## Notes

- Update the MelonGame attribute with the correct developer and game name.
- Add your mod code in Main.cs.
