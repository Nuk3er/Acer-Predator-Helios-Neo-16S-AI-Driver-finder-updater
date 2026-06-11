using System.IO;
using System.Runtime.InteropServices;

namespace HeliosToolkit.App.Services.System;

public static class KnownFolders
{
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>The user's real Downloads folder (Environment.GetFolderPath cannot provide it).</summary>
    public static string Downloads
    {
        get
        {
            try
            {
                int hr = SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out IntPtr pathPtr);
                if (hr == 0)
                {
                    try
                    {
                        string? path = Marshal.PtrToStringUni(pathPtr);
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pathPtr);
                    }
                }
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
