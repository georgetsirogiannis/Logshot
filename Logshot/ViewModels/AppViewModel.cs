using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Models;
using Logshot.Services;

namespace Logshot.ViewModels;

public partial class AppViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private ObservableCollection<ProjectViewModel> _projects = new();

    [ObservableProperty]
    private ProjectViewModel? _currentProject;

    [ObservableProperty]
    private DayViewModel? _currentDay;

    [ObservableProperty]
    private string _appTitle = "Logshot";

    [ObservableProperty]
    private bool _isLoading = false;

    public AppViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// Initialize the app by loading all projects
    /// </summary>
    [RelayCommand]
    public async Task InitializeApp()
    {
        IsLoading = true;
        try
        {
            await LoadAllProjectsCommand.ExecuteAsync(null);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Load all projects from the database
    /// </summary>
    [RelayCommand]
    public async Task LoadAllProjects()
    {
        IsLoading = true;
        try
        {
            var projects = await _databaseService.GetAllProjectsAsync();

            Projects.Clear();
            foreach (var project in projects)
            {
                var projectVM = new ProjectViewModel(_databaseService);
                projectVM.LoadFromModel(project);
                await projectVM.LoadDaysCommand.ExecuteAsync(null);
                Projects.Add(projectVM);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Create a new project
    /// </summary>
    [RelayCommand]
    public async Task CreateProject(string projectName)
    {
        // Stub for Phase 2 implementation
        var newProject = new Project { Name = projectName };
        var projectVM = new ProjectViewModel(_databaseService);
        projectVM.LoadFromModel(newProject);

        await projectVM.SaveProjectCommand.ExecuteAsync(null);
        Projects.Add(projectVM);
    }

    /// <summary>
    /// Delete a project
    /// </summary>
    [RelayCommand]
    public async Task DeleteProject(ProjectViewModel project)
    {
        if (Projects.Contains(project))
        {
            Projects.Remove(project);
            await _databaseService.DeleteProjectAsync(project.ToModel());
        }

        if (CurrentProject?.Id == project.Id)
        {
            CurrentProject = null;
            CurrentDay = null;
        }
    }

    /// <summary>
    /// Select a project as current and load its days
    /// </summary>
    [RelayCommand]
    public async Task SelectProject(ProjectViewModel project)
    {
        CurrentProject = project;
        await CurrentProject.LoadDaysCommand.ExecuteAsync(null);
        CurrentDay = null; // Clear day selection when switching projects
    }

    /// <summary>
    /// Create a new day in the current project
    /// </summary>
    [RelayCommand]
    public async Task CreateDay(string shootDayNumber)
    {
        if (CurrentProject is null)
            return;

        // Stub for Phase 2 implementation
        var newDay = new Day { ProjectId = CurrentProject.Id, ShootDayNumber = shootDayNumber };
        var dayVM = new DayViewModel(_databaseService);
        dayVM.LoadFromModel(newDay);

        await dayVM.SaveDayCommand.ExecuteAsync(null);
        CurrentProject.Days.Add(dayVM);
    }

    /// <summary>
    /// Delete a day from the current project
    /// </summary>
    [RelayCommand]
    public async Task DeleteDay(DayViewModel day)
    {
        if (CurrentProject?.Days.Contains(day) == true)
        {
            CurrentProject.Days.Remove(day);
            await _databaseService.DeleteDayAsync(day.ToModel());
            if (CurrentDay?.Id == day.Id)
            {
                CurrentDay = null;
            }
        }
    }

    /// <summary>
    /// Select a day as current and load its takes
    /// </summary>
    [RelayCommand]
    public async Task SelectDay(DayViewModel day)
    {
        CurrentDay = day;
        await CurrentDay.LoadTakesCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Create a new take in the current day
    /// </summary>
    [RelayCommand]
    public async Task CreateTake()
    {
        if (CurrentDay is null)
            return;

        // Stub for Phase 2 implementation
        var newTake = new Take 
        { 
            DayId = CurrentDay.Id,
            SequenceOrder = CurrentDay.Takes.Count,
            Shot = CurrentDay.CurrentShot
        };
        var takeVM = new TakeViewModel(_databaseService);
        takeVM.LoadFromModel(newTake);

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        CurrentDay.Takes.Add(takeVM);
        await CurrentDay.UpdateTotalTakesCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Delete a take from the current day
    /// </summary>
    [RelayCommand]
    public async Task DeleteTake(TakeViewModel take)
    {
        if (CurrentDay?.Takes.Contains(take) == true)
        {
            await CurrentDay.DeleteTakeCommand.ExecuteAsync(take);
            await CurrentDay.UpdateTotalTakesCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Export the current day to PDF
    /// </summary>
    [RelayCommand]
    public async Task ExportDayToPdf()
    {
        // Stub for Phase 6 implementation - PDF Export Engine
        if (CurrentDay is null)
            return;

        // TODO: Use QuestPDF to generate PDF
        // TODO: Include all takes, metadata, and custom rendering
    }

    /// <summary>
    /// Sync changes to Supabase
    /// </summary>
    [RelayCommand]
    public async Task SyncToSupabase()
    {
        // Stub for Phase 7 implementation - The Outbox Sync Worker
        // TODO: Push all pending changes to Supabase
    }
}
