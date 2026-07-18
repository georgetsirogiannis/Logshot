using Avalonia.Controls;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class MainView : UserControl
{
    // Phase 4: Below this width the desktop 14/48 grid is swapped for the mobile adaptive card layout.
    private const double MobileBreakpointWidth = 720;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.InitializeApplicationCommand.Execute(null);
                UpdateLayoutMode(Bounds.Width);
            }
        };

        SizeChanged += (_, e) => UpdateLayoutMode(e.NewSize.Width);
        UpdateLayoutMode(Bounds.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsMobileLayout = width < MobileBreakpointWidth;
        }
    }
}