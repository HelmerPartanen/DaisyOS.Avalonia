using DaisyOS.Shell.Services.Compositor;
using Xunit;

namespace DaisyOS.Tests;

public sealed class KWinWindowSnapshotParserTests
{
    [Fact]
    public void ParsesAuthoritativeKWinWindowState()
    {
        const string payload = """
            {"windows":[{"id":"a1b2","appId":"org.mozilla.firefox","title":"DaisyOS","desktopFileId":"firefox.desktop","iconName":"firefox","outputName":"HDMI-A-1","workspace":2,"isActive":true,"isMinimized":false,"isMaximized":true,"isFullScreen":false,"canClose":true}]}
            """;

        var parsed = KWinWindowSnapshotParser.TryParse(payload, out var windows);

        Assert.True(parsed);
        var window = Assert.Single(windows);
        Assert.Equal("a1b2", window.Id);
        Assert.Equal("org.mozilla.firefox", window.AppId);
        Assert.Equal("firefox.desktop", window.DesktopFileId);
        Assert.Equal("HDMI-A-1", window.OutputName);
        Assert.True(window.IsActive);
        Assert.True(window.IsMaximized);
    }

    [Fact]
    public void RejectsMalformedOrIncompletePayloads()
    {
        Assert.False(KWinWindowSnapshotParser.TryParse("not-json", out _));
        Assert.False(KWinWindowSnapshotParser.TryParse("{}", out _));

        Assert.True(KWinWindowSnapshotParser.TryParse("{\"windows\":[{\"id\":\"only-id\"}]}", out var windows));
        Assert.Empty(windows);
    }

    [Fact]
    public void DefaultsMissingOptionalStateSafely()
    {
        Assert.True(KWinWindowSnapshotParser.TryParse("{\"windows\":[{\"id\":\"123\",\"appId\":\"foot\"}]}", out var windows));

        var window = Assert.Single(windows);
        Assert.Equal("foot", window.Title);
        Assert.False(window.IsActive);
        Assert.False(window.IsMinimized);
        Assert.True(window.CanClose);
    }
}
