using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
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
    private readonly ObservableCollection<ConsoleGameItemViewModel> _games = [];
    private readonly List<Button> _cardTargets = [];
    private readonly DispatcherTimer _carouselTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _carouselTranslation = new();

    private Button[] _navigationTargets = [];
    private int _selectedIndex;
    private double _carouselOffset;
    private double _targetCarouselOffset;
    private CancellationTokenSource? _discoveryCts;
    private int _activeBgLayer = 1;

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
    public event EventHandler<double>? SelectionChanged;

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

    public double ParallaxPosition => _games.Count <= 1
        ? 0
        : _selectedIndex / (double)(_games.Count - 1) * 2 - 1;

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _navigationTargets = [HeaderLibraryButton, HeaderRecentButton, HeaderSettingsButton];
        foreach (var nav in _navigationTargets)
        {
            nav.Click += OnHeaderNavClicked;
        }

        ClockText.Text = DateTime.Now.ToString("HH:mm");

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

            _games.Clear();
            CarouselTrack.Children.Clear();
            _cardTargets.Clear();

            int index = 0;
            foreach (var game in discovered)
            {
                var vm = new ConsoleGameItemViewModel(game);
                _games.Add(vm);

                var tileGrid = CreateGameTile(vm, index);
                CarouselTrack.Children.Add(tileGrid);
                index++;
            }

            if (_games.Count > 0)
            {
                _selectedIndex = 0;
                ApplySelection();
            }

            foreach (var vm in _games)
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

            if (_games.Count > 0 && _games[_selectedIndex] == vm)
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
            var match = _games.FirstOrDefault(g => g.Game.Title == e.Game.Title && g.Game.Source == e.Game.Source);
            if (match != null)
            {
                match.ApplyArtworkAssets(e.Assets);
                if (_games.Count > 0 && _games[_selectedIndex] == match)
                {
                    UpdateSelectedGameSpotlight(match);
                }
            }
        });
    }

    private Grid CreateGameTile(ConsoleGameItemViewModel vm, int itemIndex)
    {
        var container = new Grid
        {
            Width = 210,
            Height = 210
        };

        var button = new Button
        {
            Classes = { "GameCoverTile" },
            Tag = itemIndex
        };
        ToolTip.SetTip(button, vm.Title);

        var cardContent = new Grid();

        // Clipping Outer Border Container
        var cardClipperBorder = new Border
        {
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#282C3A"), 0.0),
                    new GradientStop(Color.Parse("#161722"), 1.0)
                }
            }
        };

        var cardLayersGrid = new Grid();

        // 1. Cover or Hero Image Layer (Fills the square card)
        var coverImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        coverImage.Bind(Image.SourceProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.CoverImage)) { Source = vm });
        coverImage.Bind(Visual.IsVisibleProperty, new Avalonia.Data.Binding(nameof(ConsoleGameItemViewModel.HasCoverImage)) { Source = vm });
        cardLayersGrid.Children.Add(coverImage);

        // 2. Game Logo Card Layer (Used when official store logo is available)
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

        // 3. Fallback Layer (Shown only when NO game artwork exists at all)
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
            Text = vm.Title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        fallbackStack.Children.Add(iconImage);
        fallbackStack.Children.Add(fallbackTitle);
        fallbackBorder.Child = fallbackStack;
        cardLayersGrid.Children.Add(fallbackBorder);

        // 4. Card Bottom Gradient Scrim & Game Title Overlay
        var titleScrim = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 48,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#00000000"), 0.0),
                    new GradientStop(Color.Parse("#E0090A0F"), 1.0)
                }
            },
            Padding = new Thickness(10, 0, 10, 8)
        };
        var titleText = new TextBlock
        {
            Text = vm.Title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
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
            _selectedIndex = itemIndex;
            ApplySelection();
            DestinationRequested?.Invoke(this, vm.Title);
        };

        container.Children.Add(button);
        _cardTargets.Add(button);

        return container;
    }

    public void Navigate(ControllerNavigationAction action)
    {
        if (_games.Count == 0) return;

        switch (action)
        {
            case ControllerNavigationAction.Left:
            case ControllerNavigationAction.Up:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                break;
            case ControllerNavigationAction.Right:
            case ControllerNavigationAction.Down:
                _selectedIndex = Math.Min(_games.Count - 1, _selectedIndex + 1);
                break;
            case ControllerNavigationAction.Confirm:
                if (_selectedIndex < _games.Count)
                {
                    DestinationRequested?.Invoke(this, _games[_selectedIndex].Title);
                }
                break;
            case ControllerNavigationAction.Back:
                _selectedIndex = 0;
                break;
        }

        ApplySelection();
    }

    public void FocusInitialDestination()
    {
        if (_games.Count > 0)
        {
            _selectedIndex = 0;
            ApplySelection();
        }
    }

    private void OnHeaderNavClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button nav)
        {
            var tip = ToolTip.GetTip(nav)?.ToString();
            DestinationRequested?.Invoke(this, tip ?? "Nav");
        }
    }

    private void ApplySelection()
    {
        if (_games.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _games.Count) return;

        _targetCarouselOffset = -_selectedIndex * 234;
        if (!_carouselTimer.IsEnabled)
        {
            _carouselTimer.Start();
        }

        if (_selectedIndex < _cardTargets.Count)
        {
            _cardTargets[_selectedIndex].Focus();
        }

        var activeGame = _games[_selectedIndex];
        UpdateSelectedGameSpotlight(activeGame);

        SelectionChanged?.Invoke(this, ParallaxPosition);
    }

    private void UpdateSelectedGameSpotlight(ConsoleGameItemViewModel vm)
    {
        // 1. Prominent Game Title Heading
        SelectedGameTitleText.Text = vm.Title;

        // 2. Source Badge & Status Subtext
        SelectedGameSourceText.Text = vm.SourceText.ToUpperInvariant();
        SelectedGameSubtext.Text = "Installed • Ready to play";

        // 3. Smooth Hero Background Image Crossfade (High Opacity + Blur)
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
