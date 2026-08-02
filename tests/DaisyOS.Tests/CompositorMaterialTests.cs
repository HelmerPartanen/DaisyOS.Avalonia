using Avalonia.Media;
using DaisyOS.Shell.Rendering;
using Xunit;

namespace DaisyOS.Tests;

public sealed class CompositorMaterialTests
{
    [Fact]
    public void EveryMaterialPresetHasSafeTunableValues()
    {
        foreach (var preset in Enum.GetValues<MaterialPreset>())
        {
            var material = MaterialPresets.Get(preset);
            Assert.InRange(material.TintOpacity, 0, 1);
            Assert.NotEqual(default(Color), material.Tint);
        }
    }

    [Fact]
    public void LightAndDarkPresetsAreVisiblyDistinct()
    {
        var light = MaterialPresets.Get(MaterialPreset.Light);
        var dark = MaterialPresets.Get(MaterialPreset.Dark);
        Assert.NotEqual(light.Tint, dark.Tint);
    }

    [Theory]
    [InlineData(MaterialPreset.Sidebar)]
    [InlineData(MaterialPreset.Popover)]
    [InlineData(MaterialPreset.Menu)]
    public void SemanticPresetsAdaptToAppearance(MaterialPreset preset)
    {
        var dark = MaterialPresets.Get(preset, true);
        var light = MaterialPresets.Get(preset, false);
        Assert.NotEqual(dark.Tint, light.Tint);
    }
}
