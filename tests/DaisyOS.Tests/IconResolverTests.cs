using DaisyOS.Shell.Helpers;
using Xunit;

namespace DaisyOS.Tests;

public sealed class IconResolverTests
{
    [Theory]
    [InlineData("com.spotify.Client", "com.spotify.Client")]
    [InlineData("org.mozilla.firefox", "org.mozilla.firefox")]
    [InlineData("/usr/share/icons/firefox.png", "firefox")]
    [InlineData("player.svg", "player")]
    public void NormalizesDottedIconIdsWithoutTreatingThemAsFileExtensions(string icon, string expected)
    {
        Assert.Equal(expected, IconResolver.NormalizeIconName(icon));
    }

    [Fact]
    public void FindsFlatpakPngWhenThemeExportOnlyProvidesSvg()
    {
        var root = Path.Combine(Path.GetTempPath(), "daisyos-icon-tests", Guid.NewGuid().ToString("N"));
        var iconPath = Path.Combine(
            root,
            "com.example.Player",
            "x86_64",
            "stable",
            "deployment",
            "files",
            "share",
            "app-info",
            "icons",
            "flatpak",
            "128x128",
            "com.example.Player.png");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
            File.WriteAllBytes(iconPath, [137, 80, 78, 71]);

            var resolved = IconResolver.FindFlatpakAppInfoIcon("com.example.Player", [root]);

            Assert.Equal(iconPath, resolved);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void FlatpakLookupRejectsPathTraversalAsAnIconName()
    {
        var root = Path.Combine(Path.GetTempPath(), "daisyos-icon-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);

            var resolved = IconResolver.FindFlatpakAppInfoIcon("../com.example.Player", [root]);

            Assert.Null(resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
