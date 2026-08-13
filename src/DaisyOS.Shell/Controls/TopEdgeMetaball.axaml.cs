using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DaisyOS.Shell.Controls;

/// <summary>
/// A normal rounded shell pocket that grows a smooth bridge into the top screen
/// edge when it comes within <see cref="MergeThreshold"/> pixels. Animate
/// <see cref="EdgeDistance"/> to move between the detached and merged states.
/// </summary>
public partial class TopEdgeMetaball : UserControl
{
    private const double TopEdgeOverlap = 1;
    private const double RestingTopInset = 4;
    // Keep the flare local to the edge; it has fully resolved before the
    // component reaches its separate 4 px resting inset.
    public const double MergeThreshold = 1;
    // Include the 1 px seam overlap so the rendered resting inset is 4 px.
    private const double RestingEdgeDistance = TopEdgeOverlap + RestingTopInset;
    private const double EdgeFlare = 18;
    private static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan PositionDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan RevealHoldDuration = TimeSpan.FromSeconds(3);
    private const double DetachedBodyWidth = 240;
    private const double MergedBodyWidth = 240;
    private const double BodyCornerRadius = 12;
    private readonly TranslateTransform _positionTransform = new();
    private readonly ScaleTransform _revealTransform = new(1, 0);
    private IPointer? _dragPointer;
    private CancellationTokenSource? _revealCancellation;
    private Visual? _dragCoordinateSpace;
    private Point _dragStart;
    private double _dragStartDistance;
    private double _dragStartHorizontalOffset;
    private double _horizontalOffset;

    public static readonly StyledProperty<double> EdgeDistanceProperty =
        AvaloniaProperty.Register<TopEdgeMetaball, double>(nameof(EdgeDistance), 4d);

    public double EdgeDistance
    {
        get => GetValue(EdgeDistanceProperty);
        set => SetValue(EdgeDistanceProperty, value);
    }

    static TopEdgeMetaball()
    {
        EdgeDistanceProperty.Changed.AddClassHandler<TopEdgeMetaball>((surface, _) => surface.UpdateGeometry());
    }

    public TopEdgeMetaball()
    {
        InitializeComponent();
        RenderTransform = new TransformGroup
        {
            Children = { _revealTransform, _positionTransform }
        };
        RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
        SetRevealProgress(0);
    }

    /// <summary>
    /// Scales at the top edge, then moves to its resting inset. The return
    /// reverses position before scale, so the two properties never animate
    /// together. Repeated requests restart from the current frame.
    /// </summary>
    public async Task PlayKeyboardRevealAsync()
    {
        _revealCancellation?.Cancel();
        _revealCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _revealCancellation = cancellation;

        try
        {
            SetEdgeDistance(0);
            await AnimateRevealAsync(1, reverseCurve: false, cancellation.Token);
            await AnimatePositionAsync(RestingEdgeDistance, reverseCurve: false, cancellation.Token);
            await Task.Delay(RevealHoldDuration, cancellation.Token);
            await AnimatePositionAsync(0, reverseCurve: true, cancellation.Token);
            await AnimateRevealAsync(0, reverseCurve: true, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // A later shortcut press owns the next animation sequence.
        }
        finally
        {
            if (ReferenceEquals(_revealCancellation, cancellation))
            {
                _revealCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void UpdateGeometry()
    {
        var distance = Math.Max(0, EdgeDistance);
        _positionTransform.X = _horizontalOffset;
        _positionTransform.Y = distance - TopEdgeOverlap;

        var proximity = Math.Clamp(1 - (distance / MergeThreshold), 0, 1);

        // The body stays full width while detached, then contracts as it joins
        // the screen edge. Smoothstep keeps the transition calm at both ends.
        var widthMorph = proximity * proximity * (3 - (2 * proximity));
        var bodyWidth = DetachedBodyWidth - ((DetachedBodyWidth - MergedBodyWidth) * widthMorph);
        PocketBody.Width = bodyWidth;
        Canvas.SetLeft(PocketBody, (DetachedBodyWidth - bodyWidth) / 2);

        EdgeBridge.IsVisible = proximity > 0;
        if (proximity <= 0)
        {
            EdgeBridge.Data = null;
            return;
        }

        // Near the edge, the bridge is a slightly wider top cap rather than a
        // narrow neck. Its shoulders then resolve into the fixed-height,
        // radius-12 body, producing the requested flared-edge morph.
        var center = DetachedBodyWidth / 2;
        var morph = Math.Sqrt(proximity);
        var edgeHalfWidth = (bodyWidth / 2 + EdgeFlare) * morph;
        var bodyHalfWidth = bodyWidth / 2;
        // The bridge is drawn above the body's local bounds. The translation
        // puts its upper edge at the physical screen edge while keeping the
        // rounded 180 x 52 body itself fully draggable and hit-testable.
        var edgeY = -distance;
        var joinY = BodyCornerRadius;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(center - edgeHalfWidth, edgeY), isFilled: true);
            context.LineTo(new Point(center + edgeHalfWidth, edgeY), isStroked: false);
            context.CubicBezierTo(
                new Point(center + edgeHalfWidth, edgeY + distance * 0.18),
                new Point(center + bodyHalfWidth, edgeY + distance * 0.75),
                new Point(center + bodyHalfWidth, joinY),
                isStroked: false);
            context.LineTo(new Point(center - bodyHalfWidth, joinY), isStroked: false);
            context.CubicBezierTo(
                new Point(center - bodyHalfWidth, edgeY + distance * 0.75),
                new Point(center - edgeHalfWidth, edgeY + distance * 0.18),
                new Point(center - edgeHalfWidth, edgeY),
                isStroked: false);
            context.EndFigure(isClosed: true);
        }

        EdgeBridge.Data = geometry;
    }

    private async Task AnimateRevealAsync(double target, bool reverseCurve, CancellationToken cancellationToken)
    {
        await AnimateValueAsync(_revealTransform.ScaleY, target, RevealDuration, reverseCurve, SetRevealProgress, cancellationToken);
    }

    private async Task AnimatePositionAsync(double target, bool reverseCurve, CancellationToken cancellationToken)
    {
        await AnimateValueAsync(EdgeDistance, target, PositionDuration, reverseCurve, SetEdgeDistance, cancellationToken);
    }

    private async Task AnimateValueAsync(
        double start,
        double target,
        TimeSpan duration,
        bool reverseCurve,
        Action<double> setValue,
        CancellationToken cancellationToken)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            setValue(target);
            return;
        }

        if (Math.Abs(target - start) < 0.001)
        {
            setValue(target);
            return;
        }

        var completion = new TaskCompletionSource();
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        var stopwatch = Stopwatch.StartNew();

        void RenderFrame(TimeSpan _)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            var progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            var easedProgress = reverseCurve
                ? 1 - EvaluateRevealBezier(1 - progress)
                : EvaluateRevealBezier(progress);
            setValue(start + ((target - start) * easedProgress));

            if (progress >= 1)
            {
                completion.TrySetResult();
                return;
            }

            topLevel.RequestAnimationFrame(RenderFrame);
        }

