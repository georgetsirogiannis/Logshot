using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Models;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class DayViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private readonly CameraDataManager _cameraDataManager;
    private readonly ContinuityService _continuityService;

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _projectId = string.Empty;

    [ObservableProperty]
    private string _shootDayNumber = string.Empty;

    [ObservableProperty]
    private DateTime _calendarDate = DateTime.Today;

    [ObservableProperty]
    private string _generalNotes = string.Empty;

    [ObservableProperty]
    private string _topScribbleNotes = string.Empty;

    [ObservableProperty]
    private bool _isFinalized = false;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private ObservableCollection<TakeViewModel> _takes = new();

    [ObservableProperty]
    private int _totalTakes = 0;

    [ObservableProperty]
    private int _currentShot = 0;

    /// <summary>
    /// List of active camera labels for this day
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _activeCameras = new(CameraDataManager.DEFAULT_CAMERAS);

    public DayViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _cameraDataManager = new CameraDataManager();
        _continuityService = new ContinuityService(databaseService, _cameraDataManager);
    }

    public DayViewModel(DatabaseService databaseService, CameraDataManager cameraDataManager)
    {
        _databaseService = databaseService;
        _cameraDataManager = cameraDataManager;
        _continuityService = new ContinuityService(databaseService, cameraDataManager);
    }

    public DayViewModel(DatabaseService databaseService, CameraDataManager cameraDataManager, ContinuityService continuityService)
    {
        _databaseService = databaseService;
        _cameraDataManager = cameraDataManager;
        _continuityService = continuityService;
    }

    /// <summary>
    /// Load day data from the model
    /// </summary>
    public void LoadFromModel(Day day)
    {
        Id = day.Id;
        ProjectId = day.ProjectId;
        ShootDayNumber = day.ShootDayNumber;
        CalendarDate = day.CalendarDate;
        GeneralNotes = day.GeneralNotes;
        TopScribbleNotes = day.TopScribbleNotes;
        IsFinalized = day.IsFinalized;
        CreatedAt = day.CreatedAt;
    }

    /// <summary>
    /// Convert this ViewModel back to a model for database persistence
    /// </summary>
    public Day ToModel()
    {
        return new Day
        {
            Id = Id,
            ProjectId = ProjectId,
            ShootDayNumber = ShootDayNumber,
            CalendarDate = CalendarDate,
            GeneralNotes = GeneralNotes,
            TopScribbleNotes = TopScribbleNotes,
            IsFinalized = IsFinalized,
            CreatedAt = CreatedAt
        };
    }

    /// <summary>
    /// Load all takes for this day from the database
    /// </summary>
    [RelayCommand]
    public async Task LoadTakes()
    {
        // Stub for Phase 2 implementation
        // TODO: Query database for all takes in this day
        // Convert each Take model to TakeViewModel
        // Add to Takes collection
    }

    [RelayCommand]
    public async Task SaveDay()
    {
        await _databaseService.SaveDayAsync(ToModel());
    }

    [RelayCommand]
    public async Task AddTake()
    {
        // Stub for Phase 2 implementation
        // TODO: Create new Take with next sequence order
        // TODO: Increment CurrentShot if scene/episode same as last take
    }

    [RelayCommand]
    public async Task DeleteTake(TakeViewModel take)
    {
        // Stub for Phase 2 implementation
        // TODO: Remove from Takes collection
        // TODO: Persist deletion to database
    }

    [RelayCommand]
    public async Task ReorderTakes()
    {
        // Stub for Phase 3 implementation - drag and drop reordering
        // TODO: Update SequenceOrder for all affected takes based on current order in collection
    }

    [RelayCommand]
    public async Task FinalizeDay()
    {
        // Stub for Phase 3 implementation - Undoable Finalize Day mechanics
        IsFinalized = true;
        await SaveDayCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task UndoFinalizeDay()
    {
        // Stub for Phase 3 implementation
        IsFinalized = false;
        await SaveDayCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task UpdateTotalTakes()
    {
        // Helper to recalculate total take count
        TotalTakes = Takes.Count;
    }

    [RelayCommand]
    public async Task UpdateCurrentShot()
    {
        // Helper to calculate current shot (for Phase 2 Continuity Engine)
        if (Takes.Count > 0)
        {
            CurrentShot = Takes.Last().Shot;
        }
        else
        {
            CurrentShot = 0;
        }
    }

    /// <summary>
    /// Add a new camera column for all takes in this day
    /// </summary>
    [RelayCommand]
    public async Task AddCamera(string cameraLabel)
    {
        if (string.IsNullOrWhiteSpace(cameraLabel))
            return;

        // Add to UI immediately
        if (!ActiveCameras.Contains(cameraLabel))
        {
            ActiveCameras.Add(cameraLabel);
        }

        // Update camera data for all existing takes
        var cameraCreatedAt = DateTime.UtcNow;
        foreach (var take in Takes)
        {
            var cameraData = _cameraDataManager.ParseCameraData(take.CameraData);
            cameraData = _cameraDataManager.AddCamera(cameraData, cameraLabel);

            // Mark as strikethrough if take was recorded before camera was added
            cameraData = _cameraDataManager.MarkCameraStrikethrough(cameraData, cameraLabel, cameraCreatedAt, take.CreatedAt);

            take.CameraData = _cameraDataManager.SerializeCameraData(cameraData);
            await take.SaveTakeCommand.ExecuteAsync(null);
        }

        await SaveDayCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Remove a camera column from all takes in this day
    /// </summary>
    [RelayCommand]
    public async Task RemoveCamera(string cameraLabel)
    {
        if (string.IsNullOrWhiteSpace(cameraLabel))
            return;

        // Prevent removing default cameras
        if (CameraDataManager.DEFAULT_CAMERAS.Contains(cameraLabel))
            return;

        // Remove from UI
        var cameraToRemove = ActiveCameras.FirstOrDefault(c => c == cameraLabel);
        if (cameraToRemove != null)
        {
            ActiveCameras.Remove(cameraToRemove);
        }

        // Update camera data for all takes
        foreach (var take in Takes)
        {
            var cameraData = _cameraDataManager.ParseCameraData(take.CameraData);
            cameraData = _cameraDataManager.RemoveCamera(cameraData, cameraLabel);
            take.CameraData = _cameraDataManager.SerializeCameraData(cameraData);
            await take.SaveTakeCommand.ExecuteAsync(null);
        }

        await SaveDayCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Initialize cameras for this day on first load
    /// </summary>
    [RelayCommand]
    public async Task InitializeCameras()
    {
        var defaultCameras = new ObservableCollection<string>(CameraDataManager.DEFAULT_CAMERAS);
        ActiveCameras = defaultCameras;

        // Ensure all takes have camera data initialized
        foreach (var take in Takes)
        {
            if (string.IsNullOrWhiteSpace(take.CameraData) || take.CameraData == "{}")
            {
                var cameraData = _cameraDataManager.InitializeDefaultCameras();
                take.CameraData = _cameraDataManager.SerializeCameraData(cameraData);
                await take.SaveTakeCommand.ExecuteAsync(null);
            }
        }
    }

    /// <summary>
    /// Refresh the active cameras list based on the first take's camera data
    /// </summary>
    [RelayCommand]
    public async Task RefreshActiveCameras()
    {
        if (Takes.Count == 0)
        {
            ActiveCameras = new ObservableCollection<string>(CameraDataManager.DEFAULT_CAMERAS);
            return;
        }

        // Get cameras from the first take
        var firstTake = Takes[0];
        var cameraData = _cameraDataManager.ParseCameraData(firstTake.CameraData);
        var cameras = _cameraDataManager.GetActiveCameraLabels(cameraData);

        ActiveCameras = new ObservableCollection<string>(cameras);
    }

    /// <summary>
    /// Apply continuity data to a new take when Episode/Scene is set
    /// Looks up historical data and pre-fills shot number and camera setup
    /// </summary>
    public async Task ApplyContinuity(string episode, string scene)
    {
        if (string.IsNullOrWhiteSpace(episode) || string.IsNullOrWhiteSpace(scene) || string.IsNullOrWhiteSpace(ProjectId))
            return;

        var continuityData = await _continuityService.GetContinuityDataAsync(ProjectId, episode, scene);

        // Pre-fill for the next take: update the CurrentShot to the next expected shot
        CurrentShot = continuityData.NextShotNumber - 1; // We'll increment when creating the take

        // Note: Camera setup inheritance happens when the take is created
        // The new take will initialize with the inherited camera data
    }

    /// <summary>
    /// Create a new take with continuity data pre-filled
    /// </summary>
    public async Task CreateTakeWithContinuity(string episode, string scene)
    {
        if (string.IsNullOrWhiteSpace(episode) || string.IsNullOrWhiteSpace(scene))
            return;

        // Get continuity data for this Episode/Scene
        var continuityData = await _continuityService.GetContinuityDataAsync(ProjectId, episode, scene);

        // Create the new take
        var newTake = new Take
        {
            DayId = Id,
            SequenceOrder = Takes.Count,
            Episode = episode,
            Scene = scene,
            Shot = continuityData.NextShotNumber,
            TakeNumber = 1,
            CameraData = continuityData.InheritedCameraData, // Inherit camera setup from last occurrence
            CreatedAt = DateTime.UtcNow
        };

        var takeVM = new TakeViewModel(_databaseService, _cameraDataManager);
        takeVM.LoadFromModel(newTake);
        takeVM.IsFromContinuity = true;
        takeVM.ContinuityContext = $"Inherited from Shot {continuityData.LastReferenceTake?.Shot ?? 0}";

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        Takes.Add(takeVM);
        await UpdateTotalTakesCommand.ExecuteAsync(null);

        // Update CurrentShot for UI feedback
        CurrentShot = continuityData.NextShotNumber;
    }

    /// <summary>
    /// Get continuity information for a specific Episode/Scene (for UI display)
    /// </summary>
    public async Task<ContinuityService.ContinuityData> GetContinuityInfoAsync(string episode, string scene)
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            return new ContinuityService.ContinuityData();

        return await _continuityService.GetContinuityDataAsync(ProjectId, episode, scene);
    }
}
