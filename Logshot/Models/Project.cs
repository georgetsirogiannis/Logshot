using SQLite;
using System;

namespace Logshot.Models;

[Table("Projects")]
public class Project
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;
    public string Director { get; set; } = string.Empty;
    public string Dop { get; set; } = string.Empty;
    public string ProductionCompany { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}