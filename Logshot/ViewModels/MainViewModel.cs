using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

    // --- Autocorrection State ---
    [ObservableProperty]
    private bool _isAutocorrectEnabled;

    partial void OnIsAutocorrectEnabledChanged(bool value)
    {
        AutocorrectionManager.Instance.SaveSettings(value, AutocorrectionManager.Instance.CustomDictionaryText);
    }

    [ObservableProperty]
    private bool _isCustomDictModalOpen = false;

    [ObservableProperty]
    private string _customDictText = string.Empty;

    [RelayCommand]
    public void OpenCustomDict()
    {
        CustomDictText = AutocorrectionManager.Instance.CustomDictionaryText;
        IsCustomDictModalOpen = true;
    }

    [RelayCommand]
    public void SaveCustomDict()
    {
        AutocorrectionManager.Instance.SaveSettings(IsAutocorrectEnabled, CustomDictText);
        IsCustomDictModalOpen = false;
    }

    [RelayCommand]
    public void CancelCustomDict()
    {
        IsCustomDictModalOpen = false;
    }

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

    // --- Database Export / Import State ---

    [ObservableProperty]
    private int _selectedExportTargetIndex = 0;

    public System.Collections.ObjectModel.ObservableCollection<string> ExportTargets { get; } = new();

    public Action? RequestExportDbFilePicker;
    public Action? RequestImportDbFilePicker;

    [ObservableProperty]
    private bool _isImportConfirmOpen = false;

    [ObservableProperty]
    private string _importWarningMessage = string.Empty;

    private string _pendingImportPath = string.Empty;

    private bool _isImportResultOpen = false;
    public bool IsImportResultOpen
    {
        get => _isImportResultOpen;
        set => SetProperty(ref _isImportResultOpen, value);
    }

    private string _importResultTitle = string.Empty;
    public string ImportResultTitle
    {
        get => _importResultTitle;
        set => SetProperty(ref _importResultTitle, value);
    }

    private string _importResultMessage = string.Empty;
    public string ImportResultMessage
    {
        get => _importResultMessage;
        set => SetProperty(ref _importResultMessage, value);
    }

    private IRelayCommand? _closeImportResultCommand;
    public IRelayCommand CloseImportResultCommand => _closeImportResultCommand ??= new RelayCommand(CloseImportResult);

    public void CloseImportResult()
    {
        IsImportResultOpen = false;
    }

    [RelayCommand]
    public void ExportDatabase() => RequestExportDbFilePicker?.Invoke();

    [RelayCommand]
    public void ImportDatabase() => RequestImportDbFilePicker?.Invoke();

    partial void OnIsInfoViewOpenChanged(bool value)
    {
        if (value)
        {
            ExportTargets.Clear();
            ExportTargets.Add("All Projects");
            foreach (var proj in AppViewModel.Projects)
            {
                ExportTargets.Add($"Project: {proj.Name}");
            }
            SelectedExportTargetIndex = 0;
        }
    }

    public async Task ProcessExportDbAsync(string destPath)
    {
        AppViewModel.LoadingMessage = "Exporting database...";
        AppViewModel.IsLoading = true;
        try
        {
            string? targetProjectId = null;
            if (SelectedExportTargetIndex > 0 && SelectedExportTargetIndex <= AppViewModel.Projects.Count)
            {
                targetProjectId = AppViewModel.Projects[SelectedExportTargetIndex - 1].Id;
            }
            await _databaseService.ExportDatabaseAsync(destPath, targetProjectId);
            StatusMessage = "Database exported successfully.";
        }
        catch (Exception ex) { StatusMessage = $"Export Failed: {ex.Message}"; }
        finally { AppViewModel.IsLoading = false; }
    }

    public async Task ProcessImportDbAsync(string sourcePath)
    {
        AppViewModel.LoadingMessage = "Analyzing import...";
        AppViewModel.IsLoading = true;
        try
        {
            var (isValid, summaryMsg) = await _databaseService.GetImportSummaryAsync(sourcePath);
            _pendingImportPath = sourcePath;

            if (isValid)
            {
                ImportWarningMessage = summaryMsg;
                IsImportConfirmOpen = true; // Always show the merge confirmation
            }
            else
            {
                StatusMessage = "Import Failed: " + summaryMsg;
                ImportResultTitle = "Database Merge Failed";
                ImportResultMessage = summaryMsg;
                IsImportResultOpen = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import Check Failed: {ex.Message}";
            ImportResultTitle = "Database Merge Failed";
            ImportResultMessage = $"Could not open or read the imported database file.\n\nReason:\n{ex.Message}";
            IsImportResultOpen = true;
        }
        finally { AppViewModel.IsLoading = false; }
    }

    [RelayCommand]
    public async Task ConfirmImport()
    {
        IsImportConfirmOpen = false;
        AppViewModel.LoadingMessage = "Merging database...";
        AppViewModel.IsLoading = true;
        try
        {
            await ExecuteImport();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import Failed: {ex.Message}";
            ImportResultTitle = "Database Merge Failed";
            ImportResultMessage = $"An error occurred while merging data into your database.\n\nReason:\n{ex.Message}";
            IsImportResultOpen = true;
        }
        finally { AppViewModel.IsLoading = false; }
    }

    [RelayCommand]
    public void CancelImport()
    {
        IsImportConfirmOpen = false;
        _pendingImportPath = string.Empty;
    }

    private async Task ExecuteImport()
    {
        if (string.IsNullOrEmpty(_pendingImportPath)) return;

        // Execute the safe merge instead of a file replacement and get added counts
        var (addedProjects, addedDays, addedTakes) = await _databaseService.MergeDatabaseAsync(_pendingImportPath);
        _pendingImportPath = string.Empty;

        // Force a full reload of the app state after DB merge
        await AppViewModel.LoadAllProjectsCommand.ExecuteAsync(null);
        AppViewModel.CurrentProject = null;
        AppViewModel.CurrentDay = null;

        StatusMessage = "Database merged successfully.";

        // Display completion result popup with exact breakdown
        ImportResultTitle = "Database Merge Successful";
        ImportResultMessage = $"The database merge completed successfully!\n\n" +
                               $"Added Data:\n" +
                               $"• Projects: {addedProjects}\n" +
                               $"• Days: {addedDays}\n" +
                               $"• Takes: {addedTakes}";
        IsImportResultOpen = true;
    }

    // --- Cloud Sync UI Properties ---
    [ObservableProperty]
    private string _syncIcon = SupabaseService.SyncIconPaths.Synced;

    [ObservableProperty]
    private string _syncIconColor = "#A1A1AA";

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
        _supabaseService.OnSyncStatusChanged += (iconPath, text) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SyncIcon = iconPath;
                SyncIconColor = (iconPath == SupabaseService.SyncIconPaths.Warning)
                    ? "#EAB308"
                    : "#A1A1AA";
                SyncText = text;
            });
        };

        // Listen for cloud pull events and perform non-intrusive in-place UI merge
        _supabaseService.OnCloudDataReceived += () =>
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await AppViewModel.MergeCloudDataAsync();
            });
        };

        IsAutocorrectEnabled = AutocorrectionManager.Instance.IsEnabled;

        _appViewModel = new AppViewModel(databaseService);
    }

    [RelayCommand]
    public async Task InitializeApplication()
    {
        try
        {
            StatusMessage = "Loading application...";
            AppViewModel.LoadingMessage = "Loading projects...";

            // 1. Load Local UI First (Instant Startup)
            await AppViewModel.InitializeAppCommand.ExecuteAsync(null);

            IsInitialized = true;
            StatusMessage = "Ready";

            // 2. Offload cloud connection to a background thread to prevent blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    string backfillMarker = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "logshot_backfill_done.txt");

                    if (!File.Exists(backfillMarker))
                    {
                        var localProjects = await _databaseService.GetAllProjectsAsync();
                        if (localProjects.Count > 0)
                        {
                            await _databaseService.EnqueueAllExistingDataForSyncAsync();
                        }
                        File.WriteAllText(backfillMarker, "done");
                    }

                    await _supabaseService.InitializeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background Sync Init Error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ManualSync()
    {
        await _supabaseService.ManualSyncAsync();
    }
}