using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.Core.Tweaks;

namespace HeliosToolkit.App.Services.Tweaks.Primitives;

/// <summary>
/// Enables/disables one or more scheduled tasks via schtasks.exe. Task paths may
/// be exact ("\Microsoft\Windows\Application Experience\Microsoft Compatibility
/// Appraiser") or a folder prefix to disable everything beneath it (used for the
/// NVIDIA NvTm* tasks whose names carry a random suffix). NotApplicable when none
/// of the targets exist (common on a debloated OS).
/// </summary>
public sealed class ScheduledTaskTweak : ITweak
{
    private readonly IReadOnlyList<string> _taskMatchers;

    public ScheduledTaskTweak(TweakMetadata meta, params string[] taskMatchers)
    {
        Meta = meta;
        _taskMatchers = taskMatchers;
    }

    public TweakMetadata Meta { get; }

    public async Task<TweakState> DetectAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TaskRow> tasks = await QueryMatchingAsync(ct);
        if (tasks.Count == 0)
        {
            return TweakState.NotApplicable;
        }

        bool allDisabled = tasks.All(t => t.IsDisabled);
        bool anyDisabled = tasks.Any(t => t.IsDisabled);
        return allDisabled ? TweakState.Applied : anyDisabled ? TweakState.Mixed : TweakState.NotApplied;
    }

    public async Task ApplyAsync(IBackupSink backup, CancellationToken ct = default)
    {
        foreach (TaskRow task in await QueryMatchingAsync(ct))
        {
            backup.Capture(new BackupEntry
            {
                TweakId = Meta.Id,
                Kind = "task",
                Target = task.Path,
                Existed = true,
                OriginalValue = task.IsDisabled ? "Disabled" : "Enabled",
            });

            await ProcessRunner.RunAsync("schtasks", $"/Change /TN \"{task.Path}\" /DISABLE", ct);
        }
    }

    public async Task RevertAsync(IBackupSource backup, CancellationToken ct = default)
    {
        foreach (BackupEntry entry in backup.ForTweak(Meta.Id).Where(e => e.Kind == "task"))
        {
            // Only re-enable tasks we ourselves disabled.
            if (entry.OriginalValue == "Enabled")
            {
                await ProcessRunner.RunAsync("schtasks", $"/Change /TN \"{entry.Target}\" /ENABLE", ct);
            }
        }
    }

    private sealed record TaskRow(string Path, bool IsDisabled);

    private async Task<IReadOnlyList<TaskRow>> QueryMatchingAsync(CancellationToken ct)
    {
        ProcessResult r = await ProcessRunner.RunAsync("schtasks", "/Query /FO CSV /V", ct);
        if (!r.Success)
        {
            return Array.Empty<TaskRow>();
        }

        var rows = new List<TaskRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header row tells us which CSV columns hold TaskName and Status.
        int nameCol = -1, statusCol = -1;
        if (lines.Length > 0)
        {
            string[] header = SplitCsv(lines[0]);
            nameCol = Array.FindIndex(header, h => h.Equals("TaskName", StringComparison.OrdinalIgnoreCase));
            statusCol = Array.FindIndex(header, h => h.Equals("Status", StringComparison.OrdinalIgnoreCase));
        }

        if (nameCol < 0)
        {
            return Array.Empty<TaskRow>();
        }

        foreach (string line in lines.Skip(1))
        {
            string[] cols = SplitCsv(line);
            if (cols.Length <= nameCol)
            {
                continue;
            }

            string path = cols[nameCol];
            if (!_taskMatchers.Any(m => MatchesTask(path, m)) || !seen.Add(path))
            {
                continue;
            }

            bool disabled = statusCol >= 0 && cols.Length > statusCol &&
                cols[statusCol].Equals("Disabled", StringComparison.OrdinalIgnoreCase);
            rows.Add(new TaskRow(path, disabled));
        }

        return rows;
    }

    private static bool MatchesTask(string taskPath, string matcher) =>
        matcher.EndsWith('*')
            ? taskPath.StartsWith(matcher.TrimEnd('*'), StringComparison.OrdinalIgnoreCase) ||
              taskPath.Contains(matcher.TrimEnd('*').TrimStart('\\'), StringComparison.OrdinalIgnoreCase)
            : taskPath.Equals(matcher, StringComparison.OrdinalIgnoreCase);

    private static string[] SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new global::System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
