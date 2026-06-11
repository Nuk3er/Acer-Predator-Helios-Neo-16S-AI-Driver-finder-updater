using System.Management;

namespace HeliosToolkit.App.Services.Hardware;

/// <summary>
/// Thin async wrapper around WMI. Queries run on the thread pool because some
/// classes (notably Win32_PnPSignedDriver) take seconds to materialize.
/// </summary>
public sealed class WmiQueryService
{
    /// <summary>Runs a WQL query and returns each instance as a property bag.</summary>
    public Task<List<Dictionary<string, object?>>> QueryAsync(
        string wql,
        string scope = @"root\cimv2",
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var results = new List<Dictionary<string, object?>>();
            using var searcher = new ManagementObjectSearcher(scope, wql);
            using ManagementObjectCollection collection = searcher.Get();

            foreach (ManagementBaseObject item in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bag = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyData property in item.Properties)
                {
                    bag[property.Name] = property.Value;
                }

                results.Add(bag);
                item.Dispose();
            }

            return results;
        }, cancellationToken);
    }
}

public static class WmiBagExtensions
{
    public static string? GetString(this Dictionary<string, object?> bag, string key) =>
        bag.TryGetValue(key, out object? value) ? value?.ToString()?.Trim() : null;

    public static long GetInt64(this Dictionary<string, object?> bag, string key)
    {
        if (bag.TryGetValue(key, out object? value) && value is not null)
        {
            try
            {
                return Convert.ToInt64(value);
            }
            catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
            {
            }
        }

        return 0;
    }

    public static string[] GetStringArray(this Dictionary<string, object?> bag, string key) =>
        bag.TryGetValue(key, out object? value) && value is string[] array ? array : Array.Empty<string>();
}
