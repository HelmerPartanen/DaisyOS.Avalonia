using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using DaisyOS.Shell.Services.Windows;

namespace DaisyOS.Shell.Views.Components.Taskbar
{
    public partial class TaskbarView : UserControl
    {
        public event global::System.EventHandler<Avalonia.Interactivity.RoutedEventArgs>? StartButtonClicked;

        /// <summary>Raised when an app icon is tapped (i.e. pressed and released without dragging).</summary>
        public event EventHandler<string>? AppIconClicked;

        /// <summary>Raised once a drag ends and the icon order actually changed, with the new left-to-right tags.</summary>
        public event EventHandler<IReadOnlyList<string>>? OrderChanged;

        // Layout constants - keep in sync with the Width set on Border.TaskbarAppIcon in XAML.
        private const double ItemWidth = 40;
        private const double Spacing = 4;
        private const double DragThreshold = 4; // px of movement before a press becomes a drag
        private const double BottomEdgeFlare = 16;

        private Canvas? _canvas;
        private Button? _startButton;
        private Border? _taskbarBody;
        private Avalonia.Controls.Shapes.Path? _bottomEdgeBridge;
        private readonly List<Button> _order = new();
        private readonly Dictionary<string, NativeAppWindowState> _windowStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _appNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Border> _windowIndicators = new(StringComparer.Ordinal);
        private int _testAppCounter = 1;

        // Active drag state
        private Button? _dragItem;
        private bool _pointerDown;
        private bool _isDragging;
        private bool _suppressNextClick;
        private double _pointerStartX;
        private double _dragStartLeft;
        private List<string>? _orderAtDragStart;
        private IReadOnlyList<string>? _pendingOrder;

        public TaskbarView()
        {
            InitializeComponent();

            _startButton = this.FindControl<Button>("StartButton");
            if (_startButton != null)
            {
                _startButton.Click += (s, e) => StartButtonClicked?.Invoke(this, e);
            }

            _canvas = this.FindControl<Canvas>("AppIconsCanvas");
            _taskbarBody = this.FindControl<Border>("TaskbarBody");
            _bottomEdgeBridge = this.FindControl<Avalonia.Controls.Shapes.Path>("BottomEdgeBridge");
            _taskbarBody?.SizeChanged += (_, _) => UpdateBottomEdgeBridge();

            if (_canvas != null)
            {
                Loaded += (_, _) => InitializeIcons();
            }
        }

        public void SetLauncherOpen(bool isOpen) =>
            _startButton?.Classes.Set("LauncherActive", isOpen);

        /// <summary>Applies native owned-window state without changing taskbar geometry.</summary>
        public void SetAppWindowState(string appId, NativeAppWindowState state)
        {
            _windowStates[appId] = state;
            if (_order.FirstOrDefault(button => string.Equals(button.Tag as string, appId, StringComparison.Ordinal)) is { } button)
            {
                ApplyWindowState(button, appId, state);
            }
        }

        private void InitializeIcons()
        {
            if (_canvas is null) return;

            _windowIndicators.Clear();
            foreach (var indicator in _canvas.Children.OfType<Border>())
            {
                if (indicator.Tag is string appId)
                {
                    _windowIndicators[appId] = indicator;
                }
            }

            _order.Clear();
            foreach (var child in _canvas.Children.OfType<Button>())
            {
                _order.Add(child);
                WireUpIcon(child);
                if (child.Tag is string appId)
                {
                    _appNames[appId] = child.GetValue(ToolTip.TipProperty) as string ?? appId;
                    ApplyWindowState(child, appId, _windowStates.GetValueOrDefault(appId, NativeAppWindowState.NotRunning));
                }
            }

            if (_pendingOrder is { Count: > 0 })
            {
                ApplyOrderCore(_pendingOrder);
            }

            LayoutAllInstant();
            UpdateCanvasWidth();
        }

        /// <summary>Restores a persisted order while retaining newly introduced dock items.</summary>
        public void ApplyOrder(IReadOnlyList<string> order)
        {
            _pendingOrder = order;
            if (_order.Count > 0)
            {
                ApplyOrderCore(order);
                LayoutAllInstant();
            }
        }

