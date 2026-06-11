using System.Runtime.InteropServices;

namespace HeliosToolkit.App.Services.Hardware;

/// <summary>Current display mode (resolution and refresh rate) of the primary monitor.</summary>
public static class DisplayInfo
{
    private const int ENUM_CURRENT_SETTINGS = -1;

    public static (int Width, int Height, int RefreshHz) GetPrimaryDisplayMode()
    {
        var devMode = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };
        if (EnumDisplaySettingsW(null, ENUM_CURRENT_SETTINGS, ref devMode))
        {
            return ((int)devMode.dmPelsWidth, (int)devMode.dmPelsHeight, (int)devMode.dmDisplayFrequency);
        }

        return (0, 0, 0);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplaySettingsW(string? lpszDeviceName, int iModeNum, ref DEVMODEW lpDevMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODEW
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}
