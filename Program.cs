namespace MultiRoblox;

internal static class Program
{
    private const string AppMutexName = "Local\\MultiRoblox_SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        using var appMutex = new Mutex(true, AppMutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "MultiRoblox is already running in the system tray.",
                "MultiRoblox",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        AppSettings.Load();

        bool startInTray = args.Any(a =>
            a.Equals("--tray", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/tray", StringComparison.OrdinalIgnoreCase));

        using var holder = new SingletonHolder();
        using var antiAfk = new AntiAfkService();
        Application.Run(new MainForm(holder, antiAfk, startInTray));
    }
}
