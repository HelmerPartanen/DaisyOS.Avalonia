using System.Xml.Linq;
using Xunit;

namespace DaisyOS.Tests;

public sealed class ThemeResourceContractTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("OSControlHeightCompact", "32")]
    [InlineData("OSControlHeightStandard", "40")]
    [InlineData("OSControlHeightComfortable", "44")]
    [InlineData("OSSystemBarSurfaceHeight", "62")]
    [InlineData("OSSystemBarHostHeight", "78")]
    [InlineData("OSDockItemSize", "33")]
    [InlineData("OSPanelPadding", "20")]
    [InlineData("OSReadableContentWidth", "820")]
    [InlineData("OSRadiusSurface", "16")]
    public void LayoutResources_KeepSemanticGeometryContract(string key, string expected)
    {
        Assert.Equal(expected, ReadResource("OSLayout.axaml", key));
    }

    [Theory]
    [InlineData("OSMotionFeedback", "0:0:0.08")]
    [InlineData("OSMotionSelection", "0:0:0.11")]
    [InlineData("OSMotionPanelEnter", "0:0:0.15")]
    [InlineData("OSMotionPanelExit", "0:0:0.1")]
    [InlineData("OSMotionNavigation", "0:0:0.15")]
    public void MotionResources_KeepRestrainedDurationContract(string key, string expected)
    {
        Assert.Equal(expected, ReadResource("OSMotion.axaml", key));
    }

    [Theory]
    [InlineData("OSKWinBlurStrength")]
    [InlineData("OSKWinBlurSaturation")]
    [InlineData("OSKWinBlurNoiseStrength")]
    [InlineData("OSKWinBlurAmount")]
    [InlineData("OSKWinMaterialTintOpacity")]
    public void GlobalKWinBlurSettingsExposeTunableNumericValues(string key)
    {
        Assert.True(double.TryParse(ReadResource("KWinBlurSettings.axaml", key), global::System.Globalization.NumberStyles.Any, global::System.Globalization.CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void KWinBlurSettingsDoNotContainLegacyGpuParameters()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Themes", "KWinBlurSettings.axaml"));

        Assert.DoesNotContain("Gaussian", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Framebuffer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Wallpaper", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellPanelsUseOnlyCompositorBlur()
    {
        var root = FindRepositoryRoot();
        var decorator = File.ReadAllText(Path.Combine(root,
            "src", "DaisyOS.Shell", "Controls", "WallpaperBackdropDecorator.cs"));
        var shell = File.ReadAllText(Path.Combine(root,
            "src", "DaisyOS.Shell", "Views", "ShellView.axaml"));

        Assert.Contains("DaisyOS never samples the wallpaper or renders blur itself", decorator, StringComparison.Ordinal);
        Assert.DoesNotContain("WallpaperVibrancyCache", decorator, StringComparison.Ordinal);
        Assert.DoesNotContain("VibrancyCoordinator", decorator, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellRenderHost", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextMenusUseTheSharedCompactSurfaceContract()
    {
        var themePath = Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Themes", "Components", "SurfaceStyles.axaml");
        var document = XDocument.Load(themePath);
        var contextMenuTheme = document.Descendants()
            .Where(element => element.Name.LocalName == "ControlTheme")
            .Single(element => string.Equals((string?)element.Attribute("TargetType"), "ContextMenu", StringComparison.Ordinal));

        Assert.Contains(contextMenuTheme.Descendants().Where(element => element.Name.LocalName == "Setter"), setter =>
            string.Equals((string?)setter.Attribute("Property"), "Background", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSContextMenuSurfaceBrush}", StringComparison.Ordinal));
        Assert.Contains(contextMenuTheme.Descendants().Where(element => element.Name.LocalName == "Setter"), setter =>
            string.Equals((string?)setter.Attribute("Property"), "MinWidth", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "200", StringComparison.Ordinal));
        Assert.Contains(contextMenuTheme.Descendants().Where(element => element.Name.LocalName == "Setter"), setter =>
            string.Equals((string?)setter.Attribute("Property"), "CornerRadius", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSContextMenuRadius}", StringComparison.Ordinal));
        Assert.Contains(contextMenuTheme.Descendants().Where(element => element.Name.LocalName == "Setter"), setter =>
            string.Equals((string?)setter.Attribute("Property"), "Padding", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSContextMenuPadding}", StringComparison.Ordinal));
        Assert.Contains(contextMenuTheme.Descendants(), element =>
            element.Name.LocalName == "ItemsPresenter"
            && string.Equals((string?)element.Attribute("Margin"), "0", StringComparison.Ordinal));
        Assert.Contains(document.Descendants().Where(element => element.Name.LocalName == "Style"), style =>
            string.Equals((string?)style.Attribute("Selector"), "ContextMenu", StringComparison.Ordinal));
        var menuItemSetters = document.Descendants().Where(element => element.Name.LocalName == "Style")
            .Single(style => string.Equals((string?)style.Attribute("Selector"), "ContextMenu MenuItem", StringComparison.Ordinal))
            .Descendants().Where(element => element.Name.LocalName == "Setter").ToArray();
        Assert.Contains(menuItemSetters, setter =>
            string.Equals((string?)setter.Attribute("Property"), "MinHeight", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSContextMenuItemHeight}", StringComparison.Ordinal));
        Assert.Contains(menuItemSetters, setter =>
            string.Equals((string?)setter.Attribute("Property"), "Padding", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSContextMenuItemPadding}", StringComparison.Ordinal));
        Assert.Contains(menuItemSetters, setter =>
            string.Equals((string?)setter.Attribute("Property"), "CornerRadius", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSContextMenuItemRadius}", StringComparison.Ordinal));

        var hoverSetters = document.Descendants().Where(element => element.Name.LocalName == "Style")
            .Single(style => string.Equals(
                (string?)style.Attribute("Selector"),
                "ContextMenu MenuItem:pointerover, ContextMenu MenuItem:selected",
                StringComparison.Ordinal))
            .Descendants().Where(element => element.Name.LocalName == "Setter").ToArray();
        Assert.Contains(hoverSetters, setter =>
            string.Equals((string?)setter.Attribute("Property"), "Background", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSButtonHoverBrush}", StringComparison.Ordinal));

        var checkedSetters = document.Descendants().Where(element => element.Name.LocalName == "Style")
            .Single(style => string.Equals(
                (string?)style.Attribute("Selector"),
                "ContextMenu MenuItem:checked",
                StringComparison.Ordinal))
            .Descendants().Where(element => element.Name.LocalName == "Setter").ToArray();
        Assert.Contains(checkedSetters, setter =>
            string.Equals((string?)setter.Attribute("Property"), "Background", StringComparison.Ordinal)
            && string.Equals((string?)setter.Attribute("Value"), "{DynamicResource OSClearBrush}", StringComparison.Ordinal));
    }

    [Fact]
    public void LauncherContextMenusAndSystemBarUseTheGlobalButtonHoverColor()
    {
        var root = FindRepositoryRoot();
        var launcher = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Themes", "Components", "LauncherStyles.axaml"));
        var contextMenus = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Themes", "Components", "SurfaceStyles.axaml"));
        var systemBar = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Themes", "Components", "SystemBarStyles.axaml"));
        var buttons = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Themes", "Components", "ButtonStyles.axaml"));

        const string hoverToken = "{DynamicResource OSButtonHoverBrush}";
        Assert.Contains(
            "<Style Selector=\"Button:pointerover, ToggleButton:pointerover\">",
            buttons,
            StringComparison.Ordinal);
        Assert.Contains($"<Setter Property=\"Background\" Value=\"{hoverToken}\" />", buttons, StringComparison.Ordinal);
        Assert.Contains($"<Setter Property=\"Background\" Value=\"{hoverToken}\" />", launcher, StringComparison.Ordinal);
        Assert.Contains($"<Setter Property=\"Background\" Value=\"{hoverToken}\" />", contextMenus, StringComparison.Ordinal);
        Assert.Contains($"<Setter Property=\"Background\" Value=\"{hoverToken}\" />", systemBar, StringComparison.Ordinal);
        var systemBarHoverBackgrounds = XDocument.Parse(systemBar)
            .Descendants()
            .Where(element => element.Name.LocalName == "Style"
                && ((string?)element.Attribute("Selector"))?.Contains(":pointerover", StringComparison.Ordinal) == true)
            .SelectMany(style => style.Elements().Where(element =>
                element.Name.LocalName == "Setter"
                && string.Equals((string?)element.Attribute("Property"), "Background", StringComparison.Ordinal)))
            .ToArray();
        Assert.NotEmpty(systemBarHoverBackgrounds);
        Assert.All(systemBarHoverBackgrounds, setter =>
            Assert.Equal(hoverToken, (string?)setter.Attribute("Value")));
        Assert.Contains(
            "<Style Selector=\"ToggleButton.SystemBarVisibilityItem:checked\">",
            systemBar,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"Background\" Value=\"{DynamicResource OSClearBrush}\" />",
            systemBar,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShellContextMenusDoNotOverrideTheReusableVisualContract()
    {
        var views = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views");
        var contextMenus = Directory.EnumerateFiles(views, "*.axaml")
            .Select(XDocument.Load)
            .SelectMany(document => document.Descendants()
                .Where(element => element.Name.LocalName == "ContextMenu"))
            .ToArray();

        Assert.Equal(8, contextMenus.Length);
        foreach (var contextMenu in contextMenus)
        {
            Assert.DoesNotContain(contextMenu.Attributes(), attribute =>
                attribute.Name.LocalName is "Theme"
                    or "Background"
                    or "BorderBrush"
                    or "BorderThickness"
                    or "CornerRadius"
                    or "Padding");
        }
    }

    private static string ReadResource(string fileName, string key)
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "Themes", fileName));
        var resource = document.Descendants().SingleOrDefault(element =>
            string.Equals((string?)element.Attribute(X + "Key"), key, StringComparison.Ordinal));

        Assert.NotNull(resource);
        return resource!.Value.Trim();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DaisyOS.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the DaisyOS repository root.");
    }
}
