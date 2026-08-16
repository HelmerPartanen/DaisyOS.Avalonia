using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Models.Gaming;
using DaisyOS.Core.Services.Gaming;
using DaisyOS.Shell.ViewModels;
using DaisyOS.System.Gaming;

namespace DaisyOS.Shell.Views.Components.Console;

public partial class ConsoleHomeView : UserControl
{
    public static readonly StyledProperty<string> ControllerNameProperty =
        AvaloniaProperty.Register<ConsoleHomeView, string>(nameof(ControllerName), "Game controller");

    private readonly IGameDiscoveryService _discoveryService;
    private readonly IGameArtworkResolver _artworkResolver;
    private readonly ObservableCollection<ConsoleGameItemViewModel> _recentGames = [];
    private readonly ObservableCollection<ConsoleGameItemViewModel> _allLibraryGames = [];
    private readonly List<Button> _cardTargets = [];
    private readonly List<Button> _libraryCardTargets = [];
    private readonly DispatcherTimer _carouselTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _carouselTranslation = new();

    private int _selectedIndex;
    private int _librarySelectedIndex;
    private double _carouselOffset;
    private double _targetCarouselOffset;
    private CancellationTokenSource? _discoveryCts;
    private int _activeBgLayer = 1;
    private string _activeTab = "Recents";
    private bool _isHeaderFocused;
    private int _headerFocusIndex;

    public ConsoleHomeView()
        : this(new LinuxGameDiscoveryService(), new GameArtworkResolver())
    {
    }

    public ConsoleHomeView(IGameDiscoveryService discoveryService, IGameArtworkResolver artworkResolver)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _artworkResolver = artworkResolver ?? throw new ArgumentNullException(nameof(artworkResolver));

        InitializeComponent();
        CarouselTrack.RenderTransform = _carouselTranslation;
        _carouselTimer.Tick += (_, _) => AdvanceCarousel();

        _artworkResolver.ArtworkUpdated += OnArtworkUpdated;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<string>? DestinationRequested;
    public event EventHandler<GameIdentity>? GameLaunchRequested;
    public event EventHandler<double>? SelectionChanged;
    public event EventHandler? SettingsRequested;

    public void SetControllerLayout(string? name)
    {
        var type = ControllerConnectionStatus.DetectType(name);
        ControllerName = string.IsNullOrWhiteSpace(name)
            ? (type == ControllerType.PlayStation ? "PlayStation Controller" : type == ControllerType.Xbox ? "Xbox Controller" : "Game controller")
            : name;

        ActionBar?.SetControllerLayout(type);
        HeaderBar?.UpdateControllerInfo(type, ControllerName);
    }

    public void SetControllerLayout(ControllerConnectionStatus status)
    {
        SetControllerLayout(status?.Name);
    }

    public string ControllerName
    {
        get => GetValue(ControllerNameProperty);
        set
        {
            SetValue(ControllerNameProperty, value);
            var type = ControllerConnectionStatus.DetectType(value);
            HeaderBar?.UpdateControllerInfo(type, value);
        }
    }

    public double ParallaxPosition => _recentGames.Count <= 1
        ? 0
        : _selectedIndex / (double)(_recentGames.Count - 1) * 2 - 1;

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HeaderBar?.UpdateClock();

