using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Logshot.ViewModels;
using System;
using System.Collections.Specialized;

namespace Logshot.Views;

public partial class TakeGridView : UserControl
{
    private TakeViewModel? _draggedItem;
    private Point _startPoint;
    private bool _isDragging;
    private DayViewModel? _dayVm;

    public TakeGridView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_dayVm != null)
            {
                _dayVm.Takes.CollectionChanged -= Takes_CollectionChanged;
            }
            if (DataContext is DayViewModel dayVm)
            {
                _dayVm = dayVm;
                _dayVm.Takes.CollectionChanged += Takes_CollectionChanged;
            }
        };
    }

    private void Takes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _dayVm != null && !_dayVm.IsLoadingTakes)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TakesScrollViewer?.ScrollToEnd();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
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

    private void Row_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is TakeViewModel takeVm)
        {
            var point = e.GetCurrentPoint(border);
            if (point.Properties.IsLeftButtonPressed)
            {
                _startPoint = e.GetPosition(this);
                _draggedItem = takeVm;
                _isDragging = false;
            }
        }
    }

    private void Row_PointerMoved(object sender, PointerEventArgs e)
    {
        if (_draggedItem != null && !_isDragging)
        {
            var currentPoint = e.GetPosition(this);
            if (Math.Abs(currentPoint.Y - _startPoint.Y) > 5)
            {
                _isDragging = true;
            }
        }
    }

    private void Row_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        _draggedItem = null;
        _isDragging = false;
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
                    // Triggers your Sound No-Roll command
                    // Note: If your command is named differently in TakeViewModel, update the name below!
                    await takeVm.ToggleSoundNoRollCommand.ExecuteAsync(null);
                }
            }
        }
    }
}