using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private readonly SupabaseService _supabaseService;

    public DatabaseService DatabaseService => _databaseService;

    [ObservableProperty]
    private AppViewModel _appViewModel;

    [ObservableProperty]
    private bool _isInitialized = false;

    [ObservableProperty]
    private string _statusMessage = "Initializing...";

    // Phase 4: Responsive layout trigger.
    [ObservableProperty]
    private bool _isMobileLayout = false;

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    partial void OnIsMobileLayoutChanged(bool value)
    {
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
        _supabaseService = new SupabaseService(_databaseService);

        // Hook up the debounce sync trigger so it runs automatically in the background
        _databaseService.OnDataChanged += () => _supabaseService.TriggerSync();

        _appViewModel = new AppViewModel(databaseService);
    }

    [RelayCommand]
    public async Task InitializeApplication()
    {
        try
        {
            StatusMessage = "Connecting to cloud...";
            await _supabaseService.InitializeAsync();

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