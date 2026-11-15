namespace HalfSwordTrainer;

internal static class Diagnostics
{
    public static void Inspect(TextWriter log)
    {
        using var memory = new Memory();
        var unreal = new Unreal(memory);
        var all = unreal.AllObjects(true);
        var fields = all.Where(o => o.Name == "GI_Settings_C" && unreal.ObjectName(o.Class) == "BlueprintGeneratedClass")
            .Concat(all.Where(o => o.Name == "Willie_BP_C" && unreal.ObjectName(o.Class) == "BlueprintGeneratedClass"))
            .SelectMany(o => unreal.Properties(o.Address))
            .Where(p => p.Name is "Reputation on Rank" or "Exhaustion" or "Temperature" or "Muscle Power" or "Muscle Rate")
            .DistinctBy(p => p.Metadata).ToList();
        var objectNames = all.ToDictionary(o => o.Address, o => o.Name);
        foreach (var obj in all)
        {
            string kind = unreal.ObjectName(obj.Class);
            if (kind == "Function")
            {
                var header = memory.Read(obj.Address + 0x60, 16);
                int count = BitConverter.ToInt32(header, 8);
                if (count > 0 && count < 2000000)
                {
                    var script = memory.Read(BitConverter.ToInt64(header), count);
                    if (obj.Name == "Muscle Motor")
                    {
                        var propertyNames = unreal.Properties(obj.Address).Concat(unreal.Properties(obj.Outer))
                            .DistinctBy(p => p.Metadata).ToDictionary(p => p.Metadata, p => p.Name);
                        log.WriteLine($"MUSCLE-MOTOR {unreal.ObjectName(obj.Outer)} script {Convert.ToHexString(script)}");
                        for (int j = 0; j + 8 <= script.Length; j++)
                        {
                            long ptr = BitConverter.ToInt64(script, j);
                            if (propertyNames.TryGetValue(ptr, out var propertyName)) log.WriteLine($" MOTOR FIELD {j:X}: {propertyName}");
                            if (objectNames.TryGetValue(ptr, out var objectName)) log.WriteLine($" MOTOR CALL {j:X}: {objectName}");
                        }
                    }
                    if (obj.Name == "Generate Random Combat Event")
                    {
                        var propertyNames = unreal.Properties(obj.Address).Concat(fields).DistinctBy(p => p.Metadata)
                            .ToDictionary(p => p.Metadata, p => p.Name);
                        log.WriteLine($"GENERATOR {unreal.ObjectName(obj.Outer)} script {Convert.ToHexString(script)}");
                        for (int j = 0; j + 8 <= script.Length; j++)
                        {
                            long ptr = BitConverter.ToInt64(script, j);
                            if (propertyNames.TryGetValue(ptr, out var propertyName)) log.WriteLine($" GEN FIELD {j:X}: {propertyName}");
                            if (objectNames.TryGetValue(ptr, out var objectName)) log.WriteLine($" GEN CALL {j:X}: {objectName}");
                            if (script[j] == 0x1d && j + 5 <= script.Length) log.WriteLine($" GEN INT {j:X}: {BitConverter.ToInt32(script, j + 1)}");
                        }
                    }
                    foreach (var field in fields)
                    {
                        var needle = BitConverter.GetBytes(field.Metadata);
                        int start = 0;
                        while (start < script.Length)
                        {
                            int relative = script.AsSpan(start).IndexOf(needle);
                            if (relative < 0) break;
                            int at = start + relative;
                            int from = Math.Max(0, at - 70), to = Math.Min(script.Length, at + 100);
                            log.WriteLine($"REFERENCE {field.Name} in {unreal.ObjectName(obj.Outer)}.{obj.Name} at {at:X} of {count:X}: {Convert.ToHexString(script.AsSpan(from, to - from))}");
                            var referenceProperties = field.Name == "Muscle Rate"
                                ? unreal.Properties(obj.Address).Concat(unreal.Properties(obj.Outer)).DistinctBy(p => p.Metadata)
                                    .ToDictionary(p => p.Metadata, p => p.Name)
                                : null;
                            for (int j = from; j + 8 <= to; j++)
                            {
                                long ptr = BitConverter.ToInt64(script, j);
                                if (objectNames.TryGetValue(ptr, out var text)) log.WriteLine($" CALL {j:X}: {text}");
                                if (referenceProperties?.TryGetValue(ptr, out var propertyName) == true)
                                    log.WriteLine($" FIELD {j:X}: {propertyName}");
                                if (script[j] == 0x1e && j + 5 <= to) log.WriteLine($" FLOAT {j:X}: {BitConverter.ToSingle(script, j + 1):G}");
                                if (script[j] == 0x37 && j + 9 <= to) log.WriteLine($" DOUBLE {j:X}: {BitConverter.ToDouble(script, j + 1):G}");
                                if (script[j] == 0x1d && j + 5 <= to) log.WriteLine($" INT {j:X}: {BitConverter.ToInt32(script, j + 1)}");
                            }
                            start = at + 8;
                        }
                    }
                }
            }
            if (kind == "Function" && (obj.Name.Contains("Reput", StringComparison.OrdinalIgnoreCase) ||
                obj.Name.Contains("Tourn", StringComparison.OrdinalIgnoreCase) ||
                obj.Name.Contains("Temper", StringComparison.OrdinalIgnoreCase) ||
                obj.Name.Contains("Exhaust", StringComparison.OrdinalIgnoreCase)))
            {
                log.WriteLine($"FUNCTION {obj.Name} {obj.Address:X} outer={unreal.ObjectName(obj.Outer)}");
                log.WriteLine(Convert.ToHexString(memory.Read(obj.Address + 0x58, 0x60)));
            }
            if (!(kind is "Willie_BP_C" or "GI_Settings_C" or "SG_GameProgress_C" ||
                kind.Contains("Tourn", StringComparison.OrdinalIgnoreCase) ||
                kind.Contains("Rank", StringComparison.OrdinalIgnoreCase))) continue;
            log.WriteLine($"OBJECT {kind} {obj.Name} {obj.Address:X} outer={unreal.ObjectName(obj.Outer)}");
            foreach (var p in unreal.Properties(obj.Class))
            {
                if (kind == "Willie_BP_C" && !System.Text.RegularExpressions.Regex.IsMatch(p.Name,
                    "Stamina|Exhaust|Temper|Fatigue|Recover|Rest|Heat|Cold|Player|Controller|Muscle|Strength|Morph|Physique|Fat|Body|Scale|Height|Mass|Weight|Character|Willie", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                string value = p.Type switch
                {
                    "DoubleProperty" => memory.Read<double>(obj.Address + p.Offset).ToString("G"),
                    "FloatProperty" => memory.Read<float>(obj.Address + p.Offset).ToString("G"),
                    "IntProperty" => memory.Read<int>(obj.Address + p.Offset).ToString(),
                    "BoolProperty" or "ByteProperty" or "EnumProperty" => Convert.ToHexString(memory.Read(obj.Address + p.Offset, p.Size)),
                    "ObjectProperty" => unreal.ObjectName(memory.Read<long>(obj.Address + p.Offset)),
                    _ => p.Size <= 80 ? Convert.ToHexString(memory.Read(obj.Address + p.Offset, p.Size)) : ""
                };
                string boolLayout = p.Type == "BoolProperty" ? $" bool={Convert.ToHexString(memory.Read(p.Metadata + 0x70, 4))}" : "";
                log.WriteLine($" {p.Offset:X} {p.Type} {p.Name} [{p.Size}] = {value} meta={p.Metadata:X}{boolLayout}");
                if (p.Type == "StructProperty" && System.Text.RegularExpressions.Regex.IsMatch(p.Name,
                    "Rank|Reput|Character|Body|Appearance|Physique", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    long structure = memory.Read<long>(p.Metadata + 0x70);
                    foreach (var member in unreal.Properties(structure))
                    {
                        string memberValue = member.Type == "DoubleProperty"
                            ? $" = {memory.Read<double>(obj.Address + p.Offset + member.Offset):G}" : "";
                        log.WriteLine($"   MEMBER {member.Offset:X} {member.Type} {member.Name} [{member.Size}]{memberValue}");
                    }
                }
            }
        }
        foreach (var type in all.Where(o => unreal.ObjectName(o.Class) == "BlueprintGeneratedClass"))
        {
            var matches = unreal.Properties(type.Address).Where(p => System.Text.RegularExpressions.Regex.IsMatch(p.Name,
                "Reputation|Exhaustion|Temperature|Tournament|Fatigue|Muscle|Strength|Morph|Physique|Fatness|Body.?Size|Body.?Scale|Body.?Type", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
            if (matches.Count == 0) continue;
            log.WriteLine($"TYPE {type.Name} {type.Address:X}");
            foreach (var p in matches) log.WriteLine($" {p.Offset:X} {p.Type} {p.Name} [{p.Size}]");
        }
        foreach (var obj in all.Where(o =>
            unreal.ObjectName(o.Class) is "BP_HalfSwordGameMode_C" or "AI_BP_C" or "Willie_BP_C")
            .Take(24))
        {
            string kind = unreal.ObjectName(obj.Class);
            log.WriteLine($"MATCH-CONTROL {kind} {obj.Name} {obj.Address:X} outer={unreal.ObjectName(obj.Outer)}");
            foreach (var p in unreal.Properties(obj.Class).Where(p =>
                kind == "BP_HalfSwordGameMode_C" ||
                kind == "AI_BP_C" ||
                p.Name is "Invulnerable" or "Give Up" or "Vertex Paint Saving Initiated" or
                    "Spawn On Death Blood Emitter" or "Health" or "Head Health" or "Neck Health" or
                    "Heart Health" or "Consciousness" or "Consciousness Cap" or "Match Won (Player)"))
            {
                string value = p.Type switch
                {
                    "DoubleProperty" => memory.Read<double>(obj.Address + p.Offset).ToString("G"),
                    "IntProperty" => memory.Read<int>(obj.Address + p.Offset).ToString(),
                    "BoolProperty" when p.Size == 1 => memory.Read<byte>(obj.Address + p.Offset).ToString(),
                    _ => ""
                };
                string boolLayout = p.Type == "BoolProperty" ? $" bool={Convert.ToHexString(memory.Read(p.Metadata + 0x70, 4))}" : "";
                log.WriteLine($" {p.Offset:X} {p.Type} {p.Name} [{p.Size}] = {value} meta={p.Metadata:X}{boolLayout}");
            }
        }
    }
}
