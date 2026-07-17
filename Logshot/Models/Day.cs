using SQLite;
using System;

namespace Logshot.Models;

[Table("Days")]
public class Day
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed] // Speeds up queries when searching for days inside a project
    public string ProjectId { get; set; } = string.Empty;

    public string ShootDayNumber { get; set; } = string.Empty; // String to allow "54B"
    public DateTime CalendarDate { get; set; } = DateTime.Today;
    public string GeneralNotes { get; set; } = string.Empty;
    public string TopScribbleNotes { get; set; } = string.Empty;
    public bool IsFinalized { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}