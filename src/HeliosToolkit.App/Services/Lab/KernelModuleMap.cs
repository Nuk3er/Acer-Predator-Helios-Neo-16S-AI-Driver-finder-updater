using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace HeliosToolkit.App.Services.Lab;

/// <summary>
/// Maps kernel addresses (DPC/ISR routine pointers) to driver file names.
/// Snapshot via NtQuerySystemInformation(SystemModuleInformation); drivers that
/// load mid-trace are added from ETW ImageLoad events.
/// </summary>
public sealed class KernelModuleMap
{
    private readonly object _gate = new();
    private List<(ulong Base, ulong End, string Name)> _ranges = new();

    public void Snapshot()
    {
        try
        {
            const int SystemModuleInformation = 11;
            int length = 1 << 18;
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                int status = NtQuerySystemInformation(SystemModuleInformation, buffer, length, out int needed);
                if (status != 0 && needed > length)
                {
                    Marshal.FreeHGlobal(buffer);
                    length = needed + (1 << 14);
                    buffer = Marshal.AllocHGlobal(length);
                    status = NtQuerySystemInformation(SystemModuleInformation, buffer, length, out _);
                }

                if (status != 0)
                {
                    Log.Warning("NtQuerySystemInformation(modules) returned 0x{Status:X8}", status);
                    return;
                }

                int count = Marshal.ReadInt32(buffer);
                var ranges = new List<(ulong, ulong, string)>(count);

                // RTL_PROCESS_MODULE_INFORMATION (x64): Section(8) MappedBase(8) ImageBase(8)
                // ImageSize(4) Flags(4) LoadOrderIndex(2) InitOrderIndex(2) LoadCount(2)
                // OffsetToFileName(2) FullPathName(256) = 296 bytes; array starts at offset 8.
                const int EntrySize = 296;
                const int HeaderSize = 8;
                for (int i = 0; i < count; i++)
                {
                    IntPtr entry = IntPtr.Add(buffer, HeaderSize + i * EntrySize);
                    ulong imageBase = (ulong)Marshal.ReadInt64(entry, 16);
                    uint imageSize = (uint)Marshal.ReadInt32(entry, 24);
                    ushort nameOffset = (ushort)Marshal.ReadInt16(entry, 38);
                    string fullPath = Marshal.PtrToStringAnsi(IntPtr.Add(entry, 40)) ?? "";
                    string name = nameOffset < fullPath.Length ? fullPath[nameOffset..] : fullPath;
                    if (imageBase != 0 && imageSize != 0 && name.Length > 0)
                    {
                        ranges.Add((imageBase, imageBase + imageSize, name));
                    }
                }

                ranges.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                lock (_gate)
                {
                    _ranges = ranges;
                }

                Log.Information("Kernel module map: {Count} drivers", ranges.Count);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Kernel module snapshot failed");
        }
    }

    /// <summary>Adds a driver that loaded while the trace was running.</summary>
    public void Add(ulong imageBase, long imageSize, string fileName)
    {
        if (imageBase == 0 || imageSize <= 0)
        {
            return;
        }

        string name = Path.GetFileName(fileName.Replace('/', '\\'));
        lock (_gate)
        {
            var ranges = new List<(ulong, ulong, string)>(_ranges)
            {
                (imageBase, imageBase + (ulong)imageSize, name.Length > 0 ? name : fileName),
            };
            ranges.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            _ranges = ranges;
        }
    }

    public string Lookup(ulong address)
    {
        List<(ulong Base, ulong End, string Name)> ranges;
        lock (_gate)
        {
            ranges = _ranges;
        }

        int lo = 0, hi = ranges.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (address < ranges[mid].Base)
            {
                hi = mid - 1;
            }
            else if (address >= ranges[mid].End)
            {
                lo = mid + 1;
            }
            else
            {
                return ranges[mid].Name;
            }
        }

        return "unknown";
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int length, out int returnLength);
}
