# Half Sword Trainer - The Fool's Favour

A medieval jester-themed standalone trainer for **Half Sword 0.7.2**. Trainer version **1.2.0**.

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
The connection badge changes to **BETWEEN BOUTS** during loading and
**WAITING FOR GAME** when the game is closed.
Control cards scroll on shorter windows; standard keyboard focus and checkbox
accessibility are retained. The portrait is embedded, so no image files need
to be installed separately.

Open `HalfSwordTrainer.exe` from an extracted release (or
`dist\HalfSwordTrainer.exe` after building). Every feature starts OFF, but all
switches and numeric inputs are available immediately. Select combat features
before entering a fight: they activate once the local player possesses a
validated fighter. Funds and rank reputation work in menus once the game
session is available; game speed works wherever an active world is available.
Muscle power is fighter-specific and waits for possession.
Run at the same elevation as the game.
Only one game instance may be running.

* **Invulnerable**: continuously enables the player's built-in invulnerability.
* **Unlimited Health**: replenishes general and eleven named body/crush health
  values to 100 and clears bleeding. Cannot reattach severed limbs or resurrect
  an already dead character. Use before taking damage.
* **Unlimited Stamina**: replenishes stamina to 100.
* **Unlimited Consciousness**: replenishes consciousness, its cap, and leg
  consciousness to 100.
* **No Ball Or Liver Pain**: clears both pain values.
* **No Exhaustion**: holds exhaustion at 0 (fully rested).
* **Ideal Temperature**: holds temperature at 1, the character class's default
  and neutral point in the game's temperature calculations. Does not change
  equipment heat retention.
* **Easy Win**: holds living hostile NPCs in the player's level at no more than
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
* **Muscle Power Value**: check to hold the raw muscle value at 0–10,000.
  The observed player default in this build is 35. Start with small changes.
* **Funds Value**: **Set Funds** writes the live game-instance funds once.
  The UI may update on reopening the relevant menu. Normal game saves may
  persist the change; the trainer does not force a save.
* **Reputation on Rank**: while checked, raises the current rank's reputation
  to at least the entered value; never lowers reputation you already earned.
  The default **1,500** meets the reputation comparisons found in this build's
  tournament generator (50/100/200/300/400/500/750/1,000/1,500). This removes
  the reputation bottleneck, not every event-generation condition. Reopen or
  refresh events normally; playing another match/advancing a day may be needed.
  Does not force a tournament to spawn, reset the daily tournament flag, change
  rank, or grant victories. Training/free modes may not use career progression.

**STOP / Restore**, unchecking a feature, or closing the window stops holding
values. Invulnerability, speed, and muscle power are restored if their objects
are still valid and the game has not independently changed those values.
Health/stamina replenishment, fitness changes, NPC injuries, funds, and earned
rank reputation are not undone. Reputation can persist through normal saves.

The trainer checks Unreal object-array identity and serial numbers before
writes. A respawn, scene transition, or object collection pauses writes and
automatically reacquires the local player's new controller, pawn, world, and
game instance. Checked features and all numeric selections are retained. A
session without a fighter shows **OPTIONS ARMED**, with unavailable combat
readings displayed as `--`; menu-safe features remain usable. During loading,
writes pause until validated bindings are available;
no repeated error dialogs are shown for normal loading. The local-player
back-reference prevents selecting an old controller retained by the game.
Queued one-shot funds changes are cancelled on transitions; click Set Funds
again after reconnection if needed.

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

Errors appear in a dialog and in `trainer-errors.log` next to the executable.
Settings are not persisted across trainer app launches. DISARM ALL clears
switches, not numeric inputs. Automatic match reconnections and game restarts
preserve all selections; match reconnections reuse the session's save backup.

## Build and verification

```powershell
dotnet build .\HalfSwordTrainer.csproj -c Release
dotnet publish .\HalfSwordTrainer.csproj -c Release -o .\dist
Start-Process .\dist\HalfSwordTrainer.exe -ArgumentList '--self-test' -Wait
Get-Content .\dist\verification.txt
Start-Process .\dist\HalfSwordTrainer.exe -ArgumentList '--verify' -Wait
Get-Content .\dist\verification.txt
```

`--self-test` checks pre-game selection, linking/lost-session retention, disarm,
and reputation input/floor behavior without accessing the game.
`--verify` validates live bindings, NPC targeting, and Force Win property
bindings without writing.
`--test-writes` briefly tests player stats, invulnerability, funds, muscle
power, fitness, and speed with immediate readback, then restores and verifies
original values. Without a possessed fighter, it tests menu funds and rank
reputation writes with restoration instead. It does not damage NPCs or verify
combat outcomes. Use only in a safe scene: the game continues running and may
save during these tests.

`--test-combat` requires a living hostile NPC. It briefly enables protection
and Easy Win, checks player and opponent thresholds, then restores
invulnerability. NPC injuries remain and the match outcome may change.

The code discovers the name pool and object array in the module's data section,
then resolves fields through Unreal reflection. It validates expected field
types, sizes, bounds, and identities rather than using fixed gameplay offsets.
The tested game executable has no version resource; "0.7.2" is its reported
game version. Compatibility with another build is not guaranteed.

The original multi-resolution application icon is embedded in the EXE and
window. Regenerate its ICO/PNG assets with `.\tools\New-TrainerIcon.ps1`.

## Unofficial fan project

Half Sword and its game artwork belong to their respective owners. This tool
is not affiliated with or endorsed by the game developers. The jester portrait
is game artwork supplied for this interface; no ownership of that artwork is
claimed. Use at your own risk, keep independent save backups, and do not assume
the trainer will prevent every possible death or physics-related injury.
