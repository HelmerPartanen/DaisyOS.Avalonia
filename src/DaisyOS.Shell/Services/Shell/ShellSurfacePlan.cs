namespace DaisyOS.Shell.Services.Shell;

/// <summary>
/// Declarative production surface plan. KWin owns normal application windows;
/// DaisyOS owns only the layer surfaces listed here. The primary-only panel is
/// an explicit product decision, not an accidental first-output assumption.
/// </summary>
public static class ShellSurfacePlan
{
    public const int PrimaryPanelExclusiveZone = 68;

    public static IReadOnlyList<LayerShellSurfaceOptions> Create(string primaryOutputName, IEnumerable<string> outputNames)
    {
        var outputs = outputNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).ToArray();
        if (outputs.Length == 0)
        {
            outputs = [primaryOutputName];
        }

        var surfaces = outputs.Select(LayerShellSurfaceOptions.Desktop).ToList();
        surfaces.Add(LayerShellSurfaceOptions.PrimaryPanel(PrimaryPanelExclusiveZone, primaryOutputName));
        surfaces.Add(LayerShellSurfaceOptions.Overlay("daisyos.overlay.launcher", primaryOutputName));
        surfaces.Add(LayerShellSurfaceOptions.Overlay("daisyos.overlay.system", primaryOutputName));
        return surfaces;
    }
}
