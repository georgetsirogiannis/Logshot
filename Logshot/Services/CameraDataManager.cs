using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Logshot.Services;

/// <summary>
/// Manages camera data for takes, handling dynamic camera addition/removal
/// and JSON serialization/deserialization of camera information.
/// </summary>
public class CameraDataManager
{
    /// <summary>
    /// Default cameras that every day starts with
    /// </summary>
    public static readonly string[] DEFAULT_CAMERAS = { "CAM A", "CAM B" };

    /// <summary>
    /// Represents the state of a camera for a given take
    /// </summary>
    public class CameraState
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "active"; // "active", "voided", "strikethrough"

        [JsonPropertyName("timestamp")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Root camera data structure stored as JSON in Take.CameraData
    /// </summary>
    public class CameraDataStructure
    {
        [JsonPropertyName("cameras")]
        public Dictionary<string, CameraState> Cameras { get; set; } = new();

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Parse camera data JSON string into structured format
    /// </summary>
    public CameraDataStructure ParseCameraData(string jsonData)
    {
        if (string.IsNullOrWhiteSpace(jsonData) || jsonData == "{}")
            return new CameraDataStructure();

        try
        {
            return JsonSerializer.Deserialize<CameraDataStructure>(jsonData, JsonOptions)
                   ?? new CameraDataStructure();
        }
        catch
        {
            return new CameraDataStructure();
        }
    }

    /// <summary>
    /// Serialize camera data structure back to JSON string
    /// </summary>
    public string SerializeCameraData(CameraDataStructure data)
    {
        if (data?.Cameras == null || data.Cameras.Count == 0)
            return "{}";

        data.LastUpdated = DateTime.UtcNow;
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>
    /// Initialize default cameras for a new take
    /// </summary>
    public CameraDataStructure InitializeDefaultCameras()
    {
        var data = new CameraDataStructure();
        foreach (var camera in DEFAULT_CAMERAS)
        {
            data.Cameras[camera] = new CameraState
            {
                Label = camera,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };
        }
        return data;
    }

    /// <summary>
    /// Add a new camera to the camera data
    /// </summary>
    public CameraDataStructure AddCamera(CameraDataStructure data, string cameraLabel)
    {
        if (data?.Cameras == null)
            data = new CameraDataStructure();

        if (data.Cameras.ContainsKey(cameraLabel))
            return data; // Camera already exists

        data.Cameras[cameraLabel] = new CameraState
        {
            Label = cameraLabel,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        data.LastUpdated = DateTime.UtcNow;
        return data;
    }

    /// <summary>
    /// Remove a camera from the camera data
    /// </summary>
    public CameraDataStructure RemoveCamera(CameraDataStructure data, string cameraLabel)
    {
        if (data?.Cameras != null && data.Cameras.Remove(cameraLabel))
        {
            data.LastUpdated = DateTime.UtcNow;
        }
        return data;
    }

    /// <summary>
    /// Get list of all active camera labels in order
    /// </summary>
    public List<string> GetActiveCameraLabels(CameraDataStructure data)
    {
        if (data?.Cameras == null || data.Cameras.Count == 0)
            return new List<string>(DEFAULT_CAMERAS);

        return data.Cameras
            .OrderBy(kvp => GetCameraOrder(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Mark a camera as strikethrough/voided for a take added after the camera existed
    /// </summary>
    public CameraDataStructure MarkCameraStrikethrough(CameraDataStructure data, string cameraLabel, DateTime cameraCreatedAt, DateTime takeRecordedAt)
    {
        if (data?.Cameras == null || !data.Cameras.ContainsKey(cameraLabel))
            return data;

        // If camera was created after this take was recorded, mark it as strikethrough
        if (cameraCreatedAt > takeRecordedAt)
        {
            data.Cameras[cameraLabel].Status = "strikethrough";
        }

        return data;
    }

    /// <summary>
    /// Get the natural sort order for cameras (CAM A before CAM B, etc.)
    /// </summary>
    private int GetCameraOrder(string label)
    {
        return Array.IndexOf(DEFAULT_CAMERAS, label) >= 0
            ? Array.IndexOf(DEFAULT_CAMERAS, label)
            : int.MaxValue; // Custom cameras go to the end
    }

    /// <summary>
    /// Check if a camera exists and is active (not strikethrough)
    /// </summary>
    public bool IsCameraActive(CameraDataStructure data, string cameraLabel)
    {
        if (data?.Cameras == null || !data.Cameras.TryGetValue(cameraLabel, out var camera))
            return false;

        return camera.Status == "active";
    }

    /// <summary>
    /// Check if a camera is marked as strikethrough (not present when take was recorded)
    /// </summary>
    public bool IsCameraStrikethrough(CameraDataStructure data, string cameraLabel)
    {
        if (data?.Cameras == null || !data.Cameras.TryGetValue(cameraLabel, out var camera))
            return false;

        return camera.Status == "strikethrough";
    }

    /// <summary>
    /// Get camera creation timestamp
    /// </summary>
    public DateTime? GetCameraCreatedAt(CameraDataStructure data, string cameraLabel)
    {
        if (data?.Cameras == null || !data.Cameras.TryGetValue(cameraLabel, out var camera))
            return null;

        return camera.CreatedAt;
    }
}
