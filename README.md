# Half Sword Trainer - The Fool's Favour

A medieval jester-themed standalone trainer for **Half Sword 0.7 Hotfix 4**
(Steam build **26820056**). Trainer version **1.6.0**.

See [CHANGELOG.md](./CHANGELOG.md) for version history.

## Download

Download the Windows x64 ZIP from
[Releases](https://github.com/AdmiralAwsum/half-sword-trainer/releases/latest),
extract **all** its files to a writable folder, and run `HalfSwordTrainer.exe`.
Do not run it directly inside the ZIP.

Windows x64, .NET 10 Desktop Runtime. No Cheat Engine, DLL injection, loader,
game restart, executable patching, or third-party trainer downloads.
Install the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0)
if Windows prompts for it. To compile from source, install the .NET 10 SDK.

## Run

The **Fool's Favour** interface uses the supplied jester portrait, parchment
headings, gold/green switches, and live health, stamina, and consciousness
cards and an original broken-sword/fool's-crown shield icon. The trainer starts
listening automatically, even before the game is launched. **DISARM ALL** stops
listening, clears all switches, and restores reversible overrides.
**LINK TO GAME** resumes listening without clearing your selected options.
The combat-only **UNSTICK MATCH** action sits beside the live vital signs.
The connection badge changes to **BETWEEN BOUTS** during loading and
**WAITING FOR GAME** when the game is closed.
Control cards scroll on shorter windows; standard keyboard focus and checkbox
accessibility are retained. Hover over switches for more detail about their
effects and limitations. The portrait is embedded, so no image files need
to be installed separately.

Open `HalfSwordTrainer.exe` from an extracted release (or
`dist\current\HalfSwordTrainer.exe` after publishing). Every feature starts OFF, but all
switches and numeric inputs are available immediately. Select combat features
before entering a fight: they activate once the local player possesses a
validated fighter. Funds and rank reputation work in menus once the game
session is available; game speed works wherever an active world is available.
Joint drive force is fighter-specific and waits for possession. Career body build is a
separate, menu-only edit used by the next character spawn.
Run at the same elevation as the game.
Only one game instance may be running.

* **Invulnerability**: continuously enables the player's built-in invulnerability.
* **Refill Health**: replenishes general and eleven named body/crush health
  values to 100 and clears bleeding. Cannot reattach severed limbs or resurrect
  an already dead character. Use before taking damage.
* **Refill Stamina**: replenishes stamina to 100.
* **Stay Conscious**: replenishes consciousness, its cap, and leg
  consciousness to 100.
* **Clear Groin / Liver Pain**: clears both pain values.
* **No Exhaustion**: holds exhaustion at 0 (fully rested).
* **Normal Temperature**: holds temperature at 1, the character class's default
  and neutral point in the game's temperature calculations. Does not change
  equipment heat retention.
* **Weaken Enemies**: holds living hostile NPCs in the player's level at no more than
  1 health/consciousness and 0 stamina. Finish them normally. Does not grant a
  victory/reward directly. Excludes the possessed player and same-team allies;
  team 0 uses the game's free-for-all convention. Hostile NPC invulnerability
  is cleared while this option is active so defeated fighters can complete
  their normal death transition; NPC changes are not restored.
* **UNSTICK MATCH / Force Win**: a one-shot combat-only action. Clears stuck
  death/vertex-paint flags, briefly restores hostile fighters so the game can
  re-run their death transition, then marks the active match won and all
  enemies dead. This directly changes current match state and cannot be undone;
  it does not promise career rewards or tournament progression. It requires a
  validated active fighter and targets only characters and AI controllers in
  the current level.
* **Game Speed**: check to hold global time dilation at 0.10–5.00x. Large values
  can destabilize physics.
* **Joint Drive Force** (formerly Motor Power): an experimental override of the
  game's raw `Muscle Power` physics-joint drive parameter at 0–10,000. It
  affects the force used by physics joints, not muscle size or general character
  strength. Its gameplay effect is not fully understood. The observed default is
  **35**. Zero or extreme values may destabilize physics; leave this OFF unless
  intentionally experimenting. Disabling restores the previous raw value when
  possible, but cannot undo existing simulation motion or cached game state.
