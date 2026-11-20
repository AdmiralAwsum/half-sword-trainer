namespace HalfSwordTrainer;

internal static class Program
{
    public static void Log(string message) =>
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "trainer-errors.log"), $"{DateTime.Now:O} {message}\n");

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            ApplicationConfiguration.Initialize();
            using var log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "verification.txt"));
            try
            {
                TrainerForm.VerifyStaging(log).GetAwaiter().GetResult();
                SaveRepairTests.Verify(log);
            }
            catch (Exception ex) { log.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--verify-save-repair")
        {
            using var log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "verification.txt")) { AutoFlush = true };
            try { SaveRepairTests.VerifySaveCopy(log); }
            catch (Exception ex) { log.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] == "--inspect")
        {
            using var log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "inspection.txt"));
            try { Diagnostics.Inspect(log); }
            catch (Exception ex) { log.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length > 0 && args[0] is "--verify" or "--test-writes" or "--test-combat")
        {
            using var log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "verification.txt")) { AutoFlush = true };
            try
            {
                using var trainer = new Trainer();
                if (args[0] == "--test-combat") trainer.VerifyCombat(log);
                else trainer.Verify(log, args[0] == "--test-writes");
            }
            catch (Exception ex) { log.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new TrainerForm());
    }
}
