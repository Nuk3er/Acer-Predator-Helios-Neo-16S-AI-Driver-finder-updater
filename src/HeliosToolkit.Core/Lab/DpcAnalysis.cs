namespace HeliosToolkit.Core.Lab;

/// <summary>How bad a driver's worst DPC/ISR execution time is for gaming.</summary>
public enum DpcVerdict
{
    /// <summary>&lt; 100 µs — within Microsoft's driver guideline.</summary>
    Good,

    /// <summary>100–500 µs — measurable, rarely felt.</summary>
    Noticeable,

    /// <summary>500–1000 µs — can cost frame pacing at 240 Hz.</summary>
    Concerning,

    /// <summary>&gt; 1000 µs — a real stutter source (a quarter of a 240 Hz frame).</summary>
    Bad,
}

public static class DpcAnalysis
{
    public static DpcVerdict Classify(double maxMicroseconds) => maxMicroseconds switch
    {
        < 100 => DpcVerdict.Good,
        < 500 => DpcVerdict.Noticeable,
        < 1000 => DpcVerdict.Concerning,
        _ => DpcVerdict.Bad,
    };

    private static readonly (string Token, string Advice)[] AdviceTable =
    {
        ("nvlddmkm", "NVIDIA display driver — try a newer/older Game Ready driver; heavy spikes here often follow overlays or HAGS changes."),
        ("dxgkrnl", "GPU scheduler — usually follows the display driver above it; toggling HAGS can change this behavior."),
        ("netwtw", "Intel Wi-Fi driver — a classic DPC offender. Update it on the Devices page, or game on Ethernet."),
        ("rtwlan", "Realtek Wi-Fi driver — update it, or prefer Ethernet while gaming."),
        ("ndis", "Network stack — look at the actual NIC driver nearby; the Network tweaks (interrupt moderation) can help."),
        ("tcpip", "Windows network stack — usually driven by the NIC driver; check for one of those in this list."),
        ("storport", "Storage stack — check NVMe drivers and the Storage tweaks; heavy disk activity during gaming shows up here."),
        ("stornvme", "NVMe driver — normal under disk load; bad values during idle suggest a firmware/driver update."),
        ("acpi", "ACPI/firmware — often power-management related; a BIOS update from Acer's page is the usual fix."),
        ("intelppm", "CPU power management — C-state transitions; the Power tweaks (or the risky C-state toggle) change this."),
        ("hdaudbus", "Audio bus — audio driver latency; update the Realtek driver or try disabling audio enhancements."),
        ("portcls", "Audio port class — same advice as the audio driver: update it from Acer's page."),
        ("usbxhci", "USB 3 host controller — a misbehaving USB device or hub; try re-plugging peripherals to other ports."),
        ("wdf01000", "Windows Driver Framework — a framework, not the culprit: blame the highest non-Wdf driver in this list."),
        ("ntoskrnl", "Windows kernel — generally fine; high values here usually mirror another driver's work."),
    };

    /// <summary>Plain-language advice for a kernel module name (e.g. "nvlddmkm.sys").</summary>
    public static string Advice(string moduleName)
    {
        string lower = moduleName.ToLowerInvariant();
        foreach ((string token, string advice) in AdviceTable)
        {
            if (lower.Contains(token))
            {
                return advice;
            }
        }

        return "Look the driver file up — updating whatever device it belongs to is the usual fix.";
    }
}
