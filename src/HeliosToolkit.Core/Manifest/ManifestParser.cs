using System.Text.Json;

namespace HeliosToolkit.Core.Manifest;

public sealed class ManifestFormatException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Parses the curated driver manifest. Forward compatible: unknown JSON fields and
/// unknown detect types are tolerated; only a schemaVersion above what this build
/// understands is rejected.
/// </summary>
public static class ManifestParser
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static DriverManifest Parse(string json)
    {
        DriverManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<DriverManifest>(json, Options);
        }
        catch (JsonException e)
        {
            throw new ManifestFormatException("Manifest is not valid JSON.", e);
        }

        if (manifest is null)
        {
            throw new ManifestFormatException("Manifest is empty.");
        }

        if (manifest.SchemaVersion < 1)
        {
            throw new ManifestFormatException("Manifest is missing a valid schemaVersion.");
        }

        if (manifest.SchemaVersion > SupportedSchemaVersion)
        {
            throw new ManifestFormatException(
                $"Manifest schemaVersion {manifest.SchemaVersion} is newer than this app understands " +
                $"({SupportedSchemaVersion}). Update the app.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DriverComponent component in manifest.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Id))
            {
                throw new ManifestFormatException("A manifest component is missing its id.");
            }

            if (!seen.Add(component.Id))
            {
                throw new ManifestFormatException($"Duplicate component id '{component.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(component.Name))
            {
                throw new ManifestFormatException($"Component '{component.Id}' is missing a name.");
            }
        }

        return manifest;
    }
}
