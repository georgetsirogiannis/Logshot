using Avalonia.Controls;
using Avalonia.Interactivity;
using Logshot.ViewModels;
using System.Collections.Specialized;

namespace Logshot.Views;

public partial class MobileTakeListView : UserControl
{
    private DayViewModel? _dayVm;

    public MobileTakeListView()
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
                MobileScrollViewer?.ScrollToEnd();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
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

    private async void AddScene_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DayViewModel dayVm)
            return;

        var epBox = this.FindControl<TextBox>("NewEpisodeBox");
        var scBox = this.FindControl<TextBox>("NewSceneBox");
        var episode = epBox?.Text?.Trim();
        var scene = scBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(episode) || string.IsNullOrWhiteSpace(scene))
            return;

        await dayVm.AddNewSceneCommand.ExecuteAsync((episode, scene));

        if (epBox is not null) epBox.Text = string.Empty;
        if (scBox is not null) scBox.Text = string.Empty;
    }
}