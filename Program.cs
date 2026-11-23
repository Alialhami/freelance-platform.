namespace FreelancePlatform;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length > 0 && args[0] == "--self-test")
        {
            var log = args.Length > 1 ? args[1] : "test-results.txt";
            try { return SelfTests.Run(log); }
            catch (Exception ex) { File.WriteAllText(log, ex.ToString()); return 1; }
        }
        if (args.Length > 0 && args[0] == "--evidence") return SelfTests.Evidence(args.Length > 1 ? args[1] : "evidence");
        // One process owns the local store; this prototype does not provide network access.
        using var mutex = new Mutex(true, "Local\\NCC1013FreelancePlatform", out bool created);
        if (!created) { MessageBox.Show("Freelance Platform is already running."); return 0; }
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NCCFreelancePlatform", "platform.json");
        var repository = new JsonPlatformRepository(path);
        try { repository.Read(); Application.Run(new LoginForm(repository)); return 0; }
        catch (Exception ex) { MessageBox.Show(ex.Message + "\nData location: " + path, "Unable to open Freelance Platform", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        finally { mutex.ReleaseMutex(); }
    }
}
