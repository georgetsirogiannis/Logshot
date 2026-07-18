using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Logshot.ViewModels
{
    public partial class SetupGroupViewModel : ViewModelBase
    {
        private readonly DayViewModel _parentDay;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HeaderTitle))]
        private string _episode = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HeaderTitle))]
        private string _scene = string.Empty;

        [ObservableProperty]
        private bool _isCollapsed;

        // This collection holds only the takes for this specific Episode/Scene combination
        public ObservableCollection<TakeViewModel> GroupedTakes { get; } = new();

        // Generates the required mobile UI format: "ΕΠ 10 - ΣΚ 40 (3 Takes)"
        public string HeaderTitle => $"ΕΠ {Episode} - ΣΚ {Scene} ({GroupedTakes.Count} Takes)";

        public SetupGroupViewModel(string episode, string scene, DayViewModel parentDay)
        {
            Episode = episode;
            Scene = scene;
            _parentDay = parentDay;

            // Ensure the count in the header updates automatically whenever a take is added/removed
            GroupedTakes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HeaderTitle));
        }

        // -------------------------------------------------------------------------
        // 1. [ + SHOT ] Button Logic
        // -------------------------------------------------------------------------
        [RelayCommand]
        private void AddShot()
        {
            // Triggers the continuity engine in the parent DayViewModel.
            // This automatically queries the highest shot number and initializes Take 1.
            _parentDay.CreateTakeWithContinuity(Episode, Scene);
        }

        // -------------------------------------------------------------------------
        // 2. [ + TAKE ] Button Logic
        // -------------------------------------------------------------------------
        [RelayCommand]
        private void AddTake()
        {
            if (!GroupedTakes.Any()) return;

            // Find the most recent take in this specific group 
            var lastTake = GroupedTakes.OrderByDescending(t => t.CreatedAt).First();

            // Instruct the parent DayViewModel to duplicate the shot number and increment the take
            _parentDay.CreateSubsequentTake(Episode, Scene, lastTake.Shot, lastTake.TakeNumber);
        }
            // -------------------------------------------------------------------------
            // 3. [ Collapse / Expand ] Subheader Toggle
            // -------------------------------------------------------------------------
            [RelayCommand]
            private void ToggleCollapsed()
            {
                IsCollapsed = !IsCollapsed;
            }
        }
    }