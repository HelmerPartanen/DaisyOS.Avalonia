using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly TranslateTransform _carouselTranslation = new();

    private int _selectedIndex;
    private double _carouselOffset;
    private double _targetCarouselOffset;
    private CancellationTokenSource? _discoveryCts;
    private int _activeBgLayer = 1;
    private string _activeTab = "Recents";
    private bool _isHeaderFocused;
    private int _headerFocusIndex;
    private int _librarySelectedIndex;
    private ConsoleNavigationSurface _navigationSurface = ConsoleNavigationSurface.Recents;

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
        _clockTimer.Tick += (_, _) => UpdateClock();

        _artworkResolver.ArtworkUpdated += OnArtworkUpdated;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<GameIdentity>? GameLaunchRequested;
    public event EventHandler<double>? SelectionChanged;
    public event EventHandler? SettingsRequested;

    public string ControllerName
    {
        get => GetValue(ControllerNameProperty);
        set
        {
            SetValue(ControllerNameProperty, value);
            if (ControllerStatusText != null)
            {
                ControllerStatusText.Text = string.IsNullOrWhiteSpace(value) ? "Controller" : value;
            }
        }
    }

    public double ParallaxPosition => _recentGames.Count <= 1
        ? 0
        : _selectedIndex / (double)(_recentGames.Count - 1) * 2 - 1;

    public void SetControllerLayout(string? controllerName)
    {
        var playStation = IsPlayStationController(controllerName);
        SelectHintButton.Text = playStation ? "✕" : "A";
        QuickSettingsHintButton.Text = playStation ? "□" : "X";
        BackHintButton.Text = playStation ? "○" : "B";
        PreviousSectionHintButton.Text = playStation ? "L1" : "LB";
        NextSectionHintButton.Text = playStation ? "R1" : "RB";
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateClock();
        _clockTimer.Start();

        _discoveryCts = new CancellationTokenSource();
        await LoadGamesAsync(_discoveryCts.Token);
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _clockTimer.Stop();
        _discoveryCts?.Cancel();
        _discoveryCts?.Dispose();
        _discoveryCts = null;
    }

    private async Task LoadGamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var discovered = await _discoveryService.DiscoverGamesAsync(cancellationToken).ConfigureAwait(true);
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
            EmptyLibraryMessage.IsVisible = discovered.Count == 0;

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
            ClipToBounds = true
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
            Padding = new Thickness(16, 16, 16, 36),
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

        // 4. Card Title Overlay
        var titleScrim = new Border
        {
            Classes = { "ConsoleTitleScrim" },
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 48,
            Padding = new Thickness(10, 0, 10, 8)
        };

        var titleText = new TextBlock
        {
            Classes = { "ConsoleCardTitleText" },
            Text = vm.Title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        titleScrim.Child = titleText;
        cardLayersGrid.Children.Add(titleScrim);

        cardClipperBorder.Child = cardLayersGrid;
        cardContent.Children.Add(cardClipperBorder);

        button.Content = cardContent;
        button.Click += (s, e) =>
        {
            _isHeaderFocused = false;
            if (!isLibraryTile)
            {
                _navigationSurface = ConsoleNavigationSurface.Recents;
                _selectedIndex = itemIndex;
                ApplySelection();
            }
            else
            {
                _navigationSurface = ConsoleNavigationSurface.Library;
                _librarySelectedIndex = itemIndex;
            }
            GameLaunchRequested?.Invoke(this, vm.Game);
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
        if (action == ControllerNavigationAction.OpenConsole)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (action == ControllerNavigationAction.QuickSettings)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (action == ControllerNavigationAction.PreviousSection)
        {
            MoveToPreviousSection();
            return;
        }

        if (action == ControllerNavigationAction.NextSection)
        {
            MoveToNextSection();
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
                    _headerFocusIndex = Math.Min(2, _headerFocusIndex + 1);
                    FocusHeaderButton(_headerFocusIndex);
                    break;

                case ControllerNavigationAction.Down:
                    _isHeaderFocused = false;
                    if (_cardTargets.Count > 0)
                    {
                        _cardTargets[Math.Min(_selectedIndex, _cardTargets.Count - 1)].Focus();
                    }
                    break;

                case ControllerNavigationAction.Confirm:
                    if (_headerFocusIndex == 0) OnHeaderRecentClicked(this, new Avalonia.Interactivity.RoutedEventArgs());
                    else if (_headerFocusIndex == 1) OnHeaderLibraryClicked(this, new Avalonia.Interactivity.RoutedEventArgs());
                    else if (_headerFocusIndex == 2) OnHeaderSettingsClicked(this, new Avalonia.Interactivity.RoutedEventArgs());
                    break;
            }
            return;
        }

        if (_navigationSurface == ConsoleNavigationSurface.Library)
        {
            NavigateLibrary(action);
            return;
        }

        if (_recentGames.Count == 0)
        {
            if (action is ControllerNavigationAction.Up or ControllerNavigationAction.Back)
            {
                FocusHeaderForActiveTab();
            }
            return;
        }

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
                // Move focus UP from game cards into Header Navigation Bar!
                _isHeaderFocused = true;
                _headerFocusIndex = _activeTab switch
                {
                    "Library" => 1,
                    "Settings" => 2,
                    _ => 0
                };
                FocusHeaderButton(_headerFocusIndex);
                break;

            case ControllerNavigationAction.Down:
                MoveToLibrary();
                break;

            case ControllerNavigationAction.Confirm:
                if (_selectedIndex < _recentGames.Count)
                {
                    GameLaunchRequested?.Invoke(this, _recentGames[_selectedIndex].Game);
                }
                break;

            case ControllerNavigationAction.Back:
                FocusHeaderForActiveTab();
                break;
        }
    }

    private void FocusHeaderButton(int index)
    {
        switch (index)
        {
            case 0:
                HeaderRecentButton?.Focus();
                break;
            case 1:
                HeaderLibraryButton?.Focus();
                break;
            case 2:
                HeaderSettingsButton?.Focus();
                break;
        }
    }

    public void FocusInitialDestination()
    {
        _isHeaderFocused = false;
        _navigationSurface = ConsoleNavigationSurface.Recents;
        if (_recentGames.Count > 0)
        {
            _selectedIndex = 0;
            ApplySelection();
        }
    }

    private void OnHeaderRecentClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetActiveTab("Recents");
        _navigationSurface = ConsoleNavigationSurface.Recents;
        MainScrollViewer.Offset = new Vector(0, 0);
        if (_cardTargets.Count > 0 && !_isHeaderFocused)
        {
            _cardTargets[Math.Min(_selectedIndex, _cardTargets.Count - 1)].Focus();
        }
    }

    private void OnHeaderLibraryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetActiveTab("Library");
        _navigationSurface = ConsoleNavigationSurface.Library;
        MainScrollViewer.Offset = new Vector(0, 360);
        if (_libraryCardTargets.Count > 0 && !_isHeaderFocused)
        {
            _librarySelectedIndex = Math.Min(_librarySelectedIndex, _libraryCardTargets.Count - 1);
            _libraryCardTargets[_librarySelectedIndex].Focus();
        }
    }

    private void OnHeaderSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetActiveTab("Settings");
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetActiveTab(string tabName)
    {
        _activeTab = tabName;
        if (HeaderRecentButton != null) HeaderRecentButton.Classes.Set("Active", tabName == "Recents");
        if (HeaderLibraryButton != null) HeaderLibraryButton.Classes.Set("Active", tabName == "Library");
        if (HeaderSettingsButton != null) HeaderSettingsButton.Classes.Set("Active", tabName == "Settings");

        if (RecentTabIndicator != null) RecentTabIndicator.IsVisible = tabName == "Recents";
        if (LibraryTabIndicator != null) LibraryTabIndicator.IsVisible = tabName == "Library";
        if (SettingsTabIndicator != null) SettingsTabIndicator.IsVisible = tabName == "Settings";
    }

    private void MoveToLibrary()
    {
        SetActiveTab("Library");
        _navigationSurface = ConsoleNavigationSurface.Library;
        MainScrollViewer.Offset = new Vector(0, 360);
        if (_libraryCardTargets.Count > 0)
        {
            _librarySelectedIndex = Math.Min(_selectedIndex, _libraryCardTargets.Count - 1);
            _libraryCardTargets[_librarySelectedIndex].Focus();
        }
    }

    private void MoveToPreviousSection()
    {
        if (_navigationSurface == ConsoleNavigationSurface.Library)
        {
            SetActiveTab("Recents");
            _navigationSurface = ConsoleNavigationSurface.Recents;
            MainScrollViewer.Offset = new Vector(0, 0);
            ApplySelection();
            return;
        }

        FocusHeaderForActiveTab();
    }

    private void MoveToNextSection()
    {
        if (_navigationSurface == ConsoleNavigationSurface.Recents)
        {
            MoveToLibrary();
            return;
        }

        FocusHeaderForActiveTab();
    }

    private void NavigateLibrary(ControllerNavigationAction action)
    {
        if (_libraryCardTargets.Count == 0)
        {
            if (action is ControllerNavigationAction.Up or ControllerNavigationAction.Back)
            {
                FocusHeaderForActiveTab();
            }
            return;
        }

        var columns = Math.Max(1, (int)Math.Floor((LibraryGrid.Bounds.Width + 24) / 234));
        switch (action)
        {
            case ControllerNavigationAction.Left:
                _librarySelectedIndex = Math.Max(0, _librarySelectedIndex - 1);
                break;
            case ControllerNavigationAction.Right:
                _librarySelectedIndex = Math.Min(_libraryCardTargets.Count - 1, _librarySelectedIndex + 1);
                break;
            case ControllerNavigationAction.Up:
                if (_librarySelectedIndex < columns)
                {
                    FocusHeaderForActiveTab();
                    return;
                }
                _librarySelectedIndex -= columns;
                break;
            case ControllerNavigationAction.Down:
                _librarySelectedIndex = Math.Min(_libraryCardTargets.Count - 1, _librarySelectedIndex + columns);
                break;
            case ControllerNavigationAction.Confirm:
                GameLaunchRequested?.Invoke(this, _allLibraryGames[_librarySelectedIndex].Game);
                return;
            case ControllerNavigationAction.Back:
                MoveToPreviousSection();
                return;
            default:
                return;
        }

        _libraryCardTargets[_librarySelectedIndex].Focus();
    }

    private void FocusHeaderForActiveTab()
    {
        _isHeaderFocused = true;
        _headerFocusIndex = _activeTab switch
        {
            "Library" => 1,
            "Settings" => 2,
            _ => 0
        };
        FocusHeaderButton(_headerFocusIndex);
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
        SelectedGameSubtext.Text = "Installed game • Ready to play";

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

    private void UpdateClock() => ClockText.Text = DateTime.Now.ToString("HH:mm");

    private static bool IsPlayStationController(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name.Contains("sony", StringComparison.OrdinalIgnoreCase)
            || name.Contains("dualshock", StringComparison.OrdinalIgnoreCase)
            || name.Contains("dualsense", StringComparison.OrdinalIgnoreCase)
            || name.Contains("playstation", StringComparison.OrdinalIgnoreCase));

    private enum ConsoleNavigationSurface
    {
        Recents,
        Library
    }
}