* **Career Body Build**: a separate one-shot body-build editor with a conservative
  0–1 range: 0 lean, 0.5 the character default, 1 muscular. Leave the fight, apply
  **SET** in a career menu, then spawn into a new match. The readback shows the
  saved career value even when an existing character is outside the editor's
  range; simply viewing it never clamps that value. This changes only
  `Player Character.Weight` in the game instance, not the current fighter's
  morphs, mass, or joints. The game rebuilds those together on the next spawn.
  Body mass and strength-related calculations can change as well as appearance.
  Free Mode is excluded. It is not a continuous hold, and normal game saves and
  subsequent character growth can persist or change it. DISARM ALL does not
  undo an already applied career physique; a save backup is made before writing.
* **Funds**: **SET** writes the live game-instance funds once.
  The UI may update on reopening the relevant menu. Normal game saves may
  persist the change; the trainer does not force a save.
* **Minimum Rank Reputation**: while checked, raises the current rank's reputation
  to at least the entered value; never lowers reputation you already earned.
  The default **1,500** meets the reputation comparisons found in this build's
  tournament generator (50/100/200/300/400/500/750/1,000/1,500). This removes
  the reputation bottleneck, not every event-generation condition. Reopen or
  refresh events normally; playing another match/advancing a day may be needed.
  Does not force a tournament to spawn, reset the daily tournament flag, change
  rank, or grant victories. Training/free modes may not use career progression.

**STOP / Restore**, unchecking a feature, or closing the window stops holding
values. Invulnerability, speed, and joint drive force are restored if their objects
are still valid and the game has not independently changed those values.
Health/stamina replenishment, fitness changes, NPC injuries, funds, and earned
rank reputation and applied career physique are not undone. These can persist
through normal saves.

The trainer checks Unreal object-array identity and serial numbers before
writes. A respawn, scene transition, or object collection pauses writes and
automatically reacquires the local player's new controller, pawn, world, and
game instance. Checked features and all numeric selections are retained. A
session without a fighter shows **OPTIONS ARMED**, with unavailable combat
readings displayed as `--`; menu-safe features remain usable. During loading,
writes pause until validated bindings are available;
no repeated error dialogs are shown for normal loading. The local-player
back-reference prevents selecting an old controller retained by the game.
Queued one-shot funds and physique changes are cancelled on transitions; apply
them again after reconnection if needed.

Restoration skips unloaded objects rather than writing to their old addresses;
these skips and reconnections are logged. Still-live overrides are restored.
Normal game exit is not an error: writes stop, the exited process is disposed,
and the trainer waits for the next launch without a dialog. Selected options
resume after relaunch. Permission failures, invalid layouts, and other real
errors still stop the session with an explicit error. A newly launched game
gets up to 30 seconds to initialize before a missing runtime layout is reported
as unsupported. DISARM ALL cancels automatic reconnection.
If the same previously validated process destroys its Unreal globals before
Windows reports process exit, that is treated as an unloading runtime, not an
unsupported game update. Recovery identity includes process start time as well
as PID so a different game launch must validate its layout again.

Values are replenished externally about every 15 ms, not by intercepting game
logic. Damage/drain can still occur between updates. Use invulnerability
alongside health for damage prevention; no feature reverses pre-existing death
or dismemberment.

## Saves and recovery

Before the first write of each listening session (and after a game restart), all files under
`%LOCALAPPDATA%\HalfSwordUE5\Saved\SaveGames` are copied to the executable's
`backups\<timestamp>` folder. If copying fails, no write is made. This is a
best-effort file copy while the game is running, not an atomic save snapshot.
To restore, close the game and copy the desired backup's contents back to the
SaveGames folder. Keep a separate save backup before experimenting.

### Repair recurring save bloat

The **SAVE MAINTENANCE / REPAIR SAVE BLOAT** button is available without a game
connection. Use it when `GameProgress.sav` has grown because the game's
`Dead Opponents` array stores full defeated-character records, including large
vertex-paint data:

1. Exit Half Sword completely and let Steam Cloud finish syncing.
2. Click **REPAIR SAVE BLOAT**. The trainer pauses listening without clearing
   your staged options and shows the opponent count and expected file size.
3. Confirm removal of the stored dead-opponent history. Keep the game closed
   and do not start a cloud sync or another save editor during the repair.
