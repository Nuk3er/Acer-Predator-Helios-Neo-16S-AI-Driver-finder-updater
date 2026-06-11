using System.Runtime.InteropServices;
using Serilog;

namespace HeliosToolkit.App.Services.Nvidia;

/// <summary>
/// Minimal NVAPI Driver Settings (DRS) interop — just enough to read/write DWORD
/// settings on the global Base profile. Every entry point is guarded: if nvapi64.dll
/// is missing or any call fails, the feature reports unavailable instead of crashing.
/// Setting IDs and values come from NVIDIA's published NvApiDriverSettings.h.
/// </summary>
public sealed class NvApiDrs
{
    // ---- Official setting IDs (NvApiDriverSettings.h) ----
    public const uint PreferredPstateId = 0x1057EB71;       // 1 = prefer maximum performance
    public const uint OglThreadControlId = 0x20C1221E;      // 1 = threaded optimization on
    public const uint VsyncModeId = 0x00A879CF;              // 0x08416747 = force off
    public const uint PreRenderLimitId = 0x007BA09E;         // 1 = max pre-rendered frames 1
    public const uint QualityEnhancementsId = 0x00CE2691;    // 0x14 = high performance
    public const uint AnisoOpts2Id = 0x00E73211;             // 1 = anisotropic sample optimization on
    public const uint TrilinSlopeId = 0x002ECAF2;            // 1 = trilinear optimization on

    public const uint VsyncForceOff = 0x08416747;
    public const uint QualityHighPerformance = 0x14;

    /// <summary>The performance bundle this app applies to the global profile.</summary>
    public static readonly IReadOnlyDictionary<uint, uint> PerformanceProfile = new Dictionary<uint, uint>
    {
        [PreferredPstateId] = 1,
        [OglThreadControlId] = 1,
        [VsyncModeId] = VsyncForceOff,
        [PreRenderLimitId] = 1,
        [QualityEnhancementsId] = QualityHighPerformance,
        [AnisoOpts2Id] = 1,
        [TrilinSlopeId] = 1,
    };

    // ---- QueryInterface function ids (stable, verified against open-source wrappers) ----
    private const uint IdInitialize = 0x0150E828;
    private const uint IdUnload = 0xD22BDD7E;
    private const uint IdCreateSession = 0x0694D52E;
    private const uint IdDestroySession = 0xDAD9CFF8;
    private const uint IdLoadSettings = 0x375DBD6B;
    private const uint IdSaveSettings = 0xFCBC7E14;
    private const uint IdGetBaseProfile = 0xDA8466A0;
    private const uint IdGetSetting = 0x73BF8338;
    private const uint IdSetSetting = 0x577DD202;
    private const uint IdDeleteProfileSetting = 0xE4A26362;

    private delegate int InitializeFn();
    private delegate int UnloadFn();
    private delegate int CreateSessionFn(out IntPtr session);
    private delegate int DestroySessionFn(IntPtr session);
    private delegate int SessionFn(IntPtr session);
    private delegate int GetBaseProfileFn(IntPtr session, out IntPtr profile);
    private delegate int GetSettingFn(IntPtr session, IntPtr profile, uint settingId, ref NvdrsSetting setting);
    private delegate int SetSettingFn(IntPtr session, IntPtr profile, ref NvdrsSetting setting);
    private delegate int DeleteSettingFn(IntPtr session, IntPtr profile, uint settingId);

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    private struct NvdrsSetting
    {
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string SettingName;
        public uint SettingId;
        public uint SettingType;        // 0 = DWORD
        public uint SettingLocation;    // 0 = current profile
        public uint IsCurrentPredefined;
        public uint IsPredefinedValid;
        public uint PredefinedU32;      // first 4 bytes of the predefined-value union
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
        public byte[] PredefinedPad;
        public uint CurrentU32;         // first 4 bytes of the current-value union
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
        public byte[] CurrentPad;
    }

