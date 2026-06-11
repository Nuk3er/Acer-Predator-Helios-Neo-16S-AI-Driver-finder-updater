using System.IO;
using SevenZipExtractor;
using Serilog;

namespace HeliosToolkit.App.Services.Drivers;

/// <summary>A top-level component of an NVIDIA driver package, with a keep/drop recommendation.</summary>
public sealed class NvidiaComponent
{
    public required string FolderName { get; init; }
    public required string Description { get; init; }
    public required bool KeepByDefault { get; init; }
    public bool Required { get; init; }
}

public sealed record DebloatResult(string SetupExePath, string ExtractedFolder, int KeptComponents, int RemovedComponents);

/// <summary>
/// Replicates the NVCleanstall workflow in-app: extract an NVIDIA driver .exe
/// (a 7-zip self-extractor), let the user drop telemetry/GFE/app components,
/// patch setup.cfg, and hand back setup.exe to launch interactively.
/// </summary>
public sealed class NvidiaPackageDebloater
{
    // Folder name (case-insensitive prefix) → (description, keep by default, required).
    private static readonly (string Prefix, string Desc, bool Keep, bool Required)[] KnownComponents =
    {
        ("Display.Driver", "Core display driver", true, true),
        ("Display.Optimus", "Advanced Optimus / hybrid graphics (required on laptops)", true, true),
        ("NVPCF", "Dynamic Boost power controller (required on laptops)", true, true),
        ("NVI2", "Installer engine", true, true),
        ("NvContainer", "Background container (needed by kept components)", true, false),
        ("PhysX", "PhysX physics runtime", true, false),
        ("HDAudio", "HDMI/DP audio driver", true, false),
        ("NGX", "DLSS / NGX runtime", true, false),
        ("nvinodrs", "Driver settings store", true, false),
        ("GFExperience", "GeForce Experience (telemetry, login, overlay)", false, false),
        ("NvApp", "NVIDIA App (replaces GFE; telemetry/overlay)", false, false),
        ("NvTelemetry", "Telemetry collector", false, false),
        ("NvBackend", "GFE backend service", false, false),
        ("NvCamera", "Ansel / NvCamera", false, false),
        ("ShadowPlay", "ShadowPlay recording", false, false),
        ("nodejs", "Node.js runtime (used only by GFE/App)", false, false),
        ("Display.NView", "nView desktop manager", false, false),
        ("Display.Update", "Driver auto-update", false, false),
        ("FrameViewSDK", "FrameView SDK", false, false),
        ("NVWMI", "WMI provider", false, false),
        ("MSVCRT", "Visual C++ runtime", true, false),
    };

    /// <summary>Extracts the package and returns its top-level components for the user to choose.</summary>
    public Task<(string ExtractedFolder, IReadOnlyList<NvidiaComponent> Components)> ExtractAsync(
        string packageExePath, CancellationToken ct = default)
    {
        return Task.Run<(string, IReadOnlyList<NvidiaComponent>)>(() =>
        {
            string extractedFolder = Path.Combine(
                Path.GetDirectoryName(packageExePath)!,
                Path.GetFileNameWithoutExtension(packageExePath) + "_debloated");

            if (Directory.Exists(extractedFolder))
            {
                Directory.Delete(extractedFolder, recursive: true);
            }

            Directory.CreateDirectory(extractedFolder);

            using (var archive = new ArchiveFile(packageExePath, SevenZipDllPath()))
            {
                archive.Extract(extractedFolder);
            }

            var components = new List<NvidiaComponent>();
            foreach (string dir in Directory.GetDirectories(extractedFolder))
            {
                string name = Path.GetFileName(dir);
                (string Prefix, string Desc, bool Keep, bool Required)? known = KnownComponents
                    .Cast<(string Prefix, string Desc, bool Keep, bool Required)?>()
                    .FirstOrDefault(k => name.StartsWith(k!.Value.Prefix, StringComparison.OrdinalIgnoreCase));

                components.Add(new NvidiaComponent
                {
                    FolderName = name,
                    Description = known?.Desc ?? "Unknown component (kept to be safe)",
                    // Unknown components default to KEEP — never silently break an install.
                    KeepByDefault = known?.Keep ?? true,
                    Required = known?.Required ?? false,
                });
            }

            Log.Information("Extracted NVIDIA package to {Folder}: {Count} components", extractedFolder, components.Count);
            return (extractedFolder, components);
        }, ct);
    }

