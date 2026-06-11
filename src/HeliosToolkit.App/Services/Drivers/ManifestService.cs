using System.IO;
using System.Net.Http;
using System.Reflection;
using HeliosToolkit.Core.Manifest;
using Serilog;

namespace HeliosToolkit.App.Services.Drivers;

/// <summary>
/// Loads the curated driver manifest: fresh from the repo when possible,
/// otherwise the cached copy, otherwise the copy embedded at build time.
/// </summary>
public sealed class ManifestService(HttpClient http)
{
    public const string ManifestUrl =
        "https://raw.githubusercontent.com/nuk3er/acer-predator-helios-neo-16s-ai-driver-finder-updater/main/manifest/drivers.manifest.json";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static string CachePath => Path.Combine(AppPaths.Cache, "drivers.manifest.json");

    private DriverManifest? _current;

    public string? LoadedFrom { get; private set; }

    public async Task<DriverManifest> GetManifestAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (_current is not null && !forceRefresh)
        {
            return _current;
        }

        // 1. Fresh-enough cache
        if (!forceRefresh && TryReadCache(requireFresh: true) is { } cached)
        {
            _current = cached;
            LoadedFrom = "cache";
            return cached;
        }

        // 2. Network
        try
        {
            string json = await http.GetStringAsync(ManifestUrl, ct);
            DriverManifest manifest = ManifestParser.Parse(json);
            _current = manifest;
            LoadedFrom = "online";
            try
            {
                await File.WriteAllTextAsync(CachePath, json, ct);
            }
            catch (IOException e)
            {
                Log.Warning(e, "Could not write manifest cache");
            }

            return manifest;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ManifestFormatException)
        {
            Log.Warning(e, "Online manifest unavailable, falling back");
        }

        // 3. Any cache, even stale
        if (TryReadCache(requireFresh: false) is { } stale)
        {
            _current = stale;
            LoadedFrom = "stale cache";
            return stale;
        }

        // 4. Embedded copy from build time
        _current = ReadEmbedded();
        LoadedFrom = "embedded";
        return _current;
    }

    private static DriverManifest? TryReadCache(bool requireFresh)
    {
        try
        {
            var info = new FileInfo(CachePath);
            if (!info.Exists || (requireFresh && DateTime.UtcNow - info.LastWriteTimeUtc > CacheTtl))
            {
                return null;
            }

            return ManifestParser.Parse(File.ReadAllText(CachePath));
        }
        catch (Exception e)
        {
            Log.Warning(e, "Manifest cache unreadable");
            return null;
        }
    }

    private static DriverManifest ReadEmbedded()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("HeliosToolkit.App.Resources.drivers.manifest.json")
            ?? throw new InvalidOperationException("Embedded driver manifest is missing from the build.");
        using var reader = new StreamReader(stream);
        return ManifestParser.Parse(reader.ReadToEnd());
    }
}
