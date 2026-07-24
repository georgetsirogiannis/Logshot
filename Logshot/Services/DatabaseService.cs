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

    public string GetDatabasePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "logshot.db");

    public async Task InitAsync()
    {
        if (_db is not null) return;

        string databasePath = GetDatabasePath();
        _db = new SQLiteAsyncConnection(databasePath);

        await _db.CreateTableAsync<Project>();
        await _db.CreateTableAsync<Day>();
        await _db.CreateTableAsync<Take>();
        await _db.CreateTableAsync<SyncQueueItem>(); // New outbox table
    }

    public async Task CloseAsync()
    {
        if (_db != null)
        {
            await _db.CloseAsync();
            _db = null!;
        }
    }

    public async Task ExportDatabaseAsync(string destPath, string? specificProjectId)
    {
        await WaitForPendingWritesAsync();
        var sourcePath = GetDatabasePath();

        if (string.IsNullOrEmpty(specificProjectId))
        {
            // Export the entire DB
            File.Copy(sourcePath, destPath, true);
        }
        else
        {
            // Export just one project by copying everything and pruning the irrelevant data
            File.Copy(sourcePath, destPath, true);
            var tempDb = new SQLiteAsyncConnection(destPath);
            await tempDb.ExecuteAsync("DELETE FROM Projects WHERE Id != ?", specificProjectId);
            await tempDb.ExecuteAsync("DELETE FROM Days WHERE ProjectId != ?", specificProjectId);
            await tempDb.ExecuteAsync("DELETE FROM Takes WHERE DayId NOT IN (SELECT Id FROM Days WHERE ProjectId = ?)", specificProjectId);
            await tempDb.ExecuteAsync("DELETE FROM SyncQueue"); // Don't carry over sync outbox for single exports
            await tempDb.ExecuteAsync("VACUUM"); // Shrinks the DB file footprint
            await tempDb.CloseAsync();
        }
    }

    public async Task<(bool isValid, string summaryMessage)> GetImportSummaryAsync(string importPath)
    {
        var importDb = new SQLiteAsyncConnection(importPath);
        int importedTakeCount = 0;
        int importedProjectCount = 0;

        try
        {
            importedTakeCount = await importDb.Table<Take>().CountAsync();
            importedProjectCount = await importDb.Table<Project>().CountAsync();
        }
        catch
        {
            await importDb.CloseAsync();
            return (false, "The selected file does not appear to be a valid Logshot database format.");
        }
        await importDb.CloseAsync();

        string msg = $"You are about to merge {importedProjectCount} project(s) and {importedTakeCount} take(s) into your current app.\n\n" +
                     $"Only new records (that don't already exist) will be added. Existing projects, days, and takes will remain untouched.\n\n" +
                     $"Do you want to proceed?";

        return (true, msg);
    }

    public async Task MergeDatabaseAsync(string importPath)
    {
        // 1. Load imported data
        var importDb = new SQLiteAsyncConnection(importPath);
        var importedProjects = await importDb.Table<Project>().ToListAsync();
        var importedDays = await importDb.Table<Day>().ToListAsync();
        var importedTakes = await importDb.Table<Take>().ToListAsync();
        await importDb.CloseAsync();

        await InitAsync();

        var insertedSyncItems = new List<SyncQueueItem>();

        // 2. Merge only new records (non-destructive)
        await _db.RunInTransactionAsync(tran =>
        {
            // Projects
            foreach (var p in importedProjects)
            {
                if (tran.Find<Project>(p.Id) == null)
                {
                    tran.Insert(p);
                    insertedSyncItems.Add(new SyncQueueItem { EntityType = "Project", EntityId = p.Id, Action = "Upsert" });
                }
            }

            // Days
            foreach (var d in importedDays)
            {
                if (tran.Find<Day>(d.Id) == null)
                {
                    tran.Insert(d);
                    insertedSyncItems.Add(new SyncQueueItem { EntityType = "Day", EntityId = d.Id, Action = "Upsert" });
                }
            }

            // Takes
            foreach (var t in importedTakes)
            {
                if (tran.Find<Take>(t.Id) == null)
                {
                    tran.Insert(t);
                    insertedSyncItems.Add(new SyncQueueItem { EntityType = "Take", EntityId = t.Id, Action = "Upsert" });
                }
            }
        });

        // 3. Queue only actually inserted items for cloud sync
        if (insertedSyncItems.Count > 0)
        {
            await _db.InsertAllAsync(insertedSyncItems);
            OnDataChanged?.Invoke();
        }
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

    public async Task<int> EnqueueAllExistingDataForSyncAsync()
    {
        await InitAsync();

        var projects = await _db.Table<Project>().ToListAsync();
        var days = await _db.Table<Day>().ToListAsync();
        var takes = await _db.Table<Take>().ToListAsync();

        foreach (var p in projects)
            await _db.InsertAsync(new SyncQueueItem { EntityType = "Project", EntityId = p.Id, Action = "Upsert" });

        foreach (var d in days)
            await _db.InsertAsync(new SyncQueueItem { EntityType = "Day", EntityId = d.Id, Action = "Upsert" });

        foreach (var t in takes)
            await _db.InsertAsync(new SyncQueueItem { EntityType = "Take", EntityId = t.Id, Action = "Upsert" });

        return projects.Count + days.Count + takes.Count;
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