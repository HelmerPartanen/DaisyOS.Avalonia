using Xunit;

namespace DaisyOS.Tests;

public sealed class DesktopPresentationTests
{
    [Fact]
    public void LiveTilesAndDragGhostUseTheSameItemTemplate()
    {
        var root = FindRepositoryRoot();
        var desktopView = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DaisyOS.Shell",
            "Views",
            "DesktopView.axaml"));

        Assert.Contains("x:Key=\"DesktopItemTemplate\"", desktopView, StringComparison.Ordinal);
        Assert.Contains("ItemTemplate=\"{StaticResource DesktopItemTemplate}\"", desktopView, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource DesktopItemTemplate}\"", desktopView, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Source=\"avares://DaisyOS.Shell/Assets/AppIcons/FilesIcon.png\"",
            desktopView,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopGeometryUsesSharedFixedPitchTokens()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DaisyOS.Shell",
            "Themes",
            "OSLayout.axaml"));
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "DaisyOS.Shell",
            "Themes",
            "Components",
            "DesktopStyles.axaml"));

        Assert.Contains("x:Key=\"OSDesktopCellWidth\">96", layout, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"OSDesktopCellHeight\">108", layout, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource OSDesktopCellWidth}\"", styles, StringComparison.Ordinal);
        Assert.Contains("Value=\"{DynamicResource OSDesktopCellHeight}\"", styles, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DaisyOS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
