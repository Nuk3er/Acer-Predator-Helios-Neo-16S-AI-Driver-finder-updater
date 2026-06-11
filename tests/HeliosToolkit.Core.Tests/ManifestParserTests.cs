using HeliosToolkit.Core.Manifest;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class ManifestParserTests
{
    private const string Valid = """
    {
      "schemaVersion": 1,
      "updatedUtc": "2026-06-11T00:00:00Z",
      "model": "PHN16S-71",
      "acerSupportUrl": "https://example.test/acer",
      "components": [
        {
          "id": "nvidia-gpu",
          "name": "NVIDIA Driver",
          "vendor": "NVIDIA",
          "category": "Graphics",
          "detect": { "type": "nvidiaApi" }
        },
        {
          "id": "intel-wifi",
          "name": "Wi-Fi",
          "vendor": "Intel",
          "category": "Network",
          "detect": { "type": "pnpDriverVersion", "pnpClass": "Net", "nameContains": "Wi-Fi" },
          "latestVersion": "23.150.0.3",
          "downloadUrl": "https://example.test/wifi.exe",
          "sha256": "ab"
        }
      ]
    }
    """;

    [Fact]
    public void Parse_ValidManifest()
    {
        DriverManifest manifest = ManifestParser.Parse(Valid);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("PHN16S-71", manifest.Model);
        Assert.Equal(2, manifest.Components.Count);
        Assert.Equal(DetectKind.NvidiaApi, manifest.Components[0].Detect.Kind);
        Assert.Equal(DetectKind.PnpDriverVersion, manifest.Components[1].Detect.Kind);
        Assert.Equal("Wi-Fi", manifest.Components[1].Detect.NameContains);
        Assert.Equal("23.150.0.3", manifest.Components[1].LatestVersion);
    }

    [Fact]
    public void Parse_RepositoryManifestFile()
    {
        // The real manifest shipped in this repo must always parse.
        string path = FindRepoFile(Path.Combine("manifest", "drivers.manifest.json"));
        DriverManifest manifest = ManifestParser.Parse(File.ReadAllText(path));

        Assert.Equal("PHN16S-71", manifest.Model);
        Assert.NotEmpty(manifest.Components);
        Assert.Contains(manifest.Components, c => c.Detect.Kind == DetectKind.NvidiaApi);
    }

    [Fact]
    public void Parse_ToleratesUnknownFieldsAndDetectTypes()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "model": "X",
          "someFutureField": { "a": 1 },
          "components": [
            {
              "id": "future",
              "name": "Future thing",
              "detect": { "type": "quantumScan", "futureKnob": true },
              "anotherUnknown": [1, 2]
            }
          ]
        }
        """;

        DriverManifest manifest = ManifestParser.Parse(json);

        Assert.Single(manifest.Components);
        Assert.Equal(DetectKind.Unknown, manifest.Components[0].Detect.Kind);
    }

    [Fact]
    public void Parse_RejectsNewerSchema()
    {
        const string json = """{ "schemaVersion": 99, "model": "X", "components": [] }""";
        var e = Assert.Throws<ManifestFormatException>(() => ManifestParser.Parse(json));
        Assert.Contains("newer", e.Message);
    }

    [Theory]
    [InlineData("""{ "model": "X", "components": [] }""")]
    [InlineData("""{ "schemaVersion": 0, "model": "X", "components": [] }""")]
    public void Parse_RejectsMissingSchemaVersion(string json)
    {
        Assert.Throws<ManifestFormatException>(() => ManifestParser.Parse(json));
    }

    [Fact]
    public void Parse_RejectsDuplicateIds()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "model": "X",
          "components": [
            { "id": "a", "name": "A" },
            { "id": "A", "name": "A again" }
          ]
        }
        """;
        Assert.Throws<ManifestFormatException>(() => ManifestParser.Parse(json));
    }

    [Fact]
    public void Parse_RejectsGarbage()
    {
        Assert.Throws<ManifestFormatException>(() => ManifestParser.Parse("not json"));
    }

    private static string FindRepoFile(string relative)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
