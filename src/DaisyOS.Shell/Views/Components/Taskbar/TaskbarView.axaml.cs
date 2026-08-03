using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;

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

        private Canvas? _canvas;
        private readonly List<Button> _order = new();
        private int _testAppCounter = 1;

        // Active drag state
        private Button? _dragItem;
        private bool _pointerDown;
        private bool _isDragging;
        private double _pointerStartX;
        private double _dragStartLeft;
        private List<string>? _orderAtDragStart;

        public TaskbarView()
        {
            InitializeComponent();

            var startBtn = this.FindControl<Button>("StartButton");
            if (startBtn != null)
            {
                startBtn.Click += (s, e) => StartButtonClicked?.Invoke(this, e);
            }

            _canvas = this.FindControl<Canvas>("AppIconsCanvas");

            if (_canvas != null)
            {
                Loaded += (_, _) => InitializeIcons();
            }
        }

        private void InitializeIcons()
        {
            if (_canvas is null) return;

            _order.Clear();
            foreach (var child in _canvas.Children.OfType<Button>())
            {
                _order.Add(child);
                WireUpIcon(child);
            }

            LayoutAllInstant();
            UpdateCanvasWidth();
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
        }

        private void UnwireIcon(Button icon)
        {
            icon.RemoveHandler(InputElement.PointerPressedEvent, Icon_PointerPressed);
            icon.PointerMoved -= Icon_PointerMoved;
            icon.PointerReleased -= Icon_PointerReleased;
            icon.PointerCaptureLost -= Icon_PointerCaptureLost;
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
                Classes = { "TaskbarAppIcon" },
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

            e.Pointer.Capture(icon);
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
            }

            var maxLeft = SlotX(_order.Count - 1);
            var newLeft = Math.Clamp(_dragStartLeft + delta, 0, Math.Max(0, maxLeft));

            // Track the pointer 1:1 - no transition on the dragged item itself.
            icon.Transitions = null;
            Canvas.SetLeft(icon, newLeft);

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

        private static void AnimateToSlot(Button icon, int index)
        {
            icon.Transitions = SharedPositionTransitions;
            Canvas.SetLeft(icon, SlotX(index));
        }

        private void Icon_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not Button icon) return;
            EndInteraction(icon, raiseClickIfTap: true, e);
            e.Pointer.Capture(null);
        }

        private void Icon_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (sender is not Button icon) return;
            EndInteraction(icon, raiseClickIfTap: false, e);
        }

        private void EndInteraction(Button icon, bool raiseClickIfTap, RoutedEventArgs e)
        {
            if (!_pointerDown) return;

            var wasDragging = _isDragging;

            var index = _order.IndexOf(icon);
            if (index >= 0)
            {
                icon.Transitions = SharedPositionTransitions;
                Canvas.SetLeft(icon, SlotX(index));
            }

            icon.Classes.Remove("dragging");
            icon.ZIndex = 0;

            if (wasDragging)
            {
                e.Handled = true;
                var newOrder = _order.Select(b => b.Tag as string ?? string.Empty).ToList();
                if (_orderAtDragStart != null && !newOrder.SequenceEqual(_orderAtDragStart))
                {
                    OrderChanged?.Invoke(this, newOrder);
                }
            }
            else if (raiseClickIfTap && icon.Tag is string tag)
            {
                AppIconClicked?.Invoke(this, tag);
            }

            _pointerDown = false;
            _isDragging = false;
            _dragItem = null;
            _orderAtDragStart = null;
        }
    }
}