using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Logshot.ViewModels;

namespace Logshot.Views.Android;

public partial class AndroidTakeCardView : UserControl
{
    public AndroidTakeCardView()
    {
        InitializeComponent();
    }

    private void Background_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Avalonia.Visual sourceVisual && sourceVisual.FindAncestorOfType<TextBox>() != null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        topLevel?.FocusManager?.Focus(null);
    }

    private void DeleteTake_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Control ctrl && ctrl.DataContext is TakeViewModel takeVm)
        {
            Control? curr = this;
            while (curr != null)
            {
                if (curr is UserControl uc && uc.DataContext is DayViewModel dayVm)
                {
                    if (dayVm.PromptDeleteTakeCommand.CanExecute(takeVm))
                    {
                        dayVm.PromptDeleteTakeCommand.Execute(takeVm);
                    }
                    break;
                }
                curr = curr.Parent as Control ?? curr.GetVisualParent() as Control;
            }
        }
        CloseFlyout_Click(sender, e);
    }

    private void CloseFlyout_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
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