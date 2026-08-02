using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DaisyOS.Shell;
using DaisyOS.Shell.Controls;
using DaisyOS.Shell.Services;
using DaisyOS.Shell.ViewModels;
using DaisyOS.Shell.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(DaisyOS.ShellDemo.Tests.ShellDemoTestApp))]

namespace DaisyOS.ShellDemo.Tests;

public sealed class ShellDemoHeadlessTests
{
    [AvaloniaFact]
    public void DesktopDragGhostBindsToTheDraggedItemsIcon()
    {
        var item = new DesktopItemViewModel(
            "files",
            "Files",
            "avares://DaisyOS.Shell/Assets/AppIcons/FilesIcon.png");
        var view = new DesktopView();
        var window = new Window { Content = view };

        try
        {
            window.Show();
            var ghost = view.FindControl<Border>("DragGhost")
                ?? throw new InvalidOperationException("Desktop drag ghost was not found.");
            ghost.DataContext = item;
            ghost.IsVisible = true;
            window.UpdateLayout();

            var ghostImage = Assert.Single(ghost.GetVisualDescendants().OfType<Image>());
            Assert.Same(item.IconImage, ghostImage.Source);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PolishedShellDemoSurfacesAndInteractionsPass()
    {
        var services = ShellServices.CreateMock();
        using var shell = new ShellViewModel(
            services,
            new ShellStartupOptions(ForceOnboarding: false, SkipOnboarding: true));
        var window = CreateWindow(shell, width: 1280, height: 800);

        try
        {
            AssertRendered(window, "desktop");
            AssertContextMenuRendered(window);

            shell.ToggleLauncherCommand.Execute(null);
            Assert.True(shell.IsLauncherOpen);
            var launcherWindow = FindOwnedWindow(window, "DaisyOS launcher");
            Assert.True(launcherWindow.IsVisible);
            AssertRendered(launcherWindow, "launcher");

            shell.OpenLauncherPowerMenuCommand.Execute(null);
            Assert.True(shell.IsLauncherOpen);
            Assert.True(shell.Launcher.IsPowerMenuOpen);
            Assert.True(launcherWindow.IsVisible);
            AssertRendered(launcherWindow, "power menu");
            PressEscape(launcherWindow);
            Assert.False(shell.IsLauncherOpen);
            Assert.False(shell.Launcher.IsPowerMenuOpen);
            await WaitForAsync(() => !launcherWindow.IsVisible);
            Assert.False(launcherWindow.IsVisible);

            shell.ToggleQuickSearchCommand.Execute(null);
            Assert.True(shell.IsQuickSearchOpen);
            var quickSearchWindow = FindOwnedWindow(window, "DaisyOS search");
            Assert.True(quickSearchWindow.IsVisible);
            AssertRendered(quickSearchWindow, "quick search");
            var collapsedSearchHeight = quickSearchWindow.ClientSize.Height;
            var searchFieldTop = quickSearchWindow.Position.Y;
            shell.QuickSearch.Query = "settings";
            Assert.True(shell.QuickSearch.ShouldShowResults);
            AssertRendered(quickSearchWindow, "opening quick search results");
            var resultsHost = quickSearchWindow.GetVisualDescendants()
                .OfType<Border>()
                .Single(control => control.Classes.Contains("QuickSearchResultsHost"));
            Assert.Contains("Open", resultsHost.Classes);
            await WaitForAsync(() => shell.QuickSearch.Results.Count > 0);
            AssertRendered(quickSearchWindow, "quick search results");
            Assert.True(resultsHost.Bounds.Height > 0, "Quick Search results stayed collapsed after results arrived.");
            Assert.True(quickSearchWindow.ClientSize.Height > collapsedSearchHeight,
                "Quick Search native window did not grow to show its results.");
            Assert.Equal(searchFieldTop, quickSearchWindow.Position.Y);
            PressEscape(quickSearchWindow);
            Assert.False(shell.IsQuickSearchOpen);
            Assert.False(quickSearchWindow.IsVisible);

            shell.SystemBar.ToggleCalendarCommand.Execute(null);
            Assert.True(shell.IsCalendarOpen);
            AssertRendered(window, "calendar");
            PressEscape(window);
            Assert.False(shell.IsCalendarOpen);

            shell.SystemBar.OpenNotificationsCommand.Execute(null);
            Assert.True(shell.IsNotificationCenterOpen);
            AssertRendered(window, "notifications");
            PressEscape(window);
            Assert.False(shell.IsNotificationCenterOpen);

            shell.OpenUpdatesCommand.Execute(null);
            Assert.True(shell.IsSettingsOpen);
            Assert.True(shell.Settings.IsUpdatesPage);

            shell.LockScreenCommand.Execute(null);
            Assert.True(shell.IsLockScreenOpen);
            var lockPasswordWindow = FindOwnedWindow(window, "DaisyOS lock screen password");
            Assert.True(lockPasswordWindow.IsVisible);
            AssertRendered(window, "lock screen");
            Assert.Equal(new Size(240, 38), lockPasswordWindow.ClientSize);
            PressShortcut(window, Key.Space, PhysicalKey.Space, RawInputModifiers.Control);
            Assert.False(shell.IsQuickSearchOpen);
            shell.LockScreenPassword = "password";
            shell.UnlockScreenCommand.Execute(null);
            Assert.False(shell.IsLockScreenOpen);
            Assert.False(lockPasswordWindow.IsVisible);

            AssertRendered(window, "returned desktop");
            AssertSupportedDemoSizes();
            await AssertOwnedDemoWindowsAsync(shell);
            var systemBarWindow = FindOwnedWindow(window, "DaisyOS system bar");
            Assert.Equal(78, systemBarWindow.ClientSize.Height);
            systemBarWindow.MouseDown(
                new Point(systemBarWindow.ClientSize.Width - 40, systemBarWindow.ClientSize.Height - 20),
                MouseButton.Right,
                RawInputModifiers.None);
            systemBarWindow.MouseUp(
                new Point(systemBarWindow.ClientSize.Width - 40, systemBarWindow.ClientSize.Height - 20),
                MouseButton.Right,
                RawInputModifiers.None);
            var visibilityMenuWindow = FindOwnedWindow(window, "DaisyOS system bar items");
            Assert.True(visibilityMenuWindow.IsVisible);
            Assert.Equal(260, visibilityMenuWindow.ClientSize.Width);
            var visibilityToggles = visibilityMenuWindow.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Where(control => control.Classes.Contains("SystemBarVisibilityItem"))
                .ToArray();
            Assert.Equal(12, visibilityToggles.Length);
            var visibilitySurface = visibilityMenuWindow.GetVisualDescendants()
                .OfType<Border>()
                .Single(control => control.Classes.Contains("SystemBarVisibilitySurface"));
            var surfaceOrigin = visibilitySurface.TranslatePoint(default, visibilityMenuWindow);
            var firstToggleOrigin = visibilityToggles[0].TranslatePoint(default, visibilityMenuWindow);
            var lastToggleOrigin = visibilityToggles[^1].TranslatePoint(default, visibilityMenuWindow);
            Assert.NotNull(surfaceOrigin);
            Assert.NotNull(firstToggleOrigin);
            Assert.NotNull(lastToggleOrigin);
            var horizontalInset = firstToggleOrigin.Value.X - surfaceOrigin.Value.X;
            var bottomInset = surfaceOrigin.Value.Y + visibilitySurface.Bounds.Height
                - lastToggleOrigin.Value.Y - visibilityToggles[^1].Bounds.Height;
            Assert.Equal(horizontalInset, bottomInset, precision: 3);
            Assert.All(visibilityToggles, toggle =>
            {
                var origin = toggle.TranslatePoint(default, visibilityMenuWindow);
                Assert.NotNull(origin);
                Assert.True(origin.Value.Y + toggle.Bounds.Height <= visibilityMenuWindow.ClientSize.Height,
                    "A system-bar visibility option was clipped by its dedicated window.");
                var background = Assert.IsType<SolidColorBrush>(toggle.Background);
                Assert.Equal(0, background.Color.A);
                var content = Assert.IsType<Grid>(toggle.Content);
                var checkIcon = content.Children
                    .OfType<TextBlock>()
                    .Single(control => control.Classes.Contains("SystemBarVisibilityCheck"));
                Assert.Equal(toggle.IsChecked == true ? 1 : 0, checkIcon.Opacity);
            });
            AssertRendered(visibilityMenuWindow, "system bar visibility menu");
            Assert.Equal(78, systemBarWindow.ClientSize.Height);
            var originalSystemBarItems = services.Settings.VisibleSystemBarItems.ToArray();
            try
            {
                foreach (var toggle in visibilityToggles)
                {
                    toggle.IsChecked = false;
                }

                Assert.True(shell.SystemBar.ShowSystemBarRecoveryButton);
                var recoveryButton = systemBarWindow.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(control => control.Classes.Contains("SystemBarRecoveryButton"));
                Assert.True(recoveryButton.IsVisible);
                AssertRendered(systemBarWindow, "empty system bar recovery", minimumEncodedBytes: 700);
                Assert.Equal(78, systemBarWindow.ClientSize.Height);

                PressEscape(visibilityMenuWindow);
                Assert.False(visibilityMenuWindow.IsVisible);
                var recoveryOrigin = recoveryButton.TranslatePoint(default, systemBarWindow);
                Assert.NotNull(recoveryOrigin);
                var recoveryClickPoint = new Point(
                    recoveryOrigin.Value.X + recoveryButton.Bounds.Width / 2,
                    recoveryOrigin.Value.Y + recoveryButton.Bounds.Height / 2);
                systemBarWindow.MouseDown(recoveryClickPoint, MouseButton.Left, RawInputModifiers.None);
                systemBarWindow.MouseUp(recoveryClickPoint, MouseButton.Left, RawInputModifiers.None);
                Assert.True(visibilityMenuWindow.IsVisible);
            }
            finally
            {
                services.Settings.VisibleSystemBarItems = originalSystemBarItems;
            }

            PressEscape(visibilityMenuWindow);
            Assert.False(visibilityMenuWindow.IsVisible);
            AssertSystemBarMenuRendered(systemBarWindow, UIIcons.Wifi, "network controls");
            AssertSystemBarMenuRendered(systemBarWindow, UIIcons.Bluetooth, "bluetooth controls");
            AssertSystemBarMenuRendered(systemBarWindow, UIIcons.VolumeHigh, "sound controls");
            AssertSystemBarMenuRendered(systemBarWindow, UIIcons.Gaming, "gaming controls");
            AssertSystemBarMenuRendered(systemBarWindow, UIIcons.User, "user menu");
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertSupportedDemoSizes()
    {
        (int Width, int Height)[] sizes = [(1024, 640), (1280, 800), (1600, 900)];
        foreach (var (width, height) in sizes)
        {
            var services = ShellServices.CreateMock();
            using var shell = new ShellViewModel(
                services,
                new ShellStartupOptions(ForceOnboarding: false, SkipOnboarding: true));
            var window = CreateWindow(shell, width, height);

            try
            {
                var frame = AssertRendered(window, $"desktop at {width}x{height}");
                Assert.Equal(width, frame.PixelSize.Width);
                Assert.Equal(height, frame.PixelSize.Height);

                shell.ToggleLauncherCommand.Execute(null);
                AssertRendered(FindOwnedWindow(window, "DaisyOS launcher"), $"launcher at {width}x{height}");
            }
            finally
            {
                window.Close();
            }
        }
    }

    private static async Task AssertOwnedDemoWindowsAsync(ShellViewModel shell)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = shell.Settings,
            Width = 640,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.Manual
        };
        var fileManagerWindow = new FileManagerWindow
        {
            DataContext = shell.FileManager,
            Width = 720,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        try
        {
            shell.Settings.SetActive(true);
            settingsWindow.Show();
            AssertRendered(settingsWindow, "settings at minimum size");

            shell.Settings.SelectPageCommand.Execute("date-time");
            await WaitForAsync(() => shell.Settings.AvailableTimeZones.Count > 0);
            settingsWindow.Width = 900;
            settingsWindow.Height = 680;
            var dateTimeFrame = AssertRendered(settingsWindow, "date and time settings");
            Assert.Equal(900, dateTimeFrame.PixelSize.Width);
            var timeZonePicker = settingsWindow.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(control => ReferenceEquals(control.ItemsSource, shell.Settings.AvailableTimeZones));
            Assert.Equal(280, timeZonePicker.MaxDropDownHeight);
            Assert.StartsWith("(UTC", shell.Settings.CurrentTimeZoneText, StringComparison.Ordinal);
            Assert.All(
                settingsWindow.GetVisualDescendants().OfType<DatePicker>(),
                picker => Assert.False(picker.IsEffectivelyVisible));

            shell.Settings.AutomaticTime = false;
            await WaitForAsync(() => shell.Settings.CanEditManualDateTime);
            AssertRendered(settingsWindow, "date and time settings manual");
            Assert.Contains(
                settingsWindow.GetVisualDescendants().OfType<DatePicker>(),
                picker => picker.IsEffectivelyVisible);

            fileManagerWindow.Show();
            var fileManagerView = fileManagerWindow.GetVisualDescendants()
                .OfType<FileManagerView>()
                .Single();
            Assert.Contains("Compact", fileManagerView.Classes);
            Assert.All(
                fileManagerView.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(control => control.Classes.Contains("FilesSidebarHeader")),
                control => Assert.False(control.IsVisible));
            AssertRendered(fileManagerWindow, "files at minimum size");
        }
        finally
        {
            fileManagerWindow.Close();
            settingsWindow.Close();
        }
    }

    private static MainWindow CreateWindow(ShellViewModel shell, int width, int height)
    {
        var window = new MainWindow
        {
            DataContext = shell,
            WindowState = WindowState.Normal,
            Width = width,
            Height = height
        };
        window.Show();
        return window;
    }

    private static Bitmap AssertRendered(TopLevel topLevel, string surface, int minimumEncodedBytes = 2_500)
    {
        var frame = topLevel.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var encoded = new MemoryStream();
        frame.Save(encoded);
        // Compact alpha-only native surfaces compress aggressively before they
        // contain dynamic results; a transparent blank frame remains well
        // below this bounded threshold.
        Assert.True(encoded.Length > minimumEncodedBytes,
            $"The {surface} frame was blank or incomplete ({encoded.Length} bytes). ");
        SaveEvidenceIfRequested(frame, surface);
        return frame;
    }

    private static void SaveEvidenceIfRequested(Bitmap frame, string surface)
    {
        var artifactDirectory = Environment.GetEnvironmentVariable("DAISYOS_SHELL_DEMO_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(artifactDirectory))
        {
            return;
        }

        Directory.CreateDirectory(artifactDirectory);
        var safeName = string.Concat(surface.Select(character =>
            char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-'));
        frame.Save(Path.Combine(artifactDirectory, $"{safeName}.png"));
    }

    private static void PressEscape(TopLevel topLevel) =>
        topLevel.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

    private static void PressShortcut(
        TopLevel topLevel,
        Key key,
        PhysicalKey physicalKey,
        RawInputModifiers modifiers) =>
        topLevel.KeyPress(key, modifiers, physicalKey, null);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition() && DateTime.UtcNow < timeout)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), "Timed out waiting for the shell state to update.");
    }

    private static Control FindControl(Visual root, string name) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == name);

    private static Window FindOwnedWindow(Window owner, string title) =>
        owner.OwnedWindows.Single(window => string.Equals(window.Title, title, StringComparison.Ordinal));

    private static void AssertSystemBarMenuRendered(Window window, string iconGlyph, string surface)
    {
        var menu = window.GetVisualDescendants()
            .OfType<SystemBarMenuButton>()
            .Single(control => control.IconGlyph == iconGlyph);

        menu.ShowMenu();
        Assert.True(menu.IsMenuOpen);
        var content = Assert.IsAssignableFrom<Visual>(menu.MenuContent);
        var popup = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(content));
        AssertRendered(popup, surface);
        var panel = popup.GetVisualDescendants()
            .OfType<Border>()
            .Single(control => control.Classes.Contains("PanelFlyoutSurface"));
        Assert.True(panel.Bounds.Width <= popup.ClientSize.Width + 0.5,
            $"The {surface} panel is wider than its native window.");
        Assert.True(panel.Bounds.Height <= popup.ClientSize.Height + 0.5,
            $"The {surface} panel is taller than its native window.");
        if (string.Equals(iconGlyph, "person", StringComparison.Ordinal))
        {
            var labels = popup.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text ?? string.Empty)
                .ToArray();
            Assert.Contains("Signed in", labels);
            Assert.Contains("Account settings", labels);
            Assert.Contains("Lock screen", labels);
            Assert.DoesNotContain(labels, label =>
                label.Contains("session", StringComparison.OrdinalIgnoreCase)
                || label.Contains("Wayland", StringComparison.OrdinalIgnoreCase)
                || label.Contains("sign out", StringComparison.OrdinalIgnoreCase)
                || label.Contains("not available", StringComparison.OrdinalIgnoreCase));
        }
        menu.CloseMenu();
        Assert.False(menu.IsMenuOpen);
    }

    private static void AssertContextMenuRendered(Window window)
    {
        var placementTarget = window.GetVisualDescendants()
            .OfType<Control>()
            .First(control => control.IsVisible && control.ContextMenu is not null);
        var menu = Assert.IsType<ContextMenu>(placementTarget.ContextMenu);

        menu.Open(placementTarget);
        try
        {
            Assert.True(menu.IsOpen);
            var popup = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(menu));
            AssertRendered(popup, "shared context menu", minimumEncodedBytes: 1_200);
            Assert.Equal(new CornerRadius(6), menu.CornerRadius);
            Assert.Equal(new Thickness(5), menu.Padding);

            var menuItems = popup.GetVisualDescendants().OfType<MenuItem>().ToArray();
            Assert.NotEmpty(menuItems);
            Assert.All(menuItems, item =>
            {
                Assert.InRange(item.Bounds.Height, 26, 30);
                Assert.Equal(item.Padding.Left, item.Padding.Top);
                Assert.Equal(item.Padding.Top, item.Padding.Right);
                Assert.Equal(item.Padding.Right, item.Padding.Bottom);
                Assert.Equal(new CornerRadius(4), item.CornerRadius);
            });
        }
        finally
        {
            menu.Close();
        }
    }
}

public static class ShellDemoTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ShellDemoApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
                ShouldRenderOnUIThread = true
            })
            .UseSkia();
}

public sealed class ShellDemoApplication : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        // App.Initialize still loads the production resource graph. Skipping
        // App.OnFrameworkInitializationCompleted prevents the production
        // desktop lifetime from creating and owning an unrelated main window.
    }
}
