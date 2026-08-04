using Logshot.Models;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Logshot.Services;

// --- Supabase DTO Models ---

[Table("projects")]
public class SupabaseProject : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("user_id")] public string? UserId { get; set; }
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("director")] public string Director { get; set; } = string.Empty;
    [Column("dop")] public string Dop { get; set; } = string.Empty;
    [Column("production_company")] public string ProductionCompany { get; set; } = string.Empty;
    [Column("script_supervisor")] public string ScriptSupervisor { get; set; } = string.Empty;
    [Column("is_deleted")] public bool IsDeleted { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

internal sealed class FileSessionPersistence : IGotrueSessionPersistence<Session>
{
    private readonly string _sessionPath;

    public FileSessionPersistence()
    {
        _sessionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Logshot",
            "supabase-session.json");
    }

    public void SaveSession(Session session)
    {
        try
        {
            var directory = Path.GetDirectoryName(_sessionPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_sessionPath, JsonSerializer.Serialize(session));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not persist Supabase session: {ex.Message}");
        }
    }

    public Session? LoadSession()
    {
        try
        {
            if (!File.Exists(_sessionPath))
                return null;

            return JsonSerializer.Deserialize<Session>(File.ReadAllText(_sessionPath));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load Supabase session: {ex.Message}");
            return null;
        }
    }

    public void DestroySession()
    {
        try
        {
            if (File.Exists(_sessionPath))
                File.Delete(_sessionPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not clear Supabase session: {ex.Message}");
        }
    }
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


public enum SignUpResult
{
    Failed,
    SignedIn,
    VerificationRequired
}

// --- The Service & Sync Engine ---

public class SupabaseService
{
    public bool IsAuthenticated => _client?.Auth.CurrentSession != null;
    public string? CurrentUserId => _client?.Auth.CurrentSession?.User?.Id;
    public string? CurrentUserEmail => _client?.Auth.CurrentSession?.User?.Email;

    public static class SyncIconPaths
    {
        public const string Synced = "M19.35 12.04C18.67 8.59 15.64 6 12 6 9.11 6 6.6 7.64 5.35 10.04 2.34 10.36 0 12.91 0 16c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zm-8.64 6.25c-.39.39-1.02.39-1.41 0L7.2 16.2c-.39-.39-.39-1.02 0-1.41.39-.39 1.02-.39 1.41 0L10 16.18l4.48-4.48c.39-.39 1.02-.39 1.41 0 .39.39.39 1.02 0 1.41l-5.18 5.18z";
        public const string Syncing = "M12 4V2.21c0-.45-.54-.67-.85-.35l-2.8 2.79c-.2.2-.2.51 0 .71l2.79 2.79c.32.31.86.09.86-.36V6c3.31 0 6 2.69 6 6 0 .79-.15 1.56-.44 2.25-.15.36-.04.77.23 1.04.51.51 1.37.33 1.64-.34.37-.91.57-1.91.57-2.95 0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-.79.15-1.56.44-2.25.15-.36.04-.77-.23-1.04-.51-.51-1.37-.33-1.64.34C4.2 9.96 4 10.96 4 12c0 4.42 3.58 8 8 8v1.79c0 .45.54.67.85.35l2.79-2.79c.2-.2.2-.51 0-.71l-2.79-2.79c-.31-.31-.85-.09-.85.36V18z";
        public const string Pending = "M7,17h6c0.55,0,1-0.45,1-1v-2.59c0-0.27-0.11-0.52-0.29-0.71L11,10l2.71-2.71C13.89,7.11,14,6.85,14,6.59V4 c0-0.55-0.45-1-1-1H7C6.45,3,6,3.45,6,4v2.59c0,0.27,0.11,0.52,0.29,0.71L9,10l-2.71,2.71C6.11,12.89,6,13.15,6,13.41V16 C6,16.55,6.45,17,7,17z M7,6.59V4.5C7,4.22,7.22,4,7.5,4h5C12.78,4,13,4.22,13,4.5v2.09l-3,3L7,6.59z";
        public const string Warning = "M4.47 21h15.06c1.54 0 2.5-1.67 1.73-3L13.73 4.99c-.77-1.33-2.69-1.33-3.46 0L2.74 18c-.77 1.33.19 3 1.73 3zM12 14c-.55 0-1-.45-1-1v-2c0-.55.45-1 1-1s1 .45 1 1v2c0 .55-.45 1-1 1zm1 4h-2v-2h2v2z";
    }

    private Supabase.Client _client = null!;
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
        OnSyncStatusChanged?.Invoke(SyncIconPaths.Syncing, "Connecting...");

        try
        {
            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken = true,
                SessionHandler = new FileSessionPersistence()
            };
            _client = new Supabase.Client(SupabaseUrl, SupabaseKey, options);

            await ExecuteWithTimeout(async () => await _client.InitializeAsync(), 5000);

            // Only pull data if a user session is active
            if (IsAuthenticated)
            {
                await PullFromCloudAsync();
            }

            var pending = await _databaseService.GetPendingSyncItemsAsync();
            if (pending.Count == 0)
            {
                OnSyncStatusChanged?.Invoke(SyncIconPaths.Synced, "Synced");
            }
            else
            {
                TriggerSync();
            }

            StartPeriodicPull();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Supabase Init Error: {ex.Message}");
            _isInitialized = false;
            OnSyncStatusChanged?.Invoke(SyncIconPaths.Warning, "Offline (Pending)");
        }
    }

    /// <summary>
    /// Forces an immediate manual sync process (pulling remote changes and pushing local outbox items).
    /// Guarded with a semaphore to handle rapid tapping and prevent concurrent runs.
    /// </summary>
    public async Task ManualSyncAsync()
    {
        if (!_isInitialized || !IsAuthenticated)
        {
            OnSyncStatusChanged?.Invoke(SyncIconPaths.Warning, "Offline (Sign in to sync)");
            return;
        }

        _debounceCts?.Cancel();

        if (!await _syncSemaphore.WaitAsync(0))
        {
            return; // Already syncing
        }

        try
        {
            OnSyncStatusChanged?.Invoke(SyncIconPaths.Syncing, "Syncing...");
            await ProcessSyncQueueInternalAsync();
            await PullFromCloudAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manual Sync Error: {ex.Message}");
            OnSyncStatusChanged?.Invoke(SyncIconPaths.Warning, "Offline (Pending)");
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

        OnSyncStatusChanged?.Invoke(SyncIconPaths.Pending, "Pending Changes");

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
            OnSyncStatusChanged?.Invoke(SyncIconPaths.Synced, "Synced");
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
            OnSyncStatusChanged?.Invoke(SyncIconPaths.Warning, "Offline (Pending)");
        }
        else
        {
            OnSyncStatusChanged?.Invoke(SyncIconPaths.Synced, "Synced");
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
            UserId = CurrentUserId ?? string.Empty,
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

    private async Task<List<T>> FetchAllPaginatedAsync<T>() where T : BaseModel, new()
    {
        var allItems = new List<T>();
        int pageSize = 1000;
        int from = 0;

        while (true)
        {
            var response = await _client.From<T>().Range(from, from + pageSize - 1).Get();
            if (response.Models == null || response.Models.Count == 0)
                break;

            allItems.AddRange(response.Models);

            if (response.Models.Count < pageSize)
                break;

            from += pageSize;
        }

        return allItems;
    }

    public async Task PullFromCloudAsync()
    {
        if (!_isInitialized || !IsAuthenticated) return;

        try
        {
            var remoteProjects = (await FetchAllPaginatedAsync<SupabaseProject>()).Select(sp => new Project
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

            var remoteDays = (await FetchAllPaginatedAsync<SupabaseDay>()).Select(sd => new Day
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

            var remoteTakes = (await FetchAllPaginatedAsync<SupabaseTake>()).Select(st => new Take
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

    public async Task<SignUpResult> SignUpAsync(string email, string password)
    {
        try
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized || _client == null)
                return SignUpResult.Failed;

            var session = await _client.Auth.SignUp(email, password);
            if (session != null)
            {
                await PullFromCloudAsync();
                return SignUpResult.SignedIn;
            }
            return SignUpResult.VerificationRequired;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignUp Error: {ex.Message}");
            return SignUpResult.Failed;
        }
    }

    public async Task<bool> SignInAsync(string email, string password)
    {
        try
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (!_isInitialized || _client == null)
                return false;

            var session = await _client.Auth.SignIn(email, password);
            if (session != null)
            {
                await PullFromCloudAsync();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignIn Error: {ex.Message}");
            return false;
        }
    }

    public async Task SignOutAsync()
    {
        if (_client != null && IsAuthenticated)
            await _client.Auth.SignOut();
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
                        await ManualSyncAsync();
                    }
                }
                catch { /* Ignore */ }
            }
        });
    }
}