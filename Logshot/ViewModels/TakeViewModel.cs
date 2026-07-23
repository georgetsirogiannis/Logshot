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
    private bool _isSuppressingSave = false;

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

    [ObservableProperty]
    private bool _isSoundNoRoll = false;

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

    [ObservableProperty]
    private bool _showSoundNotes = true;

    // Setup group boundary flag for desktop thick borders
    [ObservableProperty]
    private bool _isGroupStart = false;

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

    [ObservableProperty]
    private bool _isNoBoard = false;

    [ObservableProperty]
    private bool _isEndBoard = false;

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

    /// <summary>
    /// True when all camera cells are marked as NoRoll or Strikethrough AND sound notes contain text.
    /// In this state, the row is a sound-only log (e.g. foley, ADR) rather than a camera take.
    /// </summary>
    public bool IsSoundOnlyRow
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SoundNotes))
                return false;

            var data = _cameraDataManager.ParseCameraData(CameraData);
            if (data.Cameras.Count == 0)
                return false;

            foreach (var kvp in data.Cameras)
            {
                if (!kvp.Value.NoRoll && kvp.Value.Status != "strikethrough")
                    return false;
            }

            return true;
        }
    }

    public string DisplayEpisode => IsSoundOnlyRow ? string.Empty : Episode;
    public string DisplayScene => IsSoundOnlyRow ? string.Empty : Scene;
    public string DisplayShot => IsSoundOnlyRow ? string.Empty : (Shot == 0 ? string.Empty : Shot.ToString());
    public string DisplayTakeNumber => (IsSoundOnlyRow || HasVoidedCameras) ? string.Empty : (TakeNumber == 0 ? string.Empty : TakeNumber.ToString());

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

    partial void OnEpisodeChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayEpisode));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnSceneChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayScene));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnShotChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayShot));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnTakeNumberChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayTakeNumber));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnIsCircledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCircledActive));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnIsFailedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowFailedActive));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnIsPickupChanged(bool value)
    {
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnTakeNotesChanged(string value)
    {
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnFalseStartCountChanged(int value)
    {
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }


    partial void OnIsLongStartChanged(bool value)
    {
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnIsBlooperChanged(bool value)
    {
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnIsNoBoardChanged(bool value)
    {
        if (value) IsEndBoard = false;
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnIsEndBoardChanged(bool value)
    {
        if (value) IsNoBoard = false;
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnSoundNotesChanged(string value)
    {
        OnPropertyChanged(nameof(IsSoundOnlyRow));
        OnPropertyChanged(nameof(DisplayEpisode));
        OnPropertyChanged(nameof(DisplayScene));
        OnPropertyChanged(nameof(DisplayShot));
        OnPropertyChanged(nameof(DisplayTakeNumber));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    partial void OnIsSoundNoRollChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSoundNormal));
        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    public bool ShowSoundNormal => !IsSoundNoRoll && !ShowRowCrossed;

    /// <summary>
    /// Load take data from the model
    /// </summary>
    public void LoadFromModel(Take take)
    {
        _isSuppressingSave = true;
        try
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
            IsSoundNoRoll = take.IsSoundNoRoll;
            TakeNotes = take.TakeNotes;
            FalseStartCount = take.FalseStartCount;
            IsLongStart = take.IsLongStart;
            IsCircled = take.IsCircled;
            IsFailed = take.IsFailed;
            IsPickup = take.IsPickup;
            IsBlooper = take.IsBlooper;
            IsNoBoard = take.IsNoBoard;
            IsEndBoard = take.IsEndBoard;
            VoidCameraLabels = take.VoidCameraLabels;
            CreatedAt = take.CreatedAt;
        }
        finally
        {
            _isSuppressingSave = false;
        }
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
            IsSoundNoRoll = IsSoundNoRoll,
            TakeNotes = TakeNotes,
            FalseStartCount = FalseStartCount,
            IsLongStart = IsLongStart,
            IsCircled = IsCircled,
            IsFailed = IsFailed,
            IsPickup = IsPickup,
            IsBlooper = IsBlooper,
            IsNoBoard = IsNoBoard,
            IsEndBoard = IsEndBoard,
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
        IsCircled = !IsCircled;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task MarkFailed()
    {
        IsFailed = !IsFailed;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task IncrementFalseStarts()
    {
        FalseStartCount++;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task ToggleLongStart()
    {
        IsLongStart = !IsLongStart;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task ToggleBlooper()
    {
        IsBlooper = !IsBlooper;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task TogglePickup()
    {
        IsPickup = !IsPickup;
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task ToggleSoundNoRoll()
    {
        // Fix: Prevent toggling No-Roll if the row is currently an ΑΚΥΡΟ CLIP row
        if (HasVoidedCameras) return;

        IsSoundNoRoll = !IsSoundNoRoll;
        if (IsSoundNoRoll)
        {
            SoundNotes = string.Empty;
        }
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task DecrementFalseStarts()
    {
        if (FalseStartCount > 0)
            FalseStartCount--;
        await SaveTakeCommand.ExecuteAsync(null);
    }

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

    public bool HasVoidedCameras => GetVoidCameraLabelsList().Count > 0;
    public double RowHeight => HasVoidedCameras ? 16 : double.NaN;
    public double RowMinHeight => HasVoidedCameras ? 16 : 30;

    public bool IsCamAVoided => IsCameraVoided("CAM A");
    public bool IsCamBVoided => IsCameraVoided("CAM B");
    public bool ShowCamACrossed => HasVoidedCameras && !IsCamAVoided && !IsCamANoRoll;
    public bool ShowCamBCrossed => HasVoidedCameras && !IsCamBVoided && !IsCamBNoRoll;

    public bool IsCamANotEditable => IsCamAVoided || IsCamANoRoll || HasVoidedCameras;
    public bool IsCamBNotEditable => IsCamBVoided || IsCamBNoRoll || HasVoidedCameras;

    public bool ShowCamANormal => !IsCamANoRoll && !HasVoidedCameras;
    public bool ShowCamBNormal => !IsCamBNoRoll && !HasVoidedCameras;

    public bool ShowCircledActive => IsCircled && !ShowRowCrossed;
    public bool ShowFailedActive => IsFailed && !ShowRowCrossed;
    public bool ShowRowCrossed => HasVoidedCameras;

    public const string CrossStitchPattern = "XXXXXXXXXXXXXXXXXXXX";

    public bool IsCameraVoided(string cameraLabel) => GetVoidCameraLabelsList().Contains(cameraLabel);

    [RelayCommand]
    public async Task ToggleVoidCamera(string cameraLabel)
    {
        var list = GetVoidCameraLabelsList();
        bool isAdding = !list.Contains(cameraLabel);

        if (isAdding)
        {
            list.Add(cameraLabel);

            var data = _cameraDataManager.ParseCameraData(CameraData);

            // Fix: When a row becomes an ΑΚΥΡΟ CLIP row, no cameras can have a no-roll.
            foreach (var kvp in data.Cameras)
            {
                kvp.Value.NoRoll = false;
            }

            if (data.Cameras.TryGetValue(cameraLabel, out var state))
            {
                state.Notes = string.Empty;
            }

            CameraData = _cameraDataManager.SerializeCameraData(data);

            // Safely remove sound no-roll as well for consistency
            IsSoundNoRoll = false;
        }
        else
        {
            list.Remove(cameraLabel);
        }

        VoidCameraLabels = JsonSerializer.Serialize(list);

        if (HasVoidedCameras)
        {
            TakeNumber = 0;
        }

        await SaveTakeCommand.ExecuteAsync(null);
        await RefreshCameraDataCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task ToggleVoidPrimaryCamera()
    {
        await ToggleVoidCamera("CAM A");
    }

    partial void OnVoidCameraLabelsChanged(string value)
    {
        OnPropertyChanged(nameof(HasVoidedCameras));
        OnPropertyChanged(nameof(ShowRowCrossed));
        OnPropertyChanged(nameof(RowHeight));
        OnPropertyChanged(nameof(RowMinHeight));
        OnPropertyChanged(nameof(IsCamAVoided));
        OnPropertyChanged(nameof(IsCamBVoided));
        OnPropertyChanged(nameof(ShowCamACrossed));
        OnPropertyChanged(nameof(ShowCamBCrossed));
        OnPropertyChanged(nameof(IsCamANotEditable));
        OnPropertyChanged(nameof(IsCamBNotEditable));
        OnPropertyChanged(nameof(ShowCamANormal));
        OnPropertyChanged(nameof(ShowCamBNormal));
        OnPropertyChanged(nameof(ShowCircledActive));
        OnPropertyChanged(nameof(ShowFailedActive));
        OnPropertyChanged(nameof(ShowSoundNormal));
        OnPropertyChanged(nameof(DisplayTakeNumber));
        OnPropertyChanged(nameof(VoidedHeadingText));

        foreach (var cell in ExtraCameraRolls)
        {
            cell.NotifyRollChanged();
        }

        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    public bool IsCamANoRoll => !HasVoidedCameras && (GetCameraFlag("CAM A", s => s.NoRoll) || IsCameraStrikethrough("CAM A"));
    public bool IsCamBNoRoll => !HasVoidedCameras && (GetCameraFlag("CAM B", s => s.NoRoll) || IsCameraStrikethrough("CAM B"));

    [RelayCommand]
    public async Task ToggleCameraNoRoll(string cameraLabel)
    {
        // Fix: Prevent toggling No-Roll if the row is currently an ΑΚΥΡΟ CLIP row
        if (HasVoidedCameras) return;

        var data = _cameraDataManager.ParseCameraData(CameraData);
        if (!data.Cameras.TryGetValue(cameraLabel, out var state))
        {
            data = _cameraDataManager.AddCamera(data, cameraLabel);
            state = data.Cameras[cameraLabel];
        }

        if (state.Status == "strikethrough")
        {
            state.Status = "active";
            state.NoRoll = false;
        }
        else
        {
            state.NoRoll = !state.NoRoll;
        }

        if (state.NoRoll)
        {
            state.Notes = string.Empty;

            var list = GetVoidCameraLabelsList();
            if (list.Remove(cameraLabel))
            {
                VoidCameraLabels = JsonSerializer.Serialize(list);
            }
        }

        CameraData = _cameraDataManager.SerializeCameraData(data);
        await SaveTakeCommand.ExecuteAsync(null);
        await RefreshCameraDataCommand.ExecuteAsync(null);
    }

    public bool IsCamARollChangeMarked => GetCameraFlag("CAM A", s => s.RollChangeMarker);
    public bool IsCamBRollChangeMarked => GetCameraFlag("CAM B", s => s.RollChangeMarker);

    [RelayCommand]
    public async Task ApplyRollChange(string cameraLabel)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        if (!data.Cameras.TryGetValue(cameraLabel, out var state))
        {
            data = _cameraDataManager.AddCamera(data, cameraLabel);
            state = data.Cameras[cameraLabel];
        }
        state.RollChangeMarker = true;
        CameraData = _cameraDataManager.SerializeCameraData(data);
        await SaveTakeCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task RemoveRollChange(string cameraLabel)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        if (data.Cameras.TryGetValue(cameraLabel, out var state))
        {
            state.RollChangeMarker = false;
            state.RollNumber = string.Empty;
            CameraData = _cameraDataManager.SerializeCameraData(data);
            await SaveTakeCommand.ExecuteAsync(null);
        }
    }

    private bool GetCameraFlag(string cameraLabel, Func<Services.CameraDataManager.CameraState, bool> selector)
        => GetCameraFlagPublic(cameraLabel, selector);

    internal bool GetCameraFlagPublic(string cameraLabel, Func<Services.CameraDataManager.CameraState, bool> selector)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        return data.Cameras.TryGetValue(cameraLabel, out var state) && selector(state);
    }

    [RelayCommand]
    public async Task RefreshCameraData()
    {
        var cameraData = _cameraDataManager.ParseCameraData(CameraData);
        var cameras = _cameraDataManager.GetActiveCameraLabels(cameraData);

        ActiveCameras = new ObservableCollection<string>(cameras);

        var strikethrough = new Dictionary<string, bool>();
        foreach (var camera in cameras)
        {
            strikethrough[camera] = _cameraDataManager.IsCameraStrikethrough(cameraData, camera);
        }
        StrikethroughCameras = strikethrough;

        OnPropertyChanged(nameof(IsCamANoRoll));
        OnPropertyChanged(nameof(IsCamBNoRoll));
    }

    public bool IsCameraStrikethrough(string cameraLabel)
    {
        var cameraData = _cameraDataManager.ParseCameraData(CameraData);
        return _cameraDataManager.IsCameraStrikethrough(cameraData, cameraLabel);
    }

    public bool IsCameraActive(string cameraLabel)
    {
        var cameraData = _cameraDataManager.ParseCameraData(CameraData);
        return _cameraDataManager.IsCameraActive(cameraData, cameraLabel);
    }

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

        if (!string.IsNullOrEmpty(value))
        {
            data.Cameras[cameraLabel].NoRoll = false;

            var list = GetVoidCameraLabelsList();
            if (list.Remove(cameraLabel))
            {
                VoidCameraLabels = JsonSerializer.Serialize(list);
            }
        }

        CameraData = _cameraDataManager.SerializeCameraData(data);
        _ = SaveTakeCommand.ExecuteAsync(null);
    }

    internal string GetCameraRollNumber(string cameraLabel)
    {
        var data = _cameraDataManager.ParseCameraData(CameraData);
        return data.Cameras.TryGetValue(cameraLabel, out var state) ? state.RollNumber : string.Empty;
    }

    internal void SetCameraRollNumber(string cameraLabel, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            value = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }

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
        _ = RefreshCameraData();
        OnPropertyChanged(nameof(CamARoll));
        OnPropertyChanged(nameof(CamBRoll));
        OnPropertyChanged(nameof(CamARollNumber));
        OnPropertyChanged(nameof(CamBRollNumber));
        OnPropertyChanged(nameof(IsCamANoRoll));
        OnPropertyChanged(nameof(IsCamBNoRoll));
        OnPropertyChanged(nameof(IsCamARollChangeMarked));
        OnPropertyChanged(nameof(IsCamBRollChangeMarked));
        OnPropertyChanged(nameof(IsCamANotEditable));
        OnPropertyChanged(nameof(IsCamBNotEditable));
        OnPropertyChanged(nameof(ShowCamANormal));
        OnPropertyChanged(nameof(ShowCamBNormal));
        OnPropertyChanged(nameof(IsSoundOnlyRow));
        OnPropertyChanged(nameof(DisplayEpisode));
        OnPropertyChanged(nameof(DisplayScene));
        OnPropertyChanged(nameof(DisplayShot));
        OnPropertyChanged(nameof(DisplayTakeNumber));

        foreach (var cell in ExtraCameraRolls)
        {
            cell.NotifyRollChanged();
        }

        if (!_isSuppressingSave)
        {
            _ = SaveTakeCommand.ExecuteAsync(null);
        }
    }

    

    public CameraRollCell? ExtraCell1 => ExtraCameraRolls.ElementAtOrDefault(0);
    public CameraRollCell? ExtraCell2 => ExtraCameraRolls.ElementAtOrDefault(1);
    public CameraRollCell? ExtraCell3 => ExtraCameraRolls.ElementAtOrDefault(2);

    public void RefreshExtraCameraRolls(IEnumerable<string> dayActiveCameras)
    {
        var extras = dayActiveCameras.Where(c => c != "CAM A" && c != "CAM B").ToList();

        for (int i = ExtraCameraRolls.Count - 1; i >= 0; i--)
        {
            if (!extras.Contains(ExtraCameraRolls[i].Label))
                ExtraCameraRolls.RemoveAt(i);
        }

        foreach (var camera in extras)
        {
            if (!ExtraCameraRolls.Any(c => c.Label == camera))
            {
                ExtraCameraRolls.Add(new CameraRollCell(this, camera));
            }
        }

        OnPropertyChanged(nameof(ExtraCell1));
        OnPropertyChanged(nameof(ExtraCell2));
        OnPropertyChanged(nameof(ExtraCell3));

        foreach (var cell in ExtraCameraRolls)
        {
            cell.NotifyRollChanged();
        }
    }

    /// <summary>
    /// Mobile view: Returns the appropriate heading text for the voided cameras section.
    /// </summary>
    public string VoidedHeadingText
    {
        get
        {
            int voidCount = 0;
            if (IsCamAVoided) voidCount++;
            if (IsCamBVoided) voidCount++;

            if (ExtraCameraRolls != null)
            {
                foreach (var cam in ExtraCameraRolls)
                {
                    if (cam.IsVoided) voidCount++;
                }
            }

            return voidCount > 1 ? "ΑΚΥΡΑ CLIPS" : "ΑΚΥΡΟ CLIP";
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

    [ObservableProperty]
    private bool _showRoll = true;

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
    public bool IsNoRoll => !_owner.HasVoidedCameras && (_owner.GetCameraFlagPublic(Label, s => s.NoRoll) || _owner.IsCameraStrikethrough(Label));
    public bool ShowCrossed => _owner.HasVoidedCameras && !IsVoided && !IsNoRoll;

    public bool IsNotEditable => IsVoided || IsNoRoll || _owner.HasVoidedCameras;
    public bool IsRollChangeMarked => _owner.GetCameraFlagPublic(Label, s => s.RollChangeMarker);
    public bool ShowNormal => !IsNoRoll && !_owner.HasVoidedCameras;

    public void NotifyRollChanged()
    {
        OnPropertyChanged(nameof(Roll));
        OnPropertyChanged(nameof(RollNumber));
        OnPropertyChanged(nameof(IsVoided));
        OnPropertyChanged(nameof(ShowCrossed));
        OnPropertyChanged(nameof(IsNoRoll));
        OnPropertyChanged(nameof(IsRollChangeMarked));
        OnPropertyChanged(nameof(IsNotEditable));
        OnPropertyChanged(nameof(ShowNormal));
    }

    [RelayCommand]
    public async Task ToggleVoid() => await _owner.ToggleVoidCamera(Label);

    [RelayCommand]
    public async Task ToggleNoRoll() => await _owner.ToggleCameraNoRoll(Label);

    [RelayCommand]
    public async Task ApplyRollChange() => await _owner.ApplyRollChange(Label);

    [RelayCommand]
    public async Task RemoveRollChange() => await _owner.RemoveRollChange(Label);
}