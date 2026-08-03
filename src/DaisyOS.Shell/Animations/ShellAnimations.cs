using System;
using Avalonia.Animation.Easings;

namespace DaisyOS.Shell.Animations;

/// <summary>
/// Centralized animation timing and easing constants for DaisyOS Shell.
/// All durations and curves are modelled after Apple's HIG animation principles:
///   - Enter animations decelerate into place (spring-like ease-out)
///   - Exit animations accelerate out of view (sharp ease-in)
///   - Overlapping/interactive elements are 10-15% faster than primary surfaces
/// </summary>
public static class ShellAnimations
{
    // ───────────────────────────────────────────────────────────────────────────
    // Durations
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>Primary surface appear (launcher, large panels). 200ms — snappy but perceptibly smooth.</summary>
    public static readonly TimeSpan SurfaceEnterDuration  = TimeSpan.FromMilliseconds(200);

    /// <summary>Primary surface translate settle on enter. 200ms.</summary>
    public static readonly TimeSpan SurfaceEnterTranslateDuration = TimeSpan.FromMilliseconds(200);

    /// <summary>Opacity lead-in on enter — finishes before position settles for a layered feel. 130ms.</summary>
    public static readonly TimeSpan SurfaceEnterOpacityDuration = TimeSpan.FromMilliseconds(130);

    /// <summary>Exit: opacity + slide simultaneously. 150ms — gone before mismatch registers.</summary>
    public static readonly TimeSpan SurfaceExitDuration   = TimeSpan.FromMilliseconds(150);

    /// <summary>Smaller element enter (cards, menus). 180ms.</summary>
    public static readonly TimeSpan ElementEnterDuration  = TimeSpan.FromMilliseconds(180);

    /// <summary>Smaller element exit. 120ms.</summary>
    public static readonly TimeSpan ElementExitDuration   = TimeSpan.FromMilliseconds(120);

    /// <summary>Micro interactions (icon presses, highlights). 100ms.</summary>
    public static readonly TimeSpan MicroDuration         = TimeSpan.FromMilliseconds(100);

    // ───────────────────────────────────────────────────────────────────────────
    // Easing Curves — Apple-like SplineEasing approximations
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enter easing: fast start, smooth deceleration into resting position.
    /// Approximates Apple's 'easeOut' / spring settle.
    /// Cubic-bezier: (0.16, 1.0, 0.3, 1.0)
    /// </summary>
    public static readonly SplineEasing EnterEasing = new SplineEasing(0.16, 1.0, 0.3, 1.0);

    /// <summary>
    /// Exit easing: gentle start, sharp acceleration out of view.
    /// Approximates Apple's 'easeIn' for dismissals.
    /// Cubic-bezier: (0.4, 0.0, 1.0, 1.0)
    /// </summary>
    public static readonly SplineEasing ExitEasing  = new SplineEasing(0.4, 0.0, 1.0, 1.0);

    /// <summary>
    /// Smooth symmetric ease for continuous motion (scrolling, resizing).
    /// Cubic-bezier: (0.45, 0.0, 0.55, 1.0)
    /// </summary>
    public static readonly SplineEasing SmoothEasing = new SplineEasing(0.45, 0.0, 0.55, 1.0);

    // ───────────────────────────────────────────────────────────────────────────
    // Launcher-specific slide offset
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pixels the launcher slides vertically on enter.
    /// Small offset keeps the motion subtle and purposeful on high-DPI displays.
    /// Exit does NOT slide — opacity-only fade avoids background/content mismatch.
    /// </summary>
    public const double LauncherSlideOffset = 12.0;
}
