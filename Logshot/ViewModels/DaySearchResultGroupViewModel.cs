using System;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class DaySearchResultGroupViewModel : DayViewModel
{
    public DaySearchResultGroupViewModel(DatabaseService databaseService) : base(databaseService)
    {
    }

    public string DayHeader => $"Day {ShootDayNumber} — {CalendarDate:MMM dd, yyyy}";
}