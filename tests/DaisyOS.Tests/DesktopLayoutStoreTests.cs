using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class DesktopLayoutStoreTests
{
    [Fact]
    public void RoundTripsDesktopPositions()
    {
        var root = Path.Combine(Path.GetTempPath(), "daisyos-layout-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "desktop-layout.json");
        var store = new DesktopLayoutStore(path);
        var expected = new Dictionary<string, DesktopItemPosition>
        {
            ["/home/test/Desktop/Browser.desktop"] = new(2, 3),
            ["/home/test/Desktop/Projects"] = new(0, 1)
        };

        try
        {
            store.Save(expected);

            var actual = store.Load();

            Assert.Equal(expected, actual);
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
    public void CorruptLayoutFallsBackToAnEmptyLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), "daisyos-layout-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "desktop-layout.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{not valid json");

            var actual = new DesktopLayoutStore(path).Load();

            Assert.Empty(actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
