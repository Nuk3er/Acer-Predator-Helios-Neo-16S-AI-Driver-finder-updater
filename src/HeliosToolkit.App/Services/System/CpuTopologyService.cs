using System.Runtime.InteropServices;
using Serilog;

namespace HeliosToolkit.App.Services.System;

/// <summary>
/// Detects P-cores vs E-cores on hybrid CPUs (Arrow Lake-HX: 8P + 16E) via
/// GetLogicalProcessorInformationEx. Higher EfficiencyClass = performance core.
/// </summary>
public sealed class CpuTopologyService
{
    private readonly Lazy<(nuint PCoreMask, int PCores, int ECores)> _topology;

    public CpuTopologyService()
    {
        _topology = new Lazy<(nuint, int, int)>(Detect);
    }

    /// <summary>Affinity mask covering all logical processors of the highest-efficiency-class cores.</summary>
    public nuint PCoreMask => _topology.Value.PCoreMask;

    public int PCoreCount => _topology.Value.PCores;

    public int ECoreCount => _topology.Value.ECores;

    public bool IsHybrid => _topology.Value.ECores > 0 && _topology.Value.PCores > 0;

    private static (nuint, int, int) Detect()
    {
        try
        {
            const int RelationProcessorCore = 0;
            uint length = 0;
            GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
            if (length == 0)
            {
                return (0, 0, 0);
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
                {
                    return (0, 0, 0);
                }

                // First pass: find the max efficiency class; second: build the mask.
                var cores = new List<(byte EfficiencyClass, nuint Mask)>();
                IntPtr current = buffer;
                long end = buffer.ToInt64() + length;
                while (current.ToInt64() < end)
                {
                    int relationship = Marshal.ReadInt32(current);
                    uint size = (uint)Marshal.ReadInt32(current, 4);

                    if (relationship == RelationProcessorCore)
                    {
                        // PROCESSOR_RELATIONSHIP after the 8-byte header:
                        // byte Flags; byte EfficiencyClass; byte[20] Reserved; ushort GroupCount; GROUP_AFFINITY[]
                        byte efficiencyClass = Marshal.ReadByte(current, 8 + 1);
                        ushort groupCount = (ushort)Marshal.ReadInt16(current, 8 + 22);

                        nuint mask = 0;
                        int affinityOffset = 8 + 24;
                        for (int g = 0; g < groupCount; g++)
                        {
                            // GROUP_AFFINITY { UIntPtr Mask; ushort Group; ushort[3] Reserved } — 16 bytes on x64.
                            nuint groupMask = (nuint)(ulong)Marshal.ReadInt64(current, affinityOffset + g * 16);
                            ushort group = (ushort)Marshal.ReadInt16(current, affinityOffset + g * 16 + IntPtr.Size);
                            if (group == 0)
                            {
                                mask |= groupMask; // 24 LPs on this machine — single group
                            }
                        }

                        cores.Add((efficiencyClass, mask));
                    }

                    current = IntPtr.Add(current, (int)size);
                }

                if (cores.Count == 0)
                {
                    return (0, 0, 0);
                }

                byte maxClass = cores.Max(c => c.EfficiencyClass);
                nuint pMask = 0;
                int pCores = 0, eCores = 0;
                foreach ((byte cls, nuint mask) in cores)
                {
                    if (cls == maxClass)
                    {
                        pMask |= mask;
                        pCores++;
                    }
                    else
                    {
                        eCores++;
                    }
                }

                // Homogeneous CPU (no E-cores): mask covers everything, which is also correct.
                Log.Information("CPU topology: {P} P-core(s), {E} E-core(s), P-mask 0x{Mask:X}",
                    pCores, eCores, (ulong)pMask);
                return (pMask, pCores, eCores);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "CPU topology detection failed");
            return (0, 0, 0);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformationEx(
        int relationshipType, IntPtr buffer, ref uint returnedLength);
}
