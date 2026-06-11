namespace HeliosToolkit.App.Services.Tweaks;

/// <summary>Locates the NVIDIA display adapter's class registry key (…\Class\{4d36e968…}\00NN).</summary>
public static class NvidiaAdapterKey
{
    private const string ClassRoot =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>Returns the full subkey path of the NVIDIA adapter, or null if not found.</summary>
    public static string? Find()
    {
        foreach (string sub in RegistryHelper.SubKeyNames("HKLM", ClassRoot))
        {
            if (sub.Length != 4 || !int.TryParse(sub, out _))
            {
                continue; // only the 0000/0001… instance keys
            }

            string path = $@"{ClassRoot}\{sub}";
            object? desc = RegistryHelper.ReadValue("HKLM", path, "DriverDesc")
                ?? RegistryHelper.ReadValue("HKLM", path, "ProviderName");
            if (desc?.ToString()?.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) == true)
            {
                return path;
            }
        }

        return null;
    }
}
