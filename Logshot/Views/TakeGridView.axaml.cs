using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class TakeGridView : UserControl
{
    private TakeViewModel? _draggedTake;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Control? _currentFlyoutTarget; // Tracks the open flyout

    public TakeGridView()
    {
        InitializeComponent();
    }

    private async void AddCamera_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DayViewModel dayVm) return;

        var textBox = this.FindControl<TextBox>("NewCameraLabelBox");
        var label = textBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(label)) return;

        await dayVm.AddCameraCommand.ExecuteAsync(label);
        if (textBox is not null) textBox.Text = string.Empty;
    }

    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;

        _dragStartPoint = e.GetPosition(border);
        _draggedTake = border.Tag as TakeViewModel;
        _isDragging = false;
    }

    private void Row_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTake is null || sender is not Border border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        var currentPoint = e.GetPosition(border);
        var delta = currentPoint - _dragStartPoint;

        if (!_isDragging && (System.Math.Abs(delta.Y) > 4 || System.Math.Abs(delta.X) > 4))
            _isDragging = true;
    }

    private async void Row_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging || _draggedTake is null || DataContext is not DayViewModel dayVm)
        {
            _draggedTake = null;
            _isDragging = false;
            return;
        }

        if (sender is not Border border || border.Tag is not TakeViewModel targetTake || ReferenceEquals(targetTake, _draggedTake))
        {
            _draggedTake = null;
            _isDragging = false;
            return;
        }

        var takes = dayVm.Takes;
        var oldIndex = takes.IndexOf(_draggedTake);
        var newIndex = takes.IndexOf(targetTake);

        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
        {
            takes.Move(oldIndex, newIndex);
            await dayVm.ReorderTakesCommand.ExecuteAsync(null);
        }

        _draggedTake = null;
        _isDragging = false;
    }

    // Opens the hidden Flyout when Context Menu option is clicked
    private void ChangeRollMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.CommandParameter is Control target)
        {
            _currentFlyoutTarget = target;

            // Delay the flyout opening slightly so the ContextMenu has time to close.
            // This prevents the two popups from fighting for window focus.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FlyoutBase.ShowAttachedFlyout(target);
            });
        }
    }

    // Closes the flyout when Apply or Remove is clicked
    private void CloseFlyout_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFlyoutTarget != null)
        {
            var target = _currentFlyoutTarget;

            // Defer hiding the flyout so the Command has time to execute
            // and the TextBox has time to push its value on LostFocus.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FlyoutBase.GetAttachedFlyout(target)?.Hide();
            });

            _currentFlyoutTarget = null;
        }
    }
}