    private static uint SettingVersion => (uint)Marshal.SizeOf<NvdrsSetting>() | (1u << 16);

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    private static T? GetFn<T>(uint id) where T : Delegate
    {
        IntPtr ptr = QueryInterface(id);
        return ptr == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    /// <summary>Runs an action inside an initialized DRS session against the Base profile.</summary>
    private static bool WithBaseProfile(Func<GetSettingFn?, SetSettingFn?, DeleteSettingFn?, IntPtr, IntPtr, bool> body, bool save)
    {
        try
        {
            var initialize = GetFn<InitializeFn>(IdInitialize);
            var unload = GetFn<UnloadFn>(IdUnload);
            var createSession = GetFn<CreateSessionFn>(IdCreateSession);
            var destroySession = GetFn<DestroySessionFn>(IdDestroySession);
            var loadSettings = GetFn<SessionFn>(IdLoadSettings);
            var saveSettings = GetFn<SessionFn>(IdSaveSettings);
            var getBaseProfile = GetFn<GetBaseProfileFn>(IdGetBaseProfile);

            if (initialize is null || createSession is null || destroySession is null
                || loadSettings is null || getBaseProfile is null || (save && saveSettings is null))
            {
                Log.Warning("NVAPI: required entry points missing");
                return false;
            }

            if (initialize() != 0)
            {
                return false;
            }

            try
            {
                if (createSession(out IntPtr session) != 0)
                {
                    return false;
                }

                try
                {
                    if (loadSettings(session) != 0 || getBaseProfile(session, out IntPtr profile) != 0)
                    {
                        return false;
                    }

                    bool ok = body(
                        GetFn<GetSettingFn>(IdGetSetting),
                        GetFn<SetSettingFn>(IdSetSetting),
                        GetFn<DeleteSettingFn>(IdDeleteProfileSetting),
                        session,
                        profile);

                    if (ok && save && saveSettings!(session) != 0)
                    {
                        return false;
                    }

                    return ok;
                }
                finally
                {
                    destroySession(session);
                }
            }
            finally
            {
                unload?.Invoke();
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException or AccessViolationException or SEHException)
        {
            Log.Warning(e, "NVAPI unavailable");
            return false;
        }
    }

    /// <summary>True when nvapi64.dll loads and a DRS session can be opened.</summary>
    public bool IsAvailable()
    {
        return WithBaseProfile((_, _, _, _, _) => true, save: false);
    }

    /// <summary>Reads current DWORD values for the given setting ids. Missing/unset ids map to null.</summary>
    public IReadOnlyDictionary<uint, uint?>? ReadSettings(IEnumerable<uint> ids)
    {
        Dictionary<uint, uint?>? result = null;
        bool ok = WithBaseProfile((getSetting, _, _, session, profile) =>
        {
            if (getSetting is null)
            {
                return false;
            }

            result = new Dictionary<uint, uint?>();
            foreach (uint id in ids)
            {
                var setting = new NvdrsSetting { Version = SettingVersion };
                int status = getSetting(session, profile, id, ref setting);
                result[id] = status == 0 ? setting.CurrentU32 : null;
            }

            return true;
        }, save: false);

        return ok ? result : null;
    }

    /// <summary>Writes DWORD settings to the Base profile and saves. Returns false on any failure.</summary>
    public bool ApplySettings(IReadOnlyDictionary<uint, uint> values)
    {
        return WithBaseProfile((_, setSetting, _, session, profile) =>
        {
            if (setSetting is null)
            {
                return false;
            }

            foreach ((uint id, uint value) in values)
            {
                var setting = new NvdrsSetting
                {
                    Version = SettingVersion,
                    SettingId = id,
                    SettingType = 0,
                    CurrentU32 = value,
                };

                int status = setSetting(session, profile, ref setting);
                if (status != 0)
                {
                    Log.Warning("NVAPI SetSetting 0x{Id:X8} failed with {Status}", id, status);
                    return false;
                }
            }

            return true;
        }, save: true);
    }

    /// <summary>Restores settings: ids with a value are re-set, ids mapped to null are removed from the profile.</summary>
    public bool RestoreSettings(IReadOnlyDictionary<uint, uint?> originals)
    {
        return WithBaseProfile((_, setSetting, deleteSetting, session, profile) =>
        {
            if (setSetting is null)
            {
                return false;
            }

            foreach ((uint id, uint? value) in originals)
            {
                if (value is { } u32)
                {
                    var setting = new NvdrsSetting
                    {
                        Version = SettingVersion,
                        SettingId = id,
                        SettingType = 0,
                        CurrentU32 = u32,
                    };
                    setSetting(session, profile, ref setting);
                }
                else
                {
                    deleteSetting?.Invoke(session, profile, id);
                }
            }

            return true;
        }, save: true);
    }
}
