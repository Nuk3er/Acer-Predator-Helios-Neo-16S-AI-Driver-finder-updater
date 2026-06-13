using HeliosToolkit.Core.Lab;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Serilog;

namespace HeliosToolkit.App.Services.Lab;

public sealed record DpcDriverRow(string Module, long Count, double AvgUs, double MaxUs)
{
    public DpcVerdict Verdict => DpcAnalysis.Classify(MaxUs);
    public string Advice => DpcAnalysis.Advice(Module);
}

/// <summary>
/// LatencyMon-lite: a real-time kernel ETW session counting DPC/ISR execution
/// time per driver. Requires elevation (we always have it).
/// </summary>
public sealed class DpcMonitorService(KernelModuleMap moduleMap) : IDisposable
{
    private const string SessionName = "HeliosToolkit-DpcIsr";

    private readonly object _gate = new();
    private readonly Dictionary<string, (long Count, double SumUs, double MaxUs)> _byModule = new();
    private TraceEventSession? _session;
    private Task? _processTask;

    public bool IsRunning { get; private set; }

    /// <summary>Stops any stale session a previous crash left behind. Call at app start.</summary>
    public static void CleanupStaleSession()
    {
        try
        {
            if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
            {
                using var stale = new TraceEventSession(SessionName);
                stale.Stop();
                Log.Information("Stopped stale ETW session {Name}", SessionName);
            }
        }
        catch (Exception e)
        {
            Log.Debug(e, "Stale-session sweep failed (usually: none existed)");
        }
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        lock (_gate)
        {
            _byModule.Clear();
        }

        moduleMap.Snapshot();

        // Constructor stops + reopens an existing same-name session, so crashes self-heal.
        // Note: the keyword's historic spelling ("Defered") is the library's, not a typo here.
        _session = new TraceEventSession(SessionName);
        _session.EnableKernelProvider(
            KernelTraceEventParser.Keywords.DeferedProcedureCalls
            | KernelTraceEventParser.Keywords.Interrupt
            | KernelTraceEventParser.Keywords.ImageLoad);

        KernelTraceEventParser kernel = _session.Source.Kernel;
        kernel.PerfInfoDPC += OnDpc;
        kernel.PerfInfoTimerDPC += OnDpc;
        kernel.PerfInfoThreadedDPC += OnDpc;
        kernel.PerfInfoISR += OnIsr;
        kernel.ImageLoad += data => moduleMap.Add(data.ImageBase, data.ImageSize, data.FileName);

        _processTask = Task.Run(() =>
        {
            try
            {
                _session.Source.Process(); // blocks until Stop()
            }
            catch (Exception e)
            {
                Log.Warning(e, "ETW processing ended abnormally");
            }
        });

        IsRunning = true;
        Log.Information("DPC/ISR monitor started");
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            _session?.Stop();
            _processTask?.Wait(TimeSpan.FromSeconds(3));
            _session?.Dispose();
        }
        catch (Exception e)
        {
            Log.Warning(e, "DPC monitor stop failed");
        }
        finally
        {
            _session = null;
            _processTask = null;
            IsRunning = false;
            Log.Information("DPC/ISR monitor stopped");
        }
    }

    /// <summary>Current per-driver aggregation, worst first.</summary>
    public IReadOnlyList<DpcDriverRow> SnapshotRows(int top = 14)
    {
        lock (_gate)
        {
            return _byModule
                .Select(kv => new DpcDriverRow(kv.Key, kv.Value.Count, kv.Value.SumUs / kv.Value.Count, kv.Value.MaxUs))
                .OrderByDescending(r => r.MaxUs)
                .Take(top)
                .ToList();
        }
    }

    private void OnDpc(DPCTraceData data) => Record(data.Routine, data.ElapsedTimeMSec);

    private void OnIsr(ISRTraceData data) => Record(data.Routine, data.ElapsedTimeMSec);

    private void Record(ulong routine, double elapsedMs)
    {
        double us = elapsedMs * 1000.0;
        if (us is <= 0 or > 1_000_000)
        {
            return; // clock glitch — discard
        }

        string module = moduleMap.Lookup(routine);
        lock (_gate)
        {
            if (_byModule.TryGetValue(module, out (long Count, double SumUs, double MaxUs) agg))
            {
                _byModule[module] = (agg.Count + 1, agg.SumUs + us, Math.Max(agg.MaxUs, us));
            }
            else
            {
                _byModule[module] = (1, us, us);
            }
        }
    }

    public void Dispose() => Stop();
}
