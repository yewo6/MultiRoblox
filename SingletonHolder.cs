using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MultiRoblox;

public sealed class SingletonHolder : IDisposable
{
    public const string MutexName = "ROBLOX_singletonMutex";
    public const string EventName = "ROBLOX_singletonEvent";

    private readonly object _sync = new();
    private Thread? _holderThread;
    private ManualResetEventSlim? _shutdown;
    private IntPtr _mutexHandle = IntPtr.Zero;
    private IntPtr _eventHandle = IntPtr.Zero;
    private bool _disposed;

    public bool IsHolding { get; private set; }
    public string StatusMessage { get; private set; } = "Idle";

    public event EventHandler? StateChanged;

    public bool TryStart()
    {
        lock (_sync)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SingletonHolder));

            if (IsHolding)
                return true;

            if (IsRobloxRunning())
            {
                StatusMessage = "Roblox already running and owns the lock. Close all Roblox windows, then enable again.";
                RaiseStateChanged();
                return false;
            }

            _shutdown = new ManualResetEventSlim(false);
            var ready = new ManualResetEventSlim(false);
            var acquired = false;
            string? error = null;

            _holderThread = new Thread(() =>
            {
                try
                {
                    _mutexHandle = CreateMutexW(IntPtr.Zero, true, MutexName);
                    if (_mutexHandle == IntPtr.Zero)
                    {
                        error = $"CreateMutex failed ({Marshal.GetLastWin32Error()}).";
                        ready.Set();
                        return;
                    }

                    int lastError = Marshal.GetLastWin32Error();
                    if (lastError == ERROR_ALREADY_EXISTS)
                        WaitForSingleObject(_mutexHandle, 0);

                    _eventHandle = CreateEventW(IntPtr.Zero, true, false, EventName);
                    if (_eventHandle == IntPtr.Zero)
                        _eventHandle = IntPtr.Zero;

                    acquired = true;
                    IsHolding = true;
                    StatusMessage = "Holding singleton lock - multi-instance enabled.";
                    ready.Set();
                    RaiseStateChanged();

                    _shutdown!.Wait();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    ready.Set();
                }
                finally
                {
                    ReleaseNativeHandles();
                    IsHolding = false;
                    if (string.IsNullOrEmpty(error))
                        StatusMessage = "Multi-instance disabled.";
                    RaiseStateChanged();
                }
            })
            {
                IsBackground = true,
                Name = "RobloxSingletonHolder",
            };

            _holderThread.Start();
            ready.Wait(TimeSpan.FromSeconds(3));

            if (!acquired)
            {
                _shutdown.Set();
                _holderThread.Join(1000);
                _shutdown.Dispose();
                _shutdown = null;
                _holderThread = null;
                StatusMessage = error ?? "Failed to acquire singleton mutex.";
                RaiseStateChanged();
                return false;
            }

            return true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_shutdown is null)
                return;

            _shutdown.Set();
            _holderThread?.Join(2000);
            _holderThread = null;
            _shutdown.Dispose();
            _shutdown = null;

            if (IsHolding)
            {
                IsHolding = false;
                StatusMessage = "Multi-instance disabled.";
                RaiseStateChanged();
            }
        }
    }

    public static bool IsRobloxRunning()
    {
        return Process.GetProcessesByName("RobloxPlayerBeta").Length > 0
            || Process.GetProcessesByName("Roblox").Length > 0;
    }

    public static int CloseAllRobloxProcesses()
    {
        int closed = 0;
        foreach (var name in new[] { "RobloxPlayerBeta", "Roblox" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(1500))
                        process.Kill(entireProcessTree: true);
                    closed++;
                }
                catch
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        closed++;
                    }
                    catch
                    {
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        Thread.Sleep(400);
        return closed;
    }

    private void ReleaseNativeHandles()
    {
        if (_mutexHandle != IntPtr.Zero)
        {
            ReleaseMutex(_mutexHandle);
            CloseHandle(_mutexHandle);
            _mutexHandle = IntPtr.Zero;
        }

        if (_eventHandle != IntPtr.Zero)
        {
            CloseHandle(_eventHandle);
            _eventHandle = IntPtr.Zero;
        }
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

    private const int ERROR_ALREADY_EXISTS = 183;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateMutexW(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEventW(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReleaseMutex(IntPtr hMutex);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
