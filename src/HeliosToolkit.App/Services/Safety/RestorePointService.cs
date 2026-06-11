using System.Management;
using HeliosToolkit.App.Services.Tweaks;
using Microsoft.Win32;
using Serilog;

namespace HeliosToolkit.App.Services.Safety;

public enum RestorePointResult { Created, Disabled, Failed }

/// <summary>Creates a System Restore point before the first tweak of a session.</summary>
public sealed class RestorePointService
{
    public Task<RestorePointResult> CreateAsync(string description)
    {
        return Task.Run(() =>
        {
            try
            {
                // Bypass the once-per-24h throttle so our pre-tweak point is never skipped.
                try
                {
                    RegistryHelper.WriteValue(
                        "HKLM",
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore",
                        "SystemRestorePointCreationFrequency",
                        0,
                        RegistryValueKind.DWord);
                }
                catch (Exception e)
                {
                    Log.Debug(e, "Could not relax restore-point frequency");
                }

                using var managementClass = new ManagementClass(@"\\.\root\default", "SystemRestore", null);
                ManagementBaseObject inParams = managementClass.GetMethodParameters("CreateRestorePoint");
                inParams["Description"] = description;
                inParams["RestorePointType"] = 12;   // MODIFY_SETTINGS
                inParams["EventType"] = 100;          // BEGIN_SYSTEM_CHANGE

                ManagementBaseObject outParams = managementClass.InvokeMethod("CreateRestorePoint", inParams, null);
                uint returnValue = Convert.ToUInt32(outParams["ReturnValue"]);

                if (returnValue == 0)
                {
                    return RestorePointResult.Created;
                }

                // 1058 = service disabled; treat as "restore is off" rather than a hard failure.
                Log.Warning("CreateRestorePoint returned {Code}", returnValue);
                return returnValue == 1058 ? RestorePointResult.Disabled : RestorePointResult.Failed;
            }
            catch (Exception e)
            {
                Log.Error(e, "System Restore point creation threw");
                return RestorePointResult.Failed;
            }
        });
    }
}
