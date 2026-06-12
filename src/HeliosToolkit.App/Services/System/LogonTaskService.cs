using Serilog;

namespace HeliosToolkit.App.Services.System;

/// <summary>
/// Installs/removes a logon task that starts the app minimized to the tray with the
/// timer hold active. /RL HIGHEST satisfies the admin manifest without a UAC prompt.
/// </summary>
public sealed class LogonTaskService
{
    private const string TaskName = @"HeliosToolkit\TrayHold";

    public async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        ProcessResult r = await ProcessRunner.RunAsync("schtasks", $"/Query /TN \"{TaskName}\"", ct);
        return r.Success;
    }

    public async Task<bool> InstallAsync(CancellationToken ct = default)
    {
        string? exe = Environment.ProcessPath;
        if (exe is null)
        {
            return false;
        }

        ProcessResult r = await ProcessRunner.RunAsync(
            "schtasks",
            $"/Create /F /RL HIGHEST /SC ONLOGON /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" --tray\"",
            ct);
        Log.Information("Logon task install: {Ok} {Out}", r.Success, r.Combined);
        return r.Success;
    }

    public async Task<bool> UninstallAsync(CancellationToken ct = default)
    {
        ProcessResult r = await ProcessRunner.RunAsync("schtasks", $"/Delete /F /TN \"{TaskName}\"", ct);
        Log.Information("Logon task uninstall: {Ok}", r.Success);
        return r.Success;
    }
}
