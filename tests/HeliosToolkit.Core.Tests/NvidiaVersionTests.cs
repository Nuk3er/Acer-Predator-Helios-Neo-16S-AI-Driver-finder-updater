using HeliosToolkit.Core.Versioning;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class NvidiaVersionTests
{
    [Theory]
    [InlineData("32.0.15.7680", "576.80")] // RTX 50 era
    [InlineData("32.0.15.6094", "560.94")]
    [InlineData("31.0.15.5222", "552.22")]
    [InlineData("30.0.14.7141", "471.41")]
    [InlineData("27.21.14.5671", "456.71")]
    public void TryFromWmiVersion_KnownPairs(string wmi, string expected)
    {
        Assert.True(NvidiaVersion.TryFromWmiVersion(wmi, out string geforce));
        Assert.Equal(expected, geforce);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("31.0.x.5222")]
    [InlineData("1.2")] // fewer than 5 digits total
    public void TryFromWmiVersion_RejectsGarbage(string? wmi)
    {
        Assert.False(NvidiaVersion.TryFromWmiVersion(wmi, out _));
    }

    [Theory]
    [InlineData("576.80", 576, 80)]
    [InlineData("552.22", 552, 22)]
    [InlineData(" 471.41 ", 471, 41)]
    public void TryParseGeForce_Valid(string text, int major, int minor)
    {
        Assert.True(NvidiaVersion.TryParseGeForce(text, out int ma, out int mi));
        Assert.Equal(major, ma);
        Assert.Equal(minor, mi);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("576")]
    [InlineData("576.80.1")]
    [InlineData("576.-1")]
    [InlineData("a.b")]
    public void TryParseGeForce_Invalid(string? text)
    {
        Assert.False(NvidiaVersion.TryParseGeForce(text, out _, out _));
    }

    [Theory]
    [InlineData("576.80", "576.80", 0)]
    [InlineData("576.08", "576.80", -1)]
    [InlineData("576.80", "560.94", 1)]
    [InlineData("552.22", "576.80", -1)]
    public void CompareGeForce_Orders(string installed, string latest, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(NvidiaVersion.CompareGeForce(installed, latest)));
    }
}
