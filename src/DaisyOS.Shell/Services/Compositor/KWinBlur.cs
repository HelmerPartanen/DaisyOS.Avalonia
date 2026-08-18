using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DaisyOS.Shell.Services.Compositor;

/// <summary>
/// Marks a visual as a compositor-backed material region. Regions belonging to the
/// same top-level are merged before being sent to KWin, because KWin sees a native
/// window surface rather than DaisyOS' individual controls.
/// </summary>
public static class KWinBlur
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<KWinBlurOwner, Control, bool>("IsEnabled");

    /// <summary>
    /// Clips KWin's rectangular blur protocol to a rounded material silhouette.
    /// KWin does not accept paths here, so the corners are represented by narrow
    /// horizontal region bands instead of blurring the transparent corner pixels.
    /// </summary>
    public static readonly AttachedProperty<double> CornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<KWinBlurOwner, Control, double>("CornerRadius", 0d);

    private static readonly ConditionalWeakTable<Control, Registration> Registrations = new();

    static KWinBlur()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>((visual, change) =>
        {
            if (change.GetNewValue<bool>())
            {
                Registrations.GetValue(visual, static item => new Registration(item)).Enable();
            }
            else if (Registrations.TryGetValue(visual, out var registration))
            {
                registration.Disable();
            }
        });

        CornerRadiusProperty.Changed.AddClassHandler<Control>((visual, _) =>
        {
            if (Registrations.TryGetValue(visual, out var registration))
            {
                registration.Invalidate();
            }
        });
    }

    public static bool GetIsEnabled(Control visual) => visual.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Control visual, bool value) => visual.SetValue(IsEnabledProperty, value);

    public static double GetCornerRadius(Control visual) => visual.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(Control visual, double value) => visual.SetValue(CornerRadiusProperty, value);

    /// <summary>
    /// Republishes a material region after a render-transform change. Layout updates
    /// do not fire for translations or scales, but KWin's native region must follow them.
    /// </summary>
    public static void Invalidate(Control visual)
    {
        if (Registrations.TryGetValue(visual, out var registration))
        {
            registration.Invalidate();
        }
    }

    /// <summary>
    /// Changes KWin's global blur-kernel strength. This affects every KWin blur
    /// surface, not only the visual passed to <see cref="SetIsEnabled"/>.
    /// </summary>
    public static Task<KWinBlurStrengthResult> SetStrengthAsync(
        int strength,
        CancellationToken cancellationToken = default) =>
        new KWinBlurSettingsService(new DaisyOS.System.Processes.SafeCommandRunner())
            .SetStrengthAsync(strength, cancellationToken);

    private sealed class Registration
    {
        private readonly Control _visual;
        private KWinBlurController? _controller;
        private bool _enabled;

        public Registration(Control visual)
        {
            _visual = visual;
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _enabled = true;
            _visual.AttachedToVisualTree += OnAttached;
            _visual.DetachedFromVisualTree += OnDetached;
            _visual.LayoutUpdated += OnLayoutUpdated;
            AttachToTopLevel();
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _enabled = false;
            _visual.AttachedToVisualTree -= OnAttached;
            _visual.DetachedFromVisualTree -= OnDetached;
            _visual.LayoutUpdated -= OnLayoutUpdated;
            _controller?.Unregister(_visual);
            _controller = null;
        }

        public void Invalidate() => _controller?.Invalidate();

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => AttachToTopLevel();

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _controller?.Unregister(_visual);
            _controller = null;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (_controller is null)
            {
                AttachToTopLevel();
            }

            _controller?.Invalidate();
        }

        private void AttachToTopLevel()
        {
            if (!_enabled)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(_visual);
            if (topLevel is null)
            {
                return;
            }

            _controller = KWinBlurController.For(topLevel);
            _controller.Register(_visual);
        }
    }
}

internal sealed class KWinBlurOwner;

internal sealed class KWinBlurController
{
    private static readonly ConditionalWeakTable<TopLevel, KWinBlurController> Controllers = new();
    private readonly TopLevel _topLevel;
    private readonly HashSet<Control> _regions = [];
    private bool _refreshQueued;

    private KWinBlurController(TopLevel topLevel)
    {
        _topLevel = topLevel;
        _topLevel.Closed += OnClosed;
    }

    public static KWinBlurController For(TopLevel topLevel) =>
        Controllers.GetValue(topLevel, static owner => new KWinBlurController(owner));

