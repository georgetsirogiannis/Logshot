using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InputPane != null)
        {
            topLevel.InputPane.StateChanged += InputPane_StateChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InputPane != null)
        {
            topLevel.InputPane.StateChanged -= InputPane_StateChanged;
        }
    }

    private void InputPane_StateChanged(object? sender, InputPaneStateEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        double keyboardHeight = topLevel.InputPane.OccludedRect.Height;

        var workspaceViewport = this.FindControl<Grid>("WorkspaceViewport");
        if (workspaceViewport != null)
        {
            workspaceViewport.Margin = new Thickness(0, 0, 0, keyboardHeight);
        }

        if (keyboardHeight > 0)
        {
            var focusedControl = topLevel.FocusManager?.GetFocusedElement() as Control;
            if (focusedControl != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    await Task.Delay(100);
                    focusedControl.BringIntoView(new Rect(0, 0, Math.Max(focusedControl.Bounds.Width, 100), Math.Max(focusedControl.Bounds.Height, 40) + 30));
                }, Avalonia.Threading.DispatcherPriority.Render);
            }
        }
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