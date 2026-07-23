using SQLite;
using System;

namespace Logshot.Models;

[Table("SyncQueue")]
public class SyncQueueItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string EntityType { get; set; } = string.Empty; // e.g. "Project", "Day", "Take"
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // e.g. "Upsert", "Delete"

    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}