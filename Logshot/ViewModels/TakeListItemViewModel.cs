using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Logshot.ViewModels;

/// <summary>
/// Base class for items in a flattened, virtualizable take list.
/// Allows mixing group headers and take cards in a single ListBox.
/// </summary>
public abstract partial class TakeListItemViewModel : ViewModelBase
{
    public abstract bool IsGroupHeader { get; }
}

/// <summary>
/// Represents a collapsible group header in the flattened take list.
/// </summary>
public partial class TakeListGroupHeaderViewModel : TakeListItemViewModel
{
    public override bool IsGroupHeader => true;

    private string _headerTitle = string.Empty;
    public string HeaderTitle
    {
        get => _headerTitle;
        private set => SetProperty(ref _headerTitle, value);
    }

    private bool _isCollapsed;
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => SetProperty(ref _isCollapsed, value);
    }

    private string _setupKey = string.Empty;
    public string SetupKey
    {
        get => _setupKey;
        private set => SetProperty(ref _setupKey, value);
    }

    private readonly DayViewModel _parentDay;
    private readonly SetupGroupViewModel _group;

    public ICommand ToggleCollapsedCommand { get; }
    public ICommand AddShotCommand { get; }
    public ICommand AddTakeCommand { get; }

    public TakeListGroupHeaderViewModel(DayViewModel parentDay, SetupGroupViewModel group)
    {
        _parentDay = parentDay;
        _group = group;
        _setupKey = $"{group.Episode}|{group.Scene}";
        _headerTitle = group.HeaderTitle;
        ToggleCollapsedCommand = new RelayCommand(ToggleCollapsed);
        AddShotCommand = new AsyncRelayCommand(AddShot);
        AddTakeCommand = new AsyncRelayCommand(AddTake);
    }

    private void ToggleCollapsed()
    {
        IsCollapsed = !IsCollapsed;
        _group.IsCollapsed = IsCollapsed;
        // Notify parent to rebuild the visible list
        _parentDay.RebuildFlatTakeList();
    }

    private async Task AddShot()
    {
        var parts = SetupKey.Split('|');
        if (parts.Length == 2)
        {
            await _parentDay.AddShotToSetup((parts[0], parts[1]));
        }
    }

    private async Task AddTake()
    {
        var parts = SetupKey.Split('|');
        if (parts.Length == 2)
        {
            await _parentDay.AddTakeToSetup((parts[0], parts[1]));
        }
    }
}

/// <summary>
/// Wraps a TakeViewModel for inclusion in the flattened list.
/// </summary>
public partial class TakeListTakeViewModel : TakeListItemViewModel
{
    public override bool IsGroupHeader => false;

    public TakeViewModel Take { get; }

    public TakeListTakeViewModel(TakeViewModel take)
    {
        Take = take;
    }
}
