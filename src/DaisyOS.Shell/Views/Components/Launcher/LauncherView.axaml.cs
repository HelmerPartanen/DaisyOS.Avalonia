using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using DaisyOS.Shell.Animations;
using DaisyOS.Shell.ViewModels;

namespace DaisyOS.Shell.Views.Components.Launcher;

public partial class LauncherView : UserControl
{
    public LauncherViewModel ViewModel { get; }

    /// <summary>True while the launcher is visible or animating in.</summary>
    public bool IsOpen => _isOpen;

    private bool _isOpen;
    private TranslateTransform? _translate;

    public LauncherView()
    {
        InitializeComponent();
        ViewModel = new LauncherViewModel();
        DataContext = ViewModel;

        _translate = RenderTransform as TranslateTransform;

        // Pre-warm: stay in visual tree so layout is measured once at startup.
        // Opacity=0 + IsHitTestVisible=false = invisible and non-interactive.
        IsHitTestVisible = false;
        Opacity = 0;
        if (_translate != null) _translate.Y = ShellAnimations.LauncherSlideOffset;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public async Task ShowAsync()
    {
        if (_isOpen) return;
        _isOpen = true;
        IsHitTestVisible = true;

        // Snap to off-screen start state with no transitions active.
        ClearTransitions();
        Opacity = 0;
        if (_translate != null) _translate.Y = ShellAnimations.LauncherSlideOffset;

        // Let Avalonia commit this start frame to the GPU before activating
        // transitions — otherwise there is nothing to animate FROM.
        await SkipRenderFrame();

        // Enter: opacity completes faster (130ms) than translate (200ms).
        // This creates a layered "content appears then settles into position"
        // feel that mirrors Apple's HIG recommendations.
        SetOpacityTransition(ShellAnimations.SurfaceEnterOpacityDuration, ShellAnimations.EnterEasing);
        SetTranslateTransition(ShellAnimations.SurfaceEnterTranslateDuration, ShellAnimations.EnterEasing);

        Opacity = 1;
        if (_translate != null) _translate.Y = 0;

        // Wait for the longer of the two (translate).
        await Task.Delay(ShellAnimations.SurfaceEnterTranslateDuration);
        ClearTransitions();
    }

    public async Task HideAsync()
    {
        if (!_isOpen) return;
        _isOpen = false;

        // Exit: opacity + slide-down simultaneously at 150ms.
        // Running both together is fast enough that the Mica transparency
        // mismatch is imperceptible — the launcher is essentially gone
        // before the eye registers the background becoming transparent.
        ClearTransitions();
        SetOpacityTransition(ShellAnimations.SurfaceExitDuration, ShellAnimations.ExitEasing);
        SetTranslateTransition(ShellAnimations.SurfaceExitDuration, ShellAnimations.ExitEasing);

        Opacity = 0;
        if (_translate != null) _translate.Y = ShellAnimations.LauncherSlideOffset;

        await Task.Delay(ShellAnimations.SurfaceExitDuration);

        IsHitTestVisible = false;
        ClearTransitions();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transition helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SetOpacityTransition(TimeSpan duration, Easing easing)
    {
        Transitions ??= new Transitions();
        // Replace or add opacity transition (avoid duplicates).
        Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = easing }
        };
    }

    private void SetTranslateTransition(TimeSpan duration, Easing easing)
    {
        if (_translate == null) return;
        _translate.Transitions = new Transitions
        {
            new DoubleTransition { Property = TranslateTransform.YProperty, Duration = duration, Easing = easing }
        };
    }

    private void ClearTransitions()
    {
        Transitions = null;
        if (_translate != null) _translate.Transitions = null;
    }

    /// <summary>
    /// Waits for Avalonia's render pipeline to process the current UI state.
    /// Ensures property changes (start state) are committed to the GPU before
    /// a Transition is activated, preventing a "jump from nothing" artifact.
    /// </summary>
    private static Task SkipRenderFrame() =>
        Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render).GetTask();

    // ─────────────────────────────────────────────────────────────────────────
    // XAML event handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnAppItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is LauncherItemViewModel item)
            ViewModel.LaunchApp(item);
    }

    private void OnSetViewAlphabetical(object? sender, RoutedEventArgs e) =>
        ViewModel.SetViewAlphabetical();

    private void OnSetViewCategory(object? sender, RoutedEventArgs e) =>
        ViewModel.SetViewCategory();
}
