using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using Logshot.Models;

namespace Logshot.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection _db = null!;

    // This event tells our Supabase worker that new changes exist
    public event Action? OnDataChanged;

    public DatabaseService()
    {
    }

    private Task _writeQueue = Task.CompletedTask;
    private readonly object _queueLock = new();

    private Task<T> Enqueue<T>(Func<Task<T>> operation)
    {
        lock (_queueLock)
        {
            var task = _writeQueue.ContinueWith(_ => operation(), TaskScheduler.Default).Unwrap();
            _writeQueue = task;
            return task;
        }
    }

    public Task WaitForPendingWritesAsync()
    {
        lock (_queueLock)
            return _writeQueue;
    }

    public async Task InitAsync()
    {
        if (_db is not null) return;

        string databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "logshot.db");
        _db = new SQLiteAsyncConnection(databasePath);

        await _db.CreateTableAsync<Project>();
        await _db.CreateTableAsync<Day>();
        await _db.CreateTableAsync<Take>();
        await _db.CreateTableAsync<SyncQueueItem>(); // New outbox table
    }

    private async Task QueueSyncAction(string entityType, string entityId, string action)
    {
        await _db.InsertAsync(new SyncQueueItem { EntityType = entityType, EntityId = entityId, Action = action });
        // Fire the event to reset the 3-second debounce timer
        OnDataChanged?.Invoke();
    }

    // --- CRUD OPERATIONS ---

    public Task<int> SaveTakeAsync(Take take)
    {
        return Enqueue(async () =>
        {
            await InitAsync();
            var result = await _db.InsertOrReplaceAsync(take);
            await QueueSyncAction("Take", take.Id, "Upsert");
            return result;
        });
    }

    public Task<int> SaveDayAsync(Day day)
    {
        return Enqueue(async () =>
        {
            await InitAsync();
            var result = await _db.InsertOrReplaceAsync(day);
            await QueueSyncAction("Day", day.Id, "Upsert");
            return result;
        });
    }

    public Task<int> SaveProjectAsync(Project project)
    {
        return Enqueue(async () =>
        {
            await InitAsync();
            var result = await _db.InsertOrReplaceAsync(project);
            await QueueSyncAction("Project", project.Id, "Upsert");
            return result;
        });
    }

    public async Task<int> DeleteTakeAsync(Take take)
    {
        await InitAsync();
        var result = await _db.DeleteAsync(take);
        await QueueSyncAction("Take", take.Id, "Delete");
        return result;
    }

    public async Task<int> DeleteDayAsync(Day day)
    {
        await InitAsync();
        var takes = await GetTakesForDayAsync(day.Id);
        foreach (var take in takes)
        {
            await _db.DeleteAsync(take);
            await QueueSyncAction("Take", take.Id, "Delete");
        }
        var result = await _db.DeleteAsync(day);
        await QueueSyncAction("Day", day.Id, "Delete");
        return result;
    }

    public async Task<int> DeleteProjectAsync(Project project)
    {
        await InitAsync();
        var days = await GetDaysForProjectAsync(project.Id);
        foreach (var day in days)
        {
            await DeleteDayAsync(day);
        }
        var result = await _db.DeleteAsync(project);
        await QueueSyncAction("Project", project.Id, "Delete");
        return result;
    }

    // --- NEW: Single Entity Retrievals for Sync Worker ---

    public async Task<Take> GetTakeAsync(string id) { await InitAsync(); return await _db.FindAsync<Take>(id); }
    public async Task<Day> GetDayAsync(string id) { await InitAsync(); return await _db.FindAsync<Day>(id); }
    public async Task<Project> GetProjectAsync(string id) { await InitAsync(); return await _db.FindAsync<Project>(id); }

    // --- OUTBOX QUEUE METHODS ---

    public async Task<List<SyncQueueItem>> GetPendingSyncItemsAsync()
    {
        await InitAsync();
        return await _db.Table<SyncQueueItem>().OrderBy(x => x.Id).ToListAsync();
    }

    public async Task DeleteSyncItemAsync(SyncQueueItem item)
    {
        await InitAsync();
        await _db.DeleteAsync(item);
    }

    // --- EXISTING QUERY OPERATIONS ---

    public async Task<List<Take>> GetTakesForDayAsync(string dayId)
    {
        await InitAsync();
        return await _db.Table<Take>().Where(t => t.DayId == dayId).OrderBy(t => t.SequenceOrder).ToListAsync();
    }

    public async Task<List<Take>> GetTakesForProjectAsync(string projectId)
    {
        await InitAsync();
        var days = await _db.Table<Day>().Where(d => d.ProjectId == projectId).ToListAsync();
        var dayIds = days.Select(d => d.Id).ToList();

        if (dayIds.Count == 0) return new List<Take>();

        var takes = new List<Take>();
        foreach (var dayId in dayIds)
        {
            var takesForDay = await _db.Table<Take>().Where(t => t.DayId == dayId).ToListAsync();
            takes.AddRange(takesForDay);
        }
        return takes.OrderBy(t => t.CreatedAt).ToList();
    }

    public static List<string> GetTokens(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();
        char[] separators = new[] { '\r', '\n', ' ', ',', '-' };
        return input.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
    }

    public async Task<List<Take>> GetTakesForEpisodeSceneAsync(string projectId, string episode, string scene)
    {
        await InitAsync();
        var allProjectTakes = await GetTakesForProjectAsync(projectId);

        var queryEpisodes = GetTokens(episode);
        var queryScenes = GetTokens(scene);

        return allProjectTakes
            .Where(t =>
            {
                var takeEpisodes = GetTokens(t.Episode);
                var takeScenes = GetTokens(t.Scene);
                bool episodeMatch = !queryEpisodes.Any() || !takeEpisodes.Any() || takeEpisodes.Intersect(queryEpisodes, StringComparer.OrdinalIgnoreCase).Any();
                bool sceneMatch = !queryScenes.Any() || !takeScenes.Any() || takeScenes.Intersect(queryScenes, StringComparer.OrdinalIgnoreCase).Any();
                return episodeMatch && sceneMatch;
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    public async Task<List<Day>> GetDaysForProjectAsync(string projectId)
    {
        await InitAsync();
        return await _db.Table<Day>().Where(d => d.ProjectId == projectId).OrderByDescending(d => d.CalendarDate).ToListAsync();
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await InitAsync();
        return await _db.Table<Project>().OrderBy(p => p.Name).ToListAsync();
    }
}