    public void Register(Control visual)
    {
        _regions.Add(visual);
        Invalidate();
    }

    public void Unregister(Control visual)
    {
        if (_regions.Remove(visual))
        {
            Invalidate();
        }
    }

    public void Invalidate()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Render);
    }

    private void Refresh()
    {
        _refreshQueued = false;

        var scale = _topLevel.RenderScaling;
        var regions = new List<KWinBlurRectangle>(_regions.Count);
        foreach (var visual in _regions)
        {
            if (ReferenceEquals(visual, _topLevel))
            {
                var clientSize = _topLevel.ClientSize;
                if (clientSize.Width > 0 && clientSize.Height > 0)
                {
                    regions.AddRange(KWinBlurRectangle.FromLogicalBounds(default, clientSize, scale, KWinBlur.GetCornerRadius(visual)));
                }

                continue;
            }

            if (!visual.IsEffectivelyVisible || visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)
            {
                continue;
            }

            if (!TryGetTransformedBounds(visual, out var origin, out var size))
            {
                continue;
            }

            regions.AddRange(KWinBlurRectangle.FromLogicalBounds(origin, size, scale, KWinBlur.GetCornerRadius(visual)));
        }

        KWinNativeBlurRegion.TrySet(_topLevel, regions);
    }

    private bool TryGetTransformedBounds(Control visual, out Point origin, out Size size)
    {
        origin = default;
        size = default;

        var topLeft = visual.TranslatePoint(default, _topLevel);
        var bottomRight = visual.TranslatePoint(new Point(visual.Bounds.Width, visual.Bounds.Height), _topLevel);
        if (topLeft is null || bottomRight is null)
        {
            return false;
        }

        origin = new Point(
            Math.Min(topLeft.Value.X, bottomRight.Value.X),
            Math.Min(topLeft.Value.Y, bottomRight.Value.Y));
        size = new Size(
            Math.Abs(bottomRight.Value.X - topLeft.Value.X),
            Math.Abs(bottomRight.Value.Y - topLeft.Value.Y));
        return size.Width > 0 && size.Height > 0;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _topLevel.Closed -= OnClosed;
        KWinNativeBlurRegion.TryClear(_topLevel);
        _regions.Clear();
    }
}

internal readonly record struct KWinBlurRectangle(int X, int Y, int Width, int Height)
{
    public static IReadOnlyList<KWinBlurRectangle> FromLogicalBounds(Point origin, Size size, double scale, double cornerRadius = 0)
    {
        var left = (int)Math.Floor(origin.X * scale);
        var top = (int)Math.Floor(origin.Y * scale);
        var right = (int)Math.Ceiling((origin.X + size.Width) * scale);
        var bottom = (int)Math.Ceiling((origin.Y + size.Height) * scale);
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, bottom - top);
        var radius = Math.Clamp((int)Math.Ceiling(cornerRadius * scale), 0, Math.Min(width, height) / 2);
        if (radius == 0)
        {
            return [new KWinBlurRectangle(left, top, width, height)];
        }

        var regions = new List<KWinBlurRectangle>((radius * 2) + 1);
        for (var row = 0; row < radius; row++)
        {
            // Sampling the centre of each physical-pixel band prevents a square
            // corner while keeping the blur region continuous around the curve.
            var vertical = radius - row - 0.5;
            var inset = (int)Math.Ceiling(radius - Math.Sqrt((radius * radius) - (vertical * vertical)));
            AddBand(regions, left + inset, top + row, width - (inset * 2));
        }

        var middleHeight = height - (radius * 2);
        if (middleHeight > 0)
        {
            regions.Add(new KWinBlurRectangle(left, top + radius, width, middleHeight));
        }

        for (var row = 0; row < radius; row++)
        {
            var vertical = row + 0.5;
            var inset = (int)Math.Ceiling(radius - Math.Sqrt((radius * radius) - (vertical * vertical)));
            AddBand(regions, left + inset, bottom - radius + row, width - (inset * 2));
        }

        return regions;
    }

    private static void AddBand(List<KWinBlurRectangle> regions, int x, int y, int width)
    {
        if (width <= 0)
        {
            return;
        }

        regions.Add(new KWinBlurRectangle(x, y, width, 1));
    }
}

