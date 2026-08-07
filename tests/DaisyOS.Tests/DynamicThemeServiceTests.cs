using Avalonia.Controls;
using Avalonia.Media;
using DaisyOS.Shell.Services.Theming;
using Xunit;

namespace DaisyOS.Tests;

public sealed class DynamicThemeServiceTests
{
    [Fact]
    public void ApplyDoesNotModifyTaskbarBorderBrush()
    {
        var resources = new ResourceDictionary();
        var staticTaskbarBorderBrush = new SolidColorBrush(Color.Parse("#1F000000"));
        resources["TaskbarBorderBrush"] = staticTaskbarBorderBrush;

        var generator = new MaterialDynamicSchemeGenerator();
        var scheme = generator.Generate(Color.Parse("#6750A4"), isDark: false);

        DynamicThemeService.Apply(resources, scheme, Colors.Black, Color.Parse("#6750A4"));

        Assert.True(resources.ContainsKey("TaskbarBorderBrush"));
        Assert.Same(staticTaskbarBorderBrush, resources["TaskbarBorderBrush"]);
    }
}
