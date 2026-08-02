using System.Text.RegularExpressions;
using System.Xml.Linq;
using DaisyOS.Core.Models;
using DaisyOS.Shell.Helpers;
using Xunit;

namespace DaisyOS.Tests;

public sealed class ShellStatePresentationTests
{
    [Fact]
    public void UserFacingFailuresStayShortCalmAndFreeOfBackendNames()
    {
        foreach (var kind in Enum.GetValues<UserFacingFailureKind>())
        {
            var message = UserFacingFailure.Message(kind);

            Assert.StartsWith("Couldn't", message, StringComparison.Ordinal);
            Assert.EndsWith(".", message, StringComparison.Ordinal);
            Assert.DoesNotContain('\n', message);
            Assert.DoesNotContain("exception", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stack", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nmcli", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("xdg-open", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("System.", message, StringComparison.Ordinal);
            Assert.InRange(message.Length, 10, 120);
        }
    }

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(false, false, false, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, false, true, false, true)]
    public void NormalToastHonorsVisibilityAndQuietModes(
        bool toastsEnabled,
        bool doNotDisturb,
        bool gamingMode,
        bool silenceWhileGaming,
        bool expected)
    {
        var notification = CreateNotification();

        Assert.Equal(expected, NotificationPresentationPolicy.ShouldShowToast(
            notification,
            toastsEnabled,
            doNotDisturb,
            gamingMode,
            silenceWhileGaming));
    }

    [Fact]
    public void ErrorsAndCriticalAlertsBypassQuietModes()
    {
        var error = CreateNotification() with { IsError = true };
        var critical = CreateNotification() with { Urgency = NotificationUrgency.Critical };

        Assert.True(NotificationPresentationPolicy.ShouldShowToast(error, false, true, true, true));
        Assert.True(NotificationPresentationPolicy.ShouldShowToast(critical, false, true, true, true));
    }

    [Fact]
    public void ViewModelsDoNotExposeRawExceptionMessages()
    {
        var viewModelDirectory = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "ViewModels");
        var rawExceptionMessage = new Regex(@"\b(?:ex|exception)\.Message\b", RegexOptions.CultureInvariant);

        foreach (var file in Directory.EnumerateFiles(viewModelDirectory, "*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.False(rawExceptionMessage.IsMatch(source), $"{Path.GetFileName(file)} exposes raw exception text.");
        }
    }

    [Theory]
    [InlineData("LauncherView.axaml", "HasNoResults")]
    [InlineData("QuickSearchView.axaml", "HasNoResults")]
    [InlineData("FileManagerView.axaml", "!HasItems")]
    [InlineData("NotificationCenterView.axaml", "HasNoNotifications")]
    [InlineData("UpdatesView.axaml", "IsChecking")]
    [InlineData("UpdatesView.axaml", "HasError")]
    [InlineData("UpdatesView.axaml", "IsIdle")]
    [InlineData("WelcomeView.axaml", "IsWiFiLoading")]
    [InlineData("WelcomeView.axaml", "IsWiFiProblem")]
    [InlineData("WelcomeView.axaml", "HasUpdateError")]
    [InlineData("SettingsView.axaml", "IsNetworkLoading")]
    [InlineData("SettingsView.axaml", "IsNetworkOffline")]
    [InlineData("SettingsView.axaml", "IsNetworkUnavailable")]
    public void MajorSurfacesRetainExplicitStateBindings(string fileName, string binding)
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views", fileName);

        Assert.Contains(binding, File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Views", "CalendarView.axaml")]
    [InlineData("Views", "NotificationCenterView.axaml")]
    public void TaskbarFlyoutsShareThePopoverMaterialContract(string folder, string fileName)
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", folder, fileName);
        var source = File.ReadAllText(path);

        Assert.Contains("PanelFlyoutSurface", source, StringComparison.Ordinal);
        Assert.Contains("MaterialPreset=\"Popover\"", source, StringComparison.Ordinal);
        Assert.Contains("PanelFlyoutBlurSurface CompositorBlurSurface BackdropUnderlay", source, StringComparison.Ordinal);
        Assert.Contains("BorderBrush=\"{DynamicResource OSGlassBorderBrush}\"", source, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"1\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NotificationCenterSurface GlassSurface", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemBarFlyoutsUseUnconstrainedNativeKWinSurfaces()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Controls", "SystemBarMenuButton.axaml.cs"));
        var styles = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Themes", "Components", "PanelFlyoutStyles.axaml"));

        Assert.Contains("new NativeShellSurfaceWindow", source, StringComparison.Ordinal);
        Assert.Contains("SizeToContent = SizeToContent.WidthAndHeight", source, StringComparison.Ordinal);
        Assert.Contains("MinWidth = MenuMinWidth", source, StringComparison.Ordinal);
        Assert.Contains("owner.Position.X", source, StringComparison.Ordinal);
        Assert.Contains("MenuButton.TranslatePoint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LayoutTransformControl", source, StringComparison.Ordinal);
        Assert.Contains("PositionMenuWindow", source, StringComparison.Ordinal);
        Assert.Contains("PanelFlyoutBlurSurface", source, StringComparison.Ordinal);
        Assert.Contains("CompositorBlurSurface", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FlyoutPresenter", source, StringComparison.Ordinal);
        Assert.Contains("NativePanelFlyoutSurface", source, StringComparison.Ordinal);
        Assert.Contains("NativePanelFlyoutSurface.Open", styles, StringComparison.Ordinal);
        Assert.Equal(2, styles.Split("<Setter Property=\"ClipToBounds\" Value=\"True\" />").Length - 1);
    }

    [Theory]
    [InlineData("SettingsWindow.axaml")]
    [InlineData("FileManagerWindow.axaml")]
    public void MovableOwnedWindowsUseNativeBlurInsteadOfWallpaperRenderHosts(string fileName)
    {
        var views = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views");
        var window = File.ReadAllText(Path.Combine(views, fileName));
        var windowStyles = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Themes", "Components", "WindowStyles.axaml"));

        Assert.Contains("Classes=\"DaisyOSTransparentWindow\"", window, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellRenderHost", window, StringComparison.Ordinal);
        Assert.Contains("TransparencyLevelHint\" Value=\"Blur,Transparent\"", windowStyles, StringComparison.Ordinal);

        var codeBehind = File.ReadAllText(Path.Combine(
            views, Path.ChangeExtension(fileName, ".axaml.cs")));
        Assert.Contains("KWinNativeBlurRegion.TryEnable(this)", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Opened -= OnOpened", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopWallpaperRemainsVisibleBehindNativeShellSurfaces()
    {
        var path = Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views", "DesktopView.axaml");
        var source = File.ReadAllText(path);

        Assert.Contains("x:Name=\"WallpaperImage\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"WallpaperImage\"\n        IsVisible=\"False\"", source,
            StringComparison.Ordinal);
        Assert.Contains("Stretch=\"UniformToFill\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopShellDoesNotPlaceAGpuRenderSurfaceAboveAvaloniaContent()
    {
        var path = Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views", "ShellView.axaml");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("ShellRenderHost", source, StringComparison.Ordinal);
        Assert.Contains("UseGpuWallpaper=\"False\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BottomShellChromeUsesDedicatedKWinBlurSurfaces()
    {
        var views = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views");
        var shell = File.ReadAllText(Path.Combine(views, "ShellView.axaml"));
        var manager = File.ReadAllText(Path.Combine(views, "BottomShellSurfaceManager.cs"));
        var surface = File.ReadAllText(Path.Combine(views, "NativeShellSurfaceWindow.cs"));
        var styles = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Themes", "Components", "SurfaceStyles.axaml"));

        Assert.DoesNotContain("<views:SystemBarView", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<views:DockView", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<views:LauncherView", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<views:QuickSearchView", shell, StringComparison.Ordinal);
        Assert.Contains("new SystemBarView", manager, StringComparison.Ordinal);
        Assert.Contains("new SystemBarVisibilityMenuView", manager, StringComparison.Ordinal);
        Assert.Contains("DaisyOS system bar items", manager, StringComparison.Ordinal);
        Assert.Contains("PositionSystemBarVisibilityMenu", manager, StringComparison.Ordinal);
        Assert.Contains("_systemBarVisibilityMenu.SizeToContent = SizeToContent.Height", manager, StringComparison.Ordinal);
        Assert.DoesNotContain("_systemBarVisibilityMenu.Height =", manager, StringComparison.Ordinal);
        Assert.Contains("new DockView", manager, StringComparison.Ordinal);
        Assert.Contains("new LauncherView", manager, StringComparison.Ordinal);
        Assert.Contains("new QuickSearchView", manager, StringComparison.Ordinal);
        Assert.Contains("new LockScreenPasswordView", manager, StringComparison.Ordinal);
        Assert.Contains("PositionLockPassword", manager, StringComparison.Ordinal);
        Assert.Contains("SizeToContent = SizeToContent.Height", manager, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Background", manager, StringComparison.Ordinal);
        Assert.Contains("_launcher.Deactivated += LauncherOnDeactivated", manager, StringComparison.Ordinal);
        Assert.Contains("_launcher.Topmost = true", manager, StringComparison.Ordinal);
        Assert.Contains("QuickSearchTopRatio = 0.25", manager, StringComparison.Ordinal);
        Assert.Contains("fieldTop - Pixels(QuickSearchFieldTopInset, scale)", manager, StringComparison.Ordinal);
        Assert.DoesNotContain("bounds.Y + (bounds.Height - height) / 2", manager, StringComparison.Ordinal);
        Assert.Contains("WindowTransparencyLevel.Transparent", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("[WindowTransparencyLevel.Blur", surface, StringComparison.Ordinal);
        Assert.Contains("SupportsExplicitKWinBlur", surface, StringComparison.Ordinal);
        Assert.Contains("KWinNativeBlurRegion.TryEnable(this, regions)", surface, StringComparison.Ordinal);
        Assert.Contains("CompositorBlurSurface", surface, StringComparison.Ordinal);
        Assert.Contains("TranslatePoint(default, this)", surface, StringComparison.Ordinal);
        Assert.Contains("CreateRoundedRegion", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("Window.NativeShellSurface", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickSearchKeepsColdIoAwayFromTheUiThread()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "ViewModels", "QuickSearchViewModel.cs"));

        Assert.Contains("Task.Run(", source, StringComparison.Ordinal);
        Assert.Contains("_services.Search.SearchAsync(query, 30, cancellation.Token)", source, StringComparison.Ordinal);
        Assert.Contains("LoadIconAsync(cancellation.Token)", source, StringComparison.Ordinal);
        Assert.Contains("IconLoadGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public QuickSearchResultViewModel(SearchResult result)\n    {\n        Result = result;\n        if (",
            source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickSearchAndSystemBarRetainTheirPolishContracts()
    {
        var root = FindRepositoryRoot();
        var quickSearch = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Views", "QuickSearchView.axaml"));
        var quickSearchStyles = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Themes", "Components", "QuickSearchStyles.axaml"));
        var systemBar = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Views", "SystemBarView.axaml"));
        var shell = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Views", "ShellView.axaml"));
        var settings = File.ReadAllText(Path.Combine(
            root, "src", "DaisyOS.Shell", "Views", "SettingsView.axaml"));

        Assert.Contains("Classes.Open=\"{Binding ShouldShowResults}\"", quickSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("DoubleTransition", quickSearchStyles, StringComparison.Ordinal);
        Assert.DoesNotContain("<ContextMenu", systemBar, StringComparison.Ordinal);
        Assert.Contains("ShowSystemBarRecoveryButton", systemBar, StringComparison.Ordinal);
        Assert.Contains("Add system bar items", systemBar, StringComparison.Ordinal);
        Assert.Contains("IsBluetoothItemVisible", systemBar, StringComparison.Ordinal);
        Assert.Contains("BatteryPercentText", systemBar, StringComparison.Ordinal);
        Assert.Contains("MediaWidgetSize", settings, StringComparison.Ordinal);
        Assert.Contains("<views:UpdatesView DataContext=\"{Binding Updates}\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdatesOverlayHost", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void LockScreenUsesTheConfiguredUserAvatar()
    {
        var views = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views");
        var source = File.ReadAllText(Path.Combine(views, "LockScreenView.axaml"));

        Assert.Contains("Source=\"{Binding SystemBar.ProfilePicture}\"", source, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding SystemBar.HasProfilePicture}\"", source, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SystemBar.ProfileInitial}\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LockScreenPasswordUsesADedicatedRoundedKWinBlurSurface()
    {
        var views = Path.Combine(FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views");
        var lockScreen = File.ReadAllText(Path.Combine(views, "LockScreenView.axaml"));
        var password = File.ReadAllText(Path.Combine(views, "LockScreenPasswordView.axaml"));

        Assert.Contains("x:Name=\"LockScreenPasswordAnchor\"", lockScreen, StringComparison.Ordinal);
        Assert.DoesNotContain("LockScreenPasswordSurface CompositorBlurSurface", lockScreen, StringComparison.Ordinal);
        Assert.Contains("LockScreenPasswordSurface CompositorBlurSurface", password, StringComparison.Ordinal);
        Assert.Contains("MaterialPreset=\"Popover\"", password, StringComparison.Ordinal);
    }

    [Fact]
    public void WallpaperPreviewsUseADedicatedRoundedImageViewport()
    {
        var root = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(root, "src", "DaisyOS.Shell", "Views", "SettingsView.axaml"));
        var settingsStyles = File.ReadAllText(Path.Combine(root, "src", "DaisyOS.Shell", "Themes", "Components", "SettingsStyles.axaml"));

        Assert.Contains("Classes=\"WallpaperImageViewport\"", settings, StringComparison.Ordinal);
        Assert.Contains("Border.WallpaperImageViewport", settingsStyles, StringComparison.Ordinal);
        Assert.Contains("CornerRadius\" Value=\"{DynamicResource OSRadiusMedium}\"", settingsStyles, StringComparison.Ordinal);
    }

    [Fact]
    public void DateAndTimeControlsStayOwnedByTheDateAndTimePage()
    {
        var path = Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Views", "SettingsView.axaml");
        var document = XDocument.Load(path);
        var stacks = document.Descendants()
            .Where(element => element.Name.LocalName == "StackPanel")
            .ToArray();
        var dateTimePage = stacks.Single(element =>
            string.Equals(
                (string?)element.Attribute("IsVisible"),
                "{Binding IsDateTimePage}",
                StringComparison.Ordinal));
        var searchPage = stacks.Single(element =>
            string.Equals(
                (string?)element.Attribute("IsVisible"),
                "{Binding IsSearchPage}",
                StringComparison.Ordinal));

        var dateTimeText = dateTimePage.Descendants()
            .Select(element => (string?)element.Attribute("Text"))
            .Where(value => value is not null)
            .ToArray();
        Assert.Contains("System bar clock", dateTimeText);
        Assert.Contains("Time zone", dateTimeText);
        Assert.Contains("Set date and time manually", dateTimeText);
        Assert.DoesNotContain("Apply", dateTimeText);
        Assert.DoesNotContain("Time zone", searchPage.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RealThemeSynchronizationDoesNotMistakeTheDaisyOSSessionForANonKdeDesktop()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "DaisyOS.Shell", "Services", "ShellServices.cs"));

        Assert.Contains("plasma-apply-colorscheme", source, StringComparison.Ordinal);
        Assert.DoesNotContain("desktop.Contains(\"KDE\"", source, StringComparison.Ordinal);
        Assert.Contains("SynchronizeLinuxThemeAsync(_settings.ThemeMode)", source, StringComparison.Ordinal);
    }

    private static NotificationItem CreateNotification() =>
        new("Test", "Test notification", DateTimeOffset.Parse("2026-07-13T12:00:00Z"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DaisyOS.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the DaisyOS repository root.");
    }
}
