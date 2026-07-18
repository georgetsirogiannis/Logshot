using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Logshot.Models;

namespace Logshot.Services;

/// <summary>
/// Cross-day continuity engine that looks up historical data for Episode/Scene
/// to find the next shot number and pre-fill camera setups.
/// </summary>
public class ContinuityService
{
    private readonly DatabaseService _databaseService;
    private readonly CameraDataManager _cameraDataManager;

    public class ContinuityData
    {
        /// <summary>
        /// The next shot number to suggest for this Episode/Scene
        /// </summary>
        public int NextShotNumber { get; set; } = 1;

        /// <summary>
        /// Camera setup to inherit from last occurrence of this Episode/Scene
        /// </summary>
        public string InheritedCameraData { get; set; } = "{}";

        /// <summary>
        /// The most recent take with this Episode/Scene (if any)
        /// </summary>
        public Take? LastReferenceTake { get; set; }

        /// <summary>
        /// Whether we found any historical data for this Episode/Scene
        /// </summary>
        public bool HasHistory { get; set; } = false;
    }

    public ContinuityService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _cameraDataManager = new CameraDataManager();
    }

    public ContinuityService(DatabaseService databaseService, CameraDataManager cameraDataManager)
    {
        _databaseService = databaseService;
        _cameraDataManager = cameraDataManager;
    }

    /// <summary>
    /// Look up continuity data for a specific Episode/Scene combination within a project
    /// </summary>
    public async Task<ContinuityData> GetContinuityDataAsync(string projectId, string episode, string scene)
    {
        var continuity = new ContinuityData();

        // Get all takes with this Episode/Scene combination across the project
        var historicalTakes = await _databaseService.GetTakesForEpisodeSceneAsync(projectId, episode, scene);

        if (historicalTakes.Count == 0)
        {
            // No history - start fresh with shot 1
            continuity.NextShotNumber = 1;
            continuity.HasHistory = false;
            return continuity;
        }

        // We have history - use the most recent take as reference
        continuity.HasHistory = true;
        continuity.LastReferenceTake = historicalTakes.First(); // Already ordered descending by date

        // Find the maximum shot number we've seen for this Episode/Scene
        int maxShot = historicalTakes.Max(t => t.Shot);
        continuity.NextShotNumber = maxShot + 1;

        // Inherit camera setup from the most recent take with this Episode/Scene
        continuity.InheritedCameraData = continuity.LastReferenceTake.CameraData;

        return continuity;
    }

    /// <summary>
    /// Get the next shot number for an Episode/Scene (queries history)
    /// </summary>
    public async Task<int> GetNextShotNumberAsync(string projectId, string episode, string scene)
    {
        var continuity = await GetContinuityDataAsync(projectId, episode, scene);
        return continuity.NextShotNumber;
    }

    /// <summary>
    /// Get the camera setup from the last occurrence of this Episode/Scene
    /// </summary>
    public async Task<string> GetInheritedCameraDataAsync(string projectId, string episode, string scene)
    {
        var continuity = await GetContinuityDataAsync(projectId, episode, scene);
        return continuity.InheritedCameraData;
    }

    /// <summary>
    /// Get all unique Episode/Scene combinations that exist in a project
    /// Useful for autocomplete or search UI
    /// </summary>
    public async Task<List<(string Episode, string Scene)>> GetUniqueEpisodeScenesAsync(string projectId)
    {
        var allTakes = await _databaseService.GetTakesForProjectAsync(projectId);

        return allTakes
            .Where(t => !string.IsNullOrWhiteSpace(t.Episode) && !string.IsNullOrWhiteSpace(t.Scene))
            .GroupBy(t => (t.Episode, t.Scene))
            .Select(g => g.Key)
            .OrderBy(es => es.Episode)
            .ThenBy(es => es.Scene)
            .ToList();
    }

    /// <summary>
    /// Get statistics for an Episode/Scene (shot count, date range, etc.)
    /// </summary>
    public async Task<EpisodeSceneStatistics> GetEpisodeSceneStatsAsync(string projectId, string episode, string scene)
    {
        var takes = await _databaseService.GetTakesForEpisodeSceneAsync(projectId, episode, scene);

        var stats = new EpisodeSceneStatistics
        {
            Episode = episode,
            Scene = scene,
            TotalTakes = takes.Count,
            MaxShotNumber = takes.Count > 0 ? takes.Max(t => t.Shot) : 0,
            MinShotNumber = takes.Count > 0 ? takes.Min(t => t.Shot) : 0,
            FirstRecordedDate = takes.Count > 0 ? takes.Last().CreatedAt : DateTime.UtcNow,
            LastRecordedDate = takes.Count > 0 ? takes.First().CreatedAt : DateTime.UtcNow,
            DaysWithThisScene = takes.Select(t => t.DayId).Distinct().Count()
        };

        return stats;
    }

    /// <summary>
    /// Statistics about an Episode/Scene's history
    /// </summary>
    public class EpisodeSceneStatistics
    {
        public string Episode { get; set; } = string.Empty;
        public string Scene { get; set; } = string.Empty;
        public int TotalTakes { get; set; }
        public int MaxShotNumber { get; set; }
        public int MinShotNumber { get; set; }
        public DateTime FirstRecordedDate { get; set; }
        public DateTime LastRecordedDate { get; set; }
        public int DaysWithThisScene { get; set; }
    }

    /// <summary>
    /// Get the most recent N takes globally (for "recent scopes" UI)
    /// </summary>
    public async Task<List<Take>> GetRecentTakesAsync(string projectId, int limit = 10)
    {
        var allTakes = await _databaseService.GetTakesForProjectAsync(projectId);
        return allTakes.Take(limit).ToList();
    }

    /// <summary>
    /// Get all takes from a specific previous day (for referencing earlier setups)
    /// </summary>
    public async Task<List<Take>> GetTakesFromPreviousDayAsync(string projectId, string currentDayId)
    {
        var currentDay = await _databaseService.GetDayAsync(currentDayId);
        if (currentDay == null)
            return new List<Take>();

        var allDays = await _databaseService.GetDaysForProjectAsync(projectId);
        var previousDay = allDays.FirstOrDefault(d => d.CalendarDate < currentDay.CalendarDate);

        if (previousDay == null)
            return new List<Take>();

        return await _databaseService.GetTakesForDayAsync(previousDay.Id);
    }
}