        topLevel.RequestAnimationFrame(RenderFrame);
        await completion.Task;
    }

    private void SetRevealProgress(double progress)
    {
        var clampedProgress = Math.Clamp(progress, 0, 1);
        _revealTransform.ScaleY = clampedProgress;
        IsHitTestVisible = clampedProgress > 0;
        // The top transform origin keeps this boundary fixed to the screen
        // edge; reveal progress changes only the component's height.
        EdgeDistance = 0;
    }

    private void SetEdgeDistance(double distance) => EdgeDistance = Math.Clamp(distance, 0, RestingEdgeDistance);

    // Cubic-bezier(0.22, 1, 0.36, 1): soft approach, with the return using
    // the mathematically reversed curve so it retraces the reveal exactly.
    private static double EvaluateRevealBezier(double progress)
    {
        var lower = 0d;
        var upper = 1d;

        for (var iteration = 0; iteration < 12; iteration++)
        {
            var parameter = (lower + upper) / 2;
            if (CubicBezier(parameter, 0.22, 0.36) < progress)
            {
                lower = parameter;
            }
            else
            {
                upper = parameter;
            }
        }

        return CubicBezier((lower + upper) / 2, 1, 1);
    }

    private static double CubicBezier(double parameter, double firstControlPoint, double secondControlPoint)
    {
        var inverse = 1 - parameter;
        return (3 * inverse * inverse * parameter * firstControlPoint)
            + (3 * inverse * parameter * parameter * secondControlPoint)
            + (parameter * parameter * parameter);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragPointer = e.Pointer;
        _dragCoordinateSpace = TopLevel.GetTopLevel(this);
        _dragStart = e.GetPosition(_dragCoordinateSpace);
        _dragStartDistance = EdgeDistance;
        _dragStartHorizontalOffset = _horizontalOffset;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _dragPointer))
        {
            return;
        }

        var current = e.GetPosition(_dragCoordinateSpace);
        var delta = current - _dragStart;
        _horizontalOffset = ClampHorizontalOffset(_dragStartHorizontalOffset + delta.X);
        EdgeDistance = Math.Max(0, _dragStartDistance + delta.Y);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _dragPointer))
        {
            return;
        }

        e.Pointer.Capture(null);
        _dragPointer = null;
        _dragCoordinateSpace = null;
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ReferenceEquals(e.Pointer, _dragPointer))
        {
            _dragPointer = null;
            _dragCoordinateSpace = null;
        }
    }

    private double ClampHorizontalOffset(double offset)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return offset;
        }

        var limit = Math.Max(0, (topLevel.ClientSize.Width - DetachedBodyWidth) / 2);
        return Math.Clamp(offset, -limit, limit);
    }
}
