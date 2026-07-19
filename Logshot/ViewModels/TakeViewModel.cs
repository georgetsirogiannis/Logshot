using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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

    // Visibility Flags for Row Spanning
    [ObservableProperty]
    private bool _showEpisode = true;

    [ObservableProperty]
    private bool _showScene = true;

    [ObservableProperty]
    private bool _showShot = true;

    [ObservableProperty]
    private bool _showCamARoll = true;

    [ObservableProperty]
    private bool _showCamBRoll = true;

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
    /// Roll cells for any cameras beyond the two defaults (CAM A / CAM B)
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CameraRollCell> _extraCameraRolls = new();

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
    /// Phase 5.7: [FS] chip decrement. Tap the "-" icon to reduce the false-start count.
    /// </summary>
    [RelayCommand]
    public async Task DecrementFalseStarts()
    {
        if (FalseStartCount > 0)
            FalseStartCount--;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    // -------------------------------------------------------------------------
    // Phase 5.5: Camera-specific ΑΚΥΡΟ CLIP (per-camera void + row cross-stitch)
    // -------------------------------------------------------------------------

    private List<string> GetVoidCameraLabelsList()
    {
        if (string.IsNullOrWhiteSpace(VoidCameraLabels))
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(VoidCameraLabels) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// True when at least one camera on this take is flagged ΑΚΥΡΟ CLIP (false clip).
    /// Collapses the row height to 16pt and crosses out every other cell.
    /// </summary>
    public bool HasVoidedCameras => GetVoidCameraLabelsList().Count > 0;

    /// <summary>
    /// Row height budget: collapses to 16pt when any camera is voided on this take.
    /// </summary>
    public double RowHeight => HasVoidedCameras ? 16 : double.NaN;

    /// <summary>
    /// XAML-friendly row minimum height (16pt collapsed, 30pt standard) avoiding NaN bindings.
    /// </summary>
    public double RowMinHeight => HasVoidedCameras ? 16 : 30;

    public bool IsCamAVoided => IsCameraVoided("CAM A");
    public bool IsCamBVoided => IsCameraVoided("CAM B");
    public bool ShowCamACrossed => HasVoidedCameras && !IsCamAVoided;
    public bool ShowCamBCrossed => HasVoidedCameras && !IsCamBVoided;

    /// <summary>
    /// Whole-row safety constraint (5.5): every non-camera cell (EP, SC, Shot, Take, Sound, Notes)
    /// is overlaid with the tight cross-stitch pattern whenever this take has a voided camera.
    /// </summary>
    public bool ShowRowCrossed => HasVoidedCameras;

    public const string CrossStitchPattern = "XXXXXXXXXXXXXXXXXXXX";

    public bool IsCameraVoided(string cameraLabel) => GetVoidCameraLabelsList().Contains(cameraLabel);

    /// <summary>
    /// Toggles the ΑΚΥΡΟ CLIP (false clip) flag for a specific camera on this take.
    /// </summary>
    [RelayCommand]
    public async Task ToggleVoidCamera(string cameraLabel)
    {
        var list = GetVoidCameraLabelsList();
        if (!list.Remove(cameraLabel))
            list.Add(cameraLabel);
        VoidCameraLabels = JsonSerializer.Serialize(list);
        await SaveTakeCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Phase 4 mobile quick-action: [ΑΚΥΡΟ] drawer button. Toggles the primary camera's
    /// voided/false-clip status using the same per-camera list as the desktop grid.
    /// </summary>
    [RelayCommand]
    public async Task ToggleVoidPrimaryCamera()
    {
        await ToggleVoidCamera("CAM A");
    }

    partial void OnVoidCameraLabelsChanged(string value)
    {
        OnPropertyChanged(nameof(HasVoidedCameras));
        OnPropertyChanged(nameof(RowHeight));
        OnPropertyChanged(nameof(RowMinHeight));
        OnPropertyChanged(nameof(IsCamAVoided));
        OnPropertyChanged(nameof(IsCamBVoided));
        OnPropertyChanged(nameof(ShowCamACrossed));
        OnPropertyChanged(nameof(ShowCamBCrossed));
        OnPropertyChanged(nameof(ShowRowCrossed));

        foreach (var cell in ExtraCameraRolls)
        {
            cell.NotifyRollChanged();
        }
    }

    // -------------------------------------------------------------------------
    // Phase 5.6: Diagonal camera slashes ("No Roll")
    // -------------------------------------------------------------------------

    public bool IsCamANoRoll => GetCameraFlag("CAM A", s => s.NoRoll);
    public bool IsCamBNoRoll => GetCameraFlag("CAM B", s => s.NoRoll);

    [RelayCommand]
    public async Task ToggleCameraNoRoll(string cameraLabel)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        if (!data.Cameras.TryGetValue(cameraLabel, out var state))
        {
            data = _cameraDataManager.AddCamera(data, cameraLabel);
            state = data.Cameras[cameraLabel];
        }
        state.NoRoll = !state.NoRoll;
        CameraData = _cameraDataManager.SerializeCameraData(data);
        await SaveTakeCommand.ExecuteAsync(null);
    }

    // -------------------------------------------------------------------------
    // Phase 5.8: Camera roll change marker
    // -------------------------------------------------------------------------

    public bool IsCamARollChangeMarked => GetCameraFlag("CAM A", s => s.RollChangeMarker);
    public bool IsCamBRollChangeMarked => GetCameraFlag("CAM B", s => s.RollChangeMarker);

    [RelayCommand]
    public async Task ToggleRollChangeMarker(string cameraLabel)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        if (!data.Cameras.TryGetValue(cameraLabel, out var state))
        {
            data = _cameraDataManager.AddCamera(data, cameraLabel);
            state = data.Cameras[cameraLabel];
        }
        state.RollChangeMarker = !state.RollChangeMarker;
        CameraData = _cameraDataManager.SerializeCameraData(data);
        await SaveTakeCommand.ExecuteAsync(null);
    }

    private bool GetCameraFlag(string cameraLabel, Func<Services.CameraDataManager.CameraState, bool> selector)
        => GetCameraFlagPublic(cameraLabel, selector);

    /// <summary>
    /// Internal helper exposed for <see cref="CameraRollCell"/> to read per-camera flags.
    /// </summary>
    internal bool GetCameraFlagPublic(string cameraLabel, Func<Services.CameraDataManager.CameraState, bool> selector)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        return data.Cameras.TryGetValue(cameraLabel, out var state) && selector(state);
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

    // -------------------------------------------------------------------------
    // Roll number editing (14/48 Grid columns: CAM A ROLL / CAM B ROLL)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Free-text field for CAM A (shot description / notes). Kept separate from the
    /// physical roll number, which is edited via the "Change Roll" popup.
    /// </summary>
    public string CamARoll
    {
        get => GetCameraRoll("CAM A");
        set => SetCameraRoll("CAM A", value);
    }

    public string CamBRoll
    {
        get => GetCameraRoll("CAM B");
        set => SetCameraRoll("CAM B", value);
    }

    public string CamARollNumber
    {
        get => GetCameraRollNumber("CAM A");
        set => SetCameraRollNumber("CAM A", value);
    }

    public string CamBRollNumber
    {
        get => GetCameraRollNumber("CAM B");
        set => SetCameraRollNumber("CAM B", value);
    }

    internal string GetCameraRoll(string cameraLabel)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        return data.Cameras.TryGetValue(cameraLabel, out var state) ? state.Notes : string.Empty;
    }

    internal void SetCameraRoll(string cameraLabel, string value)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        if (!data.Cameras.ContainsKey(cameraLabel))
        {
            data = _cameraDataManager.AddCamera(data, cameraLabel);
        }
        data.Cameras[cameraLabel].Notes = value ?? string.Empty;
        CameraData = _cameraDataManager.SerializeCameraData(data);
        _ = SaveTakeCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Gets the physical camera roll number/ID (set via the "Change Roll" popup),
    /// separate from the free-text shot description held by <see cref="GetCameraRoll"/>.
    /// </summary>
    internal string GetCameraRollNumber(string cameraLabel)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        return data.Cameras.TryGetValue(cameraLabel, out var state) ? state.RollNumber : string.Empty;
    }

    internal void SetCameraRollNumber(string cameraLabel, string value)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        if (!data.Cameras.ContainsKey(cameraLabel))
        {
            data = _cameraDataManager.AddCamera(data, cameraLabel);
        }
        data.Cameras[cameraLabel].RollNumber = value ?? string.Empty;
        CameraData = _cameraDataManager.SerializeCameraData(data);
        _ = SaveTakeCommand.ExecuteAsync(null);
        OnPropertyChanged(cameraLabel == "CAM A" ? nameof(CamARollNumber) : nameof(CamBRollNumber));
    }

    partial void OnCameraDataChanged(string value)
    {
        OnPropertyChanged(nameof(CamARoll));
        OnPropertyChanged(nameof(CamBRoll));
        OnPropertyChanged(nameof(CamARollNumber));
        OnPropertyChanged(nameof(CamBRollNumber));
        OnPropertyChanged(nameof(IsCamANoRoll));
        OnPropertyChanged(nameof(IsCamBNoRoll));
        OnPropertyChanged(nameof(IsCamARollChangeMarked));
        OnPropertyChanged(nameof(IsCamBRollChangeMarked));

        foreach (var cell in ExtraCameraRolls)
        {
            cell.NotifyRollChanged();
        }
    }

    /// <summary>
    /// Rebuilds the ExtraCameraRolls collection for cameras beyond the two defaults,
    /// based on the day's full list of active camera labels.
    /// </summary>
    public void RefreshExtraCameraRolls(IEnumerable<string> dayActiveCameras)
    {
        var extras = dayActiveCameras.Where(c => c != "CAM A" && c != "CAM B").ToList();

        // Remove cells no longer relevant
        for (int i = ExtraCameraRolls.Count - 1; i >= 0; i--)
        {
            if (!extras.Contains(ExtraCameraRolls[i].Label))
                ExtraCameraRolls.RemoveAt(i);
        }

        // Add new cells
        foreach (var camera in extras)
        {
            if (!ExtraCameraRolls.Any(c => c.Label == camera))
            {
                ExtraCameraRolls.Add(new CameraRollCell(this, camera));
            }
        }
    }
}

