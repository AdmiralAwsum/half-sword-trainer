using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace HalfSwordTrainer;

internal sealed record SaveBloatInfo(long FileBytes, int DeadOpponents, int OpponentBytes)
{
    public long RepairedBytes => FileBytes - OpponentBytes + sizeof(int);
}

internal sealed record SaveRepairResult(SaveBloatInfo Before, string? BackupPath);

internal static class SaveRepair
{
    private const string SaveClass = "/Game/Blueprints/SaveGame/SG_GameProgress.SG_GameProgress_C";
    private const string CharacterStruct = "/Game/Blueprints/Structure/Passports/Str_Passport_Character1";
    private sealed record PropertyType(string Name, List<PropertyType> Parameters);
    private sealed record Layout(SaveBloatInfo Info, long SizeOffset, long DataOffset);
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly Encoding Utf16 = new UnicodeEncoding(false, false, true);

    public static string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalfSwordUE5", "Saved", "SaveGames", "GameProgress.sav");

    public static void EnsureGameClosed()
    {
        foreach (string name in new[] { "HalfSwordUE5-Win64-Shipping", "HalfSwordUE5" })
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.Any(p => !p.HasExited))
                    throw new InvalidOperationException("Close Half Sword completely before repairing its save. Do not launch it until the repair finishes.");
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }
    }

    public static SaveBloatInfo Inspect(string path)
    {
        using var source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadLayout(source).Info;
    }

    public static SaveRepairResult Repair() => RepairFile(SavePath,
        Path.Combine(AppContext.BaseDirectory, "backups"), EnsureGameClosed);

    internal static SaveRepairResult RepairFile(string path, string backupRoot, Action ensureGameClosed)
    {
        ensureGameClosed();
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string? backup = null;
        bool replaced = false;
        try
        {
            Layout layout;
            byte[] originalHash;
            byte[] repairedHash;
            using (var source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                layout = ReadLayout(source);
                if (layout.Info.DeadOpponents == 0) return new(layout.Info, null);
                string directory = Path.Combine(backupRoot, "save-repair-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                backup = Path.Combine(directory, "GameProgress.sav");
                using (var snapshot = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.Position = 0;
                    source.CopyTo(snapshot);
                    snapshot.Flush(true);
                }
                originalHash = Hash(source);
                using (var snapshot = File.OpenRead(backup))
                    if (!Hash(snapshot).AsSpan().SequenceEqual(originalHash))
                        throw new IOException("Save backup verification failed; the original save was not replaced.");

                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    source.Position = 0;
                    CopyBytes(source, output, layout.SizeOffset);
                    output.Write(BitConverter.GetBytes(sizeof(int)));
                    source.Position += sizeof(int);
                    CopyBytes(source, output, layout.DataOffset - source.Position);
                    output.Write(BitConverter.GetBytes(0));
                    source.Position = layout.DataOffset + layout.Info.OpponentBytes;
                    source.CopyTo(output);
                    output.Flush(true);
                    VerifyReplacement(source, output, layout);
                    repairedHash = Hash(output);
                }
                ensureGameClosed();
            }

            // Windows replacement needs delete sharing; keep in-place writers excluded.
            using (var current = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                ensureGameClosed();
                if (!Hash(current).AsSpan().SequenceEqual(originalHash))
                    throw new IOException("The save changed during repair. Nothing was replaced; close the game and retry.");
                File.Replace(temporary, path, null);
                replaced = true;
            }
            using (var current = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!Hash(current).AsSpan().SequenceEqual(repairedHash) ||
                    ReadLayout(current).Info != new SaveBloatInfo(layout.Info.RepairedBytes, 0, sizeof(int)))
                    throw new IOException("The installed repair did not pass readback verification.");
            }
            return new(layout.Info, backup);
        }
        catch (Exception ex) when (backup != null && ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            throw new IOException($"{(replaced ? "The save was replaced, but final verification failed." : "The original save was not replaced.")} " +
                $"Backup: {backup}\n{ex.Message}", ex);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static Layout ReadLayout(Stream stream)
    {
        try
        {
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Utf8, true);
            if (reader.ReadUInt32() != 0x53415647 || reader.ReadInt32() != 3 ||
                reader.ReadInt32() != 522 || reader.ReadInt32() != 1012)
                throw new InvalidDataException("Unsupported save format. Expected the verified Half Sword UE5 GVAS layout.");
            Skip(stream, 10);
            _ = ReadString(reader);
            if (reader.ReadInt32() != 3)
                throw new InvalidDataException("Unsupported save custom-version format.");
            int versions = reader.ReadInt32();
            if (versions is < 0 or > 4096) throw new InvalidDataException("Invalid save custom-version count.");
            Skip(stream, versions * 20L);
            if (ReadString(reader) != SaveClass || reader.ReadByte() != 0)
                throw new InvalidDataException("Not a supported SG_GameProgress_C save.");

            Layout? found = null;
            for (int properties = 0; properties < 4096; properties++)
            {
                string name = ReadString(reader);
                if (name == "None")
                {
                    if (stream.Length - stream.Position != sizeof(int) || reader.ReadInt32() != 0)
                        throw new InvalidDataException("Unexpected data after the save property terminator.");
                    return found ?? throw new InvalidDataException("The save has no top-level Dead Opponents property.");
                }
                if (name.Length == 0) throw new InvalidDataException("Empty save property name.");
                int budget = 256;
                var type = ReadType(reader, 0, ref budget);
                long sizeOffset = stream.Position;
                int size = reader.ReadInt32();
                byte flags = reader.ReadByte();
                if ((flags & ~0x13) != 0 || ((flags & 0x10) != 0 && type.Name != "BoolProperty"))
                    throw new InvalidDataException($"Unsupported serialization flags for {name}.");
                if ((flags & 1) != 0) Skip(stream, sizeof(int));
                if ((flags & 2) != 0) Skip(stream, 16);
                long dataOffset = stream.Position;
                if (size < 0 || size > stream.Length - dataOffset)
                    throw new InvalidDataException($"Invalid serialized size for {name}.");
                if (name == "Dead Opponents")
                {
                    if (found != null || flags != 0 || size < sizeof(int) || !IsCharacterArray(type))
                        throw new InvalidDataException("Unsupported or duplicate Dead Opponents array.");
                    int count = reader.ReadInt32();
                    if (count < 0 || (count == 0 ? size != sizeof(int) : count > (size - sizeof(int)) / 9))
                        throw new InvalidDataException("Invalid Dead Opponents array count or payload.");
                    found = new(new(stream.Length, count, size), sizeOffset, dataOffset);
                }
                stream.Position = dataOffset + size;
            }
            throw new InvalidDataException("Too many top-level save properties.");
        }
        catch (Exception ex) when (ex is EndOfStreamException or DecoderFallbackException)
        {
            throw new InvalidDataException("The save is truncated or contains invalid encoded strings.", ex);
        }
    }

    private static bool IsCharacterArray(PropertyType type)
    {
        if (type.Name != "ArrayProperty" || type.Parameters.Count != 1) return false;
        var element = type.Parameters[0];
        if (element.Name != "StructProperty" || element.Parameters.Count != 2) return false;
        var structure = element.Parameters[0];
        return structure.Name == "Str_Passport_Character1" && structure.Parameters.Count == 1 &&
            structure.Parameters[0].Name == CharacterStruct && structure.Parameters[0].Parameters.Count == 0 &&
            Guid.TryParse(element.Parameters[1].Name, out _) && element.Parameters[1].Parameters.Count == 0;
    }

    private static PropertyType ReadType(BinaryReader reader, int depth, ref int budget)
    {
        if (depth > 16 || --budget < 0) throw new InvalidDataException("Save property type is too complex.");
        string name = ReadString(reader);
        int count = reader.ReadInt32();
        if (name.Length == 0 || count is < 0 or > 16)
            throw new InvalidDataException("Invalid save property type.");
        List<PropertyType> parameters = [];
        for (int i = 0; i < count; i++) parameters.Add(ReadType(reader, depth + 1, ref budget));
        return new(name, parameters);
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length == 0) return "";
        if (length is < -4096 or > 4096) throw new InvalidDataException("Invalid save string length.");
        int byteCount = length > 0 ? length : -length * 2;
        var bytes = reader.ReadBytes(byteCount);
        if (bytes.Length != byteCount) throw new EndOfStreamException();
        if (bytes[^1] != 0 || (length < 0 && bytes[^2] != 0))
            throw new InvalidDataException("Unterminated save string.");
        return length > 0 ? Utf8.GetString(bytes, 0, byteCount - 1) : Utf16.GetString(bytes, 0, byteCount - 2);
    }

    private static void Skip(Stream stream, long count)
    {
        if (count < 0 || count > stream.Length - stream.Position) throw new EndOfStreamException();
        stream.Position += count;
    }

    private static byte[] Hash(Stream stream)
    {
        stream.Position = 0;
        return SHA256.HashData(stream);
    }

    private static void CopyBytes(Stream source, Stream destination, long count)
    {
        var buffer = new byte[81920];
        while (count > 0)
        {
            int read = source.Read(buffer, 0, (int)Math.Min(count, buffer.Length));
            if (read == 0) throw new EndOfStreamException();
            destination.Write(buffer, 0, read);
            count -= read;
        }
    }

    private static void VerifyReplacement(Stream original, Stream repaired, Layout layout)
    {
        var actual = ReadLayout(repaired);
        if (actual.Info != new SaveBloatInfo(layout.Info.RepairedBytes, 0, sizeof(int)) ||
            actual.SizeOffset != layout.SizeOffset || actual.DataOffset != layout.DataOffset)
            throw new InvalidDataException("Repaired array layout verification failed.");
        CompareBytes(original, repaired, 0, 0, layout.SizeOffset);
        CompareBytes(original, repaired, layout.SizeOffset + sizeof(int), layout.SizeOffset + sizeof(int),
            layout.DataOffset - layout.SizeOffset - sizeof(int));
        long suffix = layout.DataOffset + layout.Info.OpponentBytes;
        CompareBytes(original, repaired, suffix, layout.DataOffset + sizeof(int), original.Length - suffix);
    }

    private static void CompareBytes(Stream first, Stream second, long firstOffset, long secondOffset, long count)
    {
        first.Position = firstOffset;
        second.Position = secondOffset;
        var left = new byte[81920];
        var right = new byte[left.Length];
        while (count > 0)
        {
            int size = (int)Math.Min(count, left.Length);
            first.ReadExactly(left.AsSpan(0, size));
            second.ReadExactly(right.AsSpan(0, size));
            if (!left.AsSpan(0, size).SequenceEqual(right.AsSpan(0, size)))
                throw new InvalidDataException("Repair would change data outside Dead Opponents.");
            count -= size;
        }
    }
}
