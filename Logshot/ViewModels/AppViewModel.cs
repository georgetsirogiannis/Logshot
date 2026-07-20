using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    // --- Project Dialog & Deletion State ---
    [ObservableProperty]
    private bool _isProjectPopupOpen = false;

    [ObservableProperty]
    private bool _isEditingProjectModal = false;

    [ObservableProperty]
    private string _popupProjectTitle = string.Empty;

    [ObservableProperty]
    private string _popupDirector = string.Empty;

    [ObservableProperty]
    private string _popupDop = string.Empty;

    [ObservableProperty]
    private string _popupProductionCompany = string.Empty;

    [ObservableProperty]
    private string _popupScriptSupervisor = string.Empty;

    [ObservableProperty]
    private bool _isDeleteConfirmationOpen = false;

    [ObservableProperty]
    private ProjectViewModel? _projectToDelete;

    // --- Day Dialog & Deletion State ---
    [ObservableProperty]
    private bool _isDayPopupOpen = false;

    [ObservableProperty]
    private DayViewModel? _dayBeingEdited;

    [ObservableProperty]
    private string _popupShootDayNumber = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _popupCalendarDate = DateTimeOffset.Now;

    [ObservableProperty]
    private bool _isDayDeleteConfirmationOpen = false;

    [ObservableProperty]
    private DayViewModel? _dayToDelete;

    public AppViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    // --- New Scene Dialog State ---
    [ObservableProperty]
    private bool _isAddScenePopupOpen = false;

    [ObservableProperty]
    private string _popupNewEpisode = string.Empty;

    [ObservableProperty]
    private string _popupNewScene = string.Empty;

    [RelayCommand]
    public void OpenAddSceneDialog()
    {
        if (CurrentDay is null) return;
        PopupNewEpisode = string.Empty;
        PopupNewScene = string.Empty;
        IsAddScenePopupOpen = true;
    }

    [RelayCommand]
    public void CancelAddSceneDialog()
    {
        IsAddScenePopupOpen = false;
    }

    [RelayCommand]
    public async Task ConfirmAddSceneDialog()
    {
        if (CurrentDay is null || string.IsNullOrWhiteSpace(PopupNewEpisode) || string.IsNullOrWhiteSpace(PopupNewScene))
            return;

        await CurrentDay.CreateTakeWithContinuity(PopupNewEpisode.Trim(), PopupNewScene.Trim());
        IsAddScenePopupOpen = false;
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
    /// Opens the popup to create a new project
    /// </summary>
    [RelayCommand]
    public void OpenCreateProjectDialog()
    {
        PopupProjectTitle = string.Empty;
        PopupDirector = string.Empty;
        PopupDop = string.Empty;
        PopupProductionCompany = string.Empty;
        PopupScriptSupervisor = string.Empty;
        IsEditingProjectModal = false;
        IsProjectPopupOpen = true;
    }

    /// <summary>
    /// Opens the popup to edit the currently selected project
    /// </summary>
    [RelayCommand]
    public void OpenEditProjectDialog()
    {
        if (CurrentProject is null) return;

        PopupProjectTitle = CurrentProject.Name;
        PopupDirector = CurrentProject.Director;
        PopupDop = CurrentProject.Dop;
        PopupProductionCompany = CurrentProject.ProductionCompany;
        PopupScriptSupervisor = CurrentProject.ScriptSupervisor;
        IsEditingProjectModal = true;
        IsProjectPopupOpen = true;
    }

    /// <summary>
    /// Saves the project from the popup dialog (Create or Update)
    /// </summary>
    [RelayCommand]
    public async Task SaveProjectDialog()
    {
        if (string.IsNullOrWhiteSpace(PopupProjectTitle))
            return;

        if (IsEditingProjectModal && CurrentProject != null)
        {
            CurrentProject.Name = PopupProjectTitle;
            CurrentProject.Director = PopupDirector;
            CurrentProject.Dop = PopupDop;
            CurrentProject.ProductionCompany = PopupProductionCompany;
            CurrentProject.ScriptSupervisor = PopupScriptSupervisor;
            await CurrentProject.SaveProjectCommand.ExecuteAsync(null);
        }
        else
        {
            var newProject = new Project
            {
                Name = PopupProjectTitle,
                Director = PopupDirector,
                Dop = PopupDop,
                ProductionCompany = PopupProductionCompany,
                ScriptSupervisor = PopupScriptSupervisor
            };
            var projectVM = new ProjectViewModel(_databaseService);
            projectVM.LoadFromModel(newProject);

            await projectVM.SaveProjectCommand.ExecuteAsync(null);
            Projects.Add(projectVM);
            CurrentProject = projectVM;
        }

        IsProjectPopupOpen = false;
    }

    [RelayCommand]
    public void CancelProjectDialog()
    {
        IsProjectPopupOpen = false;
    }

    /// <summary>
    /// Prompts fail-safe confirmation before deleting a project
    /// </summary>
    [RelayCommand]
    public void PromptDeleteProject(ProjectViewModel? project)
    {
        var target = project ?? CurrentProject;
        if (target is null) return;

        ProjectToDelete = target;
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    public async Task ConfirmDeleteProject()
    {
        if (ProjectToDelete != null)
        {
            await DeleteProject(ProjectToDelete);
        }
        IsDeleteConfirmationOpen = false;
        ProjectToDelete = null;
    }

    [RelayCommand]
    public void CancelDeleteProject()
    {
        IsDeleteConfirmationOpen = false;
        ProjectToDelete = null;
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
    /// Opens the popup to edit a shoot day's number and date
    /// </summary>
    [RelayCommand]
    public void OpenEditDayDialog(DayViewModel day)
    {
        if (day is null) return;

        DayBeingEdited = day;
        PopupShootDayNumber = day.ShootDayNumber;
        PopupCalendarDate = new DateTimeOffset(day.CalendarDate);
        IsDayPopupOpen = true;
    }

    /// <summary>
    /// Saves the edited shoot day details
    /// </summary>
    [RelayCommand]
    public async Task SaveDayDialog()
    {
        if (DayBeingEdited is null || string.IsNullOrWhiteSpace(PopupShootDayNumber))
            return;

        DayBeingEdited.ShootDayNumber = PopupShootDayNumber;
        DayBeingEdited.CalendarDate = PopupCalendarDate.Date;
        await DayBeingEdited.SaveDayCommand.ExecuteAsync(null);

        IsDayPopupOpen = false;
        DayBeingEdited = null;
    }

    [RelayCommand]
    public void CancelDayDialog()
    {
        IsDayPopupOpen = false;
        DayBeingEdited = null;
    }

    /// <summary>
    /// Prompts fail-safe confirmation before deleting a shoot day
    /// </summary>
    [RelayCommand]
    public void PromptDeleteDay(DayViewModel day)
    {
        if (day is null) return;

        DayToDelete = day;
        IsDayDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    public async Task ConfirmDeleteDay()
    {
        if (DayToDelete != null)
        {
            await DeleteDay(DayToDelete);
        }
        IsDayDeleteConfirmationOpen = false;
        DayToDelete = null;
    }

    [RelayCommand]
    public void CancelDeleteDay()
    {
        IsDayDeleteConfirmationOpen = false;
        DayToDelete = null;
    }

    /// <summary>
    /// Create a new day in the current project, auto-incrementing the day number and using today's date
    /// </summary>
    [RelayCommand]
    public async Task CreateDay()
    {
        if (CurrentProject is null)
            return;

        string nextDayNumber = "1";
        if (CurrentProject.Days != null && CurrentProject.Days.Count > 0)
        {
            var lastDay = CurrentProject.Days.Last();
            if (int.TryParse(lastDay.ShootDayNumber, out int lastNum))
            {
                nextDayNumber = (lastNum + 1).ToString();
            }
            else
            {
                nextDayNumber = (CurrentProject.Days.Count + 1).ToString();
            }
        }

        var newDay = new Day
        {
            ProjectId = CurrentProject.Id,
            ShootDayNumber = nextDayNumber,
            CalendarDate = DateTime.Today
        };
        var dayVM = new DayViewModel(_databaseService);
        dayVM.LoadFromModel(newDay);

        await dayVM.SaveDayCommand.ExecuteAsync(null);
        CurrentProject.Days.Add(dayVM);
        CurrentDay = dayVM;
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
        if (CurrentDay is null)
            return;
    }

    /// <summary>
    /// Sync changes to Supabase
    /// </summary>
    [RelayCommand]
    public async Task SyncToSupabase()
    {
    }
}