/// <summary>
/// A single editable roll-number cell for a dynamically added camera column.
/// </summary>
public partial class CameraRollCell : ObservableObject
{
    private readonly TakeViewModel _owner;

    public string Label { get; }

    public CameraRollCell(TakeViewModel owner, string label)
    {
        _owner = owner;
        Label = label;
    }

    public string Roll
    {
        get => _owner.GetCameraRoll(Label);
        set
        {
            _owner.SetCameraRoll(Label, value);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The physical camera roll number/ID, edited via the "Change Roll" popup,
    /// separate from the free-text shot description held by <see cref="Roll"/>.
    /// </summary>
    public string RollNumber
    {
        get => _owner.GetCameraRollNumber(Label);
        set
        {
            _owner.SetCameraRollNumber(Label, value);
            OnPropertyChanged();
        }
    }

    public bool IsVoided => _owner.IsCameraVoided(Label);
    public bool ShowCrossed => _owner.HasVoidedCameras && !IsVoided;
    public bool IsNoRoll => _owner.GetCameraFlagPublic(Label, s => s.NoRoll);
    public bool IsRollChangeMarked => _owner.GetCameraFlagPublic(Label, s => s.RollChangeMarker);

    public void NotifyRollChanged()
    {
        OnPropertyChanged(nameof(Roll));
        OnPropertyChanged(nameof(RollNumber));
        OnPropertyChanged(nameof(IsVoided));
        OnPropertyChanged(nameof(ShowCrossed));
        OnPropertyChanged(nameof(IsNoRoll));
        OnPropertyChanged(nameof(IsRollChangeMarked));
    }
}