/// <summary>
/// KWin's X11/XWayland blur contract. Avalonia 12.0 does not expose the wl_surface
/// required by org_kde_kwin_blur_manager, so native Wayland sessions safely leave this
/// unset instead of attempting to create a second, invalid Wayland connection.
/// </summary>
internal static class KWinNativeBlurRegion
{
    private const string X11HandleDescriptor = "XID";
    private const uint XcbAtomCardinal = 6;
    private const int PropModeReplace = 0;

    public static bool TrySet(TopLevel topLevel, IReadOnlyList<KWinBlurRectangle> regions)
    {
        if (!TryGetX11Window(topLevel, out var window))
        {
            return false;
        }

        if (regions.Count == 0)
        {
            return TryClear(window);
        }

        // Xlib's format-32 property API consumes C longs, which are 64-bit on
        // this target. Passing a uint[] causes it to stride over every other
        // CARDINAL and produces malformed KWin rectangles.
        var data = new IntPtr[regions.Count * 4];
        for (var index = 0; index < regions.Count; index++)
        {
            var region = regions[index];
            var offset = index * 4;
            data[offset] = (IntPtr)region.X;
            data[offset + 1] = (IntPtr)region.Y;
            data[offset + 2] = (IntPtr)region.Width;
            data[offset + 3] = (IntPtr)region.Height;
        }

        return TryChangeProperty(window, data);
    }

    public static bool TryClear(TopLevel topLevel) =>
        TryGetX11Window(topLevel, out var window) && TryClear(window);

    private static bool TryGetX11Window(TopLevel topLevel, out IntPtr window)
    {
        window = IntPtr.Zero;
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var handle = topLevel.TryGetPlatformHandle();
        if (handle is null || !string.Equals(handle.HandleDescriptor, X11HandleDescriptor, StringComparison.Ordinal))
        {
            return false;
        }

        window = handle.Handle;
        return window != IntPtr.Zero;
    }

    private static readonly object _x11Lock = new();
    private static IntPtr _sharedDisplay = IntPtr.Zero;
    private static IntPtr _cachedBlurAtom = IntPtr.Zero;
    private static bool _x11Failed;

    private static (IntPtr display, IntPtr blurAtom) GetX11Context()
    {
        if (_x11Failed) return (IntPtr.Zero, IntPtr.Zero);
        if (_sharedDisplay != IntPtr.Zero && _cachedBlurAtom != IntPtr.Zero)
            return (_sharedDisplay, _cachedBlurAtom);

        lock (_x11Lock)
        {
            if (_x11Failed) return (IntPtr.Zero, IntPtr.Zero);
            if (_sharedDisplay != IntPtr.Zero && _cachedBlurAtom != IntPtr.Zero)
                return (_sharedDisplay, _cachedBlurAtom);

            try
            {
                _sharedDisplay = XOpenDisplay(IntPtr.Zero);
                if (_sharedDisplay != IntPtr.Zero)
                {
                    _cachedBlurAtom = XInternAtom(_sharedDisplay, "_KDE_NET_WM_BLUR_BEHIND_REGION", false);
                }
                if (_sharedDisplay == IntPtr.Zero || _cachedBlurAtom == IntPtr.Zero)
                {
                    _x11Failed = true;
                }
            }
            catch
            {
                _x11Failed = true;
            }

            return (_sharedDisplay, _cachedBlurAtom);
        }
    }

    private static bool TryChangeProperty(IntPtr window, IntPtr[] data)
    {
        var (display, blurAtom) = GetX11Context();
        if (display == IntPtr.Zero || blurAtom == IntPtr.Zero) return false;

        try
        {
            XChangeProperty(display, window, blurAtom, (IntPtr)XcbAtomCardinal, 32, PropModeReplace, data, data.Length);
            XFlush(display);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryClear(IntPtr window)
    {
        var (display, blurAtom) = GetX11Context();
        if (display == IntPtr.Zero || blurAtom == IntPtr.Zero) return false;

        try
        {
            XDeleteProperty(display, window, blurAtom);
            XFlush(display);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6", CharSet = CharSet.Ansi)]
    private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

    [DllImport("libX11.so.6")]
    private static extern int XChangeProperty(
        IntPtr display,
        IntPtr window,
        IntPtr property,
        IntPtr type,
        int format,
        int mode,
        [In] IntPtr[] data,
        int elementCount);

    [DllImport("libX11.so.6")]
    private static extern int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);
}
