using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Models;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class ExtraCameraHeaderState : ObservableObject
{
    private readonly DayViewModel _parent;
    public string Label { get; }

    [ObservableProperty]
    private bool _isDisabled;

    partial void OnIsDisabledChanged(bool value)
    {
        _parent.SetCameraDisabled(Label, value);
    }

    public ExtraCameraHeaderState(DayViewModel parent, string label, bool isDisabled)
    {
        _parent = parent;
        Label = label;
        _isDisabled = isDisabled;
    }
}

public partial class DayViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private readonly CameraDataManager _cameraDataManager;
    private readonly ContinuityService _continuityService;
    private bool _isSuppressingSave = false;
    private CancellationTokenSource? _loadTakesCancellation;

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

    partial void OnGeneralNotesChanged(string value)
    {
        if (value != null && value.Contains("-->"))
        {
            GeneralNotes = value.Replace("-->", "→");
            return;
        }

        if (!_isSuppressingSave)
        {
            _ = SaveDayCommand.ExecuteAsync(null);
        }
    }

    public void SetGroupCollapsed(string setupKey, bool isCollapsed)
    {
        foreach (var group in MobileSetupGroups)
        {
            if ($"{group.Episode}|{group.Scene}" == setupKey)
            {
                group.IsCollapsed = isCollapsed;
                break;
            }
        }
    }

    [ObservableProperty]
    private bool _isFinalized = false;

    [ObservableProperty]
    private bool _isGeneralNotesOpen = false;

    [ObservableProperty]
    private bool _isLoadingTakes = false;

    partial void OnIsFinalizedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotFinalized));
        if (!_isSuppressingSave)
        {
            _ = SaveDayCommand.ExecuteAsync(null);
        }
    }

    public bool IsNotFinalized => !IsFinalized;

    partial void OnShootDayNumberChanged(string value)
    {
        if (!_isSuppressingSave)
        {
            _ = SaveDayCommand.ExecuteAsync(null);
        }
    }

    partial void OnCalendarDateChanged(DateTime value)
    {
        if (!_isSuppressingSave)
        {
            _ = SaveDayCommand.ExecuteAsync(null);
        }
    }

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
    /// Extra camera columns are dynamically added to the grid based on how many cameras are active.
    /// </summary>
    public GridLength ExtraCam1Width => ActiveCameras.Count > 2 ? new GridLength(10, GridUnitType.Star) : new GridLength(0);
    public GridLength ExtraCam2Width => ActiveCameras.Count > 3 ? new GridLength(10, GridUnitType.Star) : new GridLength(0);
    public GridLength ExtraCam3Width => ActiveCameras.Count > 4 ? new GridLength(10, GridUnitType.Star) : new GridLength(0);

    public bool IsExtraCam1Visible => ActiveCameras.Count > 2;
    public bool IsExtraCam2Visible => ActiveCameras.Count > 3;
    public bool IsExtraCam3Visible => ActiveCameras.Count > 4;

    public string ExtraCamera1Label => ActiveCameras.ElementAtOrDefault(2) ?? string.Empty;
    public string ExtraCamera2Label => ActiveCameras.ElementAtOrDefault(3) ?? string.Empty;
    public string ExtraCamera3Label => ActiveCameras.ElementAtOrDefault(4) ?? string.Empty;

    public GridLength NotesWidth
    {
        get
        {
            int extraCount = Math.Max(0, ActiveCameras.Count - 2);
            int notesStars = Math.Max(12, 48 - (10 * extraCount));
            return new GridLength(notesStars, GridUnitType.Star);
        }
    }

    /// <summary>
    /// Cameras beyond the two defaults (CAM A / CAM B), used to populate any
    /// dynamically added camera columns in the header (positioned before SOUND ROLL).
    /// </summary>
    public IEnumerable<string> ExtraActiveCameras => ActiveCameras.Where(c => c != "CAM A" && c != "CAM B");

    public ObservableCollection<string> DisabledCameras { get; } = new();
    public ObservableCollection<ExtraCameraHeaderState> ExtraCameraHeaders { get; } = new();

    public ExtraCameraHeaderState? ExtraCamera1 => ExtraCameraHeaders.ElementAtOrDefault(0);
    public ExtraCameraHeaderState? ExtraCamera2 => ExtraCameraHeaders.ElementAtOrDefault(1);
    public ExtraCameraHeaderState? ExtraCamera3 => ExtraCameraHeaders.ElementAtOrDefault(2);

    public void SetCameraDisabled(string label, bool isDisabled)
    {
        if (isDisabled && !DisabledCameras.Contains(label))
            DisabledCameras.Add(label);
        else if (!isDisabled && DisabledCameras.Contains(label))
            DisabledCameras.Remove(label);
    }

    private void UpdateExtraCameraHeaders()
    {
        var extras = ActiveCameras.Where(c => c != "CAM A" && c != "CAM B").ToList();

        for (int i = ExtraCameraHeaders.Count - 1; i >= 0; i--)
        {
            if (!extras.Contains(ExtraCameraHeaders[i].Label))
                ExtraCameraHeaders.RemoveAt(i);
        }

        foreach (var cam in extras)
        {
            if (!ExtraCameraHeaders.Any(h => h.Label == cam))
            {
                bool isDisabled = DisabledCameras.Contains(cam);
                ExtraCameraHeaders.Add(new ExtraCameraHeaderState(this, cam, isDisabled));
            }
        }

        OnPropertyChanged(nameof(ExtraCamera1));
        OnPropertyChanged(nameof(ExtraCamera2));
        OnPropertyChanged(nameof(ExtraCamera3));
    }

    // --- DAY INFO MODAL STATE ---
    [ObservableProperty]
    private bool _isDayInfoOpen = false;

    [ObservableProperty]
    private string _dayInfoScenes = string.Empty;

    [ObservableProperty]
    private int _dayInfoSetupsCount = 0;

    [ObservableProperty]
    private int _dayInfoWildShotsCount = 0;

    [ObservableProperty]
    private bool _dayInfoHasWildShots = false;

    [ObservableProperty]
    private ObservableCollection<CameraClipGroupViewModel> _dayInfoCameraGroups = new();
    // ----------------------------

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
        cameras.CollectionChanged += (_, _) =>
        {
            UpdateExtraCameraHeaders();
            OnPropertyChanged(nameof(ExtraActiveCameras));
            OnPropertyChanged(nameof(ExtraCam1Width));
            OnPropertyChanged(nameof(ExtraCam2Width));
            OnPropertyChanged(nameof(ExtraCam3Width));
            OnPropertyChanged(nameof(IsExtraCam1Visible));
            OnPropertyChanged(nameof(IsExtraCam2Visible));
            OnPropertyChanged(nameof(IsExtraCam3Visible));
            OnPropertyChanged(nameof(ExtraCamera1Label));
            OnPropertyChanged(nameof(ExtraCamera2Label));
            OnPropertyChanged(nameof(ExtraCamera3Label));
            OnPropertyChanged(nameof(NotesWidth));
            OnPropertyChanged(nameof(ExtraActiveCameras));
            UpdateExtraCameraHeaders();
        };
    }

    partial void OnActiveCamerasChanged(ObservableCollection<string>? oldValue, ObservableCollection<string> newValue)
    {
        SubscribeActiveCameras(newValue);
        OnPropertyChanged(nameof(ExtraActiveCameras));
    }

    /// <summary>
    /// Synchronizes inherited or previous camera data with the Day's current ActiveCameras.
    /// This ensures any extra cameras added mid-day are injected into brand new shots and takes.
    /// </summary>
    public string SyncCameraDataWithActiveCameras(string? baseCameraData)
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

            if (kvp.Key != "CAM A" && kvp.Key != "CAM B")
            {
                if (DisabledCameras.Contains(kvp.Key))
                {
                    kvp.Value.NoRoll = true;
                }
                else
                {
                    kvp.Value.NoRoll = false;
                }
            }
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

    partial void OnTakesChanged(ObservableCollection<TakeViewModel>? oldValue, ObservableCollection<TakeViewModel> newValue)
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
        _isSuppressingSave = true;
        try
        {
            Id = day.Id;
            ProjectId = day.ProjectId;
            ShootDayNumber = day.ShootDayNumber;
            CalendarDate = day.CalendarDate;
            GeneralNotes = day.GeneralNotes;
            IsFinalized = day.IsFinalized;
            CreatedAt = day.CreatedAt;
        }
        finally
        {
            _isSuppressingSave = false;
        }
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
        PerformanceDiagnostics.Instance.Start("Day.LoadTakes");
        IsLoadingTakes = true; // Block scrolling while loading
        _loadTakesCancellation?.Cancel();
        _loadTakesCancellation?.Dispose();
        var loadCancellation = new CancellationTokenSource();
        _loadTakesCancellation = loadCancellation;

        try
        {
            // 1. Pure background thread execution with zero UI thread dispatching
            var (takesVmList, discoveredCameras) = await Task.Run(async () =>
            {
                var rawTakes = await _databaseService.GetTakesForDayAsync(Id);
                var list = new List<TakeViewModel>();

                foreach (var take in rawTakes.OrderBy(t => t.SequenceOrder))
                {
                    loadCancellation.Token.ThrowIfCancellationRequested();
                    var takeVM = new TakeViewModel(_databaseService, _cameraDataManager);
                    takeVM.LoadFromModel(take);
                    takeVM.RefreshCameraDataSync();
                    list.Add(takeVM);
                }

                var cameras = list
                    .SelectMany(t => t.ActiveCameras)
                .Distinct()
                .ToList();

                return (list, cameras);
            }, loadCancellation.Token);

            loadCancellation.Token.ThrowIfCancellationRequested();

            foreach (var camera in discoveredCameras)
            {
                if (!ActiveCameras.Contains(camera))
                    ActiveCameras.Add(camera);
            }

            // 2. Single atomic assignment triggers 1 layout pass instead of N passes
            Takes = new ObservableCollection<TakeViewModel>(takesVmList);

            RefreshAllExtraCameraRolls();
            await UpdateTotalTakesCommand.ExecuteAsync(null);
            await UpdateCurrentShotCommand.ExecuteAsync(null);

            UpdateRowVisibilities();
            BuildHierarchicalGroups();

            System.Diagnostics.Debug.WriteLine($"[PERF] Loaded {takesVmList.Count} takes in {PerformanceDiagnostics.Instance.GetAverage("Day.LoadTakes"):F0}ms");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_loadTakesCancellation, loadCancellation))
            {
                _loadTakesCancellation = null;
                IsLoadingTakes = false;
            }

            loadCancellation.Dispose();
            PerformanceDiagnostics.Instance.Stop("Day.LoadTakes");
        }
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
        var lastValidTake = Takes.LastOrDefault(t => !t.IsSoundOnlyRow && !t.HasVoidedCameras)
                            ?? Takes.LastOrDefault(t => !t.IsSoundOnlyRow)
                            ?? Takes.LastOrDefault();
        var episode = string.IsNullOrWhiteSpace(lastValidTake?.Episode) ? "1" : lastValidTake!.Episode;
        var scene = string.IsNullOrWhiteSpace(lastValidTake?.Scene) ? "1" : lastValidTake!.Scene;

        await CreateTakeWithContinuity(episode, scene);
    }

    [RelayCommand]
    public async Task AddTake()
    {
        var lastValidTake = Takes.LastOrDefault(t => !t.IsSoundOnlyRow && !t.HasVoidedCameras)
                            ?? Takes.LastOrDefault(t => !t.IsSoundOnlyRow)
                            ?? Takes.LastOrDefault();

        if (lastValidTake != null && lastValidTake.IsWildShot)
        {
            await CreateWildShotAsync(lastValidTake.Episode);
            return;
        }

        var newTake = new Take
        {
            DayId = Id,
            SequenceOrder = Takes.Count,
            Episode = lastValidTake?.Episode ?? string.Empty,
            Scene = lastValidTake?.Scene ?? string.Empty,
            Shot = lastValidTake?.Shot ?? 0,
            TakeNumber = (lastValidTake?.TakeNumber ?? 0) + 1,
            CameraData = SyncCameraDataWithActiveCameras(lastValidTake?.CameraData),
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
        TotalTakes = Takes.Count(t => !t.IsSoundOnlyRow && !t.IsWildShot);
    }

    [RelayCommand]
    public async Task UpdateCurrentShot()
    {
        var validTakes = Takes.Where(t => !t.IsSoundOnlyRow && !t.IsWildShot).ToList();
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
    /// Creates a new "wild shot" take, which is a special type of take that has no scene and is marked as a wild shot. The new take inherits camera data from the last valid take in the day.
    /// </summary>
    public async Task CreateWildShotAsync(string episode)
    {
        var lastValidTake = Takes.LastOrDefault(t => !t.IsSoundOnlyRow && !t.HasVoidedCameras)
                            ?? Takes.LastOrDefault(t => !t.IsSoundOnlyRow)
                            ?? Takes.LastOrDefault();

        var newTake = new Take
        {
            DayId = Id,
            SequenceOrder = Takes.Count,
            Episode = episode,
            Scene = string.Empty, // Wild shots have no scene
            Shot = 0,
            TakeNumber = 0,
            IsWildShot = true,
            CameraData = SyncCameraDataWithActiveCameras(lastValidTake?.CameraData),
            CreatedAt = DateTime.UtcNow
        };

        var takeVM = new TakeViewModel(_databaseService, _cameraDataManager);
        takeVM.LoadFromModel(newTake);
        await takeVM.RefreshCameraDataCommand.ExecuteAsync(null);

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        Takes.Add(takeVM);

        RefreshAllExtraCameraRolls();
        await UpdateTotalTakesCommand.ExecuteAsync(null);
        BuildHierarchicalGroups();
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

    /// <summary>
    /// Delegate command for flat take list: adds a new shot to the specified setup.
    /// </summary>
    [RelayCommand]
    public async Task AddShotToSetup((string Episode, string Scene) setup)
    {
        await CreateTakeWithContinuity(setup.Episode, setup.Scene);
    }

    /// <summary>
    /// Delegate command for flat take list: adds a new take to the specified setup.
    /// </summary>
    [RelayCommand]
    public async Task AddTakeToSetup((string Episode, string Scene) setup)
    {
        // Find the existing group and delegate to its logic
        var group = MobileSetupGroups.FirstOrDefault(g => g.Episode == setup.Episode && g.Scene == setup.Scene);
        if (group != null)
        {
            group.AddTakeCommand.Execute(null);
        }
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

    [RelayCommand]
    public void OpenDayInfo()
    {
        CalculateDayInfo();
        IsDayInfoOpen = true;
    }

    [RelayCommand]
    public void CloseDayInfo()
    {
        IsDayInfoOpen = false;
    }

    public void CalculateDayInfo()
    {
        if (Takes == null || !Takes.Any())
        {
            DayInfoScenes = string.Empty;
            DayInfoSetupsCount = 0;
            DayInfoWildShotsCount = 0;
            DayInfoHasWildShots = false;
            DayInfoCameraGroups.Clear();
            return;
        }

        // 1. Scenes logged in the day
        var sceneTokens = new List<string>();
        foreach (var take in Takes.Where(t => !t.IsSoundOnlyRow && !t.IsWildShot && !t.HasVoidedCameras))
        {
            string ep = FormatLineBreaks(take.Episode);
            string sc = FormatLineBreaks(take.Scene);
            if (!string.IsNullOrEmpty(ep) && !string.IsNullOrEmpty(sc))
            {
                sceneTokens.Add($"{ep}/{sc}");
            }
            else if (!string.IsNullOrEmpty(sc))
            {
                sceneTokens.Add(sc);
            }
        }
        DayInfoScenes = string.Join(", ", sceneTokens.Distinct());

        // 2. Setups: total number of UNIQUE SHOTS recorded in the day 
        // (excluding AKYRO CLIP, Wild Shot rows, and Sound-only rows)
        var validShots = Takes.Where(t => !t.IsSoundOnlyRow && !t.IsWildShot && !t.HasVoidedCameras).ToList();
        DayInfoSetupsCount = validShots
            .Select(t => new { Episode = t.Episode?.Trim(), Scene = t.Scene?.Trim(), t.Shot })
            .Distinct()
            .Count();

        // 3. Wild Shots count
        int wildShotsCellCount = 0;
        var wildTakes = Takes.Where(t => t.IsWildShot).ToList();
        foreach (var wildTake in wildTakes)
        {
            var camData = _cameraDataManager.ParseCameraData(wildTake.CameraData);
            var activeLabels = _cameraDataManager.GetActiveCameraLabels(camData);
            foreach (string label in activeLabels)
            {
                bool isNoRoll = _cameraDataManager.IsCameraStrikethrough(camData, label) ||
                                (camData.Cameras.TryGetValue(label, out var st) && st.NoRoll);
                if (!isNoRoll)
                {
                    wildShotsCellCount++;
                }
            }
        }
        DayInfoWildShotsCount = wildShotsCellCount;
        DayInfoHasWildShots = wildShotsCellCount > 0;

        // 4. Cameras clip count grouped by Camera (first-level) and Roll (second-level) in chronological order
        var cameraRollClips = new Dictionary<string, List<CameraRollClipCountViewModel>>(StringComparer.OrdinalIgnoreCase);
        var currentActiveRolls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var allCameraLabels = Takes
            .SelectMany(t => _cameraDataManager.GetActiveCameraLabels(_cameraDataManager.ParseCameraData(t.CameraData)))
            .Distinct()
            .ToList();

        foreach (var label in allCameraLabels)
        {
            currentActiveRolls[label] = "DEFAULT";
            cameraRollClips[label] = new List<CameraRollClipCountViewModel>();
        }

        foreach (var take in Takes.Where(t => !t.IsSoundOnlyRow).OrderBy(t => t.SequenceOrder))
        {
            var camData = _cameraDataManager.ParseCameraData(take.CameraData);
            bool isRowVoided = take.HasVoidedCameras;

            foreach (var label in allCameraLabels)
            {
                bool exists = camData.Cameras.TryGetValue(label, out var state);
                bool isNoRoll = exists && (state.NoRoll || _cameraDataManager.IsCameraStrikethrough(camData, label));
                bool isCameraVoided = take.IsCameraVoided(label);

                if (exists && state.RollChangeMarker && !string.IsNullOrWhiteSpace(state.RollNumber))
                {
                    currentActiveRolls[label] = state.RollNumber.Trim();
                }

                if (isNoRoll) continue;
                if (isRowVoided && !isCameraVoided) continue;

                string activeRoll = currentActiveRolls.ContainsKey(label) ? currentActiveRolls[label] : "DEFAULT";

                var rollRecord = cameraRollClips[label].FirstOrDefault(r => string.Equals(r.RollName, activeRoll, StringComparison.OrdinalIgnoreCase));

                if (rollRecord == null)
                {
                    rollRecord = new CameraRollClipCountViewModel
                    {
                        RollName = activeRoll,
                        ClipCount = 0
                    };
                    cameraRollClips[label].Add(rollRecord);
                }

                rollRecord.ClipCount++;
            }
        }

        DayInfoCameraGroups.Clear();
        foreach (var camKv in cameraRollClips.OrderBy(k => k.Key))
        {
            string camLabel = camKv.Key;
            var rollList = camKv.Value;
            int totalCamClips = rollList.Sum(r => r.ClipCount);

            var groupVm = new CameraClipGroupViewModel
            {
                CameraLabel = camLabel,
                TotalClips = totalCamClips
            };

            foreach (var rollRecord in rollList)
            {
                groupVm.Rolls.Add(rollRecord);
            }

            DayInfoCameraGroups.Add(groupVm);
        }
    }

    private string FormatLineBreaks(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var parts = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(p => p.Trim());
        return string.Join("-", parts);
    }

    public ObservableCollection<SetupGroupViewModel> MobileSetupGroups { get; } = new();

    /// <summary>
    /// Flattened list of group headers and takes for virtualization support.
    /// Alternates between TakeListGroupHeaderViewModel and TakeListTakeViewModel items.
    /// </summary>
    public ObservableCollection<TakeListItemViewModel> FlatTakeList { get; } = new();

    /// <summary>
    /// Rebuilds the MobileSetupGroups collection sequentially based on logging order during the day.
    /// Consecutive takes for the same setup are grouped together. Returning to a scene shot earlier 
    /// creates a new group marked as 'IsContinued = true'.
    /// </summary>
    public void BuildHierarchicalGroups()
    {
        if (Takes == null || !Takes.Any())
        {
            MobileSetupGroups.Clear();
            FlatTakeList.Clear();
            return;
        }

        // Store existing collapse state by Episode, Scene, and Continued status
        var collapsedStates = MobileSetupGroups
            .GroupBy(g => (g.Episode, g.Scene, g.IsContinued))
            .ToDictionary(g => g.Key, g => g.First().IsCollapsed);

        var updatedGroups = new List<SetupGroupViewModel>();
        var seenSetups = new HashSet<(string Episode, string Scene)>();

        SetupGroupViewModel? currentGroup = null;

        // Iterate through takes sequentially in logging order
        foreach (var take in Takes.OrderBy(t => t.SequenceOrder))
        {
            string ep = take.Episode ?? string.Empty;
            string sc = take.Scene ?? string.Empty;

            // Start a new setup group card when changing episode/scene or starting
            if (currentGroup == null || currentGroup.Episode != ep || currentGroup.Scene != sc)
            {
                bool isContinued = seenSetups.Contains((ep, sc));
                seenSetups.Add((ep, sc));

                currentGroup = new SetupGroupViewModel(ep, sc, this)
                {
                    IsContinued = isContinued
                };

                if (collapsedStates.TryGetValue((ep, sc, isContinued), out bool wasCollapsed))
                {
                    currentGroup.IsCollapsed = wasCollapsed;
                }

                updatedGroups.Add(currentGroup);
            }

            currentGroup.GroupedTakes.Add(take);
        }

        MobileSetupGroups.Clear();
        foreach (var group in updatedGroups)
        {
            MobileSetupGroups.Add(group);
        }

        // Build flattened list for virtualization
        RebuildFlatTakeList();
    }

    /// <summary>
    /// Rebuilds FlatTakeList from MobileSetupGroups, flattening headers and takes.
    /// Called after BuildHierarchicalGroups or when collapse state changes.
    /// </summary>
    public void RebuildFlatTakeList()
    {
        FlatTakeList.Clear();

        foreach (var group in MobileSetupGroups)
        {
            var headerVm = new TakeListGroupHeaderViewModel(this, group)
            {
                IsCollapsed = group.IsCollapsed
            };
            FlatTakeList.Add(headerVm);

            if (!group.IsCollapsed)
            {
                foreach (var take in group.GroupedTakes)
                {
                    FlatTakeList.Add(new TakeListTakeViewModel(take));
                }
            }
        }
    }

    public void CreateSubsequentTake(string episode, string scene, int currentShot, int currentTake)
    {
        var previousTake = Takes.LastOrDefault(t => !t.IsSoundOnlyRow && !t.HasVoidedCameras && t.Episode == episode && t.Scene == scene && t.Shot == currentShot)
                           ?? Takes.LastOrDefault(t => !t.IsSoundOnlyRow && t.Episode == episode && t.Scene == scene)
                           ?? Takes.LastOrDefault(t => t.Episode == episode && t.Scene == scene)
                           ?? Takes.LastOrDefault();

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
                currentTake.ShowSoundNotes = true;

                foreach (var cell in currentTake.ExtraCameraRolls)
                {
                    cell.ShowRoll = true;
                }
            }
            else
            {
                // Consecutive sound-only and wild-shot rows belong to the same group.
                currentTake.IsGroupStart = !(currentTake.IsSoundOnlyRow && previousTake.IsSoundOnlyRow) &&
                                           !(currentTake.IsWildShot && previousTake.IsWildShot) &&
                                           ((currentTake.Episode != previousTake.Episode) ||
                                            (currentTake.Scene != previousTake.Scene) ||
                                             (currentTake.IsSoundOnlyRow != previousTake.IsSoundOnlyRow) ||
                                            (currentTake.IsWildShot != previousTake.IsWildShot));

                // Show Episode whenever there's a group start (whether a new episode or a new scene in the same episode)
                currentTake.ShowEpisode = currentTake.IsGroupStart;

                // Show Scene if it's a group start or the scene itself changed
                currentTake.ShowScene = currentTake.IsGroupStart || (currentTake.Scene != previousTake.Scene);
                currentTake.ShowShot = currentTake.ShowScene || (currentTake.Shot != previousTake.Shot);

                // Camera descriptions show full text if empty, at a new group start, or if different from previous take
                currentTake.ShowCamARoll = string.IsNullOrEmpty(currentTake.CamARoll) || currentTake.IsGroupStart || currentTake.CamARoll != previousTake.CamARoll;
                currentTake.ShowCamBRoll = string.IsNullOrEmpty(currentTake.CamBRoll) || currentTake.IsGroupStart || currentTake.CamBRoll != previousTake.CamBRoll;

                // Sound notes show full text if empty or different from previous take
                currentTake.ShowSoundNotes = string.IsNullOrEmpty(currentTake.SoundNotes) || currentTake.SoundNotes != previousTake.SoundNotes;

                foreach (var cell in currentTake.ExtraCameraRolls)
                {
                    var prevCell = previousTake.ExtraCameraRolls.FirstOrDefault(c => c.Label == cell.Label);
                    if (prevCell != null)
                    {
                        cell.ShowRoll = string.IsNullOrEmpty(cell.Roll) || currentTake.IsGroupStart || cell.Roll != prevCell.Roll;
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

    /// <summary>
    /// Smoothly merges remote take changes from SQLite into memory without 
    /// replacing ViewModel instances, keeping focus and screen state intact.
    /// </summary>
    public async Task MergeTakesFromCloudAsync()
    {
        var pendingItems = await _databaseService.GetPendingSyncItemsAsync();
        var pendingTakeIds = pendingItems.Where(i => i.EntityType == "Take").Select(i => i.EntityId).ToHashSet();

        var dbTakes = await _databaseService.GetTakesForDayAsync(Id);
        bool structureChanged = false;

        foreach (var tModel in dbTakes)
        {
            var existingTake = Takes.FirstOrDefault(t => t.Id == tModel.Id);
            if (existingTake == null)
            {
                var newTakeVm = new TakeViewModel(_databaseService, _cameraDataManager);
                newTakeVm.LoadFromModel(tModel);
                newTakeVm.RefreshCameraDataSync();
                Takes.Add(newTakeVm);
                structureChanged = true;
            }
            else if (!pendingTakeIds.Contains(tModel.Id))
            {
                existingTake.LoadFromModel(tModel);
                existingTake.RefreshCameraDataSync();
            }
        }

        var dbTakeIds = dbTakes.Select(t => t.Id).ToHashSet();
        for (int i = Takes.Count - 1; i >= 0; i--)
        {
            if (!dbTakeIds.Contains(Takes[i].Id) && !pendingTakeIds.Contains(Takes[i].Id))
            {
                Takes.RemoveAt(i);
                structureChanged = true;
            }
        }

        if (structureChanged)
        {
            RefreshAllExtraCameraRolls();
            await UpdateTotalTakesCommand.ExecuteAsync(null);
            await UpdateCurrentShotCommand.ExecuteAsync(null);
            UpdateRowVisibilities();
            BuildHierarchicalGroups();
        }
    }
}

public partial class CameraClipGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private string _cameraLabel = string.Empty;

    [ObservableProperty]
    private int _totalClips = 0;

    public ObservableCollection<CameraRollClipCountViewModel> Rolls { get; } = new();
}

public partial class CameraRollClipCountViewModel : ObservableObject
{
    [ObservableProperty]
    private string _rollName = string.Empty;

    [ObservableProperty]
    private int _clipCount = 0;
}