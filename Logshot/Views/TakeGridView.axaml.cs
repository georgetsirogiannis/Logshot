using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Logshot.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Logshot.Views;

public partial class TakeGridView : UserControl
{
    private static readonly DataFormat<TakeViewModel> TakeDataFormat =
        DataFormat.CreateInProcessFormat<TakeViewModel>("logshot-take-row");

    private TakeViewModel? _draggedItem;
    private PointerPressedEventArgs? _dragStartEventArgs;
    private Point _startPoint;
    private bool _isDragging;
    private int _dropInsertionIndex = -1;
    private Border? _dropMarker;
    private DayViewModel? _dayVm;

    public TakeGridView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_dayVm != null)
            {
                _dayVm.Takes.CollectionChanged -= Takes_CollectionChanged;
                _dayVm.PropertyChanged -= DayVm_PropertyChanged;
            }
            if (DataContext is DayViewModel dayVm)
            {
                _dayVm = dayVm;
                _dayVm.Takes.CollectionChanged += Takes_CollectionChanged;
                _dayVm.PropertyChanged += DayVm_PropertyChanged;
            }
        };
    }

    private void DayVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DayViewModel.Takes) && _dayVm != null)
        {
            _dayVm.Takes.CollectionChanged -= Takes_CollectionChanged;
            _dayVm.Takes.CollectionChanged += Takes_CollectionChanged;
        }
    }

    private void Takes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _dayVm != null && !_dayVm.IsLoadingTakes && _dayVm.Takes.Count > 0)
        {
            int lastIndex = _dayVm.Takes.Count - 1;
            var lastTake = _dayVm.Takes[lastIndex];

            // Post at Loaded priority so Avalonia updates the ListBox layout and item containers first
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (TakesListBox != null)
                {
                    // 1. Request ListBox to scroll item into view
                    TakesListBox.ScrollIntoView(lastIndex);
                    TakesListBox.ScrollIntoView(lastTake);

                    // 2. Set ScrollViewer Offset Y to max to guarantee scrolling to the bottom
                    var scrollViewer = TakesListBox.FindDescendantOfType<ScrollViewer>();
                    if (scrollViewer != null)
                    {
                        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, double.MaxValue);
                    }
                }

                // 3. Auto-focus the first camera input box in the newly added row
                TryFocusFirstInputInContainer(lastTake);
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void TryFocusFirstInputInContainer(TakeViewModel take)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var container = TakesListBox?.ContainerFromItem(take) as Control;
            if (container != null)
            {
                container.BringIntoView();

                var firstTextBox = container.GetVisualDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(tb => !tb.IsReadOnly && tb.IsEffectivelyVisible);

                firstTextBox?.Focus();
            }
            else
            {
                // Retry if container is being virtualized/realized
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var c = TakesListBox?.ContainerFromItem(take) as Control;
                    c?.BringIntoView();

                    var firstTextBox = c?.GetVisualDescendants()
                        .OfType<TextBox>()
                        .FirstOrDefault(tb => !tb.IsReadOnly && tb.IsEffectivelyVisible);

                    firstTextBox?.Focus();
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private async void AddCamera_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DayViewModel dayVm && NewCameraLabelBox != null)
        {
            var label = NewCameraLabelBox.Text?.Trim();
            if (!string.IsNullOrEmpty(label))
            {
                await dayVm.AddCameraCommand.ExecuteAsync(label);
                NewCameraLabelBox.Text = string.Empty;
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

    private void ReorderHandle_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (_dayVm is { IsReorderMode: true, IsFinalized: false } &&
            sender is Control handle && handle.DataContext is TakeViewModel takeVm &&
            e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            _startPoint = e.GetPosition(this);
            _draggedItem = takeVm;
            _dragStartEventArgs = e;
            _isDragging = false;
            ClearDropMarker();
            e.Pointer.Capture(handle);
        }
    }

    private async void ReorderHandle_PointerMoved(object sender, PointerEventArgs e)
    {
        if (_draggedItem == null || _dragStartEventArgs == null || _isDragging ||
            _dayVm is not { IsReorderMode: true, IsFinalized: false })
            return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _startPoint;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) >= 5)
        {
            var draggedItem = _draggedItem;
            _isDragging = true;

            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(TakeDataFormat, draggedItem));
            e.Pointer.Capture(null);

            await DragDrop.DoDragDropAsync(_dragStartEventArgs, transfer, DragDropEffects.Move);

            _isDragging = false;
            _draggedItem = null;
            _dragStartEventArgs = null;
            ClearDropMarker();
        }
    }

    private void ReorderHandle_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        if (!_isDragging)
        {
            _draggedItem = null;
            _dragStartEventArgs = null;
        }
    }

    private void TakesListBox_DragOver(object? sender, DragEventArgs e)
    {
        if (_dayVm is not { IsReorderMode: true, IsFinalized: false } ||
            !e.DataTransfer.Formats.Contains(TakeDataFormat) || TakesListBox == null)
        {
            ClearDropMarker();
            e.DragEffects = DragDropEffects.None;
            return;
        }

        UpdateDropTarget(e.GetPosition(TakesListBox));
        e.DragEffects = _dropInsertionIndex >= 0 ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void TakesListBox_DragLeave(object? sender, DragEventArgs e)
    {
        ClearDropMarker();
    }

    private async void TakesListBox_Drop(object? sender, DragEventArgs e)
    {
        if (_dayVm is not { IsReorderMode: true, IsFinalized: false } || TakesListBox == null ||
            e.DataTransfer.TryGetValue(TakeDataFormat) is not { } draggedItem)
        {
            ClearDropMarker();
            e.DragEffects = DragDropEffects.None;
            return;
        }

        UpdateDropTarget(e.GetPosition(TakesListBox));
        if (_dropInsertionIndex < 0)
        {
            ClearDropMarker();
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var oldIndex = _dayVm.Takes.IndexOf(draggedItem);
        var newIndex = _dropInsertionIndex;
        if (newIndex > oldIndex)
        {
            newIndex--;
        }

        await _dayVm.MoveTakeAsync(draggedItem, newIndex);
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
        ClearDropMarker();
    }

    private void UpdateDropTarget(Point listPosition)
    {
        if (_dayVm == null || TakesListBox == null)
            return;

        ClearDropMarker();

        foreach (var take in _dayVm.Takes)
        {
            if (TakesListBox.ContainerFromItem(take) is not Control container)
                continue;

            var row = container.GetVisualDescendants()
                .OfType<Border>()
                .FirstOrDefault(border => ReferenceEquals(border.Tag, take));
            if (row == null)
                continue;

            var rowOrigin = row.TranslatePoint(new Point(0, 0), TakesListBox);
            if (rowOrigin is not Point origin ||
                listPosition.Y < origin.Y || listPosition.Y > origin.Y + row.Bounds.Height)
            {
                continue;
            }

            var isAfter = listPosition.Y - origin.Y >= row.Bounds.Height / 2;
            _dropInsertionIndex = _dayVm.Takes.IndexOf(take) + (isAfter ? 1 : 0);
            _dropMarker = row.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(border => border.Name == "DropMarker");
            if (_dropMarker != null)
            {
                _dropMarker.VerticalAlignment = isAfter
                    ? Avalonia.Layout.VerticalAlignment.Bottom
                    : Avalonia.Layout.VerticalAlignment.Top;
                _dropMarker.IsVisible = true;
            }

            return;
        }
    }

    private void ClearDropMarker()
    {
        if (_dropMarker != null)
        {
            _dropMarker.IsVisible = false;
            _dropMarker = null;
        }

        _dropInsertionIndex = -1;
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
                    string camLabel = textBox.Tag?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(camLabel))
                    {
                        await takeVm.ToggleCameraNoRollCommand.ExecuteAsync(camLabel);
                    }
                }
                else if (textBox.DataContext is CameraRollCell extraCell)
                {
                    await extraCell.ToggleNoRollCommand.ExecuteAsync(null);
                }
            }

            if (textBox.DataContext is TakeViewModel vm)
            {
                await vm.SaveTakeCommand.ExecuteAsync(null);
            }
            else if (textBox.DataContext is CameraRollCell cell)
            {
                await cell.OwnerSaveAsync();
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

            if (textBox.DataContext is TakeViewModel vm)
            {
                await vm.FlushPendingTextSaveAsync();
            }
        }
    }

    private async void TextNotes_LostFocus(object? sender, Avalonia.Input.FocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: TakeViewModel takeViewModel })
        {
            await takeViewModel.FlushPendingTextSaveAsync();
        }
    }
}