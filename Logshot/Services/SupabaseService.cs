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
// (These map exactly to the Postgres tables created in the SQL Editor)

[Table("projects")]
public class SupabaseProject : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("director")] public string Director { get; set; } = string.Empty;
    [Column("dop")] public string Dop { get; set; } = string.Empty;
    [Column("production_company")] public string ProductionCompany { get; set; } = string.Empty;
    [Column("script_supervisor")] public string ScriptSupervisor { get; set; } = string.Empty;
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
    [Column("void_camera_labels")] public string VoidCameraLabels { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

// --- The Service & Sync Engine ---

public class SupabaseService
{
    private Client _client = null!;
    private readonly DatabaseService _databaseService;
    private bool _isInitialized = false;
    private CancellationTokenSource? _debounceCts;

    // UI Event to notify the ViewModel of state changes (Icon, Text)
    public event Action<string, string>? OnSyncStatusChanged;

    private const string SupabaseUrl = "https://wcddchyqorejtashswsu.supabase.co";
    private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6IndjZGRjaHlxb3JlanRhc2hzd3N1Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODQ3NjAyMzksImV4cCI6MjEwMDMzNjIzOX0.cZrcrRAIEEvAYOEbiTcorBGPgx04tcLfuQH3suFn3IE"; // Keep your working JWT anon key here

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

            // Wrap initialization in a 5-second timeout so it never hangs indefinitely
            await ExecuteWithTimeout(async () => await _client.InitializeAsync(), 5000);

            OnSyncStatusChanged?.Invoke("☁️", "Synced");

            // On app load, trigger a sync to clear out anything logged while offline
            TriggerSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Supabase Init Error: {ex.Message}");
            OnSyncStatusChanged?.Invoke("⚠️", "Offline (Pending)");
        }
    }

    /// <summary>
    /// Starts the 3-second debounce timer. 
    /// If called again before 3 seconds, the timer restarts.
    /// </summary>
    public void TriggerSync()
    {
        if (!_isInitialized) return;

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
                    await ProcessSyncQueueAsync();
                }
            }
            catch (TaskCanceledException) { /* Ignored, timer reset */ }
        }, token);
    }

    private async Task ProcessSyncQueueAsync()
    {
        _syncedProjectIds.Clear();
        _syncedDayIds.Clear();

        var pendingItems = await _databaseService.GetPendingSyncItemsAsync();
        if (pendingItems.Count == 0)
        {
            OnSyncStatusChanged?.Invoke("☁️", "Synced");
            return;
        }

        OnSyncStatusChanged?.Invoke("🔄", "Syncing...");
        bool hasError = false;

        foreach (var item in pendingItems)
        {
            try
            {
                // Wrap each item sync operation in a strict 5-second timeout
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

                // If successful, remove it from the local outbox queue
                await _databaseService.DeleteSyncItemAsync(item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud Sync Error on [{item.EntityType} {item.EntityId}]: {ex.Message}");
                hasError = true;
                break;
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
                        VoidCameraLabels = take.VoidCameraLabels,
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
}