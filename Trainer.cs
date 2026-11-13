namespace HalfSwordTrainer;

internal sealed record TrainerOptions(bool Invulnerable = false, bool Health = false, bool Stamina = false,
    bool Consciousness = false, bool NoPain = false, bool EasyWin = false,
    bool Speed = false, double SpeedValue = 1, bool Muscle = false, double MuscleValue = 35);

internal sealed record TrainerStatus(string Message, double Health, double Stamina, double Consciousness,
    double Muscle, int Funds, double Speed, int Opponents);

internal sealed class Trainer : IDisposable
{
    private readonly Memory memory = new(true);
    private readonly Unreal unreal;
    private GameObject controller;
    private GameObject player;
    private GameObject settings;
    private GameObject instance;
    private List<GameObject> opponents = [];
    private readonly Dictionary<(long Address, string Name), (GameObject Object, GameProperty Property, byte[] Original, byte[] Applied)> overrides = [];
    private DateTime nextOpponentScan;
    private bool backedUp;
    private readonly Action<string> notify;
    public string? BackupPath { get; private set; }
    public int ProcessId => memory.Process.Id;
    private static readonly string[] HealthNames = ["Health", "Head Health", "Body Upper Health", "Body Lower Health",
        "Arm_R Health", "Arm_L Health", "Leg_R Health", "Leg_L Health", "Neck Health", "Back Health",
        "Heart Health", "Head Health (Crush)"];
    private static readonly string[] ConsciousnessNames = ["Consciousness", "Consciousness Cap", "Consciousness 2 (Legs)"];
    private static readonly string[] PainNames = ["Ball Pain", "Liver Pain"];

    public Trainer(Action<string>? notify = null, string? backupPath = null)
    {
        this.notify = notify ?? Program.Log;
        BackupPath = backupPath;
        backedUp = backupPath != null;
        try
        {
            unreal = new Unreal(memory);
            var all = unreal.AllObjects();
            var controllers = all.Where(o => unreal.IsA(o, "PlayerController") &&
                unreal.ObjectName(o.Outer) == "PersistentLevel" &&
                IsLocalController(o)).ToList();
            if (controllers.Count != 1) throw new GameTransitionException("Waiting for a unique local player controller.");
            controller = controllers[0];
            long pawn = Pointer(controller, "AcknowledgedPawn");
            if (pawn == 0) throw new GameTransitionException("Waiting for the local player to possess a character.");
            player = unreal.Reference(pawn);
            if (!unreal.IsA(player, "Willie_BP_C"))
                throw new GameTransitionException("Waiting for a playable Half Sword character.");
            var worlds = all.Where(o => unreal.IsA(o, "WorldSettings") && o.Outer == controller.Outer).ToList();
            var instances = all.Where(o => unreal.IsA(o, "GI_Settings_C")).ToList();
            if (worlds.Count != 1 || instances.Count != 1)
                throw new GameTransitionException("Waiting for the active world and game instance.");
            settings = worlds[0];
            instance = instances[0];
            ValidateBindings();
        }
        catch { memory.Dispose(); throw; }
    }

    private long Pointer(GameObject obj, string name) =>
        memory.Read<long>(obj.Address + unreal.Property(obj, name, "ObjectProperty").Offset);

    private bool IsLocalController(GameObject obj)
    {
        long localPlayer = Pointer(obj, "Player");
        if (localPlayer == 0) return false;
        var local = unreal.Reference(localPlayer);
        return unreal.IsA(local, "LocalPlayer") && Pointer(local, "PlayerController") == obj.Address;
    }

    private void ValidateBindings()
    {
        unreal.EnsureLive(controller);
        unreal.EnsureLive(player);
        unreal.EnsureLive(settings);
        unreal.EnsureLive(instance);
        foreach (string name in HealthNames.Concat(ConsciousnessNames).Concat(PainNames).Concat(["Stamina", "Muscle Power", "Bleeding"]))
            _ = DoubleProperty(player, name);
        _ = unreal.Property(player, "Invulnerable", "BoolProperty");
        _ = unreal.Property(player, "Team Int", "IntProperty");
        _ = unreal.Property(player, "Player", "BoolProperty");
        _ = unreal.Property(settings, "TimeDilation", "FloatProperty");
        _ = unreal.Property(instance, "Player Funds", "IntProperty");
        _ = Status();
    }

