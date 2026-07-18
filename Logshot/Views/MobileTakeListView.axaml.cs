using Avalonia.Controls;
using Avalonia.Interactivity;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class MobileTakeListView : UserControl
{
    public MobileTakeListView()
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
}
