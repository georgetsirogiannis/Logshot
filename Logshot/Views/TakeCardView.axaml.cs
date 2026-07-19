using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class TakeCardView : UserControl
{
    private const double DrawerWidth = 190;
    private const double SwipeThreshold = 40;

    private Point _dragStartPoint;
    private bool _isDragging;
    private bool _isOpen;

    private Border? _cardSurface;
    private TranslateTransform? _cardTransform;
    private Control? _currentFlyoutTarget;

    public TakeCardView()
    {
        InitializeComponent();
        _cardSurface = this.FindControl<Border>("CardSurface");
        _cardTransform = _cardSurface?.RenderTransform as TranslateTransform;
    }

    private void Card_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        _dragStartPoint = e.GetPosition(border);
        _isDragging = false;
        e.Pointer.Capture(border);
    }

    private void Card_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border || _cardTransform is null) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        var currentPoint = e.GetPosition(border);
        var delta = currentPoint - _dragStartPoint;

        if (!_isDragging && System.Math.Abs(delta.X) > 4)
            _isDragging = true;

        if (_isDragging)
        {
            var baseOffset = _isOpen ? DrawerWidth : 0;
            var offset = baseOffset + delta.X;
            offset = System.Math.Clamp(offset, 0, DrawerWidth);
            _cardTransform.X = offset;
        }
    }

    private void Card_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border border) e.Pointer.Capture(null);
        if (!_isDragging || _cardTransform is null)
        {
            _isDragging = false;
            return;
        }

        _isOpen = _cardTransform.X > SwipeThreshold;
        _cardTransform.X = _isOpen ? DrawerWidth : 0;
        _isDragging = false;
    }

    private void ChangeRollMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            var parent = menuItem.Parent;
            while (parent != null && !(parent is ContextMenu))
                parent = parent.Parent;

            if (parent is ContextMenu contextMenu && contextMenu.PlacementTarget is Control target)
            {
                _currentFlyoutTarget = target;
                FlyoutBase.ShowAttachedFlyout(target);
            }
        }
    }

    private void CloseFlyout_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFlyoutTarget != null)
        {
            FlyoutBase.GetAttachedFlyout(_currentFlyoutTarget)?.Hide();
            _currentFlyoutTarget = null;
        }
    }
}