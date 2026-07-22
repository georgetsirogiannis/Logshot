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

    public DatabaseService()
    {
        // Empty constructor. We initialize asynchronously below to keep the app startup fast.
    }

    // --- Write Queue: makes sure saves finish in the order they were made, and lets us wait for them before the app closes ---
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
        // If the connection is already active, don't do anything
        if (_db is not null)
            return;

        // Environment.SpecialFolder.LocalApplicationData is cross-platform magic. 
        // On Windows it goes to AppData/Local. On Android, it goes to the app's secure internal storage.
        string databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "logshot.db");

        _db = new SQLiteAsyncConnection(databasePath);

        // This reads your C# Models and automatically builds the SQL tables
        await _db.CreateTableAsync<Project>();
        await _db.CreateTableAsync<Day>();
        await _db.CreateTableAsync<Take>();
    }

    // --- CRUD OPERATIONS (Create, Read, Update, Delete) ---

    public Task<int> SaveTakeAsync(Take take)
    {
        return Enqueue(async () =>
        {
            await InitAsync();
            // InsertOrReplaceAsync checks the ID. If it's new, it inserts. If it exists, it updates perfectly.
            return await _db.InsertOrReplaceAsync(take);
        });
    }

    public async Task<List<Take>> GetTakesForDayAsync(string dayId)
    {
        await InitAsync();
        // Grabs all takes for a specific day, respecting your custom SequenceOrder for drag-and-drop
        return await _db.Table<Take>()
                        .Where(t => t.DayId == dayId)
                        .OrderBy(t => t.SequenceOrder)
                        .ToListAsync();
    }

    public Task<int> SaveDayAsync(Day day)
    {
        return Enqueue(async () =>
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(day);
        });
    }

    public Task<int> SaveProjectAsync(Project project)
    {
        return Enqueue(async () =>
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(project);
        });
    }

    public async Task<int> DeleteTakeAsync(Take take)
    {
        await InitAsync();
        return await _db.DeleteAsync(take);
    }

    // --- QUERY OPERATIONS (For Cross-Day Continuity) ---

    /// <summary>
    /// Get all takes for a project across all days (for historical queries)
    /// </summary>
    public async Task<List<Take>> GetTakesForProjectAsync(string projectId)
    {
        await InitAsync();

        // Get all days in the project first
        var days = await _db.Table<Day>()
                           .Where(d => d.ProjectId == projectId)
                           .ToListAsync();

        // Get all takes for those days
        var dayIds = days.Select(d => d.Id).ToList();

        if (dayIds.Count == 0)
            return new List<Take>();

        var takes = new List<Take>();
        foreach (var dayId in dayIds)
        {
            var takesForDay = await _db.Table<Take>()
                                      .Where(t => t.DayId == dayId)
                                      .ToListAsync();
            takes.AddRange(takesForDay);
        }

        return takes.OrderBy(t => t.CreatedAt).ToList();
    }

   
   
    /// Helper to tokenize episode or scene strings by line breaks, spaces, commas, or hyphens.
    /// </summary>
    public static List<string> GetTokens(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();
        char[] separators = new[] { '\r', '\n', ' ', ',', '-' };
        return input.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
    }

    /// <summary>
    /// Get all takes for a specific Episode/Scene combination across a project.
    /// Supports "Multiple Scenes Intelligence" by matching overlapping scene tokens.
    /// </summary>
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

    /// <summary>
    /// Get a specific day by ID
    /// </summary>
    public async Task<Day> GetDayAsync(string dayId)
    {
        await InitAsync();
        return await _db.FindAsync<Day>(dayId);
    }

    /// <summary>
    /// Get all days for a project
    /// </summary>
    public async Task<List<Day>> GetDaysForProjectAsync(string projectId)
    {
        await InitAsync();
        return await _db.Table<Day>()
                       .Where(d => d.ProjectId == projectId)
                       .OrderByDescending(d => d.CalendarDate)
                       .ToListAsync();
    }

    /// <summary>
    /// Get all projects
    /// </summary>
    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await InitAsync();
        return await _db.Table<Project>()
                       .OrderBy(p => p.Name)
                       .ToListAsync();
    }

    /// <summary>
    /// Delete a day (and its takes) from the database
    /// </summary>
    public async Task<int> DeleteDayAsync(Day day)
    {
        await InitAsync();
        var takes = await GetTakesForDayAsync(day.Id);
        foreach (var take in takes)
        {
            await _db.DeleteAsync(take);
        }
        return await _db.DeleteAsync(day);
    }

    /// <summary>
    /// Delete a project (and its days/takes) from the database
    /// </summary>
    public async Task<int> DeleteProjectAsync(Project project)
    {
        await InitAsync();
        var days = await GetDaysForProjectAsync(project.Id);
        foreach (var day in days)
        {
            await DeleteDayAsync(day);
        }
        return await _db.DeleteAsync(project);
    }
}