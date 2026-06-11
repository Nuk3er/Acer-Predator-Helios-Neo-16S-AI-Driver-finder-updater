using HeliosToolkit.Core.Tweaks;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class TweakMetadataTests
{
    public static IEnumerable<object[]> AllTweaks =>
        TweakCatalogMetadata.All.Select(t => new object[] { t });

    [Fact]
    public void Ids_AreUniqueAndKebabCase()
    {
        var ids = TweakCatalogMetadata.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(ids, id => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", id));
    }

    [Fact]
    public void Catalog_IsNotEmpty_AndCoversBothPages()
    {
        Assert.NotEmpty(TweakCatalogMetadata.All);
        Assert.Contains(TweakCatalogMetadata.All, t => t.Page == TweakPage.Nvidia);
        Assert.Contains(TweakCatalogMetadata.All, t => t.Page == TweakPage.Windows);
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void EveryTweak_HasNameCategoryDescription(TweakMetadata tweak)
    {
        Assert.False(string.IsNullOrWhiteSpace(tweak.Name));
        Assert.False(string.IsNullOrWhiteSpace(tweak.Category));
        Assert.False(string.IsNullOrWhiteSpace(tweak.Description));
        // Descriptions should be substantial, not placeholders.
        Assert.True(tweak.Description.Length >= 40, $"{tweak.Id} description is too short");
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void RiskyTweaks_HaveAWarning(TweakMetadata tweak)
    {
        if (tweak.Risk == RiskLevel.Risky)
        {
            Assert.False(string.IsNullOrWhiteSpace(tweak.Warning),
                $"Risky tweak '{tweak.Id}' must carry a warning.");
        }
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void InfoTweaks_AreNotReboot(TweakMetadata tweak)
    {
        if (tweak.Risk == RiskLevel.Info)
        {
            Assert.False(tweak.RequiresReboot, $"Info tweak '{tweak.Id}' should not require reboot.");
        }
    }
}
