using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class TakeGridView : UserControl
{
    private TakeViewModel? _draggedTake;
    private bool _isDragging;
    private Point _dragStartPoint;

    public TakeGridView()
    {
        InitializeComponent();
    }

    private async void AddCamera_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DayViewModel dayVm)
            return;

        var textBox = this.FindControl<TextBox>("NewCameraLabelBox");
        var label = textBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(label))
            return;

        await dayVm.AddCameraCommand.ExecuteAsync(label);

        if (textBox is not null)
            textBox.Text = string.Empty;
    }

    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border)
            return;

        _dragStartPoint = e.GetPosition(border);
        _draggedTake = border.Tag as TakeViewModel;
        _isDragging = false;
    }

    private void Row_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTake is null || sender is not Border border)
            return;

        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            return;

        var currentPoint = e.GetPosition(border);
        var delta = currentPoint - _dragStartPoint;

        // Require a small movement threshold before treating this as a drag
        if (!_isDragging && (System.Math.Abs(delta.Y) > 4 || System.Math.Abs(delta.X) > 4))
        {
            _isDragging = true;
        }
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
