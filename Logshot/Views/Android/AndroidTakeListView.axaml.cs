using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Logshot.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Logshot.Views.Android;

public partial class AndroidTakeListView : UserControl
{
    private DayViewModel? _dayVm;

    public AndroidTakeListView()
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

    private void Background_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Avalonia.Visual sourceVisual && sourceVisual.FindAncestorOfType<TextBox>() != null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        topLevel?.FocusManager?.Focus(null);
    }

    private void Takes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var addedTake = e.Action == NotifyCollectionChangedAction.Add
            ? e.NewItems?.OfType<TakeViewModel>().FirstOrDefault()
            : null;

        if (addedTake != null && _dayVm != null && !_dayVm.IsLoadingTakes)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var listItem = _dayVm.FlatTakeList
                    .OfType<TakeListTakeViewModel>()
                    .FirstOrDefault(item => ReferenceEquals(item.Take, addedTake));

                if (listItem == null || AndroidTakeListBox == null)
                    return;

                AndroidTakeListBox.ScrollIntoView(listItem);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    AndroidTakeListBox.ScrollIntoView(listItem);

                    var container = AndroidTakeListBox.ContainerFromItem(listItem) as Control;
                    container?.BringIntoView();

                    if (container != null)
                    {
                        var firstTextBox = container.GetVisualDescendants()
                            .OfType<TextBox>()
                            .FirstOrDefault(tb => !tb.IsReadOnly && tb.IsEffectivelyVisible);

                        firstTextBox?.Focus();
                    }
                }, Avalonia.Threading.DispatcherPriority.Render);
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
}