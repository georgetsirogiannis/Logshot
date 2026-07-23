using System;
using System.Threading.Tasks;
using System.IO;
using Avalonia.Threading;
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

    // --- Info / Settings View State ---
    [ObservableProperty]
    private bool _isInfoViewOpen = false;

    [RelayCommand]
    public void ToggleInfoView()
    {
        IsInfoViewOpen = !IsInfoViewOpen;
    }

    [RelayCommand]
    public void CloseInfoView()
    {
        IsInfoViewOpen = false;
    }

    // --- Cloud Sync UI Properties ---
    [ObservableProperty]
    private string _syncIcon = "☁️";

    [ObservableProperty]
    private string _syncText = "Waiting...";

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

        // Listen for status changes from the Sync Engine and update the UI securely
        _supabaseService.OnSyncStatusChanged += (icon, text) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SyncIcon = icon;
                SyncText = text;
            });
        };

        _appViewModel = new AppViewModel(databaseService);
    }

    [RelayCommand]
    public async Task InitializeApplication()
    {
        try
        {
            StatusMessage = "Connecting to cloud...";
            await _supabaseService.InitializeAsync();

            // One-time: push any data that existed before cloud sync was added
            string backfillMarker = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "logshot_backfill_done.txt");

            if (!File.Exists(backfillMarker))
            {
                await _databaseService.EnqueueAllExistingDataForSyncAsync();
                File.WriteAllText(backfillMarker, "done");
                _supabaseService.TriggerSync();
            }

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