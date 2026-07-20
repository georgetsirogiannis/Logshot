using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    private bool IsSoundOnlyTake(Take take)
    {
        if (string.IsNullOrWhiteSpace(take.SoundNotes))
            return false;

        var data = _cameraDataManager.ParseCameraData(take.CameraData);
        if (data.Cameras.Count == 0)
            return false;

        foreach (var kvp in data.Cameras)
        {
            if (!kvp.Value.NoRoll && kvp.Value.Status != "strikethrough")
                return false;
        }

        return true;
    }

    private bool HasVoidedCamerasTake(Take take)
    {
        if (string.IsNullOrWhiteSpace(take.VoidCameraLabels))
            return false;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(take.VoidCameraLabels);
            return list != null && list.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Look up continuity data for a specific Episode/Scene combination within a project.
    /// Uses smallest-unused-positive-integer logic across overlapping scene tokens.
    /// </summary>
    public async Task<ContinuityData> GetContinuityDataAsync(string projectId, string episode, string scene)
    {
        var continuity = new ContinuityData();

        var allProjectTakes = await _databaseService.GetTakesForProjectAsync(projectId);
        var nonVoidedTakes = allProjectTakes.Where(t => !HasVoidedCamerasTake(t) && !IsSoundOnlyTake(t)).ToList();

        if (nonVoidedTakes.Count == 0)
        {
            continuity.NextShotNumber = 1;
            continuity.HasHistory = false;
            return continuity;
        }

        var queryEpisodes = DatabaseService.GetTokens(episode);
        var queryScenes = DatabaseService.GetTokens(scene);

        // Find all historical takes that overlap with any of the queried episodes and scenes
        var matchingTakes = nonVoidedTakes.Where(t =>
        {
            var takeEpisodes = DatabaseService.GetTokens(t.Episode);
            var takeScenes = DatabaseService.GetTokens(t.Scene);

            bool episodeMatch = !queryEpisodes.Any() || !takeEpisodes.Any() || takeEpisodes.Intersect(queryEpisodes, StringComparer.OrdinalIgnoreCase).Any();
            bool sceneMatch = !queryScenes.Any() || !takeScenes.Any() || takeScenes.Intersect(queryScenes, StringComparer.OrdinalIgnoreCase).Any();

            return episodeMatch && sceneMatch;
        }).OrderByDescending(t => t.CreatedAt).ToList();

        if (matchingTakes.Count == 0)
        {
            continuity.NextShotNumber = 1;
            continuity.HasHistory = false;
            return continuity;
        }

        continuity.HasHistory = true;
        continuity.LastReferenceTake = matchingTakes.First();
        continuity.InheritedCameraData = continuity.LastReferenceTake.CameraData;

        // Collect all shot numbers already used by these scene(s)
        var usedShots = matchingTakes.Select(t => t.Shot).Where(s => s > 0).ToHashSet();

        // Find the smallest positive integer not present in usedShots
        int nextShot = 1;
        while (usedShots.Contains(nextShot))
        {
            nextShot++;
        }
        continuity.NextShotNumber = nextShot;

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
            .Where(t => !string.IsNullOrWhiteSpace(t.Episode) && !string.IsNullOrWhiteSpace(t.Scene) && !IsSoundOnlyTake(t))
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

        // For stats, only count non-voided takes and non-sound-only rows
        var nonVoidedTakes = takes.Where(t => !HasVoidedCamerasTake(t) && !IsSoundOnlyTake(t)).ToList();

        var stats = new EpisodeSceneStatistics
        {
            Episode = episode,
            Scene = scene,
            TotalTakes = nonVoidedTakes.Count,
            MaxShotNumber = nonVoidedTakes.Count > 0 ? nonVoidedTakes.Max(t => t.Shot) : 0,
            MinShotNumber = nonVoidedTakes.Count > 0 ? nonVoidedTakes.Min(t => t.Shot) : 0,
            FirstRecordedDate = nonVoidedTakes.Count > 0 ? nonVoidedTakes.Last().CreatedAt : DateTime.UtcNow,
            LastRecordedDate = nonVoidedTakes.Count > 0 ? nonVoidedTakes.First().CreatedAt : DateTime.UtcNow,
            DaysWithThisScene = nonVoidedTakes.Select(t => t.DayId).Distinct().Count()
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
        return allTakes.Where(t => !IsSoundOnlyTake(t)).Take(limit).ToList();
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

        var takes = await _databaseService.GetTakesForDayAsync(previousDay.Id);
        return takes.Where(t => !IsSoundOnlyTake(t)).ToList();
    }
}