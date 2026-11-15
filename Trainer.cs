namespace HalfSwordTrainer;

internal sealed record TrainerOptions(bool Invulnerable = false, bool Health = false, bool Stamina = false,
    bool Consciousness = false, bool NoPain = false, bool EasyWin = false,
    bool Speed = false, double SpeedValue = 1, bool Muscle = false, double MuscleValue = 35,
    bool NoExhaustion = false, bool Temperature = false, bool RankReputation = false, int ReputationValue = 1500)
{
    public void Validate()
    {
        if (!double.IsFinite(SpeedValue) || SpeedValue is < 0.1 or > 5 ||
            !double.IsFinite(MuscleValue) || MuscleValue is < 0 or > 10000 ||
            ReputationValue is < 0 or > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(TrainerOptions), "Speed must be 0.1-5; muscle power 0-10,000; rank reputation 0-1,000,000,000.");
    }

    public int ReputationTarget(int current) => Math.Max(current, ReputationValue);
}

internal sealed record TrainerStatus(string Message, double? Health, double? Stamina, double? Consciousness,
    double? Muscle, int Funds, double? Speed, int Opponents, bool CombatReady,
    double? Exhaustion, double? Temperature, int RankReputation,
    double? CareerPhysique = null, bool PhysiqueEditable = false);

internal sealed class Trainer : IDisposable
{
    private readonly Memory memory = new(true);
    private readonly Unreal unreal;
    private sealed record ForceWinBindings(GameObject? GameMode, List<GameObject> Hostiles, List<GameObject> Controllers);
    private sealed record CombatBindings(GameObject Controller, GameObject Player);
    private CombatBindings? combat;
    private GameObject? localController;
    private GameObject? world;
    private GameObject controller => combat?.Controller ?? throw new GameTransitionException("No active fighter.");
    private GameObject player => combat?.Player ?? throw new GameTransitionException("No active fighter.");
    private GameObject settings => world ?? throw new GameTransitionException("Waiting for the active world.");
    private GameObject instance;
    private GameProperty? careerPhysiqueProperty;
    private DateTime nextCombatScan;
    private List<GameObject> opponents = [];
    private readonly Dictionary<(long Address, string Name), (GameObject Object, GameProperty Property, byte[] Original, byte[] Applied)> overrides = [];
    private DateTime nextOpponentScan;
    private bool backedUp;
    private readonly Action<string> notify;
    public string? BackupPath { get; private set; }
    public int ProcessId => memory.Process.Id;
    public GameProcessIdentity ProcessIdentity => memory.Identity;
    private static readonly string[] HealthNames = ["Health", "Head Health", "Body Upper Health", "Body Lower Health",
        "Arm_R Health", "Arm_L Health", "Leg_R Health", "Leg_L Health", "Neck Health", "Back Health",
        "Heart Health", "Head Health (Crush)"];
    private static readonly string[] ConsciousnessNames = ["Consciousness", "Consciousness Cap", "Consciousness 2 (Legs)"];
    private static readonly string[] PainNames = ["Ball Pain", "Liver Pain"];

    public Trainer(Action<string>? notify = null, string? backupPath = null,
        GameProcessIdentity? previouslyValidatedProcess = null)
    {
        this.notify = notify ?? Program.Log;
        BackupPath = backupPath;
        backedUp = backupPath != null;
        try
        {
            unreal = new Unreal(memory, memory.Identity == previouslyValidatedProcess);
            var all = unreal.AllObjects();
            var instances = all.Where(o => unreal.IsA(o, "GI_Settings_C") && unreal.ObjectName(o.Outer) == "GameEngine").ToList();
            if (instances.Count != 1) throw new GameTransitionException("Waiting for the game session.");
            instance = instances[0];
            _ = unreal.Property(instance, "Player Funds", "IntProperty");
            _ = unreal.Property(instance, "Reputation on Rank", "IntProperty");
            BindCombat(all);
        }
        catch { memory.Dispose(); throw; }
    }

