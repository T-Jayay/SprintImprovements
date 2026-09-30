# Sprint Improvements

Sprint changes only when you want it to: skills no longer switch it on while you walk, your sprint toggle survives stopping, strafing, skills and stuns, and you can choose between toggle, hold and always-on sprint.

## Features
- **No forced sprint.** In the normal game some skills switch sprint on even when you were walking. Mercenary's dash, for example, then goes further and leaves you sprinting afterwards. With this mod, a dash started while walking keeps your walking speed and you keep walking.
- **Sprint stays toggled.** Once you toggle sprint on, it stays on until you press sprint again, also on the next stage and after a revive. Stopping, moving sideways or backwards, skills that cancel sprint (Mercenary's Whirlwind, Commando's pistols, ...) and stuns only pause it; it comes back by itself afterwards.
- **Sprint modes.** Keep the toggle, or switch to **Hold** (sprint while you hold the key) or **Always** (always sprint when you can; hold the key to walk).

Works for every survivor, also modded ones: which skills pause sprint is read from the game's skill list.

## How it works
The mod presses the sprint key for you when sprint should start or stop, and the game does the rest exactly as if you had pressed it. The one thing it overrides is a skill switching sprint on while you don't want to sprint.

## Details
With the default settings:
- Skills that switch sprint on include Mercenary's Blinding Assault, Focused Assault and Eviscerate, Commando's Tactical Dive and Tactical Slide, Huntress's Blink and Phase Blink, Acrid's leaps, Artificer's Ion Surge, Bandit's Smoke Bomb, MUL-T's Transport Mode, Void Fiend's Trespass, CHEF's Roll and Oil Spill, Loader's gauntlets, False Son's Step of the Brothers, Seeker's Sojourn and Reprieve, Drifter's Repossess, Junk Cube and Tornado Slam, and Operator's Ascent Protocol. These skills no longer switch sprint on while you walk. When you want to sprint, they work as in the normal game: in Toggle mode your toggle is on (also while sprint is paused) or you turn it on during the skill; in Hold mode you hold the key; in Always mode you don't.
- After a skill or stun, sprint comes back 0.15 s after it has finished and you've let go of the skill's button. Holding the button keeps sprint paused only while it can use the skill again, so holding fire doesn't flicker between shots. Sprint never comes back in the middle of a skill that sprinting would interrupt.
- When you stop, or move sideways or backwards from where you aim, you walk as in the normal game, but sprint comes back as soon as you move forward again.
- In Toggle mode you can press sprint at any time, also while you stand still or use a skill:
  - Toggle off: it turns on. You sprint straight away if the normal game would let you (moving forward), which can cancel a skill such as Railgunner's scope as usual; otherwise sprint starts as soon as it can.
  - Sprinting: the toggle turns off and sprint stops, or right after the skill if a skill such as a dash keeps you sprinting.
  - Toggle on but sprint paused: moving forward during a skill such as a scope or firing, the press sprints straight away (which can cancel the skill, as in the normal game) and the toggle stays on. Standing still, moving sideways or backwards, or during a stun or a skill that takes over your movement (a dash, Whirlwind), it turns the toggle off.
- In Hold mode, pressing the key sprints straight away as in the normal game, so it can cancel a skill the same way. In Always mode sprint never cancels a skill: it comes back once the skill has ended.

## Side effects
- Skills that speed up while you sprint use your walking speed if you weren't sprinting: Mercenary's dashes, MUL-T's Transport Mode, CHEF's Roll and Drifter's Tornado Slam are shorter or slower than in the normal game, and CHEF's Oil Spill drops its oil less often. Sprint first to get the full effect.
- Sprint bonuses (Energy Drink, the sprint field of view, ...) don't apply during those skills unless you sprint.

## Auto-sprint mods
Don't combine this mod with an auto-sprint mod such as AutoSprint or RTAutoSprintEx: both press sprint for you and would work against each other. This mod's Always mode is an auto-sprint; otherwise turn this mod off (`Enabled`).

## Settings
In `BepInEx/config/revor.SprintImprovements.cfg`, or in game under Settings → Mod Options → **SprintImprovements** when [Risk of Options](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) is installed.
- `Enabled` (default `true`): off gives you the normal game's sprint.
- `SprintMode` (default `Toggle`): `Toggle`, `Hold` or `Always`.
- `BlockForcedSprint` (default `true`): skills don't switch sprint on when you don't want to sprint. Off: they do, and in Toggle mode that turns your toggle on, as in the normal game.

## Multiplayer
Client-side: it only changes your own character, with your own settings, so only the players who want it need to install it; the host doesn't. Most of the skills above also switch sprint on in the host's copy of your character, which a mod on your side can't prevent: while such a skill runs, the host and the other players may see you sprint. Afterwards the mod tells the host once that you aren't sprinting, through the game's own sprint message.
