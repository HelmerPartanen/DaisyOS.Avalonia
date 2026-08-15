using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Core.Models;

namespace DaisyOS.Shell.Views.Components.Console;

public partial class ConsoleHomeView : UserControl
{
    public static readonly StyledProperty<string> ControllerNameProperty =
        AvaloniaProperty.Register<ConsoleHomeView, string>(nameof(ControllerName), "Game controller");

    private Button[] _navigationTargets = [];
    private readonly string[] _navigationLabels = ["Library", "Recent", "Friends", "Settings"];
    private readonly DispatcherTimer _carouselTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _carouselTranslation = new();
    private int _selectedIndex;
    private double _carouselOffset;
    private double _targetCarouselOffset;

    public ConsoleHomeView()
    {
        InitializeComponent();
        CarouselTrack.RenderTransform = _carouselTranslation;
        _carouselTimer.Tick += (_, _) => AdvanceCarousel();
        Loaded += (_, _) =>
        {
            _navigationTargets = [LibraryButton, RecentButton, FriendsButton, SettingsButton];
            foreach (var target in _navigationTargets)
            {
                target.Click += OnTargetClicked;
            }
        };
    }

    /// <summary>Raised when Confirm is pressed on a console destination.</summary>
    public event EventHandler<string>? DestinationRequested;

    /// <summary>Raised whenever controller navigation changes the selected destination.</summary>
    public event EventHandler<double>? SelectionChanged;

    public double ParallaxPosition => _navigationTargets.Length <= 1
        ? 0
        : _selectedIndex / (double)(_navigationTargets.Length - 1) * 2 - 1;

    public string ControllerName
    {
        get => GetValue(ControllerNameProperty);
        set => SetValue(ControllerNameProperty, value);
    }

    /// <summary>Moves focus between the console's controller-first destinations.</summary>
    public void Navigate(ControllerNavigationAction action)
    {
        if (_navigationTargets.Length == 0)
        {
            return;
        }

        switch (action)
        {
            case ControllerNavigationAction.Left:
            case ControllerNavigationAction.Up:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                break;
            case ControllerNavigationAction.Right:
            case ControllerNavigationAction.Down:
                _selectedIndex = Math.Min(_navigationTargets.Length - 1, _selectedIndex + 1);
                break;
            case ControllerNavigationAction.Confirm:
                DestinationRequested?.Invoke(this, _navigationLabels[_selectedIndex]);
                break;
            case ControllerNavigationAction.Back:
                _selectedIndex = 0;
                break;
        }

        ApplySelection();
    }

    /// <summary>Sets the predictable initial selection when console mode opens.</summary>
    public void FocusInitialDestination()
    {
        if (_navigationTargets.Length > 0)
        {
            _selectedIndex = 0;
            ApplySelection();
        }
    }

    private void OnTargetClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var index = Array.IndexOf(_navigationTargets, sender);
        if (index >= 0)
        {
            _selectedIndex = index;
            ApplySelection();
            DestinationRequested?.Invoke(this, _navigationLabels[index]);
        }
    }

    private void ApplySelection()
    {
        // 210px tiles + 20px gap. The selected tile remains at the fixed carousel anchor.
        _targetCarouselOffset = -_selectedIndex * 230;
        if (!_carouselTimer.IsEnabled)
        {
            _carouselTimer.Start();
        }

        _navigationTargets[_selectedIndex].Focus();
        SelectionChanged?.Invoke(this, ParallaxPosition);
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