        _discoveryCts = new CancellationTokenSource();
        await LoadGamesAsync(_discoveryCts.Token);
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _discoveryCts?.Cancel();
        _discoveryCts?.Dispose();
        _discoveryCts = null;
    }

    private async Task LoadGamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var discovered = await _discoveryService.DiscoverGamesAsync(cancellationToken).ConfigureAwait(true);
            if (discovered.Count == 0)
            {
                discovered = [
                    new GameIdentity("Cyberpunk 2077", "Cyberpunk 2077", GameStoreSource.Steam, "1091500", Categories: ["Game"]),
                    new GameIdentity("The Witcher 3: Wild Hunt", "The Witcher 3: Wild Hunt", GameStoreSource.Steam, "292030", Categories: ["Game"]),
                    new GameIdentity("Hades", "Hades", GameStoreSource.Steam, "1145360", Categories: ["Game"]),
                    new GameIdentity("Portal 2", "Portal 2", GameStoreSource.Steam, "620", Categories: ["Game"])
                ];
            }

            _recentGames.Clear();
            _allLibraryGames.Clear();
            CarouselTrack.Children.Clear();
            LibraryGrid.Children.Clear();
            _cardTargets.Clear();
            _libraryCardTargets.Clear();

            // Populate Recents (First 6 items)
            int index = 0;
            foreach (var game in discovered.Take(6))
            {
                var vm = new ConsoleGameItemViewModel(game);
                _recentGames.Add(vm);

                var tileGrid = CreateGameTile(vm, index, isLibraryTile: false);
                CarouselTrack.Children.Add(tileGrid);
                index++;
            }

            // Populate All Library Games
            int libIndex = 0;
            foreach (var game in discovered)
            {
                var vm = new ConsoleGameItemViewModel(game);
                _allLibraryGames.Add(vm);

                var tileGrid = CreateGameTile(vm, libIndex, isLibraryTile: true);
                LibraryGrid.Children.Add(tileGrid);
                libIndex++;
            }

            LibraryCountText.Text = $"{discovered.Count} Games";

            if (_recentGames.Count > 0)
            {
                _selectedIndex = 0;
                ApplySelection();
            }

            // Request Artwork
            foreach (var vm in _recentGames.Concat(_allLibraryGames))
            {
                _ = RequestArtworkAsync(vm, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to discover games: {ex.Message}");
        }
    }

    private async Task RequestArtworkAsync(ConsoleGameItemViewModel vm, CancellationToken cancellationToken)
    {
        try
        {
            var assets = await _artworkResolver.GetArtworkAsync(vm.Game, cancellationToken).ConfigureAwait(true);
            vm.ApplyArtworkAssets(assets);

            if (_recentGames.Count > 0 && _recentGames[_selectedIndex] == vm)
            {
                UpdateSelectedGameSpotlight(vm);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private void OnArtworkUpdated(object? sender, ArtworkChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var matchRecent = _recentGames.FirstOrDefault(g => g.Game.Title == e.Game.Title && g.Game.Source == e.Game.Source);
            if (matchRecent != null)
            {
                matchRecent.ApplyArtworkAssets(e.Assets);
                if (_recentGames.Count > 0 && _recentGames[_selectedIndex] == matchRecent)
                {
                    UpdateSelectedGameSpotlight(matchRecent);
                }
            }

            var matchLib = _allLibraryGames.FirstOrDefault(g => g.Game.Title == e.Game.Title && g.Game.Source == e.Game.Source);
            matchLib?.ApplyArtworkAssets(e.Assets);
        });
    }

    private Grid CreateGameTile(ConsoleGameItemViewModel vm, int itemIndex, bool isLibraryTile)
    {
        var container = new Grid
        {
            Width = 210,
            Height = 210,
            Margin = isLibraryTile ? new Thickness(0, 0, 24, 32) : new Thickness(0),
            ClipToBounds = false
        };

        var button = new Button
        {
            Classes = { "GameCoverTile" },
            Tag = itemIndex,
            ClipToBounds = false
        };
        ToolTip.SetTip(button, vm.Title);

        var cardContent = new Grid();

        // Outer Border Container
        var cardClipperBorder = new Border
        {
            Classes = { "ConsoleCardClipper" },
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var cardLayersGrid = new Grid();

        // 1. Cover Image Layer
        var coverImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        coverImage.Bind(Image.SourceProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.CoverImage)) { Source = vm });
        coverImage.Bind(Visual.IsVisibleProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.HasCoverImage)) { Source = vm });
        cardLayersGrid.Children.Add(coverImage);

        // 2. Logo Card Layer
        var logoBorder = new Border
        {
            Padding = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        logoBorder.Bind(Visual.IsVisibleProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.IsLogoCard)) { Source = vm });

        var logoImage = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 174,
            MaxHeight = 140
        };
        logoImage.Bind(Image.SourceProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.LogoImage)) { Source = vm });
        logoBorder.Child = logoImage;
        cardLayersGrid.Children.Add(logoBorder);

        // 3. Fallback Layer
        var fallbackBorder = new Border
        {
            Padding = new Thickness(14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        fallbackBorder.Bind(Visual.IsVisibleProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.ShowFallbackUI)) { Source = vm });

        var fallbackStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };

        var iconImage = new Image
        {
            Width = 48,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        iconImage.Bind(Image.SourceProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.IconImage)) { Source = vm });
        iconImage.Bind(Visual.IsVisibleProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.HasIconImage)) { Source = vm });

        var fallbackTitle = new TextBlock
        {
            Classes = { "ConsoleFallbackTitle" },
            Text = vm.Title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        fallbackStack.Children.Add(iconImage);
        fallbackStack.Children.Add(fallbackTitle);
        fallbackBorder.Child = fallbackStack;
        cardLayersGrid.Children.Add(fallbackBorder);

        cardClipperBorder.Child = cardLayersGrid;
        cardContent.Children.Add(cardClipperBorder);

        button.Content = cardContent;
        button.Click += (s, e) =>
        {
            _isHeaderFocused = false;
            if (!isLibraryTile)
            {
                _selectedIndex = itemIndex;
                ApplySelection();
            }
            else
            {
                _librarySelectedIndex = itemIndex;
            }
            GameLaunchRequested?.Invoke(this, vm.Game);
            DestinationRequested?.Invoke(this, vm.Title);
        };

        container.Children.Add(button);

        if (isLibraryTile)
        {
            _libraryCardTargets.Add(button);
        }
        else
        {
            _cardTargets.Add(button);
        }

        return container;
    }

    public void Navigate(ControllerNavigationAction action)
    {
        if (action == ControllerNavigationAction.PreviousSection)
        {
            var tabs = new[] { "Recents", "Library" };
            int idx = Array.IndexOf(tabs, _activeTab);
            if (idx < 0) idx = 0;
            int nextIdx = (idx - 1 + tabs.Length) % tabs.Length;
            OnHeaderTabSelected(this, tabs[nextIdx]);
            _headerFocusIndex = nextIdx;
            if (_isHeaderFocused)
            {
                FocusHeaderButton(_headerFocusIndex);
            }
            else
            {
                FocusActiveTabContent();
            }
            return;
        }
        else if (action == ControllerNavigationAction.NextSection)
        {
            var tabs = new[] { "Recents", "Library" };
            int idx = Array.IndexOf(tabs, _activeTab);
            if (idx < 0) idx = 0;
            int nextIdx = (idx + 1) % tabs.Length;
            OnHeaderTabSelected(this, tabs[nextIdx]);
            _headerFocusIndex = nextIdx;
            if (_isHeaderFocused)
            {
                FocusHeaderButton(_headerFocusIndex);
            }
            else
            {
                FocusActiveTabContent();
            }
            return;
        }

        if (_isHeaderFocused)
        {
            switch (action)
            {
                case ControllerNavigationAction.Left:
                    _headerFocusIndex = Math.Max(0, _headerFocusIndex - 1);
                    FocusHeaderButton(_headerFocusIndex);
                    break;

                case ControllerNavigationAction.Right:
                    _headerFocusIndex = Math.Min(1, _headerFocusIndex + 1);
                    FocusHeaderButton(_headerFocusIndex);
                    break;

                case ControllerNavigationAction.Down:
                    _isHeaderFocused = false;
                    FocusActiveTabContent();
                    break;

                case ControllerNavigationAction.Confirm:
                    if (_headerFocusIndex == 0) OnHeaderTabSelected(this, "Recents");
                    else if (_headerFocusIndex == 1) OnHeaderTabSelected(this, "Library");
                    break;
            }
            return;
        }

        if (_activeTab == "Recents")
        {
            if (_recentGames.Count == 0) return;

            switch (action)
            {
                case ControllerNavigationAction.Left:
                    _selectedIndex = Math.Max(0, _selectedIndex - 1);
                    ApplySelection();
                    break;

                case ControllerNavigationAction.Right:
                    _selectedIndex = Math.Min(_recentGames.Count - 1, _selectedIndex + 1);
                    ApplySelection();
                    break;

                case ControllerNavigationAction.Up:
                    _isHeaderFocused = true;
                    _headerFocusIndex = 0;
                    FocusHeaderButton(_headerFocusIndex);
                    break;

                case ControllerNavigationAction.Confirm:
                    if (_selectedIndex < _recentGames.Count)
                    {
                        GameLaunchRequested?.Invoke(this, _recentGames[_selectedIndex].Game);
                        DestinationRequested?.Invoke(this, _recentGames[_selectedIndex].Title);
                    }
                    break;
            }
        }
        else if (_activeTab == "Library")
        {
            if (_allLibraryGames.Count == 0) return;

            int columns = 5;
            switch (action)
            {
                case ControllerNavigationAction.Left:
                    _librarySelectedIndex = Math.Max(0, _librarySelectedIndex - 1);
                    FocusLibraryCard(_librarySelectedIndex);
                    break;

                case ControllerNavigationAction.Right:
                    _librarySelectedIndex = Math.Min(_allLibraryGames.Count - 1, _librarySelectedIndex + 1);
                    FocusLibraryCard(_librarySelectedIndex);
                    break;

                case ControllerNavigationAction.Down:
                    _librarySelectedIndex = Math.Min(_allLibraryGames.Count - 1, _librarySelectedIndex + columns);
                    FocusLibraryCard(_librarySelectedIndex);
                    break;

                case ControllerNavigationAction.Up:
                    if (_librarySelectedIndex < columns)
                    {
                        // Moving up from top row focuses Header
                        _isHeaderFocused = true;
                        _headerFocusIndex = 1;
                        FocusHeaderButton(_headerFocusIndex);
                    }
                    else
                    {
                        _librarySelectedIndex = Math.Max(0, _librarySelectedIndex - columns);
                        FocusLibraryCard(_librarySelectedIndex);
                    }
                    break;

                case ControllerNavigationAction.Confirm:
                    if (_librarySelectedIndex < _allLibraryGames.Count)
                    {
                        GameLaunchRequested?.Invoke(this, _allLibraryGames[_librarySelectedIndex].Game);
                        DestinationRequested?.Invoke(this, _allLibraryGames[_librarySelectedIndex].Title);
                    }
                    break;
            }
        }
    }

    private void FocusHeaderButton(int index)
    {
        if (HeaderBar == null) return;
        switch (index)
        {
            case 0:
                HeaderBar.RecentButton?.Focus();
                break;
            case 1:
                HeaderBar.LibraryButton?.Focus();
                break;
        }
    }

    private void FocusActiveTabContent()
    {
        if (_activeTab == "Library" && _libraryCardTargets.Count > 0)
        {
            _libraryCardTargets[Math.Min(_librarySelectedIndex, _libraryCardTargets.Count - 1)].Focus();
        }
        else if (_cardTargets.Count > 0)
        {
            _activeTab = "Recents";
            _cardTargets[Math.Min(_selectedIndex, _cardTargets.Count - 1)].Focus();
        }
    }

    private void FocusLibraryCard(int index)
    {
        if (index >= 0 && index < _libraryCardTargets.Count)
        {
            _libraryCardTargets[index].Focus();
        }
    }

    public void FocusInitialDestination()
    {
        _isHeaderFocused = false;
        FocusActiveTabContent();
    }

    private void OnHeaderTabSelected(object? sender, string tabName)
    {
        SetActiveTab(tabName);
        if (!_isHeaderFocused)
        {
            FocusActiveTabContent();
        }
    }

    private void OnHeaderSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetActiveTab(string tabName)
    {
        _activeTab = tabName == "Library" ? "Library" : "Recents";

        if (RecentsTabContent != null) RecentsTabContent.IsVisible = _activeTab == "Recents";
        if (LibraryTabContent != null) LibraryTabContent.IsVisible = _activeTab == "Library";

        HeaderBar?.SetActiveTab(_activeTab);
    }

    private void ApplySelection()
    {
        if (_recentGames.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _recentGames.Count) return;

        _targetCarouselOffset = -_selectedIndex * 238;
        if (!_carouselTimer.IsEnabled)
        {
            _carouselTimer.Start();
        }

        if (!_isHeaderFocused && _activeTab == "Recents" && _selectedIndex < _cardTargets.Count)
        {
            _cardTargets[_selectedIndex].Focus();
        }

        var activeGame = _recentGames[_selectedIndex];
        UpdateSelectedGameSpotlight(activeGame);

        SelectionChanged?.Invoke(this, ParallaxPosition);
    }

    private void UpdateSelectedGameSpotlight(ConsoleGameItemViewModel vm)
    {
        SelectedGameTitleText.Text = vm.Title;
        SelectedGameSourceText.Text = vm.SourceText.ToUpperInvariant();
        SelectedGameSubtext.Text = "Recently Played • Ready to play";

        var bgImage = vm.HeroImage ?? vm.CoverImage ?? vm.LogoImage;
        
        var currentLayer = _activeBgLayer == 1 ? HeroBackgroundImage1 : HeroBackgroundImage2;
        var nextLayer = _activeBgLayer == 1 ? HeroBackgroundImage2 : HeroBackgroundImage1;

        if (bgImage != null)
        {
            nextLayer.Source = bgImage;
            nextLayer.Opacity = 0.75;
            currentLayer.Opacity = 0.0;
            _activeBgLayer = _activeBgLayer == 1 ? 2 : 1;
        }
        else
        {
            currentLayer.Opacity = 0.0;
            nextLayer.Opacity = 0.0;
        }
    }

    private void AdvanceCarousel()
    {
        _carouselOffset += (_targetCarouselOffset - _carouselOffset) * 0.24;
        _carouselTranslation.X = _carouselOffset;
        if (Math.Abs(_targetCarouselOffset - _carouselOffset) < 0.05)
        {
            _carouselOffset = _targetCarouselOffset;
            _carouselTranslation.X = _carouselOffset;
            _carouselTimer.Stop();
        }
    }
}