    /// <summary>Removes unchecked components, patches setup.cfg, returns the setup.exe to launch.</summary>
    public Task<DebloatResult> ApplyAsync(
        string extractedFolder, IReadOnlyCollection<NvidiaComponent> keep, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var keepNames = keep.Select(c => c.FolderName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int removed = 0, kept = 0;

            foreach (string dir in Directory.GetDirectories(extractedFolder))
            {
                string name = Path.GetFileName(dir);
                if (keepNames.Contains(name))
                {
                    kept++;
                    continue;
                }

                try
                {
                    Directory.Delete(dir, recursive: true);
                    removed++;
                }
                catch (IOException e)
                {
                    Log.Warning(e, "Could not remove component {Name}", name);
                }
            }

            PatchSetupCfg(Path.Combine(extractedFolder, "setup.cfg"));

            string setupExe = Path.Combine(extractedFolder, "setup.exe");
            if (!File.Exists(setupExe))
            {
                throw new FileNotFoundException("setup.exe not found in the extracted package.", setupExe);
            }

            Log.Information("Debloat applied: {Kept} kept, {Removed} removed", kept, removed);
            return new DebloatResult(setupExe, extractedFolder, kept, removed);
        }, ct);
    }

    /// <summary>Removes the consent/EULA file manifest lines so setup doesn't fail on pruned files.</summary>
    private static void PatchSetupCfg(string setupCfgPath)
    {
        if (!File.Exists(setupCfgPath))
        {
            return;
        }

        try
        {
            string[] dropTokens =
            {
                "${{EulaHtmlFile}}", "${{FunctionalConsentFile}}", "${{PrivacyPolicyFile}}",
            };

            string[] lines = File.ReadAllLines(setupCfgPath);
            var kept = lines.Where(line => !dropTokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase)));
            File.WriteAllLines(setupCfgPath, kept);
        }
        catch (IOException e)
        {
            Log.Warning(e, "Could not patch setup.cfg");
        }
    }

    private static string? _cachedDllPath;

    /// <summary>
    /// Returns a path to the native 7z.dll. We embed it as a resource so the
    /// single-file EXE is truly self-contained; it's written to the cache folder
    /// on first use. Falls back to a loose x64\7z.dll if one happens to be present.
    /// </summary>
    private static string SevenZipDllPath()
    {
        if (_cachedDllPath is not null && File.Exists(_cachedDllPath))
        {
            return _cachedDllPath;
        }

        // Loose file next to the EXE (non-single-file builds).
        string loose = Path.Combine(AppContext.BaseDirectory, "x64", "7z.dll");
        if (File.Exists(loose))
        {
            _cachedDllPath = loose;
            return loose;
        }

        // Extract the embedded copy.
        string target = Path.Combine(AppPaths.Cache, "7z.dll");
        try
        {
            using Stream? resource = typeof(NvidiaPackageDebloater).Assembly
                .GetManifestResourceStream("HeliosToolkit.App.Native.7z.dll");
            if (resource is not null)
            {
                Directory.CreateDirectory(AppPaths.Cache);
                if (!File.Exists(target) || new FileInfo(target).Length != resource.Length)
                {
                    using var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                    resource.CopyTo(file);
                }

                _cachedDllPath = target;
                return target;
            }
        }
        catch (IOException e)
        {
            Log.Warning(e, "Could not stage embedded 7z.dll");
        }

        return loose;
    }
}
