# Changelog

## v1.6.0 - 2026-11-23

- Updated compatibility information for Half Sword 0.7 Hotfix 4, Steam build
  26820056, and checked fighter and match bindings against the running game.
- Reworded options to explain what they do, including renaming Motor Power to
  **Joint Drive Force** and clarifying its experimental physics-only role.
- Added descriptive tooltips and layout checks for option labels.

Validation: zero-warning Release build, UI and save-repair safety self-tests,
read-only player, hostile and match bindings, and save-repair verification on
a copy of the Hotfix 4 save. No gameplay writes or combat outcomes were tested.

## v1.5.0 - 2026-11-20

- Added **REPAIR SAVE BLOAT**, an offline, confirmed repair that empties only
  the top-level `Dead Opponents` array in `GameProgress.sav`.
- Each repair creates and verifies a fresh original backup, preserves all
  unrelated bytes, atomically replaces the save, and verifies the installed
  result. Empty arrays are left alone; unsupported or damaged layouts are refused.
- Refuses repair while the game is running and rechecks before replacement.
  Listening is paused without losing staged trainer options.
- Added focused safety tests and `--verify-save-repair`, which validates the
  repair on a temporary copy without replacing the real save.
- Updated the supported game version to Half Sword 0.7.0.3 (Steam build
  26737211) and verified the live runtime bindings against the installed build.

Validation: zero-warning Release build, staging/safety self-tests, and a real
save copy reduced from 95,753,097 to 2,438,367 bytes (260 dead opponents removed),
with byte preservation and original/backup hashes verified.

## v1.3.0 - 2026-11-15

- Added a separate, menu-only career physique editor. It updates the next-spawn
  character source rather than changing a running fighter's morphs and physics
  independently. Applied values may persist through normal saves.
- Renamed the old Muscle Power control to Motor Power and clarified that it
  edits a physics-joint drive parameter, not visible muscle size.
- Added saved-physique readback, conservative input limits, transition
  cancellation, and menu-only availability checks.
- Expanded read-only diagnostics for physique and physics-motor calculations.

Validation: zero-warning Release build and focused self-tests. A live career
menu edit to 0.5 produced a fresh fighter with Muscle Rate 0.5 and mass scale
1.125. The player confirmed normal appearance and no rubberbanding in that
test. The physique card was visually checked at normal and minimum sizes.

## v1.2.1 - 2026-11-15

- Moved **UNSTICK MATCH** out of the connection controls and beside the live
  vital signs, with a distinct warning treatment for the irreversible action.
- Improved the button's spacing and added layout checks for default and
  minimum window sizes. It remains available only with an active fighter.
- Disabled the match action immediately when a scene transition starts or the
  game exits.

## v1.2.0 - 2026-11-15

- Fixed Easy Win softlocks by clearing invulnerability on hostile fighters
  before clamping their health, including fighters already down.
- Added the combat-only **UNSTICK MATCH** action. It resets stuck death and
  vertex-paint flags, briefly restores hostile fighters so the game can retry
  their death transition, then marks the active match won and enemies defeated.
- Validates the active game-mode, fighter, and AI properties and Boolean
  layouts before the one-shot action writes anything.
- Discards a queued Force Win if the player or game changes during a transition.
- Updated the UI and documentation to describe the irreversible match-state
  changes and the effect on NPC invulnerability.

Validation: zero-warning Release build, `--self-test`, and read-only `--verify`
against the installed game. The Force Win action itself was not triggered during
validation, to avoid changing the active match.

## v1.1.0 - 2026-11-15

- Made gameplay options selectable before a fight and retained them through
  automatic game launch and match reconnection.
- Added No Exhaustion, Ideal Temperature, and a configurable Reputation on Rank
  minimum.
- Enabled funds and rank-reputation editing outside combat.
- Added a distinct multi-resolution application icon.
- Fixed normal game exit and runtime teardown handling.
