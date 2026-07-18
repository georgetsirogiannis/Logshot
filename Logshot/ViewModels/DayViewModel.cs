using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

    /// <summary>
    /// Cameras beyond the two defaults (CAM A / CAM B), used to populate any
    /// dynamically added camera columns in the header (positioned before SOUND ROLL).
    /// </summary>
    public IEnumerable<string> ExtraActiveCameras => ActiveCameras.Where(c => c != "CAM A" && c != "CAM B");

    public DayViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _cameraDataManager = new CameraDataManager();
        _continuityService = new ContinuityService(databaseService, _cameraDataManager);
        SubscribeActiveCameras(ActiveCameras);
        SubscribeTakes(Takes);
    }

    public DayViewModel(DatabaseService databaseService, CameraDataManager cameraDataManager)
    {
        _databaseService = databaseService;
        _cameraDataManager = cameraDataManager;
        _continuityService = new ContinuityService(databaseService, cameraDataManager);
        SubscribeActiveCameras(ActiveCameras);
        SubscribeTakes(Takes);
    }

    public DayViewModel(DatabaseService databaseService, CameraDataManager cameraDataManager, ContinuityService continuityService)
    {
        _databaseService = databaseService;
        _cameraDataManager = cameraDataManager;
        _continuityService = continuityService;
        SubscribeActiveCameras(ActiveCameras);
        SubscribeTakes(Takes);
    }

    private void SubscribeActiveCameras(ObservableCollection<string> cameras)
    {
        cameras.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ExtraActiveCameras));
    }

    partial void OnActiveCamerasChanged(ObservableCollection<string> oldValue, ObservableCollection<string> newValue)
    {
        SubscribeActiveCameras(newValue);
        OnPropertyChanged(nameof(ExtraActiveCameras));
    }

    /// <summary>
    /// Keeps the mobile hierarchical grouping in sync with the Takes collection, watching both
    /// additions/removals and in-place edits (e.g. retroactively changing a take's Episode/Scene).
    /// </summary>
    private void SubscribeTakes(ObservableCollection<TakeViewModel> takes)
    {
        takes.CollectionChanged += Takes_CollectionChanged;
        foreach (var take in takes)
        {
            take.PropertyChanged += Take_PropertyChanged;
        }
    }

    private void Takes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TakeViewModel take in e.OldItems)
            {
                take.PropertyChanged -= Take_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (TakeViewModel take in e.NewItems)
            {
                take.PropertyChanged += Take_PropertyChanged;
            }
        }
    }

    private void Take_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TakeViewModel.Episode) || e.PropertyName == nameof(TakeViewModel.Scene))
        {
            BuildHierarchicalGroups();
        }
    }

    partial void OnTakesChanged(ObservableCollection<TakeViewModel> oldValue, ObservableCollection<TakeViewModel> newValue)
    {
        if (oldValue is not null)
        {
            oldValue.CollectionChanged -= Takes_CollectionChanged;
            foreach (var take in oldValue)
            {
                take.PropertyChanged -= Take_PropertyChanged;
            }
        }

        SubscribeTakes(newValue);
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
        var takes = await _databaseService.GetTakesForDayAsync(Id);

        Takes.Clear();
        foreach (var take in takes.OrderBy(t => t.SequenceOrder))
        {
            var takeVM = new TakeViewModel(_databaseService, _cameraDataManager);
            takeVM.LoadFromModel(take);
            await takeVM.RefreshCameraDataCommand.ExecuteAsync(null);
            Takes.Add(takeVM);
        }

        // Merge active camera labels discovered across all takes with the defaults
        var discoveredCameras = takes
            .SelectMany(t => _cameraDataManager.GetActiveCameraLabels(_cameraDataManager.ParseCameraData(t.CameraData)))
            .Distinct();

        foreach (var camera in discoveredCameras)
        {
            if (!ActiveCameras.Contains(camera))
                ActiveCameras.Add(camera);
        }

        RefreshAllExtraCameraRolls();
        await UpdateTotalTakesCommand.ExecuteAsync(null);
        await UpdateCurrentShotCommand.ExecuteAsync(null);
        BuildHierarchicalGroups();
    }

    [RelayCommand]
    public async Task SaveDay()
    {
        await _databaseService.SaveDayAsync(ToModel());
    }

    /// <summary>
    /// Desktop equivalent of the mobile "+ SHOT" button: starts a brand new Shot (Take 1)
    /// under the current Episode/Scene, using the continuity engine to inherit the camera setup.
    /// </summary>
    [RelayCommand]
    public async Task AddShot()
    {
        var lastTake = Takes.LastOrDefault();
        var episode = string.IsNullOrWhiteSpace(lastTake?.Episode) ? "1" : lastTake!.Episode;
        var scene = string.IsNullOrWhiteSpace(lastTake?.Scene) ? "1" : lastTake!.Scene;

        await CreateTakeWithContinuity(episode, scene);
    }

    [RelayCommand]
    public async Task AddTake()
    {
        var lastTake = Takes.LastOrDefault();

        var newTake = new Take
        {
            DayId = Id,
            SequenceOrder = Takes.Count,
            Episode = lastTake?.Episode ?? string.Empty,
            Scene = lastTake?.Scene ?? string.Empty,
            Shot = lastTake?.Shot ?? 0,
            TakeNumber = (lastTake?.TakeNumber ?? 0) + 1,
            CameraData = lastTake?.CameraData ?? _cameraDataManager.SerializeCameraData(_cameraDataManager.InitializeDefaultCameras()),
            CreatedAt = DateTime.UtcNow
        };

        var takeVM = new TakeViewModel(_databaseService, _cameraDataManager);
        takeVM.LoadFromModel(newTake);
        await takeVM.RefreshCameraDataCommand.ExecuteAsync(null);

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        Takes.Add(takeVM);

        RefreshAllExtraCameraRolls();
        await UpdateTotalTakesCommand.ExecuteAsync(null);
        await UpdateCurrentShotCommand.ExecuteAsync(null);
        BuildHierarchicalGroups();
    }

    /// <summary>
    /// Rebuilds the ExtraCameraRolls collection on every take, based on the day's ActiveCameras list.
    /// </summary>
    private void RefreshAllExtraCameraRolls()
    {
        foreach (var take in Takes)
        {
            take.RefreshExtraCameraRolls(ActiveCameras);
        }
    }

    [RelayCommand]
    public async Task DeleteTake(TakeViewModel take)
    {
        if (take is null || !Takes.Contains(take))
            return;

        Takes.Remove(take);
        await _databaseService.DeleteTakeAsync(take.ToModel());

        await ReorderTakesCommand.ExecuteAsync(null);
        await UpdateTotalTakesCommand.ExecuteAsync(null);
        BuildHierarchicalGroups();
    }

    [RelayCommand]
    public async Task ReorderTakes()
    {
        for (int i = 0; i < Takes.Count; i++)
        {
            if (Takes[i].SequenceOrder != i)
            {
                Takes[i].SequenceOrder = i;
                await Takes[i].SaveTakeCommand.ExecuteAsync(null);
            }
        }

        BuildHierarchicalGroups();
    }

    /// <summary>
    /// Marks the day as finalized (locked from further edits in the desktop grid).
    /// </summary>
    [RelayCommand]
    public async Task FinalizeDay()
    {
        IsFinalized = true;
        await SaveDayCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Reopens a finalized day, allowing edits to resume.
    /// </summary>
    [RelayCommand]
    public async Task UndoFinalizeDay()
    {
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
        RefreshAllExtraCameraRolls();
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
        RefreshAllExtraCameraRolls();
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

    // The collection the mobile Avalonia UI will bind to for the adaptive card layout
    public ObservableCollection<SetupGroupViewModel> MobileSetupGroups { get; } = new();

    /// <summary>
    /// Phase 2 Step 4: Hierarchical Grouping Algorithm
    /// Groups raw Take rows into Episode-Scene chunks for the mobile view.
    /// </summary>
    public void BuildHierarchicalGroups()
    {
        // 1. Identify unique Episode-Scene setups currently in the raw Takes list
        var groupedData = Takes
            .GroupBy(t => new { t.Episode, t.Scene })
            .OrderBy(g => g.Min(t => t.CreatedAt)); // Order chronologically by when the setup started

        var updatedGroups = new System.Collections.Generic.List<SetupGroupViewModel>();

        foreach (var group in groupedData)
        {
            // 2. Check if we already have this group in the UI to preserve its IsCollapsed state
            var existingGroup = MobileSetupGroups.FirstOrDefault(g =>
                g.Episode == group.Key.Episode && g.Scene == group.Key.Scene);

            var setupGroup = existingGroup ?? new SetupGroupViewModel(group.Key.Episode, group.Key.Scene, this);

            // 3. Sync the takes inside this specific group
            setupGroup.GroupedTakes.Clear();
            foreach (var take in group.OrderBy(t => t.SequenceOrder))
            {
                setupGroup.GroupedTakes.Add(take);
            }

            updatedGroups.Add(setupGroup);
        }

        // 4. Apply the final grouped list to the observable collection bound to the mobile UI
        MobileSetupGroups.Clear();
        foreach (var group in updatedGroups)
        {
            MobileSetupGroups.Add(group);
        }
    }

    /// <summary>
    /// Supports the mobile [ + TAKE ] button on the subheader card.
    /// </summary>
    public void CreateSubsequentTake(string episode, string scene, int currentShot, int currentTake)
    {
        // Create a new take that duplicates the active Shot number and increments the Take number by 1
        var newTakeModel = new Logshot.Models.Take
        {
            DayId = this.Id,
            Episode = episode,
            Scene = scene,
            Shot = currentShot,
            TakeNumber = currentTake + 1,
            CreatedAt = System.DateTime.UtcNow,
            SequenceOrder = Takes.Count + 1 // Append to the bottom of the raw list
        };

        var takeViewModel = new TakeViewModel(_databaseService, _cameraDataManager);
        takeViewModel.LoadFromModel(newTakeModel);

        // Add to the main linear collection
        Takes.Add(takeViewModel);

        // NOTE: Add your DatabaseService call here to persist the new take (e.g., takeViewModel.SaveTakeCommand.Execute(null))

        // Re-run the grouping algorithm to update the mobile UI
        BuildHierarchicalGroups();
    }
}
