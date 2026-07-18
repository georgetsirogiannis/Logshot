using Avalonia.Controls;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class MainView : UserControl
{
    // Phase 4: Below this width the desktop 14/48 grid is swapped for the mobile adaptive card layout.
    private const double MobileBreakpointWidth = 720;

    private Grid? _rootSplitGrid;
    private MainViewModel? _boundViewModel;

    public MainView()
    {
        InitializeComponent();
        _rootSplitGrid = this.FindControl<Grid>("RootSplitGrid");

        DataContextChanged += (_, _) =>
        {
            if (_boundViewModel is not null)
                _boundViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            if (DataContext is MainViewModel vm)
            {
                _boundViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
                vm.InitializeApplicationCommand.Execute(null);
                UpdateLayoutMode(Bounds.Width);
                UpdateSidebarColumnWidth();
            }
        };

        SizeChanged += (_, e) => UpdateLayoutMode(e.NewSize.Width);
        UpdateLayoutMode(Bounds.Width);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSidebarOpen))
        {
            UpdateSidebarColumnWidth();
        }
    }

    private void UpdateLayoutMode(double width)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsMobileLayout = width < MobileBreakpointWidth;
        }
    }

    /// <summary>
    /// Collapses the sidebar column to zero width when closed so the day workspace
    /// reclaims the full screen on mobile; otherwise reserves the standard 280px.
    /// </summary>
    private void UpdateSidebarColumnWidth()
    {
        if (_rootSplitGrid is null || DataContext is not MainViewModel vm)
            return;

        if (_rootSplitGrid.ColumnDefinitions.Count > 0)
        {
            _rootSplitGrid.ColumnDefinitions[0].Width = vm.IsSidebarOpen
                ? new GridLength(280)
                : new GridLength(0);
        }
    }
}