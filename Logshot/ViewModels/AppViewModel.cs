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

    // --- Targeted Episode & Scene Search State ---
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearchActive = false;

    [ObservableProperty]
    private bool _hasNoSearchResults = false;

    [ObservableProperty]
    private ObservableCollection<DaySearchResultGroupViewModel> _searchResultGroups = new();

    partial void OnSearchQueryChanged(string value)
    {
        _ = ExecuteSearchAsync(value);
    }

    public async Task ExecuteSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || CurrentProject == null)
        {
            IsSearchActive = false;
            HasNoSearchResults = false;
            SearchResultGroups.Clear();
            return;
        }

        string queryTrimmed = query.Trim();
        char separator = '\0';
        if (queryTrimmed.Contains('/')) separator = '/';
        else if (queryTrimmed.Contains('.')) separator = '.';

        if (separator == '\0')
        {
            IsSearchActive = true;
            HasNoSearchResults = true;
            SearchResultGroups.Clear();
            return;
        }

        var parts = queryTrimmed.Split(separator, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            IsSearchActive = true;
            HasNoSearchResults = true;
            SearchResultGroups.Clear();
            return;
        }

        string episode = parts[0].Trim();
        string scene = parts[1].Trim();

        if (string.IsNullOrEmpty(episode) || string.IsNullOrEmpty(scene))
        {
            IsSearchActive = true;
            HasNoSearchResults = true;
            SearchResultGroups.Clear();
            return;
        }

        IsSearchActive = true;

        var matchingTakes = await _databaseService.GetTakesForEpisodeSceneAsync(CurrentProject.Id, episode, scene);

        SearchResultGroups.Clear();

        if (matchingTakes.Count == 0)
        {
            HasNoSearchResults = true;
            return;
        }

        var takesByDay = matchingTakes.GroupBy(t => t.DayId).ToDictionary(g => g.Key, g => g.ToList());
        var projectDays = await _databaseService.GetDaysForProjectAsync(CurrentProject.Id);

        // Sort days chronologically: earliest calendar date to latest
        var sortedDays = projectDays
            .Where(d => takesByDay.ContainsKey(d.Id))
            .OrderBy(d => d.CalendarDate)
            .ThenBy(d => int.TryParse(d.ShootDayNumber, out int n) ? n : 0);

        foreach (var dayModel in sortedDays)
        {
            var groupVM = new DaySearchResultGroupViewModel(_databaseService);
            groupVM.LoadFromModel(dayModel);
            groupVM.Takes.Clear();

            foreach (var take in takesByDay[dayModel.Id].OrderBy(t => t.SequenceOrder))
            {
                var takeVM = new TakeViewModel(_databaseService);
                takeVM.LoadFromModel(take);
                await takeVM.RefreshCameraDataCommand.ExecuteAsync(null);
                groupVM.Takes.Add(takeVM);
            }

            SearchResultGroups.Add(groupVM);
        }

        HasNoSearchResults = SearchResultGroups.Count == 0;
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
        IsSearchActive = false;
        HasNoSearchResults = false;
        SearchResultGroups.Clear();
    }

    partial void OnCurrentProjectChanged(ProjectViewModel? oldValue, ProjectViewModel? newValue)
    {
        ClearSearch();
    }

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

        var ep = PopupNewEpisode.Trim();
        var sc = PopupNewScene.Trim();

        IsAddScenePopupOpen = false;
        await CurrentDay.CheckContinuityAndPromptAsync(ep, sc);
    }

    public bool IsTakeDeleteConfirmationOpen => CurrentDay?.IsTakeDeleteConfirmationOpen ?? false;

    partial void OnCurrentDayChanged(DayViewModel? oldValue, DayViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= CurrentDay_PropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += CurrentDay_PropertyChanged;
        }
        OnPropertyChanged(nameof(IsTakeDeleteConfirmationOpen));
    }

    private void CurrentDay_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DayViewModel.IsTakeDeleteConfirmationOpen))
        {
            OnPropertyChanged(nameof(IsTakeDeleteConfirmationOpen));
        }
    }

    [RelayCommand]
    public async Task ConfirmDeleteTake()
    {
        if (CurrentDay != null)
        {
            await CurrentDay.ConfirmDeleteTakeCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    public void CancelDeleteTake()
    {
        CurrentDay?.CancelDeleteTakeCommand.Execute(null);
    }

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

    [RelayCommand]
    public async Task SelectProject(ProjectViewModel project)
    {
        CurrentProject = project;
        await CurrentProject.LoadDaysCommand.ExecuteAsync(null);
        CurrentDay = null;
    }

    [RelayCommand]
    public void OpenEditDayDialog(DayViewModel day)
    {
        if (day is null) return;

        DayBeingEdited = day;
        PopupShootDayNumber = day.ShootDayNumber;
        PopupCalendarDate = new DateTimeOffset(day.CalendarDate);
        IsDayPopupOpen = true;
    }

    [RelayCommand]
    public async Task SaveDayDialog()
    {
        if (DayBeingEdited is null || string.IsNullOrWhiteSpace(PopupShootDayNumber))
            return;

        DayBeingEdited.ShootDayNumber = PopupShootDayNumber;
        DayBeingEdited.CalendarDate = PopupCalendarDate.Date;
        await DayBeingEdited.SaveDayCommand.ExecuteAsync(null);
        CurrentProject?.SortDays();

        IsDayPopupOpen = false;
        DayBeingEdited = null;
    }

    [RelayCommand]
    public void CancelDayDialog()
    {
        IsDayPopupOpen = false;
        DayBeingEdited = null;
    }

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
        CurrentProject.SortDays();
        CurrentDay = dayVM;
    }

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

    [RelayCommand]
    public async Task SelectDay(DayViewModel day)
    {
        ClearSearch();
        CurrentDay = day;
        await CurrentDay.LoadTakesCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task CreateTake()
    {
        if (CurrentDay is null)
            return;

        var lastValidTake = CurrentDay.Takes.LastOrDefault(t => !t.IsSoundOnlyRow && !t.HasVoidedCameras)
                            ?? CurrentDay.Takes.LastOrDefault(t => !t.IsSoundOnlyRow)
                            ?? CurrentDay.Takes.LastOrDefault();

        var newTake = new Take
        {
            DayId = CurrentDay.Id,
            SequenceOrder = CurrentDay.Takes.Count,
            Shot = lastValidTake?.Shot ?? CurrentDay.CurrentShot,
            TakeNumber = (lastValidTake?.TakeNumber ?? 0) + 1,
            Episode = lastValidTake?.Episode ?? string.Empty,
            Scene = lastValidTake?.Scene ?? string.Empty,
            CameraData = lastValidTake?.CameraData ?? "{}"
        };
        var takeVM = new TakeViewModel(_databaseService);
        takeVM.LoadFromModel(newTake);

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        CurrentDay.Takes.Add(takeVM);
        await CurrentDay.UpdateTotalTakesCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task DeleteTake(TakeViewModel take)
    {
        if (CurrentDay?.Takes.Contains(take) == true)
        {
            await CurrentDay.DeleteTakeCommand.ExecuteAsync(take);
            await CurrentDay.UpdateTotalTakesCommand.ExecuteAsync(null);
        }
    }

    // --- PDF Export State ---
    [ObservableProperty]
    private bool _isPdfExportErrorOpen = false;

    // Delegate to ask the View to open the Save File Dialog
    public Action? RequestPdfFilePicker;

    [RelayCommand]
    public void ExportDayToPdf()
    {
        if (CurrentDay is null) return;

        // Ensure the user has finalized the day
        if (!CurrentDay.IsFinalized)
        {
            IsPdfExportErrorOpen = true;
            return;
        }

        // Trigger the file picker in the View
        RequestPdfFilePicker?.Invoke();
    }

    [RelayCommand]
    public void ClosePdfExportError()
    {
        IsPdfExportErrorOpen = false;
    }

    /// <summary>
    /// Called by the View once the user selects a save destination.
    /// Passes a Stream so it works natively on both Desktop and Android.
    /// </summary>
    public async Task GeneratePdfAsync(System.IO.Stream stream)
    {
        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                // Activate the PDF Export Engine
                var service = new PdfExportService(CurrentProject!, CurrentDay!);
                service.Generate(stream);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PDF Export failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SyncToSupabase()
    {
    }
}