        private void ApplyOrderCore(IReadOnlyList<string> order)
        {
            var rank = order
                .Select((id, index) => (id, index))
                .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
            _order.Sort((left, right) =>
            {
                var leftId = left.Tag as string ?? string.Empty;
                var rightId = right.Tag as string ?? string.Empty;
                var leftRank = rank.TryGetValue(leftId, out var l) ? l : int.MaxValue;
                var rightRank = rank.TryGetValue(rightId, out var r) ? r : int.MaxValue;
                return leftRank != rightRank ? leftRank.CompareTo(rightRank) : string.Compare(leftId, rightId, StringComparison.Ordinal);
            });
        }

        private void WireUpIcon(Button icon)
        {
            icon.Cursor = new Cursor(StandardCursorType.Hand);

            // Use tunnel strategy for pointer pressed so we can intercept drag
            // before the Button's default handling swallows the event.
            icon.AddHandler(InputElement.PointerPressedEvent, Icon_PointerPressed, RoutingStrategies.Tunnel);
            icon.PointerMoved += Icon_PointerMoved;
            icon.PointerReleased += Icon_PointerReleased;
            icon.PointerCaptureLost += Icon_PointerCaptureLost;
            icon.Click += Icon_Click;
        }

        private void UnwireIcon(Button icon)
        {
            icon.RemoveHandler(InputElement.PointerPressedEvent, Icon_PointerPressed);
            icon.PointerMoved -= Icon_PointerMoved;
            icon.PointerReleased -= Icon_PointerReleased;
            icon.PointerCaptureLost -= Icon_PointerCaptureLost;
            icon.Click -= Icon_Click;
        }

        private static readonly Transitions SharedPositionTransitions = new()
        {
            new DoubleTransition { Property = Canvas.LeftProperty, Duration = TimeSpan.FromSeconds(0.25), Easing = new CubicEaseOut() }
        };

