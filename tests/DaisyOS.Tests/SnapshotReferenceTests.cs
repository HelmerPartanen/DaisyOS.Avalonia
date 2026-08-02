using System.Reflection;
using DaisyOS.Shell.Layout;
using Xunit;

namespace DaisyOS.Tests;

public sealed class SnapshotReferenceTests
{
    [Fact]
    public void VerifySidebarWidthsAndEdgeGapConstants()
    {
        Assert.Equal(900, TwoPaneLayoutPolicy.CompactBreakpoint);
        Assert.Equal(56, TwoPaneLayoutPolicy.CompactSidebarWidth);
    }

    [Fact]
    public void VerifySurfaceManagerConstants_EdgeGapIs8px()
    {
        var type = typeof(DaisyOS.Shell.Views.BottomShellSurfaceManager);
        var edgeGapField = type.GetField("EdgeGap", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(edgeGapField);
        Assert.Equal(8.0, (double)edgeGapField.GetValue(null)!);
    }
}
