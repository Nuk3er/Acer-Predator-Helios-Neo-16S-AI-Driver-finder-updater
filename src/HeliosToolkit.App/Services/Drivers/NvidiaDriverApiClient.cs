using System.Net.Http;
using System.Text.Json;
using HeliosToolkit.Core.Nvidia;
using Serilog;

namespace HeliosToolkit.App.Services.Drivers;

public sealed record NvidiaDriverResult(
    string Version,
    string DownloadUrl,
    string? ReleaseDate,
    string? DetailsUrl,
    string DisplayName,
    int Pfid);

/// <summary>
/// Looks up the latest Game Ready driver via NVIDIA's AjaxDriverService —
/// the same endpoint TinyNvidiaUpdateChecker uses. The GPU product family id
/// (pfid) is resolved at runtime from the community-maintained ZenitH-AT
/// dataset, with hardcoded fallbacks for the GPUs this laptop ships with.
/// </summary>
public sealed class NvidiaDriverApiClient(HttpClient http)
{
    private const string GpuDataUrl = "https://raw.githubusercontent.com/ZenitH-AT/nvidia-data/main/gpu-data.json";
    private const string AjaxUrl =
        "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php";

    // Verified against ZenitH-AT data, June 2026. The plain RTX 5070 Laptop GPU
    // was not yet listed there; runtime lookup is primary for a reason.
    private static readonly Dictionary<string, int> FallbackPfids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GeForce RTX 5090 Laptop GPU"] = 1073,
        ["GeForce RTX 5080 Laptop GPU"] = 1074,
        ["GeForce RTX 5070 Ti Laptop GPU"] = 1075,
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private Dictionary<string, int>? _notebookPfids;

    public async Task<NvidiaDriverResult?> GetLatestAsync(string gpuName, int osBuild, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gpuName))
        {
            return null;
        }

        int? pfid = await ResolvePfidAsync(gpuName, ct);
        if (pfid is null)
        {
            Log.Warning("No pfid found for GPU {Gpu}; cannot query NVIDIA", gpuName);
            return null;
        }

        int osId = osBuild >= 22000 ? 135 : 57; // Windows 11 / Windows 10 x64
        string url = $"{AjaxUrl}?func=DriverManualLookup&pfid={pfid}&osID={osId}&dch=1&upCRD=0";

        try
        {
            await using var stream = await http.GetStreamAsync(url, ct);
            var response = await JsonSerializer.DeserializeAsync<AjaxDriverResponse>(stream, JsonOptions, ct);
            AjaxDownloadInfo? info = response?.IsSuccess == true
                ? response.Ids.FirstOrDefault()?.DownloadInfo
                : null;

            if (info?.Version is null || info.DownloadUrl is null)
            {
                Log.Warning("NVIDIA lookup returned no driver (pfid {Pfid}, osId {OsId})", pfid, osId);
                return null;
            }

            return new NvidiaDriverResult(
                info.Version, info.DownloadUrl, info.ReleaseDateTime, info.DetailsUrl, info.DisplayName, pfid.Value);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            Log.Warning(e, "NVIDIA driver lookup failed");
            return null;
        }
    }

    private async Task<int?> ResolvePfidAsync(string gpuName, CancellationToken ct)
    {
        string normalized = gpuName.Replace("NVIDIA ", "", StringComparison.OrdinalIgnoreCase).Trim();

        Dictionary<string, int>? table = await GetNotebookPfidsAsync(ct);
        if (table is not null)
        {
            if (table.TryGetValue(normalized, out int exact))
            {
                return exact;
            }

            // Tolerate small naming differences ("Laptop GPU" suffix variations etc.)
            foreach ((string name, int pfid) in table)
            {
                if (name.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    normalized.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pfid;
                }
            }
        }

        return FallbackPfids.TryGetValue(normalized, out int fallback) ? fallback : null;
    }

    private async Task<Dictionary<string, int>?> GetNotebookPfidsAsync(CancellationToken ct)
    {
        if (_notebookPfids is not null)
        {
            return _notebookPfids;
        }

        try
        {
            string json = await http.GetStringAsync(GpuDataUrl, ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("notebook", out JsonElement notebook))
            {
                return null;
            }

            var table = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in notebook.EnumerateObject())
            {
                string? value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => null,
                };

                if (int.TryParse(value, out int pfid))
                {
                    table[property.Name] = pfid;
                }
            }

            _notebookPfids = table;
            return table;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            Log.Warning(e, "Could not fetch GPU pfid dataset; using hardcoded fallbacks");
            return null;
        }
    }
}
