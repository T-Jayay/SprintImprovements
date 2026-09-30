# SprintImprovements

A Risk of Rain 2 (BepInEx) mod: sprint changes only when the player wants it to. Skills no longer turn sprint on when the player doesn't want to sprint, the sprint toggle survives stopping, strafing, skills and stuns (they only pause it), and there are Toggle, Hold and Always sprint modes.

- Store page: [revoreverse/SprintImprovements](https://thunderstore.io/c/riskofrain2/p/revoreverse/SprintImprovements/). Its source, [thunderstore/SprintImprovements/README.md](thunderstore/SprintImprovements/README.md), describes every feature and setting for players; this file is for development.
- [Modding guide](https://github.com/T-Jayay/DroneImprovements/blob/main/docs/MODDING_GUIDE.md) (in the DroneImprovements repo): how this mod, [DroneImprovements](https://github.com/T-Jayay/DroneImprovements) and [MarkAllSeen](https://github.com/T-Jayay/MarkAllSeen) are set up, built, tested and published.
- License: [the Unlicense](LICENSE) (public domain).

## Building

You need the [.NET SDK](https://dotnet.microsoft.com/download) 8 or later. The game, BepInEx and Risk of Options are referenced from NuGet (the game's publicized reference assemblies, `RiskOfRain2.GameLibs`, come from the [BepInEx feed](https://nuget.bepinex.dev/)), so the first build needs internet access but not the game.

```
dotnet build SprintImprovements.sln -c Release
```

Each build copies `SprintImprovements.dll` and its PDB into a mod manager profile for testing, as `<ProfileDir>\BepInEx\plugins\SprintImprovements\`, if that profile exists. The test profile needs BepInExPack and HookGenPatcher, and Risk of Options for the in-game settings (players can use the mod without it). Set these properties on the command line or as environment variables:

| Property | Default | Example |
|---|---|---|
| `ProfileDir` | Thunderstore Mod Manager's `Test` profile: `%APPDATA%\Thunderstore Mod Manager\DataFolder\RiskOfRain2\profiles\Test` | an r2modman profile named `Dev`: `-p:ProfileDir="C:\Users\<you>\AppData\Roaming\r2modmanPlus-local\RiskOfRain2\profiles\Dev"` |
| `DeployToProfile` | `true` | `-p:DeployToProfile=false` builds without deploying |

Close the game before building: it keeps the plugin locked, so the copy fails. The build takes the version from `thunderstore/SprintImprovements/manifest.json` and embeds `thunderstore/SprintImprovements/icon.png` for the Risk of Options mod list.

## Releasing

1. Set the new version in `thunderstore/SprintImprovements/manifest.json` (`version_number`) and in `PluginVersion` in `src/SprintImprovements/SprintImprovementsPlugin.cs`, and list the changes under `## <version>` in `thunderstore/SprintImprovements/CHANGELOG.md`. An uploaded version can't be changed, so any change, even to the manifest alone, needs a new version.
2. Commit.
3. `python tools/package.py` (Python 3.8 or later) checks the package against Thunderstore's rules and the version against `PluginVersion` and the changelog, builds the mod and writes `dist/SprintImprovements-<version>.zip`. It refuses to package uncommitted changes or to overwrite an existing zip; `--force` skips both checks, for test builds only.
4. Upload the zip at https://thunderstore.io/package/create/ with the team **revoreverse** and the community Risk of Rain 2, and tick the **AI Generated** category.

The full checklist, including the first push to GitHub, is under Publishing in the [modding guide](https://github.com/T-Jayay/DroneImprovements/blob/main/docs/MODDING_GUIDE.md#publishing).

## How it works

- `SprintHooks.cs` has the two hooks: an IL hook in `PlayerCharacterMasterController.PollButtonInput` right after it reads `CharacterBody.isSprinting` (if a game update changes that code, `TryGotoNext` fails, the log says so and the mod does nothing, as the R2Wiki's IL Hook page recommends), and a MonoMod `Hook` on the `isSprinting` setter (MMHOOK has no hooks for property accessors).
- `SprintGuard.cs` (its class summary explains the design) keeps what each player wants and presses the sprint key for them (`sprintInputPressReceived`) when sprint should start or stop. The game's own input path then changes sprint and tells the server, as for a real key press; this is how auto-sprint mods such as [AutoSprint](https://github.com/goldenguy00/AutoSprint) work too. The only override is BlockForcedSprint: the setter hook skips a skill's write of "sprinting" while the player doesn't want to sprint and the sprint input is up. Most of those skills also force sprint on the server's copy of a client's body (`SkillDef.forceSprintDuringState` runs on every copy), which a client-side mod can't stop, so `ServerSprintSync.cs` sends the game's own `CmdUpdateSprint(false)` once afterwards (its class summary explains why that is needed and why once is enough).
- `SprintPause.cs` decides when sprint is held off; `SprintStates.cs` reads the sprint-sensitive entity states from the skill catalog at startup (`[SystemInitializer(typeof(SkillCatalog))]`), plus a short list of states that aren't a skill's activation state.
- Client-side: it only affects your own character. It adds no content and no network messages, so it isn't on the game's network mod list.
- The IL hook's types come from NuGet `MMHOOK.RoR2` (compile only); players get `MMHOOK_RoR2.dll` from HookGenPatcher, a manifest dependency. The setter hook is MonoMod's `Hook`, which ships with BepInEx.
- Risk of Options is optional (a soft dependency): `RiskOfOptionsCompat.cs` is only used when it is installed.

## Testing

Solo testing covers most of it. Before a release, also check:
- as a client in a real lobby: use a dash while walking, and check on the host that you aren't shown sprinting afterwards;
- with modded survivors, whose skills are picked up from the skill catalog;
- without Risk of Options installed.

## Icon

`tools/make_icon.py` (needs Pillow) draws the store icon `thunderstore/SprintImprovements/icon.png`. Its helpers are copied from DroneImprovements' `tools/make_icons.py`; keep the copies identical. With Pillow 12.3.0 it reproduces the committed PNG byte for byte.
