using System;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace DaisyOS.Shell.Services.Desktop;

public static class ItemMotionAnimator
{
    public static void Attach(Control control, TimeSpan duration)
    {
        var visual = ElementComposition.GetElementVisual(control);
        if (visual is null)
        {
            // If visual is not initialized yet, attach on AttachedToVisualTree
            control.AttachedToVisualTree += OnAttached;
            return;
        }

        ConfigureImplicitAnimations(control, visual, duration);
    }

    private static void OnAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            control.AttachedToVisualTree -= OnAttached;
            var visual = ElementComposition.GetElementVisual(control);
            if (visual is not null)
            {
                ConfigureImplicitAnimations(control, visual, DesktopMotionSettings.Default.ReorderAnimationDuration);
            }
        }
    }

    private static void ConfigureImplicitAnimations(Control control, CompositionVisual visual, TimeSpan duration)
    {
        try
        {
            var compositor = visual.Compositor;
            var animation = compositor.CreateVector3KeyFrameAnimation();
            animation.Duration = duration;
            animation.Target = "Offset";
            // Expression keyframe smoothly interpolates to the layout's new FinalValue from current rendered position
            animation.InsertExpressionKeyFrame(1f, "this.FinalValue");

            var implicitAnimations = compositor.CreateImplicitAnimationCollection();
            implicitAnimations["Offset"] = animation;
            visual.ImplicitAnimations = implicitAnimations;
        }
        catch
        {
            // Fallback gracefully if composition animation is unavailable in environment
        }
    }

    public static void Detach(Control control)
    {
        var visual = ElementComposition.GetElementVisual(control);
        if (visual is not null)
        {
            visual.ImplicitAnimations = null;
        }
    }
}
