namespace MultiRoblox;

internal static class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MultiRoblox",
        "settings.txt");

    public static bool StartWithWindows { get; set; }
    public static bool StartEnabled { get; set; } = true;
    public static bool StartMinimizedToTray { get; set; } = true;
    public static bool AntiAfkEnabled { get; set; }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;

            foreach (var line in File.ReadAllLines(FilePath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                    continue;

                switch (parts[0].Trim())
                {
                    case "StartWithWindows":
                        StartWithWindows = parts[1].Trim().Equals("1", StringComparison.Ordinal);
                        break;
                    case "StartEnabled":
                        StartEnabled = parts[1].Trim().Equals("1", StringComparison.Ordinal);
                        break;
                    case "StartMinimizedToTray":
                        StartMinimizedToTray = parts[1].Trim().Equals("1", StringComparison.Ordinal);
                        break;
                    case "AntiAfkEnabled":
                        AntiAfkEnabled = parts[1].Trim().Equals("1", StringComparison.Ordinal);
                        break;
                }
            }
        }
        catch
        {
        }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllLines(FilePath,
            [
                $"StartWithWindows={(StartWithWindows ? 1 : 0)}",
                $"StartEnabled={(StartEnabled ? 1 : 0)}",
                $"StartMinimizedToTray={(StartMinimizedToTray ? 1 : 0)}",
                $"AntiAfkEnabled={(AntiAfkEnabled ? 1 : 0)}",
            ]);
        }
        catch
        {
        }
    }

    public static void ApplyStartupShortcut()
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, writable: true)
            ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(runKey);

        if (StartWithWindows)
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            key.SetValue("MultiRoblox", $"\"{exe}\" --tray");
        }
        else
        {
            key.DeleteValue("MultiRoblox", throwOnMissingValue: false);
        }
    }
}
