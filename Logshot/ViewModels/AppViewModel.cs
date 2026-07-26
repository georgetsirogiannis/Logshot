using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Logshot.Models;
using Logshot.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Logshot.ViewModels;

public partial class AppViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private readonly System.Threading.SemaphoreSlim _loadLock = new(1, 1);

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

    [ObservableProperty]
    private string _loadingMessage = "Loading...";

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
        bool isWildShotSearch = queryTrimmed.Equals("wild shot", StringComparison.OrdinalIgnoreCase) ||
                                queryTrimmed.Equals("wild shots", StringComparison.OrdinalIgnoreCase);

        List<Take> matchingTakes;

        if (isWildShotSearch)
        {
            IsSearchActive = true;
            var allTakes = await _databaseService.GetTakesForProjectAsync(CurrentProject.Id);
            matchingTakes = allTakes.Where(t => t.IsWildShot).ToList();
        }
        else
        {
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
            matchingTakes = await _databaseService.GetTakesForEpisodeSceneAsync(CurrentProject.Id, episode, scene);
        }

        SearchResultGroups.Clear();

        if (matchingTakes.Count == 0)
        {
            HasNoSearchResults = true;
            return;
        }

        var takesByDay = matchingTakes.GroupBy(t => t.DayId).ToDictionary(g => g.Key, g => g.ToList());
        var projectDays = await _databaseService.GetDaysForProjectAsync(CurrentProject.Id);

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
                takeVM.RefreshCameraDataSync();
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

    // ADD THIS NEW PROPERTY
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeleteProjectCommand))]
    private string _deleteProjectConfirmationText = string.Empty;

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

    [RelayCommand]
    public async Task ConfirmAddWildShotDialog()
    {
        if (CurrentDay is null) return;
        var ep = PopupNewEpisode?.Trim() ?? string.Empty;
        IsAddScenePopupOpen = false;
        await CurrentDay.CreateWildShotAsync(ep);
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

        if (newValue != null)
        {
            _ = LoadDayTakesAsync(newValue);
        }
    }

    private async Task LoadDayTakesAsync(DayViewModel day)
    {
        LoadingMessage = $"Loading Day {day.ShootDayNumber}...";
        IsLoading = true;

        // Force Avalonia to render the loading progress bar before execution starts
        await Task.Yield();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Render);

        try
        {
            await day.LoadTakesCommand.ExecuteAsync(null);
        }
        finally
        {
            IsLoading = false;
        }
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
        await _loadLock.WaitAsync();
        LoadingMessage = "Loading projects...";
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
            _loadLock.Release();
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
        DeleteProjectConfirmationText = string.Empty; // Reset text field
        IsDeleteConfirmationOpen = true;
        IsProjectPopupOpen = false; // Hide the Edit modal when the confirmation pops up
    }

    private bool CanConfirmDeleteProject()
    {
        // Only allow deletion if the typed text matches the project name (case-insensitive)
        return ProjectToDelete != null &&
               string.Equals(DeleteProjectConfirmationText?.Trim(), ProjectToDelete.Name?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand(CanExecute = nameof(CanConfirmDeleteProject))]
    public async Task ConfirmDeleteProject()
    {
        if (ProjectToDelete != null)
        {
            await DeleteProject(ProjectToDelete);
        }
        IsDeleteConfirmationOpen = false;
        ProjectToDelete = null;
        DeleteProjectConfirmationText = string.Empty;
    }

    [RelayCommand]
    public void CancelDeleteProject()
    {
        IsDeleteConfirmationOpen = false;
        ProjectToDelete = null;
        DeleteProjectConfirmationText = string.Empty;
    }

    [RelayCommand]
    public async Task DeleteProject(ProjectViewModel? project)
    {
        if (project != null && Projects.Contains(project))
        {
            Projects.Remove(project);
            await _databaseService.DeleteProjectAsync(project.ToModel());
        }

        if (CurrentProject?.Id == project?.Id)
        {
            CurrentProject = null;
            CurrentDay = null;
        }
    }

    [RelayCommand]
    public async Task SelectProject(ProjectViewModel? project)
    {
        if (project == null) return;
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
    public async Task DeleteDay(DayViewModel? day)
    {
        // FIX: Replaced the '?.Contains() == true' shortcut with explicit null checks
        if (day != null && CurrentProject != null && CurrentProject.Days.Contains(day))
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
    public async Task SelectDay(DayViewModel? day)
    {
        if (day == null) return;
        ClearSearch();
        CurrentDay = day;

        LoadingMessage = $"Loading Day {day.ShootDayNumber}...";
        IsLoading = true;
        await Task.Yield(); // Allows Avalonia to render the loading indicator immediately
        try
        {
            await CurrentDay.LoadTakesCommand.ExecuteAsync(null);
        }
        finally
        {
            IsLoading = false;
        }
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
            CameraData = CurrentDay.SyncCameraDataWithActiveCameras(lastValidTake?.CameraData)
        };
        var takeVM = new TakeViewModel(_databaseService);
        takeVM.LoadFromModel(newTake);

        await takeVM.SaveTakeCommand.ExecuteAsync(null);
        CurrentDay.Takes.Add(takeVM);
        await CurrentDay.UpdateTotalTakesCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task DeleteTake(TakeViewModel? take)
    {
        // FIX: Replaced the '?.Contains() == true' shortcut with explicit null checks
        if (take != null && CurrentDay != null && CurrentDay.Takes.Contains(take))
        {
            await CurrentDay.DeleteTakeCommand.ExecuteAsync(take);
            await CurrentDay.UpdateTotalTakesCommand.ExecuteAsync(null);
        }
    }

    // --- PDF Export State ---
    [ObservableProperty]
    private bool _isPdfExportErrorOpen = false;

    public bool IsPdfExportSupported => Services.PdfExportServiceRegistry.Instance?.IsSupported ?? false;

    // Delegate to ask the View to open the Save File Dialog
    public Action? RequestPdfFilePicker;

    [RelayCommand]
    public void ExportDayToPdf()
    {
        if (CurrentDay is null || !IsPdfExportSupported) return;

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
        var exporter = Services.PdfExportServiceRegistry.Instance;
        if (exporter == null || !IsPdfExportSupported) return;
        
        LoadingMessage = "Exporting PDF...";
        IsLoading = true;
        try
        {
            await Task.Run(async () =>
            {
                await exporter.GeneratePdfAsync(CurrentProject!, CurrentDay!, stream);
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

    /// <summary>
    /// Non-intrusive in-place merge of cloud data into active ViewModels.
    /// Preserves current selections, focus, and open views without duplicates.
    /// </summary>
    public async Task MergeCloudDataAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            var dbProjects = await _databaseService.GetAllProjectsAsync();
            foreach (var pModel in dbProjects)
            {
                var existingProj = Projects.FirstOrDefault(p => p.Id == pModel.Id);
                if (existingProj == null)
                {
                    var newProjVm = new ProjectViewModel(_databaseService);
                    newProjVm.LoadFromModel(pModel);
                    await newProjVm.LoadDaysCommand.ExecuteAsync(null);
                    Projects.Add(newProjVm);
                }
                else
                {
                    existingProj.Name = pModel.Name;
                    existingProj.Director = pModel.Director;
                    existingProj.Dop = pModel.Dop;
                    existingProj.ProductionCompany = pModel.ProductionCompany;
                    existingProj.ScriptSupervisor = pModel.ScriptSupervisor;
                }
            }

            // Clean up any projects deleted on another device
            var dbProjectIds = dbProjects.Select(p => p.Id).ToHashSet();
            for (int i = Projects.Count - 1; i >= 0; i--)
            {
                if (!dbProjectIds.Contains(Projects[i].Id))
                {
                    if (CurrentProject?.Id == Projects[i].Id)
                    {
                        CurrentProject = null;
                        CurrentDay = null;
                    }
                    Projects.RemoveAt(i);
                }
            }

            if (CurrentProject == null) return;

            var dbDays = await _databaseService.GetDaysForProjectAsync(CurrentProject.Id);
            foreach (var dModel in dbDays)
            {
                var existingDay = CurrentProject.Days.FirstOrDefault(d => d.Id == dModel.Id);
                if (existingDay == null)
                {
                    var newDayVm = new DayViewModel(_databaseService);
                    newDayVm.LoadFromModel(dModel);
                    CurrentProject.Days.Add(newDayVm);
                    CurrentProject.SortDays();
                }
                else
                {
                    existingDay.ShootDayNumber = dModel.ShootDayNumber;
                    existingDay.CalendarDate = dModel.CalendarDate;
                    existingDay.IsFinalized = dModel.IsFinalized;
                }
            }

            // Clean up any days deleted on another device
            var dbDayIds = dbDays.Select(d => d.Id).ToHashSet();
            for (int i = CurrentProject.Days.Count - 1; i >= 0; i--)
            {
                if (!dbDayIds.Contains(CurrentProject.Days[i].Id))
                {
                    if (CurrentDay?.Id == CurrentProject.Days[i].Id)
                    {
                        CurrentDay = null;
                    }
                    CurrentProject.Days.RemoveAt(i);
                }
            }

            if (CurrentDay == null) return;

            await CurrentDay.MergeTakesFromCloudAsync();
        }
        finally
        {
            _loadLock.Release();
        }
    }
}