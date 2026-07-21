using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class MainView : UserControl
{
    private const double MobileBreakpointWidth = 720;

    private Grid? _rootSplitGrid;
    private MainViewModel? _boundViewModel;

    public MainView()
    {
        InitializeComponent();
        _rootSplitGrid = this.FindControl<Grid>("RootSplitGrid");

        DataContextChanged += (_, _) =>
        {
            // Unsubscribe from old context
            if (_boundViewModel is not null)
            {
                _boundViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _boundViewModel.AppViewModel.RequestPdfFilePicker -= OnRequestPdfFilePicker;
            }

            // Subscribe to new context
            if (DataContext is MainViewModel vm)
            {
                _boundViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
                vm.AppViewModel.RequestPdfFilePicker += OnRequestPdfFilePicker;

                vm.InitializeApplicationCommand.Execute(null);
                UpdateLayoutMode(Bounds.Width);
                UpdateSidebarColumnWidth();
            }
        };

        SizeChanged += (_, e) => UpdateLayoutMode(e.NewSize.Width);
        UpdateLayoutMode(Bounds.Width);
    }

    private async void OnRequestPdfFilePicker()
    {
        if (_boundViewModel?.AppViewModel.CurrentProject == null || _boundViewModel?.AppViewModel.CurrentDay == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var projectName = _boundViewModel.AppViewModel.CurrentProject.Name.Replace(" ", "_");
        var dayNum = _boundViewModel.AppViewModel.CurrentDay.ShootDayNumber;
        var suggestedName = $"{projectName}_DAY_{dayNum}.pdf";

        // Native File Picker (Works on Desktop and Mobile)
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Day Report to PDF",
            DefaultExtension = "pdf",
            SuggestedFileName = suggestedName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF Document") { Patterns = new[] { "*.pdf" } }
            }
        });

        if (file != null)
        {
            try
            {
                // Get a writable stream from the chosen destination and pass it to the generator
                await using var stream = await file.OpenWriteAsync();
                await _boundViewModel.AppViewModel.GeneratePdfAsync(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save PDF: {ex.Message}");
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSidebarOpen))
        {
            UpdateSidebarColumnWidth();
        }
    }

    private void UpdateLayoutMode(double width)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsMobileLayout = width < MobileBreakpointWidth;
        }
    }

    private void UpdateSidebarColumnWidth()
    {
        if (_rootSplitGrid is null || DataContext is not MainViewModel vm)
            return;

        if (_rootSplitGrid.ColumnDefinitions.Count > 0)
        {
            _rootSplitGrid.ColumnDefinitions[0].Width = vm.IsSidebarOpen
                ? new GridLength(280)
                : new GridLength(0);
        }
    }
}