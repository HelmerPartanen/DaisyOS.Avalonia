using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DaisyOS.Shell.Controls;

/// <summary>
/// A static top-edge pocket with the merged top-edge silhouette.
/// </summary>
public partial class TopEdgeMetaball : UserControl
{
    private const double BodyWidth = 180;
    private const double EdgeFlare = 18;
    private const double BodyCornerRadius = 12;

    public TopEdgeMetaball()
    {
        InitializeComponent();
        BuildMergedEdgeGeometry();
    }

    private void BuildMergedEdgeGeometry()
    {
        var center = BodyWidth / 2;
        var edgeHalfWidth = (BodyWidth / 2) + EdgeFlare;
        var bodyHalfWidth = BodyWidth / 2;

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(center - edgeHalfWidth, 0), isFilled: true);
        context.LineTo(new Point(center + edgeHalfWidth, 0), isStroked: false);
        context.CubicBezierTo(
            new Point(center + edgeHalfWidth, 0),
            new Point(center + bodyHalfWidth, 0),
            new Point(center + bodyHalfWidth, BodyCornerRadius),
            isStroked: false);
        context.LineTo(new Point(center - bodyHalfWidth, BodyCornerRadius), isStroked: false);
        context.CubicBezierTo(
            new Point(center - bodyHalfWidth, 0),
            new Point(center - edgeHalfWidth, 0),
            new Point(center - edgeHalfWidth, 0),
            isStroked: false);
        context.EndFigure(isClosed: true);
        EdgeBridge.Data = geometry;
    }
}
