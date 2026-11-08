using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MultiRoblox;

public sealed class AntiAfkService : IDisposable
{
    private readonly object _sync = new();
    private readonly Random _rng = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;

    public bool IsEnabled { get; private set; }
    public string StatusMessage { get; private set; } = "Anti-AFK off.";
    public DateTime? LastPulseUtc { get; private set; }
    public int LastWindowCount { get; private set; }

    public event EventHandler? StateChanged;

    public void Start()
    {
        lock (_sync)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AntiAfkService));
            if (IsEnabled)
                return;

            _cts = new CancellationTokenSource();
            IsEnabled = true;
            StatusMessage = "Anti-AFK on - waiting for first pulse.";
            RaiseStateChanged();
            _loop = Task.Run(() => RunLoop(_cts.Token));
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!IsEnabled)
                return;

            _cts?.Cancel();
            try
            {
                _loop?.Wait(2000);
            }
            catch
            {
            }

            _cts?.Dispose();
            _cts = null;
            _loop = null;
            IsEnabled = false;
            StatusMessage = "Anti-AFK off.";
            RaiseStateChanged();
        }
    }

    private async Task RunLoop(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), token);

            while (!token.IsCancellationRequested)
            {
                PulseAllClients();
                int seconds = _rng.Next(8 * 60, 12 * 60);
                StatusMessage = $"Anti-AFK on - next pulse in ~{seconds / 60}m ({LastWindowCount} window(s)).";
                RaiseStateChanged();
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Anti-AFK error: {ex.Message}";
            RaiseStateChanged();
        }
    }

    private void PulseAllClients()
    {
        var windows = GetRobloxWindows();
        LastWindowCount = windows.Count;
        LastPulseUtc = DateTime.UtcNow;

        if (windows.Count == 0)
        {
            StatusMessage = "Anti-AFK on - no Roblox windows found.";
            RaiseStateChanged();
            return;
        }

        IntPtr previous = GetForegroundWindow();
        int pulsed = 0;

        foreach (var hwnd in windows)
        {
            if (!IsWindow(hwnd))
                continue;

            try
            {
                NudgeWindow(hwnd, previous);
                pulsed++;
            }
            catch
            {
            }
        }

        if (previous != IntPtr.Zero && IsWindow(previous))
            ForceForeground(previous);

        StatusMessage = pulsed == 0
            ? "Anti-AFK on - no windows pulsed."
            : $"Anti-AFK on - pulsed {pulsed} client(s).";
        RaiseStateChanged();
    }

    private static void NudgeWindow(IntPtr hwnd, IntPtr restoreTo)
    {
        var placement = new WINDOWPLACEMENT();
        placement.length = Marshal.SizeOf<WINDOWPLACEMENT>();
        bool gotPlacement = GetWindowPlacement(hwnd, ref placement);
        bool wasMinimized = gotPlacement && placement.showCmd == SW_SHOWMINIMIZED;

        if (wasMinimized)
            ShowWindow(hwnd, SW_RESTORE);

        ForceForeground(hwnd);
        Thread.Sleep(40);
        SendTinyMouseJiggle();
        Thread.Sleep(40);

        if (wasMinimized)
            ShowWindow(hwnd, SW_MINIMIZE);

        if (restoreTo != IntPtr.Zero && restoreTo != hwnd && IsWindow(restoreTo))
            ForceForeground(restoreTo);
    }

    private static void SendTinyMouseJiggle()
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].U.mi = new MOUSEINPUT { dx = 1, dy = 0, dwFlags = MOUSEEVENTF_MOVE };
        inputs[1].type = INPUT_MOUSE;
        inputs[1].U.mi = new MOUSEINPUT { dx = -1, dy = 0, dwFlags = MOUSEEVENTF_MOVE };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void ForceForeground(IntPtr hwnd)
    {
        IntPtr foreground = GetForegroundWindow();
        uint targetThread = GetWindowThreadProcessId(hwnd, out _);
        uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
        uint currentThread = GetCurrentThreadId();

        bool attachedFront = false;
        bool attachedTarget = false;

        try
        {
            if (foregroundThread != currentThread)
                attachedFront = AttachThreadInput(currentThread, foregroundThread, true);
            if (targetThread != currentThread && targetThread != foregroundThread)
                attachedTarget = AttachThreadInput(currentThread, targetThread, true);

            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attachedTarget)
                AttachThreadInput(currentThread, targetThread, false);
            if (attachedFront)
                AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    private static List<IntPtr> GetRobloxWindows()
    {
        var handles = new List<IntPtr>();
        foreach (var name in new[] { "RobloxPlayerBeta", "Roblox" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    var hwnd = process.MainWindowHandle;
                    if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
                        handles.Add(hwnd);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return handles.Distinct().ToList();
    }

    private void RaiseStateChanged()
    {
        try
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const int SW_RESTORE = 9;
    private const int SW_MINIMIZE = 6;
    private const int SW_SHOWMINIMIZED = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public Rectangle rcNormalPosition;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
}
