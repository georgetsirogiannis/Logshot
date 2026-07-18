using System;
using System.Collections.ObjectModel;
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
            CreatedAt = CreatedAt
        };
    }

    [RelayCommand]
    public async Task SaveProject()
    {
        // Stub for Phase 2 implementation
        await _databaseService.SaveProjectAsync(ToModel());
    }

    [RelayCommand]
    public async Task LoadDays()
    {
        // Stub for Phase 2 implementation - will load all days for this project
        // TODO: Implement day loading from database
    }

    [RelayCommand]
    public async Task AddDay()
    {
        // Stub for Phase 2 implementation - creates new day in this project
        // TODO: Implement adding new day
    }

    [RelayCommand]
    public async Task DeleteDay(DayViewModel day)
    {
        // Stub for Phase 2 implementation
        // TODO: Implement day deletion
    }
}
