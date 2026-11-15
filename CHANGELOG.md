# Changelog

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
