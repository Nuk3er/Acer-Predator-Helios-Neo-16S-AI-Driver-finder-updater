using System.Runtime.InteropServices;
using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.Core.Tweaks;

namespace HeliosToolkit.App.Services.Tweaks.Primitives;

/// <summary>
/// A registry tweak for the mouse pointer settings that also pushes the change live
/// via SystemParametersInfo(SPI_SETMOUSE), so acceleration changes take effect without
/// a sign-out.
/// </summary>
public sealed class SystemParametersTweak : RegistryValueTweak
{
    private const uint SPI_SETMOUSE = 0x0004;
    private const uint SPIF_SENDCHANGE = 0x0002;

    public SystemParametersTweak(TweakMetadata meta, params RegistryValueSpec[] specs) : base(meta, specs)
    {
    }

    public override async Task ApplyAsync(IBackupSink backup, CancellationToken ct = default)
    {
        await base.ApplyAsync(backup, ct);
        ApplyLiveMouse(0, 0, 0);
    }

    public override async Task RevertAsync(IBackupSource backup, CancellationToken ct = default)
    {
        await base.RevertAsync(backup, ct);
        ApplyLiveMouse(6, 10, 1); // Windows defaults: threshold1=6, threshold2=10, acceleration on
    }

    private static void ApplyLiveMouse(int threshold1, int threshold2, int acceleration)
    {
        try
        {
            int[] mouseParams = { threshold1, threshold2, acceleration };
            GCHandle handle = GCHandle.Alloc(mouseParams, GCHandleType.Pinned);
            try
            {
                SystemParametersInfo(SPI_SETMOUSE, 0, handle.AddrOfPinnedObject(), SPIF_SENDCHANGE);
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Registry value is still set; it will apply on next sign-in.
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
}
