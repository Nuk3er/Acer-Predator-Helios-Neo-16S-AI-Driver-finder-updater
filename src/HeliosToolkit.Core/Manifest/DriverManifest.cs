namespace HeliosToolkit.Core.Manifest;

/// <summary>How a component's installed version is found on the machine.</summary>
public enum DetectKind
{
    /// <summary>detect.type was missing or not recognized — render the component link-only.</summary>
    Unknown,

    /// <summary>Match a PnP device and read its signed driver version.</summary>
    PnpDriverVersion,

    /// <summary>Resolved through NVIDIA's AjaxDriverService lookup.</summary>
    NvidiaApi,

    /// <summary>No detection — the card only offers links.</summary>
    LinkOnly,
}

public sealed record DetectSpec
{
    public string? Type { get; init; }

    /// <summary>Prefix match against any of the device's hardware IDs, e.g. "PCI\VEN_8086".</summary>
    public string? HardwareIdPrefix { get; init; }

    /// <summary>Exact PnP class name, e.g. "Net", "Media", "System".</summary>
    public string? PnpClass { get; init; }

    /// <summary>Case-insensitive substring of the device's friendly name, e.g. "Wi-Fi".</summary>
    public string? NameContains { get; init; }

    public DetectKind Kind => Type?.Trim().ToLowerInvariant() switch
    {
        "pnpdriverversion" => DetectKind.PnpDriverVersion,
        "nvidiaapi" => DetectKind.NvidiaApi,
        "linkonly" => DetectKind.LinkOnly,
        _ => DetectKind.Unknown,
    };
}

public sealed record DriverComponent
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Vendor { get; init; } = "";
    public string Category { get; init; } = "";
    public DetectSpec Detect { get; init; } = new();

    /// <summary>Latest known version, dotted-numeric when comparable. Null/placeholder ⇒ info only.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Direct download for in-app downloading. Null ⇒ only PageUrl is offered.</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Vendor or Acer page to open in the browser.</summary>
    public string? PageUrl { get; init; }

    public string? ReleaseNotesUrl { get; init; }

    /// <summary>Optional SHA-256 of the file behind DownloadUrl.</summary>
    public string? Sha256 { get; init; }

    public string? InstallNotes { get; init; }
}

public sealed record DriverManifest
{
    public int SchemaVersion { get; init; }
    public DateTimeOffset? UpdatedUtc { get; init; }
    public string Model { get; init; } = "";
    public string? AcerSupportUrl { get; init; }
    public IReadOnlyList<DriverComponent> Components { get; init; } = Array.Empty<DriverComponent>();
}