4. Wait for the verified result and backup location, then start the game.
   **LINK TO GAME** resumes your staged trainer options.

Each repair makes a fresh, SHA-256-verified copy of the original in
`backups\save-repair-<timestamp>-<unique ID>\GameProgress.sav` beside the trainer.
The repair sets only the top-level `Dead Opponents` array to empty and adjusts
its serialized size. Every byte outside that array's payload and size field is
compared with the original before atomic replacement; the installed file is
then read back and verified. Funds, rank, inventory, the player character, and
other save properties are not rewritten. If backup or preparation fails, the
original is not replaced. An already-empty array is a no-op.

This removes dead-opponent history, not just its paint blobs. Game mechanics
that use those records may be affected. It does not stop the game accumulating
new entries, so repeat the manual repair if bloat returns. Unsupported,
ambiguous, or truncated save layouts are refused rather than guessed. The
repair is for the verified UE5 GVAS format used by the supported game build,
not a general save-file recovery tool.

If needed, close the game and restore `GameProgress.sav` from the exact backup
shown in the repair result. Other save files are never edited by this action.
Steam Cloud can restore a different copy; resolve cloud conflicts deliberately
instead of assuming the repaired file will always win.

Errors appear in a dialog and in `trainer-errors.log` next to the executable.
Settings are not persisted across trainer app launches. DISARM ALL clears
switches, not numeric inputs. Automatic match reconnections and game restarts
preserve all selections; match reconnections reuse the session's save backup.

## Build and verification

Build outputs go in `bin` and `obj`; published builds go in `dist`. Local
backup and log directories are ignored by Git and must not be included in releases.

```powershell
dotnet build .\HalfSwordTrainer.csproj -c Release
dotnet publish .\HalfSwordTrainer.csproj -c Release -o .\dist\current
Start-Process .\dist\current\HalfSwordTrainer.exe -ArgumentList '--self-test' -Wait
Get-Content .\dist\current\verification.txt
Start-Process .\dist\current\HalfSwordTrainer.exe -ArgumentList '--verify' -Wait
Get-Content .\dist\current\verification.txt
```

`--self-test` checks option text layout, pre-game selection, linking/lost-session retention, disarm,
reputation input/floor behavior, physique limits and nested-property bounds,
menu-only availability, and the match-action layout without accessing the game.
It also tests save-repair byte preservation, fresh backups, no-op behavior,
malformed/unsupported saves, and aborts on backup failure or game launch using
temporary fixtures; it never edits your save.
`--verify-save-repair` repairs a temporary copy of your current `GameProgress.sav`,
verifies the resulting size and backup, and checks that the original file's
SHA-256 is unchanged. It does not replace the real save or require a running game.
`--verify` validates live bindings, NPC targeting, and Force Win property
bindings without writing, and reports the career physique source plus the
spawned fighter's physique and mass scaling.
`--test-writes` briefly tests player stats, invulnerability, funds, joint
drive force, fitness, and speed with immediate readback, then restores and verifies
original values. Without a possessed fighter, it tests menu funds and rank
reputation writes with restoration instead. It does not damage NPCs or verify
combat outcomes. Use only in a safe scene: the game continues running and may
save during these tests.

`--test-combat` requires a living hostile NPC. It briefly enables protection
and Weaken Enemies, checks player and opponent thresholds, then restores
invulnerability. NPC injuries remain and the match outcome may change.

The code discovers the name pool and object array in the module's data section,
then resolves fields through Unreal reflection. It validates expected field
types, sizes, bounds, and identities rather than using fixed gameplay offsets.
The installed game executable has no version resource. This trainer's live
bindings were checked against Half Sword **0.7 Hotfix 4**, Steam build **26820056**.
Compatibility with other builds is not guaranteed.

The original multi-resolution application icon is embedded in the EXE and
window. Regenerate its ICO/PNG assets with `.\tools\New-TrainerIcon.ps1`.

## Unofficial fan project

Half Sword and its game artwork belong to their respective owners. This tool
is not affiliated with or endorsed by the game developers. The jester portrait
is game artwork supplied for this interface; no ownership of that artwork is
claimed. Use at your own risk, keep independent save backups, and do not assume
the trainer will prevent every possible death or physics-related injury.
