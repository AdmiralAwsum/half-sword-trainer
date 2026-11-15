using System.Text;

namespace HalfSwordTrainer;

internal sealed record GameObject(long Address, int Index, string Name, long Class, long Outer, int Serial);
internal sealed record GameProperty(string Name, string Type, int Offset, int Size, long Metadata);

internal sealed class Unreal
{
    public Memory Memory { get; }
    public long Names { get; }
    public long Objects { get; }
    private readonly Dictionary<int, string> names = [];
    private readonly Dictionary<long, Dictionary<string, GameProperty>> properties = [];
    private readonly Dictionary<long, HashSet<string>> ancestors = [];

    public Unreal(Memory memory, bool recoveringKnownProcess = false)
    {
        Memory = memory;
        var section = memory.Sections().Single(s => s.Name == ".data");
        var data = memory.Read(section.Address, section.Size);
        for (int i = 16; i < data.Length - 32; i += 8)
        {
            long ptr = BitConverter.ToInt64(data, i);
            if (ptr < 0x100000000 || ptr > 0x7fffffffffff) continue;
            if (Names == 0 && (ptr & 0xffff) == 0 &&
                BitConverter.ToInt32(data, i - 8) is >= 0 and < 8192 &&
                BitConverter.ToInt32(data, i - 4) is > 0 and < 0x20000 &&
                memory.TryRead(ptr, 8, out var first) && (BitConverter.ToUInt16(first) >> 6) == 4 &&
                Encoding.ASCII.GetString(first, 2, 4) == "None")
                Names = section.Address + i - 16;
            int max = BitConverter.ToInt32(data, i + 16), num = BitConverter.ToInt32(data, i + 20);
            int maxChunks = BitConverter.ToInt32(data, i + 24), chunks = BitConverter.ToInt32(data, i + 28);
            if (Objects == 0 && num is > 10000 and < 5000000 && max >= num &&
                maxChunks is > 0 and < 1000 && max == maxChunks * 65536 &&
                chunks == (num + 65535) / 65536 && memory.TryRead(ptr, 8, out var chunk) &&
                memory.TryRead(BitConverter.ToInt64(chunk), 24, out var item) &&
                memory.TryRead(BitConverter.ToInt64(item), 40, out var obj) && BitConverter.ToInt32(obj, 12) == 0)
                Objects = section.Address + i;
            if (Names != 0 && Objects != 0) break;
        }
        if (Names == 0 || Objects == 0)
        {
            if (memory.Process.HasExited) throw new GameUnavailableException();
            if (memory.Process.MainWindowHandle == 0) throw new GameUnavailableException();
            if (recoveringKnownProcess || DateTime.Now - memory.Process.StartTime < TimeSpan.FromSeconds(30))
                throw new GameTransitionException("Waiting for Unreal runtime data during startup, loading, or shutdown.");
            throw new InvalidOperationException("Unsupported game layout: Unreal name pool/object array could not be validated.");
        }
        if (Name(0) != "None") throw new InvalidOperationException("Unreal name pool validation failed.");
    }

    public string Name(int id)
    {
        if (names.TryGetValue(id, out var result)) return result;
        uint index = unchecked((uint)id);
        if ((index >> 16) >= 8192) throw new InvalidOperationException($"Invalid Unreal name index {id}.");
        long block = Memory.Read<long>(Names + 16 + (index >> 16) * 8);
        long entry = block + (index & 65535) * 2;
        ushort header = Memory.Read<ushort>(entry);
        int length = header >> 6;
        if (length > 1023) throw new InvalidOperationException("Invalid Unreal name length.");
        var text = Memory.Read(entry + 2, length * ((header & 1) != 0 ? 2 : 1));
        result = (header & 1) != 0 ? Encoding.Unicode.GetString(text) : Encoding.UTF8.GetString(text);
        names[id] = result;
        return result;
    }

    public string ObjectName(long address) => address == 0 ? "(null)" : Name(Memory.Read<int>(address + 0x18));

