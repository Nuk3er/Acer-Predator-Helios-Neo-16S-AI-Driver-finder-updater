using HeliosToolkit.Core.Update;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("V2.0", 2, 0, 0)]
    [InlineData("0.0.0-ci.42", 0, 0, 0)]
    [InlineData("1.4.0-beta.2", 1, 4, 0)]
    public void TryParse_Valid(string tag, int major, int minor, int patch)
    {
        Assert.True(ReleaseVersion.TryParse(tag, out var v));
        Assert.Equal((major, minor, patch), v);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1.2.3.4")]
    [InlineData("v.x")]
    public void TryParse_Invalid(string? tag)
    {
        Assert.False(ReleaseVersion.TryParse(tag, out _));
    }

    [Theory]
    [InlineData("v1.2.4", "1.2.3", true)]
    [InlineData("v1.3.0", "1.2.9", true)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("v1.2.3", "1.2.3", false)]
    [InlineData("v1.2.2", "1.2.3", false)]
    [InlineData("v1.0.0", "0.1.0-ci.99", true)]
    public void IsNewer_Compares(string latest, string current, bool expected)
    {
        Assert.Equal(expected, ReleaseVersion.IsNewer(latest, current));
    }

    [Fact]
    public void IsNewer_FalseOnGarbage()
    {
        Assert.False(ReleaseVersion.IsNewer("not-a-version", "1.0.0"));
        Assert.False(ReleaseVersion.IsNewer("1.0.0", "garbage"));
    }
}
