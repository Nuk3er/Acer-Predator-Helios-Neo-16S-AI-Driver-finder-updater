using System.Runtime.InteropServices;

namespace HeliosToolkit.App.Services.System;

/// <summary>AC vs battery state via GetSystemPowerStatus.</summary>
public static class PowerStatus
{
    public static bool IsOnAcPower()
    {
        if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
        {
            // 1 = online, 0 = offline, 255 = unknown (treat unknown as AC: desktops)
            return status.ACLineStatus != 0;
        }

        return true;
    }

    public static int BatteryPercent()
    {
        if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status) && status.BatteryLifePercent <= 100)
        {
            return status.BatteryLifePercent;
        }

        return -1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);
}
