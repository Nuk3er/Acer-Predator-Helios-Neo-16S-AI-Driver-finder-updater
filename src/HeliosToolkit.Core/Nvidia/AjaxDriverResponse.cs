using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeliosToolkit.Core.Nvidia;

/// <summary>
/// DTOs for NVIDIA's AjaxDriverService DriverManualLookup response.
/// The service is loose with types (Success arrives as a number or a string),
/// so scalar fields use a tolerant converter.
/// </summary>
public sealed record AjaxDriverResponse
{
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Success { get; init; }

    [JsonPropertyName("IDS")]
    public IReadOnlyList<AjaxDriverId> Ids { get; init; } = Array.Empty<AjaxDriverId>();

    public bool IsSuccess => Success == "1";
}

public sealed record AjaxDriverId
{
    [JsonPropertyName("downloadInfo")]
    public AjaxDownloadInfo? DownloadInfo { get; init; }
}

public sealed record AjaxDownloadInfo
{
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Version { get; init; }

    [JsonPropertyName("DownloadURL")]
    public string? DownloadUrl { get; init; }

    public string? ReleaseDateTime { get; init; }

    [JsonPropertyName("DetailsURL")]
    public string? DetailsUrl { get; init; }

    /// <summary>URL-encoded display name, e.g. "GeForce%20Game%20Ready%20Driver".</summary>
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Name { get; init; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? IsBeta { get; init; }

    public string DisplayName => string.IsNullOrEmpty(Name) ? "NVIDIA driver" : Uri.UnescapeDataString(Name);
}

/// <summary>Reads a JSON string, number or bool as a string.</summary>
public sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out long l)
                ? l.ToString(global::System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "1",
            JsonTokenType.False => "0",
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string."),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
