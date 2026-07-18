using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Models;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class TakeViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private readonly CameraDataManager _cameraDataManager;

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _dayId = string.Empty;

    [ObservableProperty]
    private int _sequenceOrder;

    // Core Hierarchy
    [ObservableProperty]
    private string _episode = string.Empty;

    [ObservableProperty]
    private string _scene = string.Empty;

    [ObservableProperty]
    private int _shot;

    [ObservableProperty]
    private int _takeNumber;

    // Camera & Sound
    [ObservableProperty]
    private string _cameraData = "{}";

    [ObservableProperty]
    private string _soundNotes = string.Empty;

    // Gestures & Modifiers
    [ObservableProperty]
    private string _takeNotes = string.Empty;

    [ObservableProperty]
    private int _falseStartCount = 0;

    [ObservableProperty]
    private bool _isLongStart = false;

    [ObservableProperty]
    private bool _isCircled = false;

    [ObservableProperty]
    private bool _isFailed = false;

    [ObservableProperty]
    private bool _isPickup = false;

    [ObservableProperty]
    private bool _isBlooper = false;

    // False Clip Tracking
    [ObservableProperty]
    private string _voidCameraLabels = "[]";

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    /// <summary>
    /// List of camera labels with their respective data for this take
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _activeCameras = new();

    /// <summary>
    /// Tracks which cameras are strikethrough (not active at time of take)
    /// </summary>
    [ObservableProperty]
    private Dictionary<string, bool> _strikethroughCameras = new();

    /// <summary>
    /// Indicates if this take was pre-filled from continuity data
    /// Useful for UI to show a "continuity" badge
    /// </summary>
    [ObservableProperty]
    private bool _isFromContinuity = false;

    /// <summary>
    /// Stores continuity context (previous take info, shot history, etc.)
    /// Helpful for reverting or undoing continuity pre-fills
    /// </summary>
    [ObservableProperty]
    private string _continuityContext = string.Empty;

    public TakeViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _cameraDataManager = new CameraDataManager();
    }

    public TakeViewModel(DatabaseService databaseService, CameraDataManager cameraDataManager)
    {
        _databaseService = databaseService;
        _cameraDataManager = cameraDataManager;
    }

    /// <summary>
    /// Load take data from the model
    /// </summary>
    public void LoadFromModel(Take take)
    {
        Id = take.Id;
        DayId = take.DayId;
        SequenceOrder = take.SequenceOrder;
        Episode = take.Episode;
        Scene = take.Scene;
        Shot = take.Shot;
        TakeNumber = take.TakeNumber;
        CameraData = take.CameraData;
        SoundNotes = take.SoundNotes;
        TakeNotes = take.TakeNotes;
        FalseStartCount = take.FalseStartCount;
        IsLongStart = take.IsLongStart;
        IsCircled = take.IsCircled;
        IsFailed = take.IsFailed;
        IsPickup = take.IsPickup;
        IsBlooper = take.IsBlooper;
        VoidCameraLabels = take.VoidCameraLabels;
        CreatedAt = take.CreatedAt;
    }

    /// <summary>
    /// Convert this ViewModel back to a model for database persistence
    /// </summary>
    public Take ToModel()
    {
        return new Take
        {
            Id = Id,
            DayId = DayId,
            SequenceOrder = SequenceOrder,
            Episode = Episode,
            Scene = Scene,
            Shot = Shot,
            TakeNumber = TakeNumber,
            CameraData = CameraData,
            SoundNotes = SoundNotes,
            TakeNotes = TakeNotes,
            FalseStartCount = FalseStartCount,
            IsLongStart = IsLongStart,
            IsCircled = IsCircled,
            IsFailed = IsFailed,
            IsPickup = IsPickup,
            IsBlooper = IsBlooper,
            VoidCameraLabels = VoidCameraLabels,
            CreatedAt = CreatedAt
        };
    }

    [RelayCommand]
    public async Task SaveTake()
    {
        await _databaseService.SaveTakeAsync(ToModel());
    }

    [RelayCommand]
    public async Task MarkCircled()
    {
        // Stub for Phase 5 implementation - tap gesture logic
        IsCircled = !IsCircled;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task MarkFailed()
    {
        // Stub for Phase 5 implementation - double-tap gesture logic
        IsFailed = !IsFailed;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task IncrementFalseStarts()
    {
        // Stub for Phase 2 implementation - increments FS count
        FalseStartCount++;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task ToggleLongStart()
    {
        // Stub for Phase 2 implementation
        IsLongStart = !IsLongStart;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task ToggleBlooper()
    {
        // Stub for Phase 2 implementation
        IsBlooper = !IsBlooper;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task TogglePickup()
    {
        // Stub for Phase 2 implementation
        IsPickup = !IsPickup;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Refresh camera information from CameraData JSON
    /// Used when loading a take or when cameras are added/removed
    /// </summary>
    [RelayCommand]
    public async Task RefreshCameraData()
    {
        var cameraData = _cameraDataManager.ParseCameraData(CameraData);
        var cameras = _cameraDataManager.GetActiveCameraLabels(cameraData);

        ActiveCameras = new ObservableCollection<string>(cameras);

        // Build strikethrough tracking
        var strikethrough = new Dictionary<string, bool>();
        foreach (var camera in cameras)
        {
            strikethrough[camera] = _cameraDataManager.IsCameraStrikethrough(cameraData, camera);
        }
        StrikethroughCameras = strikethrough;
    }

    /// <summary>
    /// Check if a specific camera is strikethrough for this take
    /// </summary>
    public bool IsCameraStrikethrough(string cameraLabel)
    {
        return StrikethroughCameras.TryGetValue(cameraLabel, out var isStrikethrough) && isStrikethrough;
    }

    /// <summary>
    /// Check if a specific camera is active for this take
    /// </summary>
    public bool IsCameraActive(string cameraLabel)
    {
        var cameraData = _cameraDataManager.ParseCameraData(CameraData);
        return _cameraDataManager.IsCameraActive(cameraData, cameraLabel);
    }
}
