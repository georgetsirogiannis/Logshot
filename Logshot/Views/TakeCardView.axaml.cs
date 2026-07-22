using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
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

    // Opens the hidden Flyout when Context Menu option is clicked
    private void ChangeRollMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            // Find the ContextMenu by traversing visual and logical parent trees
            ContextMenu? contextMenu = null;
            Control? current = menuItem;
            while (current != null)
            {
                if (current is ContextMenu cm)
                {
                    contextMenu = cm;
                    break;
                }
                current = current.Parent as Control ?? current.GetVisualParent() as Control;
            }

            // In Avalonia, the logical Parent of an inline ContextMenu points to its owner control
            Control? targetControl = (contextMenu?.Parent as Control) ?? (contextMenu?.PlacementTarget as Control);
            if (targetControl != null)
            {
                Control? targetWithFlyout = targetControl;
                while (targetWithFlyout != null)
                {
                    var flyout = FlyoutBase.GetAttachedFlyout(targetWithFlyout);
                    if (flyout != null)
                    {
                        flyout.ShowAt(targetWithFlyout);
                        return;
                    }

                    targetWithFlyout = targetWithFlyout.Parent as Control ?? targetWithFlyout.GetVisualParent() as Control;
                }

                // Fallback
                var fallbackFlyout = FlyoutBase.GetAttachedFlyout(targetControl);
                fallbackFlyout?.ShowAt(targetControl);
            }
        }
    }

    private void CloseFlyout_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            // Defer closing so the command has time to run and TextBox can commit its text on LostFocus
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (btn.Tag is Flyout flyout)
                {
                    flyout.Hide();
                }
                else
                {
                    var flyoutPresenter = btn.FindAncestorOfType<FlyoutPresenter>();
                    if (flyoutPresenter?.Parent is Popup popupCtrl)
                    {
                        popupCtrl.IsOpen = false;
                    }
                }
            });
        }
    }

    private async void CameraRoll_LostFocus(object? sender, Avalonia.Input.FocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var text = textBox.Text?.Trim();
            if (text == "---" || text == "----")
            {
                if (textBox.DataContext is TakeViewModel takeVm)
                {
                    // This handles CAM A and CAM B
                    string camLabel = textBox.Tag?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(camLabel))
                    {
                        await takeVm.ToggleCameraNoRollCommand.ExecuteAsync(camLabel);
                    }
                }
                else if (textBox.DataContext is CameraRollCell extraCell)
                {
                    // This handles dynamically added Extra Cameras
                    await extraCell.ToggleNoRollCommand.ExecuteAsync(null);
                }
            }
        }
    }

    private async void SoundRoll_LostFocus(object? sender, Avalonia.Input.FocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var text = textBox.Text?.Trim();
            if (text == "---" || text == "----")
            {
                if (textBox.DataContext is TakeViewModel takeVm)
                {
                    await takeVm.ToggleSoundNoRollCommand.ExecuteAsync(null);
                }
            }
        }
    }
}