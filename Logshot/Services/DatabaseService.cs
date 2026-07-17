using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using Logshot.Models;

namespace Logshot.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection _db;

    public DatabaseService()
    {
        // Empty constructor. We initialize asynchronously below to keep the app startup fast.
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

    public async Task<int> SaveTakeAsync(Take take)
    {
        await InitAsync();
        // InsertOrReplaceAsync checks the ID. If it's new, it inserts. If it exists, it updates perfectly.
        return await _db.InsertOrReplaceAsync(take);
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

    public async Task<int> SaveDayAsync(Day day)
    {
        await InitAsync();
        return await _db.InsertOrReplaceAsync(day);
    }

    public async Task<int> SaveProjectAsync(Project project)
    {
        await InitAsync();
        return await _db.InsertOrReplaceAsync(project);
    }

    public async Task<int> DeleteTakeAsync(Take take)
    {
        await InitAsync();
        return await _db.DeleteAsync(take);
    }
}