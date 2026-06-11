using System.Diagnostics;
using System.Text;

namespace HeliosToolkit.App.Services.System;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
    public string Combined => string.Join('\n', new[] { StdOut, StdErr }.Where(s => s.Length > 0));
}

/// <summary>Runs a console tool (powercfg, bcdedit, schtasks…) and captures its output.</summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        return new ProcessResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }
}
