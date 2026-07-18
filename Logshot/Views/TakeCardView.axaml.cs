using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class TakeCardView : UserControl
{
    // Phase 4: Swipe-right quick-action drawer. The card slides right to reveal
    // the FS / LS / ΑΚΥΡΟ buttons underneath, then snaps back to open or closed.
    private const double DrawerWidth = 190;
    private const double SwipeThreshold = 40;

    private Point _dragStartPoint;
    private bool _isDragging;
    private bool _isOpen;

    private Border? _cardSurface;
    private TranslateTransform? _cardTransform;

    public TakeCardView()
    {
        InitializeComponent();
        _cardSurface = this.FindControl<Border>("CardSurface");
        _cardTransform = _cardSurface?.RenderTransform as TranslateTransform;
    }

    private void Card_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border)
            return;

        _dragStartPoint = e.GetPosition(border);
        _isDragging = false;
        e.Pointer.Capture(border);
    }

    private void Card_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border || _cardTransform is null)
            return;

        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            return;

        var currentPoint = e.GetPosition(border);
        var delta = currentPoint - _dragStartPoint;

        if (!_isDragging && System.Math.Abs(delta.X) > 4)
        {
            _isDragging = true;
        }

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
        if (sender is Border border)
            e.Pointer.Capture(null);

        if (!_isDragging || _cardTransform is null)
        {
            _isDragging = false;
            return;
        }

        // Snap open if past the threshold, otherwise snap closed.
        _isOpen = _cardTransform.X > SwipeThreshold;
        _cardTransform.X = _isOpen ? DrawerWidth : 0;
        _isDragging = false;
    }

    // Phase 5.1: Single-tap on the take number toggles Circled, double-tap toggles Failed.
    private async void TakeNumber_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBox { DataContext: TakeViewModel takeVm })
        {
            await takeVm.MarkCircledCommand.ExecuteAsync(null);
        }
    }

    private async void TakeNumber_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBox { DataContext: TakeViewModel takeVm })
        {
            await takeVm.MarkFailedCommand.ExecuteAsync(null);
        }
    }
}
