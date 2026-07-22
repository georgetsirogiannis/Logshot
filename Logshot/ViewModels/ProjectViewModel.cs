using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Models;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class ProjectViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _director = string.Empty;

    [ObservableProperty]
    private string _dop = string.Empty;

    [ObservableProperty]
    private string _productionCompany = string.Empty;

    [ObservableProperty]
    private string _scriptSupervisor = string.Empty;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private ObservableCollection<DayViewModel> _days = new();

    public ProjectViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// Load project data from the model
    /// </summary>
    public void LoadFromModel(Project project)
    {
        Id = project.Id;
        Name = project.Name;
        Director = project.Director;
        Dop = project.Dop;
        ProductionCompany = project.ProductionCompany;
        ScriptSupervisor = project.ScriptSupervisor;
        CreatedAt = project.CreatedAt;
    }

    /// <summary>
    /// Convert this ViewModel back to a model for database persistence
    /// </summary>
    public Project ToModel()
    {
        return new Project
        {
            Id = Id,
            Name = Name,
            Director = Director,
            Dop = Dop,
            ProductionCompany = ProductionCompany,
            ScriptSupervisor = ScriptSupervisor,
            CreatedAt = CreatedAt
        };
    }

    [RelayCommand]
    public async Task SaveProject()
    {
        await _databaseService.SaveProjectAsync(ToModel());
    }

    [RelayCommand]
    public async Task LoadDays()
    {
        var days = await _databaseService.GetDaysForProjectAsync(Id);

        Days.Clear();
        foreach (var day in days)
        {
            var dayVM = new DayViewModel(_databaseService);
            dayVM.LoadFromModel(day);
            await dayVM.LoadTakesCommand.ExecuteAsync(null);
            Days.Add(dayVM);
            SortDays();
        }

        SortDays();
    }

    [RelayCommand]
    public async Task AddDay()
    {
        string nextDayNumber = "1";
        if (Days != null && Days.Count > 0)
        {
            var lastDay = Days.Last();
            if (int.TryParse(lastDay.ShootDayNumber, out int lastNum))
            {
                nextDayNumber = (lastNum + 1).ToString();
            }
            else
            {
                nextDayNumber = (Days.Count + 1).ToString();
            }
        }

        var newDay = new Day
        {
            ProjectId = Id,
            ShootDayNumber = nextDayNumber,
            CalendarDate = DateTime.Today
        };
        var dayVM = new DayViewModel(_databaseService);
        dayVM.LoadFromModel(newDay);

        await dayVM.SaveDayCommand.ExecuteAsync(null);
        Days.Add(dayVM);
    }

    [RelayCommand]
    public async Task DeleteDay(DayViewModel? day)
    {
        if (day != null && Days.Contains(day))
        {
            Days.Remove(day);
            await _databaseService.DeleteDayAsync(day.ToModel());
        }
    }

    /// <summary>
    /// Re-sorts Days numerically by ShootDayNumber, using Move so the ListBox's
    /// current selection isn't disturbed.
    /// </summary>
    public void SortDays()
    {
        var ordered = Days
            .OrderBy(d => Day.GetSortableDayNumber(d.ShootDayNumber), StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            int currentIndex = Days.IndexOf(ordered[i]);
            if (currentIndex != i)
                Days.Move(currentIndex, i);
        }
    }
}