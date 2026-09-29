# SprintImprovements

A Risk of Rain 2 (BepInEx) mod: sprint changes only when the player wants it to. Skills no longer turn sprint on when the player doesn't want to sprint, the sprint toggle survives stopping, strafing, skills and stuns (they only pause it), and there are Toggle, Hold and Always sprint modes.

- Store page: [revoreverse/SprintImprovements](https://thunderstore.io/c/riskofrain2/p/revoreverse/SprintImprovements/). Its source, [thunderstore/SprintImprovements/README.md](thunderstore/SprintImprovements/README.md), describes every feature and setting for players; this file is for development.
- [Modding guide](https://github.com/T-Jayay/DroneImprovements/blob/main/docs/MODDING_GUIDE.md) (in the DroneImprovements repo): how this mod, [DroneImprovements](https://github.com/T-Jayay/DroneImprovements) and [MarkAllSeen](https://github.com/T-Jayay/MarkAllSeen) are set up, built, tested and published.
- License: [the Unlicense](LICENSE) (public domain).

## Building

You need:
- the [.NET SDK](https://dotnet.microsoft.com/download) 8 or later;
- Risk of Rain 2;
- a mod manager profile (Thunderstore Mod Manager, r2modman, ...) with BepInExPack and Risk of Options installed. The build references BepInEx, Harmony and Risk of Options from it and deploys the plugin into it, so use a profile for testing. (Risk of Options is only needed to build; players can use the mod without it.)

```
dotnet build SprintImprovements.sln -c Release
```

`Directory.Build.props` says where the game and the profile are. If yours are elsewhere, pass these properties on the command line or set them as environment variables:

| Property | Default | Example |
|---|---|---|
| `GameDir` | `C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2` | `-p:GameDir="D:\SteamLibrary\steamapps\common\Risk of Rain 2"` |
| `ProfileDir` | Thunderstore Mod Manager's `Test` profile: `%APPDATA%\Thunderstore Mod Manager\DataFolder\RiskOfRain2\profiles\Test` | an r2modman profile named `Dev`: `-p:ProfileDir="C:\Users\<you>\AppData\Roaming\r2modmanPlus-local\RiskOfRain2\profiles\Dev"` |
| `DeployToProfile` | `true` | `-p:DeployToProfile=false` builds without deploying |

A path that doesn't match stops the build with an error naming the property to set. Each build copies `SprintImprovements.dll` and its PDB into `<ProfileDir>\BepInEx\plugins\SprintImprovements\`. Close the game before building: it keeps the plugin locked, and the deploy then fails with an error saying so. The build takes the version from `thunderstore/SprintImprovements/manifest.json` and embeds `thunderstore/SprintImprovements/icon.png` for the Risk of Options mod list.

## Releasing

1. Set the new version in `thunderstore/SprintImprovements/manifest.json` (`version_number`) and in `PluginVersion` in `src/SprintImprovements/SprintImprovementsPlugin.cs`, and list the changes under `## <version>` in `thunderstore/SprintImprovements/CHANGELOG.md`. An uploaded version can't be changed, so any change, even to the manifest alone, needs a new version.
2. Commit.
3. `python tools/package.py` (Python 3.8 or later) checks the package against Thunderstore's rules and the version against `PluginVersion` and the changelog, builds the mod and writes `dist/SprintImprovements-<version>.zip`. It refuses to package uncommitted changes or to overwrite an existing zip; `--force` skips both checks, for test builds only.
4. Upload the zip at https://thunderstore.io/package/create/ with the team **revoreverse** and the community Risk of Rain 2, and tick the **AI Generated** category.

The full checklist, including the first push to GitHub, is under Publishing in the [modding guide](https://github.com/T-Jayay/DroneImprovements/blob/main/docs/MODDING_GUIDE.md#publishing).

## How it works

- The logic is in `src/SprintImprovements/SprintGuard.cs`, whose class summary explains it; the hooks are in `SprintPatches.cs`.
- Client-side: it only affects your own character. It adds no content and no network messages, so it isn't on the game's network mod list; as a client it uses the game's own sprint command to tell the host when a blocked forced sprint ends (`ServerSprintSync.cs`).
- The patches are applied all or nothing (`SprintImprovementsPlugin.Patch`), because they only work together: if one fails after a game update, all of them are removed again and sprint works as in the base game. The exception is the `PollButtonInput` transpiler: if that method doesn't look as expected, the transpiler leaves its code unchanged and the other patches stay, so `SprintMode` and `KeepSprintToggled` stop working while `BlockForcedSprint` keeps working (the log says so).
- Risk of Options is optional (a soft dependency): `RiskOfOptionsCompat.cs` is only used when it is installed.

## Testing

Solo testing covers most of it. Before a release, also check:
- as a client in a real lobby: walk into a dash, and check on the host that you aren't shown sprinting afterwards;
- with the auto-sprint of RTAutoSprintEx turned on;
- without Risk of Options installed.

## Icon

`tools/make_icon.py` (needs Pillow) draws the store icon `thunderstore/SprintImprovements/icon.png`. Its helpers are copied from DroneImprovements' `tools/make_icons.py`; keep the copies identical. With Pillow 12.3.0 it reproduces the committed PNG byte for byte.
