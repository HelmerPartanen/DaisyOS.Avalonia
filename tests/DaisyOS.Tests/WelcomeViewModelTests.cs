using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.Shell.Services;
using DaisyOS.Shell.ViewModels;
using Xunit;

namespace DaisyOS.Tests;

public sealed class WelcomeViewModelTests
{
    [Fact]
    public async Task NavigationStartsWifiScanAndHidesInternalFailureDetails()
    {
        using var fixture = new WelcomeFixture
        {
            WirelessHandler = _ => throw new InvalidOperationException("NetworkManager backend exploded")
        };
        using var viewModel = fixture.Create(WelcomeMode.MandatoryFirstRun);

        await viewModel.GoNextAsync();
        await viewModel.GoNextAsync();

        Assert.True(viewModel.IsWiFiStep);
        Assert.Equal(WelcomeWiFiState.Error, viewModel.WiFiState);
        Assert.Equal("Wi-Fi scan failed", viewModel.WiFiStatusTitle);
        Assert.Equal("Couldn't look for Wi-Fi networks. Try again, or continue without connecting.", viewModel.WiFiStatusDetail);
        Assert.DoesNotContain("NetworkManager", viewModel.WiFiStatusDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnavailableWifiStatusDoesNotExposeBackendDetail()
    {
        using var fixture = new WelcomeFixture
        {
            WirelessHandler = _ => Task.FromResult(new WirelessNetworkStatus(
                false,
                [],
                "nmcli failed because NetworkManager D-Bus was unavailable"))
        };
        using var viewModel = fixture.Create(WelcomeMode.MandatoryFirstRun);

        await viewModel.GoNextAsync();
        await viewModel.GoNextAsync();

        Assert.Equal(WelcomeWiFiState.Unavailable, viewModel.WiFiState);
        Assert.Equal("Wi-Fi is unavailable", viewModel.WiFiStatusTitle);
        Assert.Equal("Wi-Fi isn't available right now. You can try again or continue without connecting.", viewModel.WiFiStatusDetail);
        Assert.DoesNotContain("nmcli", viewModel.WiFiStatusDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStepUsesCalmFailureStateWhenServiceThrows()
    {
        using var fixture = new WelcomeFixture
        {
            UpdateHandler = _ => throw new InvalidOperationException("pacman database lock internals")
        };
        using var viewModel = fixture.Create(WelcomeMode.MandatoryFirstRun);

        while (!viewModel.IsUpdatesStep)
        {
            await viewModel.GoNextAsync();
        }

        Assert.Equal(WelcomeUpdateState.Error, viewModel.UpdateState);
        Assert.Equal("Update check failed", viewModel.UpdateStatusTitle);
        Assert.Equal("Couldn't check for updates. You can try again later from Settings.", viewModel.UpdateStatusDetail);
        Assert.DoesNotContain("pacman", viewModel.UpdateStatusDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinishingPersistsCurrentVersionAndInvokesCompletionOnce()
    {
        using var fixture = new WelcomeFixture();
        var completionCount = 0;
        using var viewModel = fixture.Create(WelcomeMode.MandatoryFirstRun, completed: () => completionCount++);

        while (!viewModel.IsFinishStep)
        {
            await viewModel.GoNextAsync();
        }

        await viewModel.GoNextAsync();

        Assert.Equal(1, completionCount);
        Assert.Equal(OnboardingPreferences.CurrentVersion, fixture.Settings.OnboardingPreferences.CompletedVersion);
    }

    [Fact]
    public void MandatoryAndReopenableModesKeepTheirExitRulesSeparate()
    {
        using var fixture = new WelcomeFixture();
        var skipped = 0;
        var closed = 0;
        using var mandatory = fixture.Create(WelcomeMode.MandatoryFirstRun, skipped: () => skipped++);
        using var reopenable = fixture.Create(WelcomeMode.Reopenable, closed: () => closed++);

        Assert.False(mandatory.RequestClose());
        mandatory.Skip();
        Assert.Equal(1, skipped);
        Assert.Equal(OnboardingPreferences.CurrentVersion, fixture.Settings.OnboardingPreferences.CompletedVersion);

        Assert.True(reopenable.RequestClose());
        Assert.Equal(1, closed);
    }

    private sealed class WelcomeFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "daisyos-welcome-tests", Guid.NewGuid().ToString("N"));

        public WelcomeFixture()
        {
            Directory.CreateDirectory(_directory);
            Settings = new JsonSettingsService(Path.Combine(_directory, "settings.json"), []);
        }

        public JsonSettingsService Settings { get; }

        public Func<CancellationToken, Task<WirelessNetworkStatus>> WirelessHandler { get; set; } =
            _ => Task.FromResult(new WirelessNetworkStatus(true, [], "No networks are available in this test."));

        public Func<CancellationToken, Task<UpdateStatus>> UpdateHandler { get; set; } =
            _ => Task.FromResult(new UpdateStatus(
                IsChecking: false,
                IsUpdating: false,
                IsRebootRequired: false,
                HasError: false,
                ErrorMessage: string.Empty,
                PendingCount: 0,
                TotalDownloadSize: 0,
                PendingPackages: [],
                LastChecked: DateTimeOffset.Parse("2026-07-13T12:00:00Z"),
                Detail: "No updates are available."));

        public WelcomeViewModel Create(
            WelcomeMode mode,
            Action? completed = null,
            Action? skipped = null,
            Action? closed = null) =>
            new(
                Settings,
                new FakeWirelessService(() => WirelessHandler),
                new FakeThemeService(),
                new FakeUpdateService(() => UpdateHandler),
                new EmptyAppLauncherService(),
                mode,
                completed,
                skipped,
                closed);

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeWirelessService(Func<Func<CancellationToken, Task<WirelessNetworkStatus>>> handler) : IWirelessNetworkService
    {
        public Task<WirelessNetworkStatus> GetNetworksAsync(CancellationToken cancellationToken = default) => handler()(cancellationToken);

        public Task<bool> ConnectToNetworkAsync(string ssid, string? password = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> DisconnectFromNetworkAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeUpdateService(Func<Func<CancellationToken, Task<UpdateStatus>>> handler) : IUpdateService
    {
        public event EventHandler? UpdateStatusChanged
        {
            add { }
            remove { }
        }

        public Task<UpdateStatus> CheckAsync(CancellationToken ct = default) => handler()(ct);

        public Task<UpdateStatus> GetCachedStatusAsync(CancellationToken ct = default) => handler()(ct);
    }

    private sealed class FakeThemeService : IThemeService
    {
        public ThemeMode CurrentTheme { get; private set; } = ThemeMode.Dark;
        public AccentColor CurrentAccentColor { get; private set; } = AccentColor.Blue;
        public bool HighContrast { get; private set; }
        public bool ReduceMotion { get; private set; }
        public void SetTheme(ThemeMode themeMode) => CurrentTheme = themeMode;
        public void SetAccentColor(AccentColor accentColor) => CurrentAccentColor = accentColor;
        public void SetHighContrast(bool enabled) => HighContrast = enabled;
        public void SetReduceMotion(bool enabled) => ReduceMotion = enabled;
    }

    private sealed class EmptyAppLauncherService : IAppLauncherService
    {
        public IReadOnlyList<AppEntry> GetAvailableApps() => [];

        public Task<AppLaunchResult> LaunchAsync(AppEntry app, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppLaunchResult(false, "App launching is unavailable in this test."));
    }
}
