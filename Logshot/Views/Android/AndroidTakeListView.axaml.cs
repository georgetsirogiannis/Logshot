using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Logshot.ViewModels;
using System.Collections.Specialized;
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
            }
            if (DataContext is DayViewModel dayVm)
            {
                _dayVm = dayVm;
                _dayVm.Takes.CollectionChanged += Takes_CollectionChanged;
            }
        };
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
        if (e.Action == NotifyCollectionChangedAction.Add && _dayVm != null && !_dayVm.IsLoadingTakes)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AndroidScrollViewer?.ScrollToEnd();

                // Focus the first camera input box in the newly created card
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var lastCard = this.GetVisualDescendants()
                        .OfType<AndroidTakeCardView>()
                        .LastOrDefault();

                    if (lastCard != null)
                    {
                        var firstTextBox = lastCard.GetVisualDescendants()
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