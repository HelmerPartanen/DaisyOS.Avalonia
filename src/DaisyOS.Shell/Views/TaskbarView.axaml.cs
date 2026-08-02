using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace DaisyOS.Shell.Views
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
        private readonly List<Border> _order = new();

        // Active drag state
        private Border? _dragItem;
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
            foreach (var child in _canvas.Children.OfType<Border>())
            {
                _order.Add(child);
                WireUpIcon(child);
            }

            LayoutAllInstant();
            UpdateCanvasWidth();
        }

        private void WireUpIcon(Border icon)
        {
            icon.Cursor = new Cursor(StandardCursorType.Hand);

            // A dedicated scale transform so the icon can "lift" while dragging, with its own
            // springy transition independent of the position transition.
            var scale = new ScaleTransform(1, 1)
            {
                Transitions = new Transitions
                {
                    new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromSeconds(0.15), Easing = new BackEaseOut() },
                    new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromSeconds(0.15), Easing = new BackEaseOut() },
                }
            };
            icon.RenderTransform = scale;

            icon.PointerPressed += Icon_PointerPressed;
            icon.PointerMoved += Icon_PointerMoved;
            icon.PointerReleased += Icon_PointerReleased;
            icon.PointerCaptureLost += Icon_PointerCaptureLost;
        }

        private static Transitions CreatePositionTransitions() => new()
        {
            new DoubleTransition { Property = Canvas.LeftProperty, Duration = TimeSpan.FromSeconds(0.20), Easing = new CubicEaseOut() }
        };

        private void LayoutAllInstant()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                var icon = _order[i];
                icon.Transitions = null; // avoid animating in from 0 on first layout
                Canvas.SetLeft(icon, SlotX(i));
                icon.Transitions = CreatePositionTransitions();
            }
        }

        private void UpdateCanvasWidth()
        {
            if (_canvas is null) return;
            var count = _order.Count;
            _canvas.Width = count == 0 ? 0 : count * ItemWidth + (count - 1) * Spacing;
        }

        private static double SlotX(int index) => index * (ItemWidth + Spacing);

        private void Icon_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border icon || _canvas is null) return;
            if (!e.GetCurrentPoint(icon).Properties.IsLeftButtonPressed) return;

            _dragItem = icon;
            _pointerDown = true;
            _isDragging = false;
            _pointerStartX = e.GetPosition(_canvas).X;
            _dragStartLeft = Canvas.GetLeft(icon);
            _orderAtDragStart = _order.Select(b => b.Tag as string ?? string.Empty).ToList();

            e.Pointer.Capture(icon);
        }

        private void Icon_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_pointerDown || _dragItem is null || _canvas is null) return;
            if (sender is not Border icon || icon != _dragItem) return;

            var currentX = e.GetPosition(_canvas).X;
            var delta = currentX - _pointerStartX;

            if (!_isDragging)
            {
                if (Math.Abs(delta) < DragThreshold) return;
                BeginDrag(icon);
            }

            // The launcher lives entirely outside this canvas, so clamping to [0, maxLeft]
            // is all that's needed to guarantee icons can never reach or pass it.
            var maxLeft = SlotX(_order.Count - 1);
            var newLeft = Math.Clamp(_dragStartLeft + delta, 0, Math.Max(0, maxLeft));

            // Track the pointer 1:1 - no transition on the dragged item itself.
            icon.Transitions = null;
            Canvas.SetLeft(icon, newLeft);

            ReorderIfNeeded(icon, newLeft);
        }

        private void BeginDrag(Border icon)
        {
            _isDragging = true;
            icon.Classes.Add("dragging");
            icon.ZIndex = 1000;

            if (icon.RenderTransform is ScaleTransform s)
            {
                s.ScaleX = 1.08;
                s.ScaleY = 1.08;
            }
        }

        private void ReorderIfNeeded(Border dragged, double draggedLeft)
        {
            var draggedCenter = draggedLeft + ItemWidth / 2;
            var index = _order.IndexOf(dragged);

            // Bubble left once the dragged item's center crosses the boundary of its own
            // current slot (NOT the neighbor's center - the drag clamp can make a neighbor's
            // full center unreachable, especially for the outermost slot, which silently
            // prevented any swap from ever firing).
            while (index > 0)
            {
                var leftBoundary = SlotX(index) - Spacing / 2;
                if (draggedCenter >= leftBoundary) break;

                var neighbor = _order[index - 1];
                (_order[index - 1], _order[index]) = (_order[index], _order[index - 1]);
                index--;

                AnimateToSlot(neighbor, index + 1);
            }

            // Bubble right likewise.
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

        private void AnimateToSlot(Border icon, int index)
        {
            icon.Transitions ??= CreatePositionTransitions();
            Canvas.SetLeft(icon, SlotX(index));
        }

        private void Icon_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not Border icon) return;
            EndInteraction(icon, raiseClickIfTap: true);
            e.Pointer.Capture(null);
        }

        private void Icon_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (sender is not Border icon) return;
            EndInteraction(icon, raiseClickIfTap: false);
        }

        private void EndInteraction(Border icon, bool raiseClickIfTap)
        {
            if (!_pointerDown) return;

            var wasDragging = _isDragging;

            // Snap into its final slot with the springy position transition restored.
            var index = _order.IndexOf(icon);
            if (index >= 0)
            {
                icon.Transitions = CreatePositionTransitions();
                Canvas.SetLeft(icon, SlotX(index));
            }

            icon.Classes.Remove("dragging");
            icon.ZIndex = 0;
            if (icon.RenderTransform is ScaleTransform s)
            {
                s.ScaleX = 1.0;
                s.ScaleY = 1.0;
            }

            if (wasDragging)
            {
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