    private void BindCombat(List<GameObject>? all = null)
    {
        nextCombatScan = DateTime.UtcNow.AddMilliseconds(350);
        var localPlayers = unreal.Property(instance, "LocalPlayers", "ArrayProperty");
        var array = memory.Read(instance.Address + localPlayers.Offset, 16);
        int count = BitConverter.ToInt32(array, 8);
        if (count == 0) return;
        if (count != 1) throw new InvalidOperationException("Only a single local player is supported.");
        var local = unreal.Reference(memory.Read<long>(BitConverter.ToInt64(array)));
        if (!unreal.IsA(local, "LocalPlayer")) throw new GameTransitionException("Local player is loading.");
        long controllerAddress = Pointer(local, "PlayerController");
        if (controllerAddress == 0) return;
        var candidate = unreal.Reference(controllerAddress);
        if (!IsLocalController(candidate)) throw new GameTransitionException("Local controller is changing.");
        localController = candidate;
        if (world == null)
        {
            var worlds = (all ?? unreal.AllObjects()).Where(o => unreal.IsA(o, "WorldSettings") && o.Outer == candidate.Outer).ToList();
            if (worlds.Count == 1) world = worlds[0];
        }
        long pawn = Pointer(candidate, "AcknowledgedPawn");
        if (pawn == 0 || Pointer(candidate, "Pawn") != pawn) return;
        var fighter = unreal.Reference(pawn);
        if (!unreal.IsA(fighter, "Willie_BP_C")) return;
        combat = new(candidate, fighter);
        ValidateBindings();
    }

    private void EnsureSession()
    {
        if (memory.Process.HasExited) throw new GameUnavailableException();
        unreal.EnsureLive(instance);
        if (localController != null)
        {
            unreal.EnsureLive(localController);
            if (!IsLocalController(localController)) throw new GameTransitionException("The active local controller changed.");
        }
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
        if (world != null) unreal.EnsureLive(world);
        unreal.EnsureLive(instance);
        foreach (string name in HealthNames.Concat(ConsciousnessNames).Concat(PainNames).Concat(["Stamina", "Muscle Power", "Bleeding", "Exhaustion", "Temperature"]))
            _ = DoubleProperty(player, name);
        _ = unreal.Property(player, "Invulnerable", "BoolProperty");
        _ = unreal.Property(player, "Team Int", "IntProperty");
        _ = unreal.Property(player, "Player", "BoolProperty");
        if (world != null) _ = unreal.Property(world, "TimeDilation", "FloatProperty");
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
        EnsureSession();
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
        EnsureSession();
        if (obj.Address != instance.Address && obj.Address != world?.Address) EnsurePlayer();
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
        var p = BooleanProperty(player, "Invulnerable");
        Put(player, p, [enabled ? (byte)1 : (byte)0], true);
    }

    private GameProperty BooleanProperty(GameObject obj, string name)
    {
        var p = unreal.Property(obj, name, "BoolProperty");
        var layout = memory.Read(p.Metadata + 0x70, 4);
        if (p.Size != 1 || layout[1] != 0 || layout[2] != 1 || layout[3] != 255)
            throw new InvalidOperationException($"Unsupported {name} boolean layout.");
        return p;
    }

    private void WriteBool(GameObject obj, string name, bool value) =>
        Put(obj, BooleanProperty(obj, name), [value ? (byte)1 : (byte)0]);

    private ForceWinBindings ResolveForceWinBindings()
    {
        EnsurePlayer();
        var all = unreal.AllObjects();
        var gameModes = all.Where(o => o.Outer == player.Outer && unreal.IsA(o, "BP_HalfSwordGameMode_C")).ToList();
        if (gameModes.Count > 1) throw new InvalidOperationException("Multiple match game modes found in the active level.");
        var gameMode = gameModes.SingleOrDefault();
        var hostiles = all.Where(o => o.Address != player.Address && o.Outer == player.Outer &&
            unreal.IsA(o, "Willie_BP_C") && Hostile(o)).ToList();
        var controllers = all.Where(o => o.Outer == player.Outer && unreal.IsA(o, "AI_BP_C")).ToList();

        foreach (var character in hostiles.Append(player))
        {
            foreach (string name in new[] { "Invulnerable", "Give Up", "Vertex Paint Saving Initiated", "Spawn On Death Blood Emitter" })
                _ = BooleanProperty(character, name);
            foreach (string name in new[] { "Health", "Head Health", "Neck Health", "Heart Health", "Consciousness", "Consciousness Cap" })
                _ = DoubleProperty(character, name);
        }
        foreach (var ai in controllers)
            foreach (string name in new[] { "My Fallen", "My Give Up", "DED" })
                _ = BooleanProperty(ai, name);
        _ = BooleanProperty(player, "Match Won (Player)");
        if (gameMode != null)
        {
            var enemyCount = unreal.Property(gameMode, "Enemy Count", "IntProperty");
            if (enemyCount.Size != 4) throw new InvalidOperationException("Unsupported match enemy counter size.");
            _ = BooleanProperty(gameMode, "All Enemies Dead");
            _ = BooleanProperty(gameMode, "Match Won");
        }
        return new(gameMode, hostiles, controllers);
    }

