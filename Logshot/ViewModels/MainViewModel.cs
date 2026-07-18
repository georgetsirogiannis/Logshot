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
