using Logshot.Models;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Logshot.Services;

// --- Supabase DTO Models ---

[Table("projects")]
public class SupabaseProject : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("director")] public string Director { get; set; } = string.Empty;
    [Column("dop")] public string Dop { get; set; } = string.Empty;
    [Column("production_company")] public string ProductionCompany { get; set; } = string.Empty;
    [Column("script_supervisor")] public string ScriptSupervisor { get; set; } = string.Empty;
    [Column("is_deleted")] public bool IsDeleted { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("days")]
public class SupabaseDay : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("project_id")] public string ProjectId { get; set; } = string.Empty;
    [Column("shoot_day_number")] public string ShootDayNumber { get; set; } = string.Empty;
    [Column("calendar_date")] public DateTime CalendarDate { get; set; }
    [Column("general_notes")] public string GeneralNotes { get; set; } = string.Empty;
    [Column("is_finalized")] public bool IsFinalized { get; set; }
    [Column("is_deleted")] public bool IsDeleted { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("takes")]
public class SupabaseTake : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("day_id")] public string DayId { get; set; } = string.Empty;
    [Column("sequence_order")] public int SequenceOrder { get; set; }
    [Column("episode")] public string Episode { get; set; } = string.Empty;
    [Column("scene")] public string Scene { get; set; } = string.Empty;
    [Column("shot")] public int Shot { get; set; }
    [Column("take_number")] public int TakeNumber { get; set; }
    [Column("camera_data")] public string CameraData { get; set; } = string.Empty;
    [Column("sound_notes")] public string SoundNotes { get; set; } = string.Empty;
    [Column("is_sound_no_roll")] public bool IsSoundNoRoll { get; set; }
    [Column("take_notes")] public string TakeNotes { get; set; } = string.Empty;
    [Column("false_start_count")] public int FalseStartCount { get; set; }
    [Column("is_long_start")] public bool IsLongStart { get; set; }
    [Column("is_circled")] public bool IsCircled { get; set; }
    [Column("is_failed")] public bool IsFailed { get; set; }
    [Column("is_pickup")] public bool IsPickup { get; set; }
    [Column("is_blooper")] public bool IsBlooper { get; set; }
    [Column("is_no_board")] public bool IsNoBoard { get; set; }
    [Column("is_end_board")] public bool IsEndBoard { get; set; }
    [Column("is_wild_shot")] public bool IsWildShot { get; set; }
    [Column("void_camera_labels")] public string VoidCameraLabels { get; set; } = string.Empty;
    [Column("is_deleted")] public bool IsDeleted { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

// --- The Service & Sync Engine ---

public class SupabaseService
{
    private Client _client = null!;
    private readonly DatabaseService _databaseService;
    private bool _isInitialized = false;
    private CancellationTokenSource? _debounceCts;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);

    public event Action<string, string>? OnSyncStatusChanged;
    public event Action? OnCloudDataReceived;

    private const string SupabaseUrl = "https://wcddchyqorejtashswsu.supabase.co";
    private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6IndjZGRjaHlxb3JlanRhc2hzd3N1Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODQ3NjAyMzksImV4cCI6MjEwMDMzNjIzOX0.cZrcrRAIEEvAYOEbiTcorBGPgx04tcLfuQH3suFn3IE";

    public SupabaseService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        _isInitialized = true;
        OnSyncStatusChanged?.Invoke("🔄", "Connecting...");

        try
        {
            var options = new SupabaseOptions { AutoConnectRealtime = true };
            _client = new Client(SupabaseUrl, SupabaseKey, options);

            await ExecuteWithTimeout(async () => await _client.InitializeAsync(), 5000);

            OnSyncStatusChanged?.Invoke("☁️", "Synced");

            await PullFromCloudAsync();
            TriggerSync();
            StartPeriodicPull();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Supabase Init Error: {ex.Message}");
            OnSyncStatusChanged?.Invoke("⚠️", "Offline (Pending)");
        }
    }

    /// <summary>
    /// Forces an immediate manual sync process (pulling remote changes and pushing local outbox items).
    /// Guarded with a semaphore to handle rapid tapping and prevent concurrent runs.
    /// </summary>
    public async Task ManualSyncAsync()
    {
        if (!_isInitialized) return;

        _debounceCts?.Cancel();

        if (!await _syncSemaphore.WaitAsync(0))
        {
            return; // Already syncing
        }

        try
        {
            OnSyncStatusChanged?.Invoke("🔄", "Syncing...");
            await PullFromCloudAsync();
            await ProcessSyncQueueInternalAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manual Sync Error: {ex.Message}");
            OnSyncStatusChanged?.Invoke("⚠️", "Offline (Pending)");
        }
        finally
        {
            _syncSemaphore.Release();
        }
    }

    /// <summary>
    /// Starts or resets the 3-second debounce timer when local data changes.
    /// </summary>
    public void TriggerSync()
    {
        if (!_isInitialized) return;

        OnSyncStatusChanged?.Invoke("⏳", "Pending Changes");

        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000, token);
                if (!token.IsCancellationRequested)
                {
                    await ManualSyncAsync();
                }
            }
            catch (TaskCanceledException) { /* Timer reset */ }
        }, token);
    }

    private async Task ProcessSyncQueueInternalAsync()
    {
        _syncedProjectIds.Clear();
        _syncedDayIds.Clear();

        var pendingItems = await _databaseService.GetPendingSyncItemsAsync();
        if (pendingItems.Count == 0)
        {
            OnSyncStatusChanged?.Invoke("☁️", "Synced");
            return;
        }

        bool hasError = false;

        foreach (var item in pendingItems)
        {
            try
            {
                await ExecuteWithTimeout(async () =>
                {
                    if (item.Action == "Upsert")
                    {
                        await ProcessUpsertAsync(item);
                    }
                    else if (item.Action == "Delete")
                    {
                        await ProcessDeleteAsync(item);
                    }
                }, 5000);

                await _databaseService.DeleteSyncItemAsync(item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud Sync Error on [{item.EntityType} {item.EntityId}]: {ex.Message}");
                hasError = true;
            }
        }

        if (hasError)
        {
            OnSyncStatusChanged?.Invoke("⚠️", "Offline (Pending)");
        }
        else
        {
            OnSyncStatusChanged?.Invoke("☁️", "Synced");
        }
    }

    private async Task ExecuteWithTimeout(Func<Task> action, int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var task = action();
        var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token));
        if (completedTask != task)
        {
            throw new TimeoutException("Supabase operation timed out.");
        }
        await task;
    }

    private readonly HashSet<string> _syncedProjectIds = new();
    private readonly HashSet<string> _syncedDayIds = new();

    private async Task UpsertProjectIfNeeded(string projectId)
    {
        if (string.IsNullOrEmpty(projectId) || _syncedProjectIds.Contains(projectId)) return;
        var project = await _databaseService.GetProjectAsync(projectId);
        if (project == null) return;

        await _client.From<SupabaseProject>().Upsert(new SupabaseProject
        {
            Id = project.Id,
            Name = project.Name,
            Director = project.Director,
            Dop = project.Dop,
            ProductionCompany = project.ProductionCompany,
            ScriptSupervisor = project.ScriptSupervisor,
            IsDeleted = project.IsDeleted,
            CreatedAt = project.CreatedAt
        });
        _syncedProjectIds.Add(projectId);
    }

    private async Task UpsertDayIfNeeded(string dayId)
    {
        if (string.IsNullOrEmpty(dayId) || _syncedDayIds.Contains(dayId)) return;
        var day = await _databaseService.GetDayAsync(dayId);
        if (day == null) return;

        await UpsertProjectIfNeeded(day.ProjectId);

        await _client.From<SupabaseDay>().Upsert(new SupabaseDay
        {
            Id = day.Id,
            ProjectId = day.ProjectId,
            ShootDayNumber = day.ShootDayNumber,
            CalendarDate = day.CalendarDate,
            GeneralNotes = day.GeneralNotes,
            IsFinalized = day.IsFinalized,
            IsDeleted = day.IsDeleted,
            CreatedAt = day.CreatedAt
        });
        _syncedDayIds.Add(dayId);
    }

    private async Task ProcessUpsertAsync(SyncQueueItem item)
    {
        switch (item.EntityType)
        {
            case "Project":
                await UpsertProjectIfNeeded(item.EntityId);
                break;

            case "Day":
                await UpsertDayIfNeeded(item.EntityId);
                break;

            case "Take":
                var take = await _databaseService.GetTakeAsync(item.EntityId);
                if (take != null)
                {
                    await UpsertDayIfNeeded(take.DayId);

                    await _client.From<SupabaseTake>().Upsert(new SupabaseTake
                    {
                        Id = take.Id,
                        DayId = take.DayId,
                        SequenceOrder = take.SequenceOrder,
                        Episode = take.Episode,
                        Scene = take.Scene,
                        Shot = take.Shot,
                        TakeNumber = take.TakeNumber,
                        CameraData = take.CameraData,
                        SoundNotes = take.SoundNotes,
                        IsSoundNoRoll = take.IsSoundNoRoll,
                        TakeNotes = take.TakeNotes,
                        FalseStartCount = take.FalseStartCount,
                        IsLongStart = take.IsLongStart,
                        IsCircled = take.IsCircled,
                        IsFailed = take.IsFailed,
                        IsPickup = take.IsPickup,
                        IsBlooper = take.IsBlooper,
                        IsNoBoard = take.IsNoBoard,
                        IsEndBoard = take.IsEndBoard,
                        IsWildShot = take.IsWildShot,
                        VoidCameraLabels = take.VoidCameraLabels,
                        IsDeleted = take.IsDeleted,
                        CreatedAt = take.CreatedAt
                    });
                }
                break;
        }
    }

    private async Task ProcessDeleteAsync(SyncQueueItem item)
    {
        switch (item.EntityType)
        {
            case "Project":
                await _client.From<SupabaseProject>().Where(x => x.Id == item.EntityId).Delete();
                break;
            case "Day":
                await _client.From<SupabaseDay>().Where(x => x.Id == item.EntityId).Delete();
                break;
            case "Take":
                await _client.From<SupabaseTake>().Where(x => x.Id == item.EntityId).Delete();
                break;
        }
    }

    public async Task PullFromCloudAsync()
    {
        if (!_isInitialized) return;

        var pending = await _databaseService.GetPendingSyncItemsAsync();
        if (pending.Count > 0) return; // Skip pull if local edits are queued

        try
        {
            var responseProjects = await _client.From<SupabaseProject>().Get();
            var responseDays = await _client.From<SupabaseDay>().Get();
            var responseTakes = await _client.From<SupabaseTake>().Get();

            var remoteProjects = responseProjects.Models.Select(sp => new Project
            {
                Id = sp.Id,
                Name = sp.Name,
                Director = sp.Director,
                Dop = sp.Dop,
                ProductionCompany = sp.ProductionCompany,
                ScriptSupervisor = sp.ScriptSupervisor,
                IsDeleted = sp.IsDeleted,
                CreatedAt = sp.CreatedAt
            }).ToList();

            var remoteDays = responseDays.Models.Select(sd => new Day
            {
                Id = sd.Id,
                ProjectId = sd.ProjectId,
                ShootDayNumber = sd.ShootDayNumber,
                CalendarDate = sd.CalendarDate,
                GeneralNotes = sd.GeneralNotes,
                IsFinalized = sd.IsFinalized,
                IsDeleted = sd.IsDeleted,
                CreatedAt = sd.CreatedAt
            }).ToList();

            var remoteTakes = responseTakes.Models.Select(st => new Take
            {
                Id = st.Id,
                DayId = st.DayId,
                SequenceOrder = st.SequenceOrder,
                Episode = st.Episode,
                Scene = st.Scene,
                Shot = st.Shot,
                TakeNumber = st.TakeNumber,
                CameraData = st.CameraData,
                SoundNotes = st.SoundNotes,
                IsSoundNoRoll = st.IsSoundNoRoll,
                TakeNotes = st.TakeNotes,
                FalseStartCount = st.FalseStartCount,
                IsLongStart = st.IsLongStart,
                IsCircled = st.IsCircled,
                IsFailed = st.IsFailed,
                IsPickup = st.IsPickup,
                IsBlooper = st.IsBlooper,
                IsNoBoard = st.IsNoBoard,
                IsEndBoard = st.IsEndBoard,
                IsWildShot = st.IsWildShot,
                VoidCameraLabels = st.VoidCameraLabels,
                IsDeleted = st.IsDeleted,
                CreatedAt = st.CreatedAt
            }).ToList();

            var (pCount, dCount, tCount) = await _databaseService.SaveCloudDataAsync(remoteProjects, remoteDays, remoteTakes);
            if (pCount > 0 || dCount > 0 || tCount > 0)
            {
                OnCloudDataReceived?.Invoke();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cloud Pull Error: {ex.Message}");
        }
    }

    private void StartPeriodicPull()
    {
        Task.Run(async () =>
        {
            while (_isInitialized)
            {
                try
                {
                    await Task.Delay(15000);
                    if (_isInitialized)
                    {
                        await PullFromCloudAsync();
                    }
                }
                catch { /* Ignore */ }
            }
        });
    }
}