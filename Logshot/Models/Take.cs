using SQLite;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace Logshot.Models;

[Table("Takes")]
public class Take
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string DayId { get; set; } = string.Empty;

    public int SequenceOrder { get; set; }

    // Core Hierarchy
    public string Episode { get; set; } = string.Empty; // String to allow "10-11"
    public string Scene { get; set; } = string.Empty; // String to allow "28-29"
    public int Shot { get; set; }
    public int TakeNumber { get; set; } // Named 'TakeNumber' to avoid class name conflict

    // Camera & Sound
    public string CameraData { get; set; } = "{}"; // JSON for dynamic multi-cam columns
    public string SoundNotes { get; set; } = string.Empty;

    // Gestures & Modifiers
    public string TakeNotes { get; set; } = string.Empty;
    public int FalseStartCount { get; set; } = 0;
    public bool IsLongStart { get; set; } = false;
    public bool IsCircled { get; set; } = false;
    public bool IsFailed { get; set; } = false;
    public bool IsPickup { get; set; } = false;
    public bool IsBlooper { get; set; } = false;

    // False Clip Tracking
    public string VoidCameraLabels { get; set; } = "[]"; // JSON array for specific voided cameras

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if this take has any voided cameras (ΑΚΥΡΟ CLIP marked)
    /// </summary>
    public bool HasVoidedCameras
    {
        get
        {
            try
            {
                if (string.IsNullOrWhiteSpace(VoidCameraLabels))
                    return false;

                var voidedCameras = JsonSerializer.Deserialize<List<string>>(VoidCameraLabels);
                return voidedCameras != null && voidedCameras.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}