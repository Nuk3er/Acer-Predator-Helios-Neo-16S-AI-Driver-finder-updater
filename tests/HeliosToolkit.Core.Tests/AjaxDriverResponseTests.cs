using System.Text.Json;
using HeliosToolkit.Core.Nvidia;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class AjaxDriverResponseTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Parses_RealShape_WithNumericSuccess()
    {
        // Shape observed live from gfwsl.geforce.com (Success is a number, not a string).
        const string json = """
        {
          "Success": 1,
          "IDS": [
            {
              "downloadInfo": {
                "Version": "610.47",
                "DownloadURL": "https://uk.download.nvidia.com/Windows/610.47/610.47-notebook-win10-win11-64bit-international-dch-whql.exe",
                "ReleaseDateTime": "Tue May 26, 2026",
                "DetailsURL": "https://www.nvidia.com/en-gb/drivers/details/271614/",
                "Name": "GeForce%20Game%20Ready%20Driver",
                "IsBeta": 0
              }
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<AjaxDriverResponse>(json, Options);

        Assert.NotNull(response);
        Assert.True(response.IsSuccess);
        var info = Assert.Single(response.Ids).DownloadInfo;
        Assert.NotNull(info);
        Assert.Equal("610.47", info.Version);
        Assert.StartsWith("https://uk.download.nvidia.com", info.DownloadUrl);
        Assert.Equal("GeForce Game Ready Driver", info.DisplayName);
        Assert.Equal("0", info.IsBeta);
    }

    [Fact]
    public void Parses_StringSuccess()
    {
        const string json = """{ "Success": "1", "IDS": [] }""";
        var response = JsonSerializer.Deserialize<AjaxDriverResponse>(json, Options);
        Assert.NotNull(response);
        Assert.True(response.IsSuccess);
        Assert.Empty(response.Ids);
    }

    [Fact]
    public void Failure_WhenSuccessZeroOrMissing()
    {
        var zero = JsonSerializer.Deserialize<AjaxDriverResponse>("""{ "Success": 0 }""", Options);
        var missing = JsonSerializer.Deserialize<AjaxDriverResponse>("{}", Options);
        Assert.False(zero!.IsSuccess);
        Assert.False(missing!.IsSuccess);
    }
}
