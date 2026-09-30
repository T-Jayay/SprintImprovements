# Changelog

## 1.1.0

- Reworked to go through the sprint key: to resume or stop sprint, the mod now presses sprint for you and the game does the rest, as for a real key press. It still stops skills from switching sprint on while you don't want to sprint.
- Which skills pause sprint is now read from the game's skill list at startup, so skills from other mods count too.
- Toggle mode: pressing sprint while paused by a stun or a skill that takes over your movement (a dash, Whirlwind) now turns the toggle off; before, the press was lost. With `BlockForcedSprint` off, a skill's sprint now turns your toggle on, as in the normal game.
- As a client, after a skill's forced sprint was blocked, the mod tells the host "not sprinting" once when the skill ends, instead of twice on a timer (most of these skills also switch sprint on in the host's copy of your character).
- Settings: `Enabled` is a plain on/off switch and no longer turns the other settings on or off. `KeepSprintToggled` is gone: Toggle mode always keeps your toggle (its old line in the config file does nothing now).
- Needs HookGenPatcher (mod managers install it automatically). The code follows the Risk of Rain 2 modding community's conventions, and errors show up in the log instead of being hidden.

## 1.0.0

- Initial release.
- Skills no longer switch sprint on when you don't want to sprint (Mercenary's dashes, Commando's Tactical Dive, Huntress's Blink, MUL-T's Transport Mode, ...).
- Sprint stays toggled until you press sprint again, also on the next stage and after a revive: stopping, strafing, skills and stuns only pause it.
- Sprint modes: Toggle (default), Hold or Always (hold the key to walk).
- In-game settings with Risk of Options (optional). The master switch and the other settings turn each other on and off.
