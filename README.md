# ModMenu

A BepInEx mod for Megabonk that lets you change the settings of every installed BepInEx mod without leaving the game.

## Usage

Open the main menu or pause the game and press **Mods**, the new button next to Settings.

- The left column lists every loaded mod that has settings, plus BepInEx itself.
- Pick a mod to see its settings grouped by section, with the descriptions the mod author wrote.
- Each setting gets a control that fits its type: a toggle, a slider, a list to step through, a "Press a key…" button for hotkeys, or a text field.
- **Reset** next to a setting puts it back to its default.

Changes are saved to the mod's `.cfg` file and handed to the running mod immediately. Whether the mod picks them up right away depends on the mod: some only read their settings on startup, so **some settings may require a game restart to take effect**.

Invalid values (letters in a number, a number outside the allowed range, a value that is not in the list) are rejected and shown in red, and the file is not touched.

## Files…

Files in `BepInEx/config` that no running mod has open are listed under **Files…**, for example configs left behind by mods you removed.

- Files in the BepInEx config format get the same controls. Only the edited value is changed, comments are kept.
- Any other text file opens in a plain editor with a **Save** button.

These files are not loaded by any running mod, so changes take effect after a restart.

## Notes

- Mods that keep their settings in their own config object instead of the standard one (Useful Info UI does this) are found and edited live as well.
- While the window is open the game does not receive keyboard or controller input. Esc closes the window.
- The interface is in English. Setting names and descriptions are shown as their authors wrote them.

## Install

With a mod manager (r2modman, Gale): install ModMenu from the [Megabonk community on Thunderstore](https://thunderstore.io/c/megabonk/).

Manually: install [BepInExPack_IL2CPP](https://thunderstore.io/c/megabonk/p/BepInEx/BepInExPack_IL2CPP/), start the game once, then put `ModMenu.dll` into `BepInEx/plugins`.

## Building

You need the .NET SDK (8 or newer) and Megabonk with BepInEx that has been started at least once, so that `BepInEx/interop` exists.

```sh
dotnet build -c Release -p:GameDir=/path/to/Megabonk/
```

Instead of `-p:GameDir` you can put the path into `Local.props` (ignored by git):

```xml
<Project>
  <PropertyGroup>
    <GameDir>/path/to/Megabonk/</GameDir>
  </PropertyGroup>
</Project>
```

Without either, the default Steam path on Windows is used. The path must end with a slash. After a build the DLL is copied into the game's `BepInEx/plugins`.

The parts that do not need the game (config parsing, validation, finding configs) have tests:

```sh
dotnet run --project tests/ModMenu.Tests
```

To release, bump the version in `Plugin.cs` (`BepInPlugin`) and `thunderstore/manifest.json`, then run `./pack.sh`. The Thunderstore zip ends up in `dist/`.

## License

MIT
