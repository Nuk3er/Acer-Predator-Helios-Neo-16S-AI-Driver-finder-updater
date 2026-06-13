using System.IO;
using System.Diagnostics;
using System.Management;
using System.Windows.Threading;
using Serilog;

namespace HeliosToolkit.App.Services.Boost;

/// <summary>
/// Watches for configured game exes starting/stopping. Uses WMI process-trace
/// events (instant, admin-only — we always have it) with a periodic reconciliation
/// poll, and falls back to pure polling if the watcher can't start (debloated OS).
/// </summary>
public sealed class GameWatchService(BoostConfigStore store) : IDisposable
{
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private DispatcherTimer? _reconcileTimer;
    private readonly HashSet<string> _runningWatched = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _watchedExes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when the first watched game starts (exe name).</summary>
    public event Action<string>? FirstGameStarted;

    /// <summary>Raised (after a debounce) when the last watched game exits.</summary>
    public event Action? LastGameStopped;

    public bool IsWatching { get; private set; }

    public void Start()
    {
        Stop();
        _watchedExes = store.LoadConfig().WatchedGames
            .Select(g => g.ExeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_watchedExes.Count == 0)
        {
            return;
        }

        // Seed with already-running games so an immediate exit still balances.
        foreach (string exe in _watchedExes)
        {
            if (IsRunning(exe))
            {
                _runningWatched.Add(exe);
            }
        }

        try
        {
            _startWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _startWatcher.EventArrived += (_, e) => OnProcessEvent(e, started: true);
            _startWatcher.Start();

            _stopWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            _stopWatcher.EventArrived += (_, e) => OnProcessEvent(e, started: false);
            _stopWatcher.Start();

            Log.Information("Game watcher active (WMI) for {Count} game(s)", _watchedExes.Count);
        }
        catch (Exception e)
        {
            Log.Warning(e, "WMI process trace unavailable; using polling fallback");
            DisposeWatchers();
        }

        // Reconciliation / fallback poll (30 s normally, also the sole mechanism if WMI failed).
        _reconcileTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _reconcileTimer.Tick += (_, _) => Reconcile();
        _reconcileTimer.Start();
        IsWatching = true;
    }

    public void Stop()
    {
        DisposeWatchers();
        _reconcileTimer?.Stop();
        _reconcileTimer = null;
        _runningWatched.Clear();
        IsWatching = false;
    }

    private void OnProcessEvent(EventArrivedEventArgs e, bool started)
    {
        try
        {
            string? name = e.NewEvent["ProcessName"]?.ToString();
            if (name is null || !_watchedExes.Contains(name))
            {
                return;
            }

            if (started)
            {
                bool wasEmpty = _runningWatched.Count == 0;
                _runningWatched.Add(name);
                if (wasEmpty)
                {
                    FirstGameStarted?.Invoke(name);
                }
            }
            else
            {
                _runningWatched.Remove(name);
                if (_runningWatched.Count == 0)
                {
                    DebounceLastStopped();
                }
            }
        }
        catch (ManagementException ex)
        {
            Log.Debug(ex, "Process event read failed");
        }
    }

    /// <summary>Re-syncs against reality in case a start/stop event was missed.</summary>
    private void Reconcile()
    {
        bool hadGames = _runningWatched.Count > 0;
        _runningWatched.Clear();
        foreach (string exe in _watchedExes)
        {
            if (IsRunning(exe))
            {
                _runningWatched.Add(exe);
            }
        }

        bool hasGames = _runningWatched.Count > 0;
        if (!hadGames && hasGames)
        {
            FirstGameStarted?.Invoke(_runningWatched.First());
        }
        else if (hadGames && !hasGames)
        {
            DebounceLastStopped();
        }
    }

    private CancellationTokenSource? _debounceCts;

    private void DebounceLastStopped()
    {
        // 10 s grace so a launcher respawn (Steam relaunching the game) doesn't flap.
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        CancellationToken token = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
                if (_watchedExes.All(exe => !IsRunning(exe)))
                {
                    LastGameStopped?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private static bool IsRunning(string exeName)
    {
        string baseName = Path.GetFileNameWithoutExtension(exeName);
        try
        {
            Process[] processes = Process.GetProcessesByName(baseName);
            bool any = processes.Length > 0;
            foreach (Process p in processes)
            {
                p.Dispose();
            }

            return any;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void DisposeWatchers()
    {
        try
        {
            _startWatcher?.Stop();
            _startWatcher?.Dispose();
            _stopWatcher?.Stop();
            _stopWatcher?.Dispose();
        }
        catch (ManagementException e)
        {
            Log.Debug(e, "Watcher dispose failed");
        }
        finally
        {
            _startWatcher = null;
            _stopWatcher = null;
        }
    }

    public void Dispose() => Stop();
}