        private void LayoutAllInstant()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                var icon = _order[i];
                icon.Transitions = null; // avoid animating in from 0 on first layout
                Canvas.SetLeft(icon, SlotX(i));
                icon.Transitions = SharedPositionTransitions;
                PositionIndicator(icon, SlotX(i));
            }
        }

        private void UpdateCanvasWidth()
        {
            if (_canvas is null) return;
            // Only the Canvas needs an explicit width (Canvas doesn't auto-size).
            // The outer Border auto-sizes to its content via Padding - no manual math needed.
            var count = _order.Count;
            _canvas.Width = count == 0 ? 0 : count * ItemWidth + (count - 1) * Spacing;
        }

        private void ApplyWindowState(Button button, string appId, NativeAppWindowState state)
        {
            var presentation = TaskbarWindowPresentation.From(state);
            button.Classes.Set("WindowRunning", presentation.IsRunning);
            button.Classes.Set("WindowActive", presentation.IsActive);
            button.Classes.Set("WindowMinimized", presentation.IsMinimized);

            if (_windowIndicators.TryGetValue(appId, out var indicator))
            {
                indicator.Classes.Set("WindowRunning", presentation.IsRunning && !presentation.IsMinimized);
                indicator.Classes.Set("WindowActive", presentation.IsActive);
                indicator.Classes.Set("WindowMinimized", presentation.IsMinimized);
                PositionIndicator(button, Canvas.GetLeft(button));
            }

            var appName = _appNames.GetValueOrDefault(appId, button.GetValue(ToolTip.TipProperty) as string ?? appId);
            var accessibleName = presentation.StateLabel is null ? appName : $"{appName} — {presentation.StateLabel}";
            button.SetValue(ToolTip.TipProperty, accessibleName);
            button.SetValue(AutomationProperties.NameProperty, accessibleName);
        }

        private void UpdateBottomEdgeBridge()
        {
            if (_taskbarBody is null || _bottomEdgeBridge is null)
            {
                return;
            }

            var width = _taskbarBody.Bounds.Width;
            var height = _taskbarBody.Bounds.Height;
            if (width <= 0 || height <= 0)
            {
                _bottomEdgeBridge.Data = null;
                return;
            }

            var center = width / 2;
            var bodyHalfWidth = width / 2;
            var edgeHalfWidth = bodyHalfWidth + BottomEdgeFlare;
            // Start at the visual midpoint, leaving the upper half calm while
            // the lower half flares into the physical bottom bezel.
            var joinY = height / 2;

            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            context.BeginFigure(new Point(center - edgeHalfWidth, height), isFilled: true);
            context.LineTo(new Point(center + edgeHalfWidth, height), isStroked: false);
            context.CubicBezierTo(
                new Point(center + edgeHalfWidth, height),
                new Point(center + bodyHalfWidth, height),
                new Point(center + bodyHalfWidth, joinY),
                isStroked: false);
            context.LineTo(new Point(center - bodyHalfWidth, joinY), isStroked: false);
            context.CubicBezierTo(
                new Point(center - bodyHalfWidth, height),
                new Point(center - edgeHalfWidth, height),
                new Point(center - edgeHalfWidth, height),
                isStroked: false);
            context.EndFigure(isClosed: true);
            _bottomEdgeBridge.Data = geometry;
        }

        public void AddTestApp()
        {
            if (_canvas is null) return;

            var appId = $"test_app_{_testAppCounter}";
            var appName = $"App {_testAppCounter}";
            _testAppCounter++;

            // Material symbol icon glyph names array for variety
            string[] testGlyphs = { "rocket_launch", "terminal", "code", "sports_esports", "language", "folder", "palette", "schedule" };
            var glyph = testGlyphs[(_testAppCounter - 2) % testGlyphs.Length];

            var iconButton = new Button
            {
                Classes = { "ShellButton", "TaskbarApp", "TaskbarAppIcon" },
                Tag = appId,
                [ToolTip.TipProperty] = appName,
                Content = new Panel
                {
                    Width = 32,
                    Height = 32,
                    Children =
                    {
                        new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                            CornerRadius = new Avalonia.CornerRadius(6),
                            Width = 32,
                            Height = 32,
                            Child = new TextBlock
                            {
                                Text = glyph,
                                FontFamily = new FontFamily("avares://DaisyOS.Shell/Assets/fonts#Material Symbols Rounded"),
                                FontSize = 18,
                                Foreground = Brushes.White,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            }
                        }
                    }
                }
            };

            // Add context menu for individual removal
            var menu = new ContextMenu();
            var removeItem = new MenuItem { Header = $"Remove {appName}" };
            removeItem.Click += (_, _) => RemoveSpecificApp(iconButton);
            menu.Items.Add(removeItem);
            iconButton.ContextMenu = menu;

            _canvas.Children.Add(iconButton);
            _order.Add(iconButton);
            WireUpIcon(iconButton);

            // Animate position smoothly
            var newIndex = _order.Count - 1;
            Canvas.SetLeft(iconButton, SlotX(newIndex));
            iconButton.Transitions = SharedPositionTransitions;

            UpdateCanvasWidth();
        }

        public void RemoveTestApp()
        {
            if (_order.Count == 0) return;
            var lastIcon = _order.LastOrDefault(btn => (btn.Tag as string)?.StartsWith("test_app_") == true) ?? _order.Last();
            RemoveSpecificApp(lastIcon);
        }

        public void RemoveSpecificApp(Button icon)
        {
            if (_canvas is null || !_order.Contains(icon)) return;

            UnwireIcon(icon);
            _order.Remove(icon);
            _canvas.Children.Remove(icon);

            // Smoothly shift remaining icons to their updated slots
            for (int i = 0; i < _order.Count; i++)
            {
                AnimateToSlot(_order[i], i);
            }

            UpdateCanvasWidth();
        }

        private static double SlotX(int index) => index * (ItemWidth + Spacing);

        private void Icon_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Button icon || _canvas is null) return;
            if (!e.GetCurrentPoint(icon).Properties.IsLeftButtonPressed) return;

            _dragItem = icon;
            _pointerDown = true;
            _isDragging = false;
            _pointerStartX = e.GetPosition(_canvas).X;
            _dragStartLeft = Canvas.GetLeft(icon);
            // Leave a normal press to Avalonia's Button lifecycle. Capture begins only once this
            // gesture crosses the drag threshold, preserving the same pressed/click effect as Start.
        }

        private void Icon_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_pointerDown || _dragItem is null || _canvas is null) return;
            if (sender is not Button icon || icon != _dragItem) return;

            var currentX = e.GetPosition(_canvas).X;
            var delta = currentX - _pointerStartX;

            if (!_isDragging)
            {
                if (Math.Abs(delta) < DragThreshold) return;
                BeginDrag(icon);
                e.Pointer.Capture(icon);
            }

            var maxLeft = SlotX(_order.Count - 1);
            var newLeft = Math.Clamp(_dragStartLeft + delta, 0, Math.Max(0, maxLeft));

            // Track the pointer 1:1 - no transition on the dragged item itself.
            icon.Transitions = null;
            Canvas.SetLeft(icon, newLeft);
            PositionIndicator(icon, newLeft);

            ReorderIfNeeded(icon, newLeft);
        }

        private void BeginDrag(Button icon)
        {
            _isDragging = true;
            _orderAtDragStart = _order.Select(b => b.Tag as string ?? string.Empty).ToList();
            icon.Classes.Add("dragging");
            icon.ZIndex = 1000;
        }

        private void ReorderIfNeeded(Button dragged, double draggedLeft)
        {
            var draggedCenter = draggedLeft + ItemWidth / 2;
            var index = _order.IndexOf(dragged);

            while (index > 0)
            {
                var leftBoundary = SlotX(index) - Spacing / 2;
                if (draggedCenter >= leftBoundary) break;

                var neighbor = _order[index - 1];
                (_order[index - 1], _order[index]) = (_order[index], _order[index - 1]);
                index--;

                AnimateToSlot(neighbor, index + 1);
            }

            while (index < _order.Count - 1)
            {
                var rightBoundary = SlotX(index) + ItemWidth + Spacing / 2;
                if (draggedCenter <= rightBoundary) break;

                var neighbor = _order[index + 1];
                (_order[index + 1], _order[index]) = (_order[index], _order[index + 1]);
                index++;

                AnimateToSlot(neighbor, index - 1);
            }
        }

        private void AnimateToSlot(Button icon, int index)
        {
            var left = SlotX(index);
            icon.Transitions = SharedPositionTransitions;
            Canvas.SetLeft(icon, left);
            PositionIndicator(icon, left);
        }

        private void Icon_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not Button icon) return;
            EndInteraction(icon, e);
            e.Pointer.Capture(null);
        }

        private void Icon_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (sender is not Button icon) return;
            EndInteraction(icon, e);
        }

        private void EndInteraction(Button icon, RoutedEventArgs e)
        {
            if (!_pointerDown) return;

            var wasDragging = _isDragging;

            var index = _order.IndexOf(icon);
            if (index >= 0)
            {
                icon.Transitions = SharedPositionTransitions;
                Canvas.SetLeft(icon, SlotX(index));
                PositionIndicator(icon, SlotX(index));
            }

            icon.Classes.Remove("dragging");
            icon.ZIndex = 0;

            if (wasDragging)
            {
                e.Handled = true;
                _suppressNextClick = true;
                var newOrder = _order.Select(b => b.Tag as string ?? string.Empty).ToList();
                if (_orderAtDragStart != null && !newOrder.SequenceEqual(_orderAtDragStart))
                {
                    OrderChanged?.Invoke(this, newOrder);
                }
            }
            _pointerDown = false;
            _isDragging = false;
            _dragItem = null;
            _orderAtDragStart = null;
        }

        // Let Button own normal activation. The pointer handlers above are only for reordering;
        // emitting activation from both paths made taps susceptible to capture/drag sequencing.
        private void Icon_Click(object? sender, RoutedEventArgs e)
        {
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }

            if (sender is Button { Tag: string tag })
            {
                AppIconClicked?.Invoke(this, tag);
            }
        }

        private void PositionIndicator(Button icon, double itemLeft)
        {
            if (icon.Tag is not string appId || !_windowIndicators.TryGetValue(appId, out var indicator))
            {
                return;
            }

            if (double.IsNaN(itemLeft))
            {
                var index = _order.IndexOf(icon);
                if (index < 0)
                {
                    return;
                }

                itemLeft = SlotX(index);
            }

            var presentation = TaskbarWindowPresentation.From(
                _windowStates.GetValueOrDefault(appId, NativeAppWindowState.NotRunning));
            Canvas.SetLeft(indicator, itemLeft + (ItemWidth - presentation.IndicatorWidth) / 2);
        }
    }
}