    private GameProperty DoubleProperty(GameObject obj, string name)
    {
        var p = unreal.Property(obj, name, "DoubleProperty");
        if (p.Size != 8) throw new InvalidOperationException($"Unexpected numeric size for {name}.");
        return p;
    }

    public double Number(GameObject obj, string name)
    {
        var value = memory.Read<double>(obj.Address + DoubleProperty(obj, name).Offset);
        if (!double.IsFinite(value)) throw new InvalidOperationException($"Invalid numeric value: {name}.");
        return value;
    }

    private void EnsurePlayer()
    {
        if (memory.Process.HasExited) throw new InvalidOperationException("Half Sword has exited.");
        unreal.EnsureLive(controller);
        unreal.EnsureLive(player);
        if (!IsLocalController(controller) ||
            Pointer(controller, "AcknowledgedPawn") != player.Address ||
            Pointer(controller, "Pawn") != player.Address)
            throw new GameTransitionException("Player changed; waiting for the new match.");
    }

    private void Backup()
    {
        if (backedUp) return;
        string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HalfSwordUE5", "Saved", "SaveGames");
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException($"Cannot back up saves: {source}");
        string destination = Path.Combine(AppContext.BaseDirectory, "backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(destination);
        foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target, false);
        }
        BackupPath = destination;
        backedUp = true;
    }

    private void Put(GameObject obj, GameProperty prop, byte[] bytes, bool reversible = false)
    {
        if (bytes.Length != prop.Size) throw new InvalidOperationException($"Size mismatch for {prop.Name}.");
        EnsurePlayer();
        unreal.EnsureLive(obj);
        Backup();
        long address = obj.Address + prop.Offset;
        byte[] current = memory.Read(address, bytes.Length);
        var key = (obj.Address, prop.Name);
        if (reversible)
        {
            if (overrides.TryGetValue(key, out var previous))
                overrides[key] = (obj, prop, previous.Original, bytes);
            else
                overrides[key] = (obj, prop, current, bytes);
        }
        if (!current.AsSpan().SequenceEqual(bytes)) memory.Write(address, bytes);
    }

    private void Set(GameObject obj, string name, double value, bool reversible = false) =>
        Put(obj, DoubleProperty(obj, name), BitConverter.GetBytes(value), reversible);

    private void SetInvulnerable(bool enabled)
    {
        var p = unreal.Property(player, "Invulnerable", "BoolProperty");
        var layout = memory.Read(p.Metadata + 0x70, 4);
        if (p.Size != 1 || layout[1] != 0 || layout[2] != 1 || layout[3] != 255)
            throw new InvalidOperationException("Unsupported Invulnerable boolean layout.");
        Put(player, p, [enabled ? (byte)1 : (byte)0], true);
    }

    private void Restore(string name)
    {
        foreach (var pair in overrides.Where(p => p.Key.Name == name).ToArray())
        {
            var value = pair.Value;
            try
            {
                unreal.EnsureLive(value.Object);
                long address = value.Object.Address + value.Property.Offset;
                if (memory.Read(address, value.Applied.Length).AsSpan().SequenceEqual(value.Applied))
                    memory.Write(address, value.Original);
            }
            catch (GameTransitionException ex)
            {
                notify($"Skipped restoring {name} on unloaded object 0x{value.Object.Address:X}: {ex.Message}");
            }
            overrides.Remove(pair.Key);
        }
    }

    public void Apply(TrainerOptions options)
    {
        if (!double.IsFinite(options.SpeedValue) || options.SpeedValue is < 0.1 or > 5 ||
            !double.IsFinite(options.MuscleValue) || options.MuscleValue is < 0 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(options), "Speed must be 0.1–5; muscle power 0–10,000.");
        EnsurePlayer();
        if (options.Invulnerable) SetInvulnerable(true); else Restore("Invulnerable");
        if (options.Health)
        {
            foreach (string name in HealthNames) Set(player, name, 100);
            Set(player, "Bleeding", 0);
        }
        if (options.Stamina) Set(player, "Stamina", 100);
        if (options.Consciousness)
            foreach (string name in ConsciousnessNames) Set(player, name, 100);
        if (options.NoPain)
            foreach (string name in PainNames) Set(player, name, 0);
        if (options.Muscle) Set(player, "Muscle Power", options.MuscleValue, true); else Restore("Muscle Power");
        if (options.Speed)
            Put(settings, unreal.Property(settings, "TimeDilation", "FloatProperty"),
                BitConverter.GetBytes((float)options.SpeedValue), true);
        else Restore("TimeDilation");
        if (options.EasyWin)
        {
            if (DateTime.UtcNow >= nextOpponentScan)
            {
                opponents = unreal.AllObjects().Where(o => o.Address != player.Address &&
                    o.Outer == player.Outer && unreal.IsA(o, "Willie_BP_C") && Hostile(o)).ToList();
                nextOpponentScan = DateTime.UtcNow.AddSeconds(1);
            }
            foreach (var opponent in opponents)
            {
                unreal.EnsureLive(opponent);
                if (!Hostile(opponent)) continue;
                double health = Number(opponent, "Health");
                if (health <= 0) continue;
                Set(opponent, "Health", Math.Min(health, 1));
                Set(opponent, "Stamina", 0);
                Set(opponent, "Consciousness", Math.Min(Number(opponent, "Consciousness"), 1));
            }
        }
        else { opponents.Clear(); nextOpponentScan = DateTime.MinValue; }
    }

    private bool Hostile(GameObject obj)
    {
        int team = memory.Read<int>(obj.Address + unreal.Property(obj, "Team Int", "IntProperty").Offset);
        int playerTeam = memory.Read<int>(player.Address + unreal.Property(player, "Team Int", "IntProperty").Offset);
        bool isPlayer = memory.Read<byte>(obj.Address + unreal.Property(obj, "Player", "BoolProperty").Offset) != 0;
        return !isPlayer && (team == 0 || playerTeam == 0 || team != playerTeam);
    }

    public void SetFunds(int value)
    {
        if (value is < 0 or > 1000000000) throw new ArgumentOutOfRangeException(nameof(value), "Funds must be 0–1,000,000,000.");
        Put(instance, unreal.Property(instance, "Player Funds", "IntProperty"), BitConverter.GetBytes(value));
        if (Status().Funds != value) throw new InvalidOperationException("Game did not retain the requested funds value.");
    }

    public TrainerStatus Status()
    {
        EnsurePlayer();
        unreal.EnsureLive(instance);
        unreal.EnsureLive(settings);
        int funds = memory.Read<int>(instance.Address + unreal.Property(instance, "Player Funds", "IntProperty").Offset);
        float speed = memory.Read<float>(settings.Address + unreal.Property(settings, "TimeDilation", "FloatProperty").Offset);
        if (funds < 0 || !float.IsFinite(speed) || speed <= 0)
            throw new InvalidOperationException("Live funds or game speed validation failed.");
        return new($"Attached to PID {ProcessId} | player 0x{player.Address:X}", Number(player, "Health"),
            Number(player, "Stamina"), Number(player, "Consciousness"), Number(player, "Muscle Power"),
            funds, speed, opponents.Count);
    }

    public void Verify(TextWriter log, bool writeTest)
    {
        log.WriteLine(Status());
        foreach (string name in HealthNames.Concat(ConsciousnessNames).Concat(PainNames).Concat(["Stamina", "Muscle Power"]))
            log.WriteLine($"{name}: {Number(player, name):F3}");
        var hostiles = unreal.AllObjects().Where(o => o.Address != player.Address &&
            o.Outer == player.Outer && unreal.IsA(o, "Willie_BP_C") && Hostile(o)).ToList();
        log.WriteLine($"Hostile NPCs resolved: {hostiles.Count}; player excluded; same-team allies excluded.");
        if (!writeTest) return;
        var saved = new List<(GameObject Object, GameProperty Property, byte[] Value)>();
        void Check(GameObject obj, GameProperty property, byte[] expected)
        {
            saved.Add((obj, property, memory.Read(obj.Address + property.Offset, property.Size)));
            Put(obj, property, expected);
            var actual = memory.Read(obj.Address + property.Offset, property.Size);
            if (!actual.AsSpan().SequenceEqual(expected)) throw new InvalidOperationException($"Write verification failed: {property.Name}");
            log.WriteLine($"PASS write/readback {property.Name}: {Convert.ToHexString(actual)}");
        }
        try
        {
            Check(player, unreal.Property(player, "Invulnerable", "BoolProperty"), [1]);
            Check(player, DoubleProperty(player, "Bleeding"), BitConverter.GetBytes(0d));
            foreach (string name in HealthNames.Concat(ConsciousnessNames).Concat(["Stamina"]))
                Check(player, DoubleProperty(player, name), BitConverter.GetBytes(100d));
            foreach (string name in PainNames) Check(player, DoubleProperty(player, name), BitConverter.GetBytes(0d));
            Check(player, DoubleProperty(player, "Muscle Power"), BitConverter.GetBytes(Number(player, "Muscle Power") + 1));
            Check(settings, unreal.Property(settings, "TimeDilation", "FloatProperty"), BitConverter.GetBytes(1.1f));
            int funds = Status().Funds;
            Check(instance, unreal.Property(instance, "Player Funds", "IntProperty"), BitConverter.GetBytes(funds == 1000000000 ? funds - 1 : funds + 1));
            var options = new TrainerOptions(Invulnerable: true, Health: true, Stamina: true, Consciousness: true,
                NoPain: true, Speed: true, SpeedValue: 1.1, Muscle: true, MuscleValue: 36);
            for (int i = 0; i < 20; i++)
            {
                Apply(options);
                var status = Status();
                if (status.Health < 99 || status.Stamina < 99 || status.Consciousness < 99 ||
                    status.Muscle != 36 || Math.Abs(status.Speed - 1.1) > 0.0001 ||
                    PainNames.Any(name => Number(player, name) != 0) ||
                    HealthNames.Any(name => Number(player, name) < 99))
                    throw new InvalidOperationException("Continuous trainer application did not maintain requested thresholds.");
                Thread.Sleep(15);
            }
            Apply(new TrainerOptions());
            if (overrides.Count != 0) throw new InvalidOperationException("Disabling features left outstanding overrides.");
            log.WriteLine("PASS 20 continuous Apply cycles: health/stamina/consciousness >=99, body health >=99, pain 0, speed 1.1x, muscle 36.");
            log.WriteLine("PASS feature disable restored tracked overrides.");
            log.WriteLine("PASS all requested player/value bindings. Easy Win targeting verified read-only; NPC damage not tested.");
        }
        finally
        {
            Stop();
            foreach (var entry in saved.AsEnumerable().Reverse())
            {
                unreal.EnsureLive(entry.Object);
                memory.Write(entry.Object.Address + entry.Property.Offset, entry.Value);
                if (!memory.Read(entry.Object.Address + entry.Property.Offset, entry.Value.Length).AsSpan().SequenceEqual(entry.Value))
                    throw new InvalidOperationException($"Restoration verification failed: {entry.Property.Name}");
            }
            log.WriteLine("Original values restored and read back.");
        }
        var invulnerability = unreal.Property(player, "Invulnerable", "BoolProperty");
        byte before = memory.Read<byte>(player.Address + invulnerability.Offset);
        var stalePlayer = player with { Serial = player.Serial ^ int.MaxValue };
        overrides.Add((player.Address, "Invulnerable"), (stalePlayer, invulnerability, [(byte)(before == 0 ? 1 : 0)], [before]));
        var dilation = unreal.Property(settings, "TimeDilation", "FloatProperty");
        byte[] previousSpeed = memory.Read(settings.Address + dilation.Offset, dilation.Size);
        Put(settings, dilation, BitConverter.GetBytes(1.05f), true);
        Stop();
        if (overrides.Count != 0 || memory.Read<byte>(player.Address + invulnerability.Offset) != before ||
            !memory.Read(settings.Address + dilation.Offset, dilation.Size).AsSpan().SequenceEqual(previousSpeed))
            throw new InvalidOperationException("Stale-object cleanup wrote to a replaced object or failed to restore the live world.");
        log.WriteLine("PASS transition cleanup: stale player override discarded without writing; live world speed restored; no leftover overrides.");
        log.WriteLine($"Save backup: {BackupPath}");
    }

    public void Stop()
    {
        List<Exception> errors = [];
        foreach (string name in overrides.Keys.Select(k => k.Name).Distinct().ToArray())
        {
            try { Restore(name); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            { errors.Add(ex); }
        }
        if (errors.Count != 0) throw new AggregateException("Some previous values could not be restored; the game may have unloaded them.", errors);
    }

    public void VerifyCombat(TextWriter log)
    {
        var targets = unreal.AllObjects().Where(o => o.Address != player.Address &&
            o.Outer == player.Outer && unreal.IsA(o, "Willie_BP_C") && Hostile(o) && Number(o, "Health") > 0).ToList();
        if (targets.Count == 0) throw new InvalidOperationException("No living hostile NPCs. Enter a match before testing combat.");
        log.WriteLine($"Combat test: {targets.Count} living hostile NPC(s).");
        foreach (var target in targets)
            log.WriteLine($"Before 0x{target.Address:X}: health {Number(target, "Health"):F2}, stamina {Number(target, "Stamina"):F2}, consciousness {Number(target, "Consciousness"):F2}");
        try
        {
            var options = new TrainerOptions(Invulnerable: true, Health: true, Stamina: true,
                Consciousness: true, NoPain: true, EasyWin: true);
            for (int i = 0; i < 240; i++)
            {
                Apply(options);
                var status = Status();
                if (status.Health < 99 || status.Stamina < 99 || status.Consciousness < 99 ||
                    PainNames.Any(name => Number(player, name) != 0))
                    throw new InvalidOperationException($"Player protection thresholds failed during combat test: {status}; ball pain {Number(player, "Ball Pain")}; liver pain {Number(player, "Liver Pain")}.");
                if (i % 40 == 0) log.WriteLine($"Player {status.Health:F1} HP, {status.Stamina:F1} stamina, {status.Consciousness:F1} consciousness; {status.Opponents} targets.");
                Thread.Sleep(15);
            }
            Apply(options);
            foreach (var target in targets)
            {
                unreal.EnsureLive(target);
                double health = Number(target, "Health");
                double stamina = Number(target, "Stamina");
                double consciousness = Number(target, "Consciousness");
                log.WriteLine($"After 0x{target.Address:X}: health {health:F4}, stamina {stamina:F4}, consciousness {consciousness:F4}.");
                // The game can regenerate stats on another thread between writes and reads.
                if (health > 2 || stamina > 2 || consciousness > 2)
                    throw new InvalidOperationException($"Easy Win thresholds failed for 0x{target.Address:X}.");
                log.WriteLine($"PASS NPC 0x{target.Address:X}: health {health:F2}, stamina {stamina:F2}, consciousness {consciousness:F2}.");
            }
            log.WriteLine("PASS combat-time protection and hostile-only Easy Win weakening: NPC stats <=2 allowing tick regeneration after writes to <=1/0.");
            log.WriteLine("Match victory/rewards are not forced or verified.");
        }
        finally { Stop(); }
        log.WriteLine($"Protection disabled; invulnerability restored. NPC injuries intentionally remain. Save backup: {BackupPath}");
    }
    public void Dispose() => memory.Dispose();
}
