using System.Security.Cryptography;
using System.Text;

namespace HalfSwordTrainer;

internal static class SaveRepairTests
{
    public static void Verify(TextWriter log)
    {
        string directory = Path.Combine(Path.GetTempPath(), "HalfSword-save-repair-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "GameProgress.sav");
            string backups = Path.Combine(directory, "backups");
            var original = Fixture(2);
            File.WriteAllBytes(path, original);
            int guards = 0;
            var result = SaveRepair.RepairFile(path, backups, () => guards++);
            if (guards != 3 || result.Before.DeadOpponents != 2 || result.BackupPath == null ||
                !File.ReadAllBytes(result.BackupPath).AsSpan().SequenceEqual(original) ||
                !File.ReadAllBytes(path).AsSpan().SequenceEqual(Fixture(0)))
                throw new InvalidOperationException("Save repair did not preserve the backup and all unrelated bytes.");
            var empty = SaveRepair.RepairFile(path, backups, () => { });
            if (empty.BackupPath != null || empty.Before.DeadOpponents != 0 ||
                !File.ReadAllBytes(path).AsSpan().SequenceEqual(Fixture(0)))
                throw new InvalidOperationException("Repairing an empty opponent array was not a no-op.");
            log.WriteLine("PASS save repair clears only the top-level opponent array, preserves all other bytes and a verified original backup; repeat repair is a no-op.");

            var malformed = new[]
            {
                Fixture(2, propertyName: "Other History"),
                Fixture(2, elementName: "WrongCharacterStruct"),
                Fixture(2, duplicate: true),
                Fixture(2, flags: 4),
                Fixture(-1),
                Fixture(0, extraPayload: true),
                original[..^1],
                original.Concat(new byte[] { 1 }).ToArray(),
                original.Select((b, i) => i == 12 ? (byte)0 : b).ToArray()
            };
            foreach (var bytes in malformed)
            {
                File.WriteAllBytes(path, bytes);
                try
                {
                    SaveRepair.RepairFile(path, backups, () => { });
                    throw new InvalidOperationException("An unsupported or damaged save was accepted.");
                }
                catch (InvalidDataException) { }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                    throw new InvalidOperationException("A rejected save was changed.");
            }
            log.WriteLine("PASS missing/duplicate/wrong-type arrays, unsupported flags/version, malformed counts and truncated/trailing data are refused without modifying the save.");

            File.WriteAllBytes(path, original);
            string notDirectory = Path.Combine(directory, "not-a-backup-directory");
            File.WriteAllText(notDirectory, "blocked");
            ExpectFailure(() => SaveRepair.RepairFile(path, notDirectory, () => { }));
            for (int failAt = 1; failAt <= 3; failAt++)
            {
                int checks = 0;
                ExpectFailure(() => SaveRepair.RepairFile(path, backups, () =>
                {
                    if (++checks == failAt) throw new InvalidOperationException("Simulated game launch.");
                }));
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
                    throw new InvalidOperationException("A game launch or backup failure did not leave the original intact.");
            }
            if (Directory.EnumerateFiles(directory, "*.tmp").Any())
                throw new InvalidOperationException("Repair left an unfinished temporary save behind.");
            log.WriteLine("PASS backup failures and game-closed checks at each repair stage abort without replacing the original; temporary files are cleaned.");
        }
        finally { Directory.Delete(directory, true); }
    }

    public static void VerifySaveCopy(TextWriter log)
    {
        string directory = Path.Combine(Path.GetTempPath(), "HalfSword-save-copy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string copy = Path.Combine(directory, "GameProgress.sav");
            byte[] originalHash;
            using (var original = File.Open(SaveRepair.SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originalHash = SHA256.HashData(original);
                original.Position = 0;
                using var output = File.Create(copy);
                original.CopyTo(output);
            }
            var result = SaveRepair.RepairFile(copy, Path.Combine(directory, "backups"), () => { });
            var actual = SaveRepair.Inspect(copy);
            if (actual.DeadOpponents != 0 || actual.FileBytes != result.Before.RepairedBytes)
                throw new InvalidOperationException("Real-save copy repair failed.");
            if (result.BackupPath != null)
            {
                using var backup = File.OpenRead(result.BackupPath);
                if (!SHA256.HashData(backup).AsSpan().SequenceEqual(originalHash))
                    throw new InvalidOperationException("Real-save copy backup differs from the original.");
            }
            using var unchanged = File.OpenRead(SaveRepair.SavePath);
            if (!SHA256.HashData(unchanged).AsSpan().SequenceEqual(originalHash))
                throw new IOException("The original save changed while testing its copy.");
            log.WriteLine($"PASS real-save copy: {result.Before.DeadOpponents} -> 0 Dead Opponents; " +
                $"{result.Before.FileBytes:N0} -> {actual.FileBytes:N0} bytes. Backup hash and unrelated bytes verified; original save unchanged.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("An unsafe save repair unexpectedly succeeded.");
    }

    private static byte[] Fixture(int count, string propertyName = "Dead Opponents",
        string elementName = "Str_Passport_Character1", bool duplicate = false, byte flags = 0, bool extraPayload = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(0x53415647);
        writer.Write(3);
        writer.Write(522);
        writer.Write(1012);
        writer.Write(new byte[10]);
        WriteString(writer, "++Halfsword+Main");
        writer.Write(3);
        writer.Write(0);
        WriteString(writer, "/Game/Blueprints/SaveGame/SG_GameProgress.SG_GameProgress_C");
        writer.Write((byte)0);
        WriteProperty(writer, "Player Character", "StrProperty", Encoding.UTF8.GetBytes("nested Dead Opponents decoy"));
        WriteProperty(writer, "Player Funds", "IntProperty", BitConverter.GetBytes(12345));
        WriteOpponents();
        if (duplicate) WriteOpponents();
        WriteProperty(writer, "Reputation on Rank", "IntProperty", BitConverter.GetBytes(1806));
        WriteProperty(writer, "Inventory \u00c4", "StrProperty", [0, 4, 9, 255], wide: true);
        WriteString(writer, "None");
        writer.Write(0);
        return stream.ToArray();

        void WriteOpponents()
        {
            WriteString(writer, propertyName);
            WriteString(writer, "ArrayProperty");
            writer.Write(1);
            WriteString(writer, "StructProperty");
            writer.Write(2);
            WriteString(writer, elementName);
            writer.Write(1);
            WriteString(writer, "/Game/Blueprints/Structure/Passports/Str_Passport_Character1");
            writer.Write(0);
            WriteString(writer, "186c699a-4d6a-f0e8-1522-929b3295615b");
            writer.Write(0);
            writer.Write(4 + Math.Max(0, count) * 9 + (extraPayload ? 1 : 0));
            writer.Write(flags);
            writer.Write(count);
            for (int i = 0; i < count; i++) WriteString(writer, "None");
            if (extraPayload) writer.Write((byte)0);
        }
    }

    private static void WriteProperty(BinaryWriter writer, string name, string type, byte[] payload, bool wide = false)
    {
        WriteString(writer, name, wide);
        WriteString(writer, type);
        writer.Write(0);
        writer.Write(payload.Length);
        writer.Write((byte)0);
        writer.Write(payload);
    }

    private static void WriteString(BinaryWriter writer, string value, bool wide = false)
    {
        writer.Write(wide ? -(value.Length + 1) : Encoding.UTF8.GetByteCount(value) + 1);
        writer.Write((wide ? Encoding.Unicode : Encoding.UTF8).GetBytes(value + "\0"));
    }
}
