# Half Sword Trainer - The Fool's Favour

A medieval jester-themed standalone trainer for **Half Sword 0.7.2**.

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
cards. **LINK TO GAME** attaches; **DISARM ALL** stops and restores overrides.
The connection badge changes to **BETWEEN BOUTS** during automatic recovery.
Control cards scroll on shorter windows; standard keyboard focus and checkbox
accessibility are retained. The portrait is embedded, so no image files need
to be installed separately.

Open `HalfSwordTrainer.exe` from an extracted release (or
`dist\HalfSwordTrainer.exe` after building), enter a playable scene, and click
**LINK TO GAME**. Every feature starts OFF. Run at the same elevation as the game.
Only one game instance may be running.

* **Invulnerable**: continuously enables the player's built-in invulnerability.
* **Unlimited Health**: replenishes general and eleven named body/crush health
  values to 100 and clears bleeding. Cannot reattach severed limbs or resurrect
  an already dead character. Use before taking damage.
* **Unlimited Stamina**: replenishes stamina to 100.
* **Unlimited Consciousness**: replenishes consciousness, its cap, and leg
  consciousness to 100.
* **No Ball Or Liver Pain**: clears both pain values.
* **Easy Win**: holds living hostile NPCs in the player's level at no more than
  1 health/consciousness and 0 stamina. Finish them normally. Does not grant a
  victory/reward directly. Excludes the possessed player and same-team allies;
  team 0 uses the game's free-for-all convention.
* **Game Speed**: check to hold global time dilation at 0.10–5.00x. Large values
  can destabilize physics.
* **Muscle Power Value**: check to hold the raw muscle value at 0–10,000.
  The observed player default in this build is 35. Start with small changes.
* **Funds Value**: **Set Funds** writes the live game-instance funds once.
  The UI may update on reopening the relevant menu. Normal game saves may
  persist the change; the trainer does not force a save.

**STOP / Restore**, unchecking a feature, or closing the window stops holding
values. Invulnerability, speed, and muscle power are restored if their objects
are still valid and the game has not independently changed those values.
Health/stamina replenishment, NPC injuries, and funds are not undone.

The trainer checks Unreal object-array identity and serial numbers before
writes. A respawn, scene transition, or object collection pauses writes and
automatically reacquires the local player's new controller, pawn, world, and
game instance. Checked features and speed/muscle values are retained. The status
shows **Waiting for the next match** until validated bindings are available;
no repeated error dialogs are shown for normal loading. The local-player
back-reference prevents selecting an old controller retained by the game.
Queued one-shot funds changes are cancelled on transitions; click Set Funds
again after reconnection if needed.

Restoration skips unloaded objects rather than writing to their old addresses;
these skips and reconnections are logged. Still-live overrides are restored.
Game exit, permission failures, invalid layouts, and other real errors stop the
session with an explicit error. STOP cancels automatic reconnection.

Values are replenished externally about every 15 ms, not by intercepting game
logic. Damage/drain can still occur between updates. Use invulnerability
alongside health for damage prevention; no feature reverses pre-existing death
or dismemberment.

## Saves and recovery

Before the first write of each manually started attachment session, all files under
`%LOCALAPPDATA%\HalfSwordUE5\Saved\SaveGames` are copied to the executable's
`backups\<timestamp>` folder. If copying fails, no write is made. This is a
best-effort file copy while the game is running, not an atomic save snapshot.
To restore, close the game and copy the desired backup's contents back to the
SaveGames folder. Keep a separate save backup before experimenting.

Errors appear in a dialog and in `trainer-errors.log` next to the executable.
Settings are not persisted across app launches or manual Stop/Attach; automatic
match reconnections preserve them and reuse the session's save backup.

## Build and verification

```powershell
dotnet build .\HalfSwordTrainer.csproj -c Release
dotnet publish .\HalfSwordTrainer.csproj -c Release -o .\dist
Start-Process .\dist\HalfSwordTrainer.exe -ArgumentList '--verify' -Wait
Get-Content .\dist\verification.txt
```

`--verify` validates live bindings and NPC targeting without writing.
`--test-writes` briefly tests player stats, invulnerability, funds, muscle
power, and speed with immediate readback, then restores and verifies original
values. It does not damage NPCs or verify combat outcomes. Use only in a safe
scene: the game continues running during these tests.

`--test-combat` requires a living hostile NPC. It briefly enables protection
and Easy Win, checks player and opponent thresholds, then restores
invulnerability. NPC injuries remain and the match outcome may change.

The code discovers the name pool and object array in the module's data section,
then resolves fields through Unreal reflection. It validates expected field
types, sizes, bounds, and identities rather than using fixed gameplay offsets.
The tested game executable has no version resource; "0.7.2" is its reported
game version. Compatibility with another build is not guaranteed.

## Unofficial fan project

Half Sword and its game artwork belong to their respective owners. This tool
is not affiliated with or endorsed by the game developers. The jester portrait
is game artwork supplied for this interface; no ownership of that artwork is
claimed. Use at your own risk, keep independent save backups, and do not assume
the trainer will prevent every possible death or physics-related injury.
