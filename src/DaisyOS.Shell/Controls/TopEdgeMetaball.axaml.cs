using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DaisyOS.Core.Services;
using DaisyOS.Shell.Services.Compositor;
using DaisyOS.System.Audio;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Controls;

/// <summary>
/// A rounded top-edge volume pocket that remains a stable rectangle while it
/// reveals, moves, and can be dragged across the display.
/// </summary>
public partial class TopEdgeMetaball : UserControl
{
    private const double ContainerHeight = 40;
    private const double RestingTopInset = 4;
    private const double RestingEdgeDistance = RestingTopInset;
    private const double HiddenEdgeDistance = -ContainerHeight;
    private const double MinimumDragEdgeDistance = -(ContainerHeight / 2);
    private static readonly TimeSpan PositionInDuration = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan PositionOutDuration = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan RevealHoldDuration = TimeSpan.FromSeconds(3);
    private const double DetachedBodyWidth = 263;
    private readonly TranslateTransform _positionTransform = new();
    private readonly IAudioService _audioService = new LinuxAudioService(new SafeCommandRunner());
    private IPointer? _dragPointer;
    private CancellationTokenSource? _revealCancellation;
    private CancellationTokenSource? _volumeUpdateCancellation;
    private Visual? _dragCoordinateSpace;
    private Point _dragStart;
    private double _dragStartDistance;
    private double _dragStartHorizontalOffset;
    private double _horizontalOffset;
    private bool _isSynchronizingVolume;

    public static readonly StyledProperty<double> EdgeDistanceProperty =
        AvaloniaProperty.Register<TopEdgeMetaball, double>(nameof(EdgeDistance), HiddenEdgeDistance);

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
        RenderTransform = _positionTransform;
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SetEdgeDistance(HiddenEdgeDistance);
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await RefreshVolumeAsync();
    }

    private async Task RefreshVolumeAsync()
    {
        var volumeSlider = this.FindControl<QuickSettingsSlider>("VolumeSlider");
        if (volumeSlider is null)
        {
            return;
        }

        try
        {
            var volume = await _audioService.GetVolumeAsync();
            _isSynchronizingVolume = true;
            try
            {
                volumeSlider.IsEnabled = volume is not null;
                if (volume is not null)
                {
                    volumeSlider.Value = volume.Value;
                }
            }
            finally
            {
                _isSynchronizingVolume = false;
            }
        }
        catch
        {
            volumeSlider.IsEnabled = false;
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _volumeUpdateCancellation?.Cancel();
        _volumeUpdateCancellation?.Dispose();
        _volumeUpdateCancellation = null;
    }

    private async void OnVolumeChanged(object? sender, EventArgs e)
    {
        if (_isSynchronizingVolume || sender is not QuickSettingsSlider slider || !slider.IsLoaded)
        {
            return;
        }

        _volumeUpdateCancellation?.Cancel();
        _volumeUpdateCancellation?.Dispose();
        _volumeUpdateCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(80, _volumeUpdateCancellation.Token);
            await _audioService.SetVolumeAsync(slider.Value, _volumeUpdateCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            slider.IsEnabled = false;
        }
    }

    /// <summary>Slides the panel in from above the screen edge, then reverses that path.</summary>
    public async Task PlayKeyboardRevealAsync()
    {
        _ = RefreshVolumeAsync();
        _revealCancellation?.Cancel();
        _revealCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _revealCancellation = cancellation;

        try
        {
            if (!IsVisible)
            {
                IsVisible = true;
                SetEdgeDistance(HiddenEdgeDistance);
            }

            await AnimateOpenAsync(cancellation.Token);
            await Task.Delay(RevealHoldDuration, cancellation.Token);
            await AnimateCloseAsync(cancellation.Token);
            IsVisible = false;
            IsHitTestVisible = false;
            KWinBlur.Invalidate(this);
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
        _positionTransform.X = _horizontalOffset;
        _positionTransform.Y = EdgeDistance;
        IsHitTestVisible = IsVisible && EdgeDistance >= 0;
        KWinBlur.Invalidate(this);
    }

    private Task AnimateOpenAsync(CancellationToken cancellationToken) =>
        AnimatePhasesAsync(
            cancellationToken,
            new AnimationPhase(EdgeDistance, RestingEdgeDistance, PositionInDuration, ReverseCurve: false, SetEdgeDistance));

    private Task AnimateCloseAsync(CancellationToken cancellationToken) =>
        AnimatePhasesAsync(
            cancellationToken,
            new AnimationPhase(EdgeDistance, HiddenEdgeDistance, PositionOutDuration, ReverseCurve: true, SetEdgeDistance));

    private async Task AnimatePhasesAsync(CancellationToken cancellationToken, params AnimationPhase[] phases)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            foreach (var phase in phases)
            {
                phase.SetValue(phase.Target);
            }

            return;
        }

        var completion = new TaskCompletionSource();
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        var stopwatch = Stopwatch.StartNew();
        var phaseIndex = 0;
        var completedPhaseDuration = TimeSpan.Zero;

        void RenderFrame(TimeSpan _)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            var elapsed = stopwatch.Elapsed - completedPhaseDuration;
            while (phaseIndex < phases.Length)
            {
                var phase = phases[phaseIndex];
                if (elapsed < phase.Duration)
                {
                    var progress = elapsed.TotalMilliseconds / phase.Duration.TotalMilliseconds;
                    var easedProgress = phase.ReverseCurve
                        ? 1 - EvaluateRevealBezier(1 - progress)
                        : EvaluateRevealBezier(progress);
                    phase.SetValue(phase.Start + ((phase.Target - phase.Start) * easedProgress));
                    topLevel.RequestAnimationFrame(RenderFrame);
                    return;
                }

                phase.SetValue(phase.Target);
                completedPhaseDuration += phase.Duration;
                elapsed -= phase.Duration;
                phaseIndex++;
            }

            completion.TrySetResult();
        }

        topLevel.RequestAnimationFrame(RenderFrame);
        await completion.Task;
    }

    private sealed record AnimationPhase(
        double Start,
        double Target,
        TimeSpan Duration,
        bool ReverseCurve,
        Action<double> SetValue);

    private void SetEdgeDistance(double distance)
    {
        var clampedDistance = Math.Clamp(distance, HiddenEdgeDistance, RestingEdgeDistance);
        if (Math.Abs(EdgeDistance - clampedDistance) < 0.001)
        {
            UpdateGeometry();
            return;
        }

        EdgeDistance = clampedDistance;
    }

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
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.Source is Visual source && (source is QuickSettingsSlider || source.FindAncestorOfType<QuickSettingsSlider>() is not null))
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
        EdgeDistance = Math.Clamp(_dragStartDistance + delta.Y, MinimumDragEdgeDistance, RestingEdgeDistance);
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