    public List<GameObject> AllObjects(bool includeTemplates = false)
    {
        int count = Memory.Read<int>(Objects + 20);
        if (count is < 1 or > 5000000) throw new InvalidOperationException("Invalid Unreal object count.");
        long chunks = Memory.Read<long>(Objects);
        var results = new List<GameObject>(count);
        for (int start = 0; start < count; start += 65536)
        {
            long chunk = Memory.Read<long>(chunks + (start / 65536) * 8);
            var entries = Memory.Read(chunk, Math.Min(65536, count - start) * 24);
            for (int j = 0; j < entries.Length; j += 24)
            {
                long address = BitConverter.ToInt64(entries, j);
                if (address == 0) continue;
                if (!Memory.TryRead(address, 40, out var obj)) continue; // Objects may be collected during enumeration.
                if (BitConverter.ToInt32(obj, 12) != start + j / 24) continue;
                if ((BitConverter.ToUInt32(obj, 8) & (includeTemplates ? 0x18000 : 0x18030)) != 0) continue;
                results.Add(new(address, start + j / 24, Name(BitConverter.ToInt32(obj, 24)),
                    BitConverter.ToInt64(obj, 16), BitConverter.ToInt64(obj, 32), BitConverter.ToInt32(entries, j + 16)));
            }

        }
        return results;
    }

    public GameObject Reference(long address)
    {
        var obj = Memory.Read(address, 40);
        int index = BitConverter.ToInt32(obj, 12);
        var item = Item(index);
        var result = new GameObject(address, index, Name(BitConverter.ToInt32(obj, 24)),
            BitConverter.ToInt64(obj, 16), BitConverter.ToInt64(obj, 32), BitConverter.ToInt32(item, 16));
        EnsureLive(result);
        return result;
    }

    private byte[] Item(int index)
    {
        int count = Memory.Read<int>(Objects + 20);
        if (index < 0 || index >= count) throw new GameTransitionException("Object no longer exists.");
        long chunks = Memory.Read<long>(Objects);
        long chunk = Memory.Read<long>(chunks + (index / 65536) * 8);
        return Memory.Read(chunk + (index % 65536) * 24, 24);
    }

    public void EnsureLive(GameObject obj)
    {
        var item = Item(obj.Index);
        var header = Memory.Read(obj.Address, 40);
        if (BitConverter.ToInt64(item) != obj.Address || BitConverter.ToInt32(item, 16) != obj.Serial ||
            BitConverter.ToInt32(header, 12) != obj.Index || BitConverter.ToInt64(header, 16) != obj.Class ||
            (BitConverter.ToUInt32(header, 8) & 0x18030) != 0 ||
            (BitConverter.ToUInt32(item, 8) & 0x30200000) != 0)
            throw new GameTransitionException("Game objects changed or are being destroyed. Waiting for the new scene.");
    }

    public bool IsA(GameObject obj, string className)
    {
        if (ancestors.TryGetValue(obj.Class, out var classes)) return classes.Contains(className);
        classes = [];
        long type = obj.Class;
        var seen = new HashSet<long>();
        while (type != 0 && seen.Add(type))
        {
            classes.Add(ObjectName(type));
            type = Memory.Read<long>(type + 0x40);
        }
        ancestors.Add(obj.Class, classes);
        return classes.Contains(className);
    }

    public GameProperty Property(GameObject obj, string name, string kind)
    {
        if (!properties.TryGetValue(obj.Class, out var props))
        {
            props = [];
            foreach (var p in Properties(obj.Class)) props.TryAdd(p.Name, p);
            properties.Add(obj.Class, props);
        }
        if (!props.TryGetValue(name, out var property) || property.Type != kind ||
            property.Offset < 0x28 || property.Offset + property.Size > Memory.Read<int>(obj.Class + 0x58))
            throw new InvalidOperationException($"Unsupported build: {ObjectName(obj.Class)}.{name} is not a valid {kind}.");
        return property;
    }
    public IEnumerable<GameProperty> Properties(long type)
    {
        var visited = new HashSet<long>();
        while (type != 0 && visited.Add(type))
        {
            long field = Memory.Read<long>(type + 0x50);
            var fields = new HashSet<long>();
            while (field != 0 && fields.Add(field))
            {
                var bytes = Memory.Read(field, 0x78);
                string name = Name(BitConverter.ToInt32(bytes, 0x20));
                long fieldClass = BitConverter.ToInt64(bytes, 8);
                string kind = Name(Memory.Read<int>(fieldClass));
                yield return new(name, kind, BitConverter.ToInt32(bytes, 0x44),
                    BitConverter.ToInt32(bytes, 0x34), field);
                field = BitConverter.ToInt64(bytes, 0x18);
            }
            type = Memory.Read<long>(type + 0x40);
        }
    }
}
