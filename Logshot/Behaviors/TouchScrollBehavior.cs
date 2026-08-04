using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;

namespace Logshot.Behaviors;

public static class TouchScrollBehavior
{
    private static Point _startPoint;
    private static bool _isTouchDragging;
    private static TextBox? _activeTouchTextBox;
    private const double TouchSlop = 10.0; // 10px slop threshold for scroll vs tap

    public static readonly AttachedProperty<bool> EnableTouchScrollProtectionProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("EnableTouchScrollProtection", typeof(TouchScrollBehavior));

    public static void SetEnableTouchScrollProtection(AvaloniaObject element, bool value) => element.SetValue(EnableTouchScrollProtectionProperty, value);
    public static bool GetEnableTouchScrollProtection(AvaloniaObject element) => element.GetValue(EnableTouchScrollProtectionProperty);

    static TouchScrollBehavior()
    {
        EnableTouchScrollProtectionProperty.Changed.AddClassHandler<TextBox>(HandleEnableChanged);
    }

    private static void HandleEnableChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            textBox.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            textBox.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
            textBox.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        }
        else
        {
            textBox.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            textBox.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            textBox.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBox tb && e.Pointer.Type == PointerType.Touch)
        {
            _startPoint = e.GetPosition(tb);
            _isTouchDragging = false;
            _activeTouchTextBox = tb;
        }
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is TextBox tb && _activeTouchTextBox == tb && e.Pointer.Type == PointerType.Touch)
        {
            var currentPoint = e.GetPosition(tb);
            double deltaX = Math.Abs(currentPoint.X - _startPoint.X);
            double deltaY = Math.Abs(currentPoint.Y - _startPoint.Y);

            if (deltaX > TouchSlop || deltaY > TouchSlop)
            {
                if (!_isTouchDragging)
                {
                    _isTouchDragging = true;
                    // User is dragging to scroll: clear focus so the soft keyboard does not open
                    var topLevel = TopLevel.GetTopLevel(tb);
                    if (topLevel?.FocusManager?.GetFocusedElement() == tb)
                    {
                        topLevel.FocusManager.Focus(null);
                    }
                }
            }
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is TextBox tb && _activeTouchTextBox == tb)
        {
            if (_isTouchDragging)
            {
                var topLevel = TopLevel.GetTopLevel(tb);
                if (topLevel?.FocusManager?.GetFocusedElement() == tb)
                {
                    topLevel.FocusManager.Focus(null);
                }
            }
            _activeTouchTextBox = null;
            _isTouchDragging = false;
        }
    }
}