    public void ForceWin()
    {
        var bindings = ResolveForceWinBindings();
        if (bindings.Hostiles.Count > 0)
        {
            foreach (var opponent in bindings.Hostiles)
            {
                unreal.EnsureLive(opponent);
                WriteBool(opponent, "Invulnerable", false);
                WriteBool(opponent, "Give Up", false);
                WriteBool(opponent, "Vertex Paint Saving Initiated", false);
                WriteBool(opponent, "Spawn On Death Blood Emitter", false);
                Set(opponent, "Health", 50);
                Set(opponent, "Head Health", 50);
                Set(opponent, "Neck Health", 50);
                Set(opponent, "Heart Health", 50);
                Set(opponent, "Consciousness", 50);
                Set(opponent, "Consciousness Cap", 100);
            }
            foreach (var ai in bindings.Controllers)
            {
                unreal.EnsureLive(ai);
                WriteBool(ai, "My Fallen", false);
                WriteBool(ai, "My Give Up", false);
                WriteBool(ai, "DED", false);
            }
            Thread.Sleep(300);
            EnsurePlayer();
            foreach (var opponent in bindings.Hostiles)
            {
                unreal.EnsureLive(opponent);
                WriteBool(opponent, "Invulnerable", false);
                Set(opponent, "Health", -100);
                Set(opponent, "Head Health", -100);
                Set(opponent, "Neck Health", -100);
                Set(opponent, "Heart Health", -100);
                Set(opponent, "Consciousness", -100);
            }
            Thread.Sleep(500);
        }

        EnsurePlayer();
        WriteBool(player, "Vertex Paint Saving Initiated", false);
        WriteBool(player, "Match Won (Player)", true);
        if (bindings.GameMode is { } gameMode)
        {
            unreal.EnsureLive(gameMode);
            var enemyCount = unreal.Property(gameMode, "Enemy Count", "IntProperty");
            Put(gameMode, enemyCount, BitConverter.GetBytes(0));
            WriteBool(gameMode, "All Enemies Dead", true);
            WriteBool(gameMode, "Match Won", true);
        }
        notify("Forced win / unstuck current match.");
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
            catch (Exception ex) when (ex is GameTransitionException or GameUnavailableException)
            {
                notify($"Skipped restoring {name} on unloaded object 0x{value.Object.Address:X}: {ex.Message}");
            }
            overrides.Remove(pair.Key);
        }
    }

    public void Apply(TrainerOptions options)
    {
        options.Validate();
        EnsureSession();
        if (combat == null && DateTime.UtcNow >= nextCombatScan) BindCombat();
        if (combat != null) EnsurePlayer();
        if (options.RankReputation)
        {
            var property = unreal.Property(instance, "Reputation on Rank", "IntProperty");
            int current = memory.Read<int>(instance.Address + property.Offset);
            int target = options.ReputationTarget(current);
            if (current != target) Put(instance, property, BitConverter.GetBytes(target));
        }
        if (options.Speed && world != null)
            Put(world, unreal.Property(world, "TimeDilation", "FloatProperty"),
                BitConverter.GetBytes((float)options.SpeedValue), true);
        else if (!options.Speed) Restore("TimeDilation");
        if (combat == null) return;
        if (options.Invulnerable) SetInvulnerable(true); else Restore("Invulnerable");
        if (options.Health)
        {
            foreach (string name in HealthNames) Set(player, name, 100);
            Set(player, "Bleeding", 0);
        }
        if (options.Stamina) Set(player, "Stamina", 100);
        if (options.NoExhaustion) Set(player, "Exhaustion", 0);
        if (options.Temperature) Set(player, "Temperature", 1);
        if (options.Consciousness)
            foreach (string name in ConsciousnessNames) Set(player, name, 100);
        if (options.NoPain)
            foreach (string name in PainNames) Set(player, name, 0);
        if (options.Muscle) Set(player, "Muscle Power", options.MuscleValue, true); else Restore("Muscle Power");
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
                WriteBool(opponent, "Invulnerable", false);
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

    private GameProperty CareerPhysiqueProperty()
    {
        if (careerPhysiqueProperty != null) return careerPhysiqueProperty;
        var character = unreal.Property(instance, "Player Character", "StructProperty");
        long structure = memory.Read<long>(character.Metadata + 0x70);
        var members = unreal.Properties(structure).Where(p =>
            p.Name.StartsWith("Weight_", StringComparison.Ordinal)).ToList();
        if (members.Count != 1 || memory.Read<int>(structure + 0x58) != character.Size)
            throw new InvalidOperationException("Unsupported career character physique layout.");
        careerPhysiqueProperty = ComposeCareerPhysiqueProperty(character, members[0], memory.Read<int>(instance.Class + 0x58));
        return careerPhysiqueProperty;
    }

    internal static GameProperty ComposeCareerPhysiqueProperty(GameProperty character, GameProperty weight, int classSize)
    {
        if (character.Name != "Player Character" || character.Type != "StructProperty" ||
            character.Offset < 0x28 || character.Size < 8 || classSize < character.Offset ||
            character.Size > classSize - character.Offset ||
            !weight.Name.StartsWith("Weight_", StringComparison.Ordinal) ||
            weight.Type != "DoubleProperty" || weight.Size != 8 ||
            weight.Offset < 0 || weight.Offset > character.Size - weight.Size)
            throw new InvalidOperationException("Unsupported career character physique layout.");
        return weight with { Name = "Player Character.Weight", Offset = character.Offset + weight.Offset };
    }

    private bool CanEditCareerPhysique()
    {
        if (combat != null || localController == null) return false;
        var freeMode = BooleanProperty(instance, "Free Mode Activated");
        if (memory.Read<byte>(instance.Address + freeMode.Offset) != 0) return false;
        long pawn = Pointer(localController, "AcknowledgedPawn");
        if (Pointer(localController, "Pawn") != pawn) return false;
        return pawn == 0 || !unreal.IsA(unreal.Reference(pawn), "Willie_BP_C");
    }

    internal static void ValidateCareerPhysique(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Career physique must be between 0 and 1.");
    }

    public void SetCareerPhysique(double value)
    {
        ValidateCareerPhysique(value);
        EnsureSession();
        if (combat == null) BindCombat();
        if (!CanEditCareerPhysique())
            throw new GameTransitionException("Physique request cancelled. Apply it from a career menu outside a fight.");
        var property = CareerPhysiqueProperty();
        // Change only the spawn source; live morphs, masses, and joints must stay in sync.
        Put(instance, property, BitConverter.GetBytes(value));
        if (memory.Read<double>(instance.Address + property.Offset) != value)
            throw new InvalidOperationException("Game did not retain the requested career physique.");
        notify($"Career physique set to {value:F2} for the next spawn. No active fighter was changed; normal saves may persist it.");
    }

    public TrainerStatus Status()
    {
        EnsureSession();
        if (combat != null) EnsurePlayer();
        if (world != null) unreal.EnsureLive(world);
        int funds = memory.Read<int>(instance.Address + unreal.Property(instance, "Player Funds", "IntProperty").Offset);
        float? speed = world == null ? null : memory.Read<float>(world.Address + unreal.Property(world, "TimeDilation", "FloatProperty").Offset);
        int reputation = memory.Read<int>(instance.Address + unreal.Property(instance, "Reputation on Rank", "IntProperty").Offset);
        double physique = memory.Read<double>(instance.Address + CareerPhysiqueProperty().Offset);
        if (funds < 0 || (speed.HasValue && (!float.IsFinite(speed.Value) || speed <= 0)))
            throw new InvalidOperationException("Live funds or game speed validation failed.");
        if (!double.IsFinite(physique)) throw new InvalidOperationException("Live career physique validation failed.");
        return new(combat == null ? $"Attached to PID {ProcessId} | session ready; waiting for a fighter" :
            $"Attached to PID {ProcessId} | player 0x{player.Address:X}",
            combat == null ? null : Number(player, "Health"),
            combat == null ? null : Number(player, "Stamina"),
            combat == null ? null : Number(player, "Consciousness"),
            combat == null ? null : Number(player, "Muscle Power"), funds, speed, opponents.Count, combat != null,
            combat == null ? null : Number(player, "Exhaustion"), combat == null ? null : Number(player, "Temperature"), reputation,
            physique, CanEditCareerPhysique());
    }

    public void Verify(TextWriter log, bool writeTest)
    {
        log.WriteLine(Status());
        var physiqueProperty = CareerPhysiqueProperty();
        log.WriteLine($"PASS career physique source: {physiqueProperty.Name} at reflected offset 0x{physiqueProperty.Offset:X}; no physique writes.");
        if (combat == null)
        {
            log.WriteLine("PASS session without fighter: funds and rank reputation resolved without a possessed pawn.");
            if (writeTest) VerifySessionWrites(log);
            return;
        }
        foreach (string name in HealthNames.Concat(ConsciousnessNames).Concat(PainNames).Concat(["Stamina", "Muscle Power", "Exhaustion", "Temperature"]))
            log.WriteLine($"{name}: {Number(player, name):F3}");
        log.WriteLine($"Spawned physique: Muscle Rate={Number(player, "Muscle Rate"):G}; mass scale={Number(player, "Mass Scale (Set in BP)"):G}");
        var hostiles = unreal.AllObjects().Where(o => o.Address != player.Address &&
            o.Outer == player.Outer && unreal.IsA(o, "Willie_BP_C") && Hostile(o)).ToList();
        log.WriteLine($"Hostile NPCs resolved: {hostiles.Count}; player excluded; same-team allies excluded.");
        var matchBindings = ResolveForceWinBindings();
        log.WriteLine($"PASS force-win bindings: {matchBindings.Hostiles.Count} hostile character(s), {matchBindings.Controllers.Count} AI controller(s), game mode {(matchBindings.GameMode == null ? "optional/unavailable" : "validated")}.");
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
            Check(player, DoubleProperty(player, "Exhaustion"), BitConverter.GetBytes(0d));
            Check(player, DoubleProperty(player, "Temperature"), BitConverter.GetBytes(1d));
            foreach (string name in HealthNames.Concat(ConsciousnessNames).Concat(["Stamina"]))
                Check(player, DoubleProperty(player, name), BitConverter.GetBytes(100d));
            foreach (string name in PainNames) Check(player, DoubleProperty(player, name), BitConverter.GetBytes(0d));
            Check(player, DoubleProperty(player, "Muscle Power"), BitConverter.GetBytes(Number(player, "Muscle Power") + 1));
            Check(settings, unreal.Property(settings, "TimeDilation", "FloatProperty"), BitConverter.GetBytes(1.1f));
            int funds = Status().Funds;
            Check(instance, unreal.Property(instance, "Player Funds", "IntProperty"), BitConverter.GetBytes(funds == 1000000000 ? funds - 1 : funds + 1));
            var options = new TrainerOptions(Invulnerable: true, Health: true, Stamina: true, Consciousness: true,
                NoPain: true, Speed: true, SpeedValue: 1.1, Muscle: true, MuscleValue: 36, NoExhaustion: true, Temperature: true);
            for (int i = 0; i < 20; i++)
            {
                Apply(options);
                var status = Status();
                if (status.Health < 99 || status.Stamina < 99 || status.Consciousness < 99 ||
                    status.Muscle != 36 || status.Speed == null || Math.Abs(status.Speed.Value - 1.1) > 0.0001 ||
                    status.Exhaustion is null or > 0.01 || status.Temperature is null ||
                    Math.Abs(status.Temperature.Value - 1) > 0.05 ||
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

    private void VerifySessionWrites(TextWriter log)
    {
        foreach (string name in new[] { "Player Funds", "Reputation on Rank" })
        {
            var property = unreal.Property(instance, name, "IntProperty");
            byte[] original = memory.Read(instance.Address + property.Offset, property.Size);
            int current = BitConverter.ToInt32(original);
            int test = current == 1000000000 ? current - 1 : current + 1;
            try
            {
                if (name == "Player Funds") SetFunds(test);
                else Apply(new TrainerOptions(RankReputation: true, ReputationValue: test));
                if (memory.Read<int>(instance.Address + property.Offset) != test)
                    throw new InvalidOperationException($"Menu write failed: {name}");
                log.WriteLine($"PASS outside-fight write/readback: {name}.");
            }
            finally { Put(instance, property, original); }
            if (!memory.Read(instance.Address + property.Offset, original.Length).AsSpan().SequenceEqual(original))
                throw new InvalidOperationException($"Menu restoration failed: {name}");
        }
    }

    public void Stop()
    {
        if (memory.Process.HasExited)
        {
            if (overrides.Count > 0) notify("Game exited normally; discarded overrides for the terminated process without writing.");
            overrides.Clear();
            return;
        }
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
