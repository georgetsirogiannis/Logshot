using Avalonia.Controls;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class MainWindow : Window
{
    private bool _readyToClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_readyToClose)
        {
            e.Cancel = true;
            FlushThenClose();
        }
        base.OnClosing(e);
    }

    private async void FlushThenClose()
    {
        if (DataContext is MainViewModel vm)
            await vm.DatabaseService.WaitForPendingWritesAsync();

        _readyToClose = true;
        Close();
    }
}