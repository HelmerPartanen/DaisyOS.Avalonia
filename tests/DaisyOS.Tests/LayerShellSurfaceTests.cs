using DaisyOS.Shell.Services.Shell;
using Xunit;

namespace DaisyOS.Tests;

public sealed class LayerShellSurfaceTests
{
    [Fact]
    public void Desktop_surface_covers_its_output_without_reserving_workspace()
    {
        var options = LayerShellSurfaceOptions.Desktop("HDMI-A-1");

        Assert.Equal(LayerShellSurfaceRole.Background, options.Role);
        Assert.Equal(0, options.ExclusiveZone);
        Assert.Equal(LayerShellAnchor.Top | LayerShellAnchor.Bottom | LayerShellAnchor.Left | LayerShellAnchor.Right, options.Anchor);
        options.Validate();
    }

    [Fact]
    public void Primary_panel_reserves_only_its_bottom_safe_area()
    {
        var options = LayerShellSurfaceOptions.PrimaryPanel(ShellSurfacePlan.PrimaryPanelExclusiveZone, "eDP-1");

        Assert.Equal(LayerShellSurfaceRole.Panel, options.Role);
        Assert.Equal(LayerShellAnchor.Bottom | LayerShellAnchor.Left | LayerShellAnchor.Right, options.Anchor);
        Assert.Equal(ShellSurfacePlan.PrimaryPanelExclusiveZone, options.ExclusiveZone);
        options.Validate();
    }

    [Fact]
    public void Production_plan_uses_a_desktop_on_every_output_but_one_primary_panel()
    {
        var plan = ShellSurfacePlan.Create("eDP-1", ["eDP-1", "HDMI-A-1"]);

        Assert.Equal(2, plan.Count(surface => surface.Role == LayerShellSurfaceRole.Background));
        var panel = Assert.Single(plan, surface => surface.Role == LayerShellSurfaceRole.Panel);
        Assert.Equal("eDP-1", panel.OutputName);
    }

    [Theory]
    [InlineData("interface: 'zwlr_layer_shell_v1', version: 5", true)]
    [InlineData("interface: 'xdg_wm_base', version: 6", false)]
    [InlineData(null, false)]
    public void Capability_probe_requires_the_standard_layer_shell_global(string? registryDump, bool expected)
    {
        Assert.Equal(expected, LayerShellCapability.Supports(registryDump));
    }
}
