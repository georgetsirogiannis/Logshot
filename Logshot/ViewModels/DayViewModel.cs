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

    // --- CONTINUITY PROMPT STATE ---
    [ObservableProperty]
    private bool _isContinuityPopupOpen = false;

    [ObservableProperty]
    private string _continuityMessage = string.Empty;

    [ObservableProperty]
    private string _continuityOption1Text = string.Empty;

    [ObservableProperty]
    private string _continuityOption2Text = string.Empty;

    private ContinuityService.ContinuityData? _pendingContinuityData;
    private string _pendingEpisode = string.Empty;
    private string _pendingScene = string.Empty;
    // -------------------------------

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
    /// Synchronizes inherited or previous camera data with the Day's current ActiveCameras.
    /// This ensures any extra cameras added mid-day are injected into brand new shots and takes.
    /// </summary>
    private string SyncCameraDataWithActiveCameras(string baseCameraData)
    {
        // Fallback to default cameras if the base data is empty
        if (string.IsNullOrWhiteSpace(baseCameraData) || baseCameraData == "{}")
        {
            baseCameraData = _cameraDataManager.SerializeCameraData(_cameraDataManager.InitializeDefaultCameras());
        }

        // Parse the data and get a list of the cameras it currently holds
        var cameraData = _cameraDataManager.ParseCameraData(baseCameraData);
        var existingCameras = _cameraDataManager.GetActiveCameraLabels(cameraData).ToList();

        // Loop through the day's master list. If the new camera isn't in the data, add it.
        foreach (var activeCam in ActiveCameras)
        {
            if (!existingCameras.Contains(activeCam))
            {
                cameraData = _cameraDataManager.AddCamera(cameraData, activeCam);
            }
        }

        // Ensure newly created subsequent takes do not inherit a roll change marker
        foreach (var kvp in cameraData.Cameras)
        {
            kvp.Value.RollChangeMarker = false;
        }

        return _cameraDataManager.SerializeCameraData(cameraData);
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

        // Recalculate visibility when rows are added or removed
        UpdateRowVisibilities();
        _ = UpdateTotalTakesCommand.ExecuteAsync(null);
        _ = UpdateCurrentShotCommand.ExecuteAsync(null);
    }

    private void Take_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TakeViewModel.Episode) || e.PropertyName == nameof(TakeViewModel.Scene))
        {
            BuildHierarchicalGroups();
        }

        // Recalculate visibility if one of the span-enabled text fields (OR the CameraData JSON) was edited
        if (e.PropertyName == nameof(TakeViewModel.Episode) ||
            e.PropertyName == nameof(TakeViewModel.Scene) ||
            e.PropertyName == nameof(TakeViewModel.Shot) ||
            e.PropertyName == nameof(TakeViewModel.CamARoll) ||
            e.PropertyName == nameof(TakeViewModel.CamBRoll) ||
            e.PropertyName == nameof(TakeViewModel.CameraData) ||
            e.PropertyName == nameof(TakeViewModel.SoundNotes) ||
            e.PropertyName == nameof(TakeViewModel.IsSoundOnlyRow))
        {
            UpdateRowVisibilities();
            _ = UpdateTotalTakesCommand.ExecuteAsync(null);
            _ = UpdateCurrentShotCommand.ExecuteAsync(null);
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

        UpdateRowVisibilities();
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
        var lastTake = Takes.LastOrDefault(t => !t.IsSoundOnlyRow) ?? Takes.LastOrDefault();
        var episode = string.IsNullOrWhiteSpace(lastTake?.Episode) ? "1" : lastTake!.Episode;
        var scene = string.IsNullOrWhiteSpace(lastTake?.Scene) ? "1" : lastTake!.Scene;

        await CreateTakeWithContinuity(episode, scene);
    }

    [RelayCommand]
    public async Task AddTake()
    {
        var lastTake = Takes.LastOrDefault(t => !t.IsSoundOnlyRow) ?? Takes.LastOrDefault();

        var newTake = new Take
        {
            DayId = Id,
            SequenceOrder = Takes.Count,
            Episode = lastTake?.Episode ?? string.Empty,
            Scene = lastTake?.Scene ?? string.Empty,
            Shot = lastTake?.Shot ?? 0,
            TakeNumber = (lastTake?.TakeNumber ?? 0) + 1,
            CameraData = SyncCameraDataWithActiveCameras(lastTake?.CameraData),
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
    /// Mobile [ + SCENE ] button handler: initializes a completely new Episode-Scene setup.
    /// </summary>
    [RelayCommand]
    public async Task AddNewScene(object parameter)
    {
        string episode = string.Empty;
        string scene = string.Empty;

        if (parameter is (string ep, string sc))
        {
            episode = ep;
            scene = sc;
        }

        if (string.IsNullOrWhiteSpace(episode) || string.IsNullOrWhiteSpace(scene))
            return;

        await CheckContinuityAndPromptAsync(episode, scene);
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

    // --- Take Deletion Safety Modal State ---
    [ObservableProperty]
    private bool _isTakeDeleteConfirmationOpen = false;

    [ObservableProperty]
    private TakeViewModel? _takeToDelete;

    [RelayCommand]
    public void PromptDeleteTake(TakeViewModel? take)
    {
        if (take is null) return;
        TakeToDelete = take;
        IsTakeDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    public async Task ConfirmDeleteTake()
    {
        if (TakeToDelete != null)
        {
            await DeleteTake(TakeToDelete);
        }
        IsTakeDeleteConfirmationOpen = false;
        TakeToDelete = null;
    }

    [RelayCommand]
    public void CancelDeleteTake()
    {
        IsTakeDeleteConfirmationOpen = false;
        TakeToDelete = null;
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
        TotalTakes = Takes.Count(t => !t.IsSoundOnlyRow);
    }

    [RelayCommand]
    public async Task UpdateCurrentShot()
    {
        var validTakes = Takes.Where(t => !t.IsSoundOnlyRow).ToList();
        if (validTakes.Count > 0)
        {
            CurrentShot = validTakes.Last().Shot;
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

        if (!ActiveCameras.Contains(cameraLabel))
        {
            ActiveCameras.Add(cameraLabel);
        }

        var cameraCreatedAt = DateTime.UtcNow;
        foreach (var take in Takes)
        {
            var cameraData = _cameraDataManager.ParseCameraData(take.CameraData);
            cameraData = _cameraDataManager.AddCamera(cameraData, cameraLabel);
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

        if (CameraDataManager.DEFAULT_CAMERAS.Contains(cameraLabel))
            return;

        var cameraToRemove = ActiveCameras.FirstOrDefault(c => c == cameraLabel);
        if (cameraToRemove != null)
        {
            ActiveCameras.Remove(cameraToRemove);
        }

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

    [RelayCommand]
    public async Task InitializeCameras()
    {
        var defaultCameras = new ObservableCollection<string>(CameraDataManager.DEFAULT_CAMERAS);
        ActiveCameras = defaultCameras;

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

    [RelayCommand]
    public async Task RefreshActiveCameras()
    {
        if (Takes.Count == 0)
        {
            ActiveCameras = new ObservableCollection<string>(CameraDataManager.DEFAULT_CAMERAS);
            return;
        }

        var firstTake = Takes[0];
        var cameraData = _cameraDataManager.ParseCameraData(firstTake.CameraData);
        var cameras = _cameraDataManager.GetActiveCameraLabels(cameraData);

        ActiveCameras = new ObservableCollection<string>(cameras);
    }

    public async Task ApplyContinuity(string episode, string scene)
    {
        if (string.IsNullOrWhiteSpace(episode) || string.IsNullOrWhiteSpace(scene) || string.IsNullOrWhiteSpace(ProjectId))
            return;

        var continuityData = await _continuityService.GetContinuityDataAsync(ProjectId, episode, scene);
        CurrentShot = continuityData.NextShotNumber - 1;
    }

    public async Task CreateTakeWithContinuity(string episode, string scene)
    {
        if (string.IsNullOrWhiteSpace(episode) || string.IsNullOrWhiteSpace(scene))
            return;

        var continuityData = await _continuityService.GetContinuityDataAsync(ProjectId, episode, scene);

        var cameraData = _cameraDataManager.ParseCameraData(continuityData.InheritedCameraData);
        foreach (var kvp in cameraData.Cameras)
        {
            kvp.Value.Notes = string.Empty;
            kvp.Value.NoRoll = false;
            kvp.Value.RollChangeMarker = false;
        }
        var cleanedCameraDataJson = _cameraDataManager.SerializeCameraData(cameraData);

        var newTake = new Take
        {
            DayId = Id,
            SequenceOrder = Takes.Count,
            Episode = episode,
            Scene = scene,
            Shot = continuityData.NextShotNumber,
            TakeNumber = 1,
            CameraData = SyncCameraDataWithActiveCameras(cleanedCameraDataJson),
            CreatedAt = DateTime.UtcNow
        };

        var takeVM = new TakeViewModel(_databaseService, _cameraDataManager);
        takeVM.LoadFromModel(newTake);
        takeVM.IsFromContinuity = true;
        takeVM.ContinuityContext = $"Inherited from Shot {continuityData.LastReferenceTake?.Shot ?? 0}";

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        Takes.Add(takeVM);

        RefreshAllExtraCameraRolls();
        await UpdateTotalTakesCommand.ExecuteAsync(null);
        BuildHierarchicalGroups();

        CurrentShot = continuityData.NextShotNumber;
    }

    public async Task<ContinuityService.ContinuityData> GetContinuityInfoAsync(string episode, string scene)
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            return new ContinuityService.ContinuityData();

        return await _continuityService.GetContinuityDataAsync(ProjectId, episode, scene);
    }

    // === START NEW CONTINUITY PROMPT LOGIC ===
    private string FormatSceneString(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return string.Empty;
        char[] separators = new[] { '\r', '\n', ' ', ',' };
        var parts = rawValue.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
        return string.Join("-", parts);
    }

    public async Task CheckContinuityAndPromptAsync(string episode, string scene)
    {
        if (string.IsNullOrWhiteSpace(episode) || string.IsNullOrWhiteSpace(scene)) return;

        var continuityData = await _continuityService.GetContinuityDataAsync(ProjectId, episode, scene);

        if (!continuityData.HasHistory || continuityData.LastReferenceTake == null)
        {
            // No history found, just create normally (Shot 1, Take 1)
            await CreateTakeWithContinuity(episode, scene);
            return;
        }

        // History found! Let's find all the unique days this scene was shot on.
        var historicalTakes = await _databaseService.GetTakesForEpisodeSceneAsync(ProjectId, episode, scene);
        var distinctDayIds = historicalTakes.Select(t => t.DayId).Distinct().ToList();

        var dayNumbers = new List<string>();
        foreach (var dId in distinctDayIds)
        {
            var day = await _databaseService.GetDayAsync(dId);
            if (day != null && !string.IsNullOrWhiteSpace(day.ShootDayNumber))
            {
                dayNumbers.Add(day.ShootDayNumber);
            }
        }

        // historicalTakes is ordered descending by date, so reverse the day numbers to show them chronologically
        dayNumbers.Reverse();

        string daysString = "Unknown Day";
        if (dayNumbers.Count == 1)
        {
            daysString = $"Day {dayNumbers[0]}";
        }
        else if (dayNumbers.Count == 2)
        {
            daysString = $"Days {dayNumbers[0]} and {dayNumbers[1]}";
        }
        else if (dayNumbers.Count > 2)
        {
            daysString = $"Days {string.Join(", ", dayNumbers.Take(dayNumbers.Count - 1))}, and {dayNumbers.Last()}";
        }

        _pendingEpisode = episode;
        _pendingScene = scene;
        _pendingContinuityData = continuityData;

        // Format episode and scene with consistent dashes
        string formattedEp = FormatSceneString(episode);
        string formattedSc = FormatSceneString(scene);

        // Build the prompt text with the dynamic days string and dash-formatted scenes
        ContinuityMessage = $"Scene {formattedEp}/{formattedSc} already has recorded shots on {daysString}. How would you like to continue?";

        int nextShot = continuityData.NextShotNumber;
        ContinuityOption1Text = $"[ ADD A NEW SHOT ]";

        int lastShot = continuityData.LastReferenceTake.Shot;
        int lastTake = continuityData.LastReferenceTake.TakeNumber;
        ContinuityOption2Text = $"[ Continue with SHOT {lastShot} TAKE {lastTake + 1} ]";

        IsContinuityPopupOpen = true;
    }

    [RelayCommand]
    public async Task ConfirmContinuityNewShot()
    {
        IsContinuityPopupOpen = false;
        // Option 1: Add a new shot. This behaves identically to the standard auto-continuity
        await CreateTakeWithContinuity(_pendingEpisode, _pendingScene);
    }

    [RelayCommand]
    public async Task ConfirmContinuitySameShot()
    {
        IsContinuityPopupOpen = false;
        if (_pendingContinuityData == null || _pendingContinuityData.LastReferenceTake == null) return;

        int shot = _pendingContinuityData.LastReferenceTake.Shot;
        int nextTake = _pendingContinuityData.LastReferenceTake.TakeNumber + 1;

        // Inherit and clean the camera data
        var cameraData = _cameraDataManager.ParseCameraData(_pendingContinuityData.InheritedCameraData);
        foreach (var kvp in cameraData.Cameras)
        {
            kvp.Value.Notes = string.Empty;
            kvp.Value.NoRoll = false;
            kvp.Value.RollChangeMarker = false;
        }
        var cleanedCameraDataJson = _cameraDataManager.SerializeCameraData(cameraData);

        var newTake = new Take
        {
            DayId = Id,
            SequenceOrder = Takes.Count,
            Episode = _pendingEpisode,
            Scene = _pendingScene,
            Shot = shot,
            TakeNumber = nextTake,
            CameraData = SyncCameraDataWithActiveCameras(cleanedCameraDataJson),
            CreatedAt = DateTime.UtcNow
        };

        var takeVM = new TakeViewModel(_databaseService, _cameraDataManager);
        takeVM.LoadFromModel(newTake);
        takeVM.IsFromContinuity = true;
        takeVM.ContinuityContext = $"Continued from Shot {shot} Take {nextTake - 1}";

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        Takes.Add(takeVM);

        RefreshAllExtraCameraRolls();
        await UpdateTotalTakesCommand.ExecuteAsync(null);
        BuildHierarchicalGroups();

        CurrentShot = shot;
    }

    [RelayCommand]
    public void CancelContinuityPrompt()
    {
        IsContinuityPopupOpen = false;
    }
    // === END NEW CONTINUITY PROMPT LOGIC ===

    public ObservableCollection<SetupGroupViewModel> MobileSetupGroups { get; } = new();

    public void BuildHierarchicalGroups()
    {
        var groupedData = Takes
            .GroupBy(t => new { t.Episode, t.Scene })
            .OrderBy(g => g.Min(t => t.CreatedAt));

        var updatedGroups = new System.Collections.Generic.List<SetupGroupViewModel>();

        foreach (var group in groupedData)
        {
            var existingGroup = MobileSetupGroups.FirstOrDefault(g =>
                g.Episode == group.Key.Episode && g.Scene == group.Key.Scene);

            var setupGroup = existingGroup ?? new SetupGroupViewModel(group.Key.Episode, group.Key.Scene, this);

            setupGroup.GroupedTakes.Clear();
            foreach (var take in group.OrderBy(t => t.SequenceOrder))
            {
                setupGroup.GroupedTakes.Add(take);
            }

            updatedGroups.Add(setupGroup);
        }

        MobileSetupGroups.Clear();
        foreach (var group in updatedGroups)
        {
            MobileSetupGroups.Add(group);
        }
    }

    public void CreateSubsequentTake(string episode, string scene, int currentShot, int currentTake)
    {
        var previousTake = Takes.LastOrDefault(t => t.Episode == episode && t.Scene == scene && t.Shot == currentShot && t.TakeNumber == currentTake && !t.IsSoundOnlyRow)
                           ?? Takes.LastOrDefault(t => t.Episode == episode && t.Scene == scene && !t.IsSoundOnlyRow);

        var newTakeModel = new Logshot.Models.Take
        {
            DayId = this.Id,
            Episode = episode,
            Scene = scene,
            Shot = currentShot,
            TakeNumber = currentTake + 1,
            CameraData = SyncCameraDataWithActiveCameras(previousTake?.CameraData),
            CreatedAt = System.DateTime.UtcNow,
            SequenceOrder = Takes.Count + 1
        };

        var takeViewModel = new TakeViewModel(_databaseService, _cameraDataManager);
        takeViewModel.LoadFromModel(newTakeModel);

        Takes.Add(takeViewModel);
        BuildHierarchicalGroups();
    }

    public void UpdateRowVisibilities()
    {
        if (Takes == null || Takes.Count == 0) return;

        TakeViewModel? previousTake = null;

        foreach (var currentTake in Takes.OrderBy(t => t.SequenceOrder))
        {
            if (previousTake == null)
            {
                currentTake.IsGroupStart = false;
                currentTake.ShowEpisode = true;
                currentTake.ShowScene = true;
                currentTake.ShowShot = true;
                currentTake.ShowCamARoll = true;
                currentTake.ShowCamBRoll = true;

                foreach (var cell in currentTake.ExtraCameraRolls)
                {
                    cell.ShowRoll = true;
                }
            }
            else
            {
                // A new group starts if either the episode or the scene changes
                currentTake.IsGroupStart = (currentTake.Episode != previousTake.Episode) || (currentTake.Scene != previousTake.Scene);

                // Show Episode whenever there's a group start (whether a new episode or a new scene in the same episode)
                currentTake.ShowEpisode = currentTake.IsGroupStart;

                // Show Scene if it's a group start or the scene itself changed
                currentTake.ShowScene = currentTake.IsGroupStart || (currentTake.Scene != previousTake.Scene);
                currentTake.ShowShot = currentTake.ShowScene || (currentTake.Shot != previousTake.Shot);
                currentTake.ShowCamARoll = currentTake.CamARoll != previousTake.CamARoll;
                currentTake.ShowCamBRoll = currentTake.CamBRoll != previousTake.CamBRoll;

                foreach (var cell in currentTake.ExtraCameraRolls)
                {
                    var prevCell = previousTake.ExtraCameraRolls.FirstOrDefault(c => c.Label == cell.Label);
                    if (prevCell != null)
                    {
                        cell.ShowRoll = cell.Roll != prevCell.Roll;
                    }
                    else
                    {
                        cell.ShowRoll = true;
                    }
                }
            }

            previousTake = currentTake;
        }
    }
}