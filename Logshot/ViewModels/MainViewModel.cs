using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private AppViewModel _appViewModel;

    [ObservableProperty]
    private bool _isInitialized = false;

    [ObservableProperty]
    private string _statusMessage = "Initializing...";

    // Phase 4: Responsive layout trigger. True when the workspace width drops below the mobile breakpoint.
    [ObservableProperty]
    private bool _isMobileLayout = false;

    /// <summary>
    /// Controls whether the left Project/Day sidebar is expanded. On mobile layouts it starts
    /// collapsed so the day workspace can use the full screen width, and can be toggled via a
    /// hamburger button; on desktop it always stays open.
    /// </summary>
    [ObservableProperty]
    private bool _isSidebarOpen = true;

    partial void OnIsMobileLayoutChanged(bool value)
    {
        // Auto-collapse the sidebar the moment we drop into mobile layout so the day view
        // gets the full screen; always keep it open again once back on desktop.
        IsSidebarOpen = !value;
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    public MainViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _appViewModel = new AppViewModel(databaseService);
    }

    [RelayCommand]
    public async Task InitializeApplication()
    {
        try
        {
            StatusMessage = "Loading application...";
            await AppViewModel.InitializeAppCommand.ExecuteAsync(null);
            IsInitialized = true;
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
