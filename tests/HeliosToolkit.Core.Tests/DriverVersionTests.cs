using HeliosToolkit.Core.Versioning;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class DriverVersionTests
{
    [Theory]
    [InlineData("10.1.19444.8378")]
    [InlineData("6.0.9971.1")]
    [InlineData("576.80")]
    [InlineData("1")]
    public void TryParse_Valid(string text)
    {
        Assert.True(DriverVersion.TryParse(text, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10.1.x")] // manifest placeholder must not parse
    [InlineData("10..1")]
    [InlineData("v10.1")]
    [InlineData("10.1-beta")]
    public void TryParse_Invalid(string? text)
    {
        Assert.False(DriverVersion.TryParse(text, out _));
    }

    [Theory]
    [InlineData("10.1.19444.8378", "10.1.19444.8377", 1)]
    [InlineData("10.1.19444.8378", "10.1.19444.8378", 0)]
    [InlineData("10.1", "10.1.0.0", 0)] // missing parts are zero
    [InlineData("10.1", "10.1.0.1", -1)]
    [InlineData("9.99.99.99", "10.0", -1)]
    [InlineData("6.0.9971.1", "6.0.9660.1", 1)]
    public void CompareTo_Orders(string left, string right, int expectedSign)
    {
        Assert.True(DriverVersion.TryParse(left, out DriverVersion a));
        Assert.True(DriverVersion.TryParse(right, out DriverVersion b));
        Assert.Equal(expectedSign, Math.Sign(a.CompareTo(b)));
    }

    [Fact]
    public void Equality_IgnoresTrailingZeros()
    {
        Assert.True(DriverVersion.TryParse("10.1", out DriverVersion a));
        Assert.True(DriverVersion.TryParse("10.1.0.0", out DriverVersion b));
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
