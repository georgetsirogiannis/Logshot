using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class MainView : UserControl
{
    private const double MobileBreakpointWidth = 720;

    private Grid? _rootSplitGrid;
    private MainViewModel? _boundViewModel;
    private AccountCreationWindow? _accountCreationWindow;

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
                _boundViewModel.RequestOpenAccountCreation -= OnRequestOpenAccountCreation;
                _boundViewModel.RequestCloseAccountCreation -= OnRequestCloseAccountCreation;
            }

            // Subscribe to new context
            if (DataContext is MainViewModel vm)
            {
                _boundViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
                vm.AppViewModel.RequestPdfFilePicker += OnRequestPdfFilePicker;
                vm.RequestOpenAccountCreation += OnRequestOpenAccountCreation;
                vm.RequestCloseAccountCreation += OnRequestCloseAccountCreation;

                vm.InitializeApplicationCommand.Execute(null);
                UpdateLayoutMode(Bounds.Width);
                UpdateSidebarColumnWidth();
            }

        };

        SizeChanged += (_, e) => UpdateLayoutMode(e.NewSize.Width);
        UpdateLayoutMode(Bounds.Width);
    }

    private async void MainView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (DataContext is not MainViewModel vm ||
            vm.IsInfoViewOpen ||
            vm.AppViewModel.CurrentDay is not { IsLoadingTakes: false } day)
            return;

        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                e.Handled = true;
                vm.AppViewModel.OpenAddSceneDialogCommand.Execute(null);
                break;
            case Key.D2:
            case Key.NumPad2:
                e.Handled = true;
                await day.AddShotCommand.ExecuteAsync(null);
                break;
            case Key.D3:
            case Key.NumPad3:
                e.Handled = true;
                await day.AddTakeCommand.ExecuteAsync(null);
                break;
            case Key.OemComma:
            case Key.OemPeriod:
            case Key.Oem2:
                var focusedTake = topLevel is null ? null : GetFocusedTake(topLevel);
                if (focusedTake is null)
                    break;

                e.Handled = true;
                if (e.Key == Key.OemComma)
                    await focusedTake.TogglePickupCommand.ExecuteAsync(null);
                else if (e.Key == Key.OemPeriod)
                    await focusedTake.MarkCircledCommand.ExecuteAsync(null);
                else
                    await focusedTake.MarkFailedCommand.ExecuteAsync(null);
                break;
        }
    }

    private static TakeViewModel? GetFocusedTake(TopLevel topLevel)
    {
        var control = topLevel.FocusManager?.GetFocusedElement() as Control;
        while (control is not null)
        {
            if (control.DataContext is TakeViewModel take)
                return take;
            if (control.DataContext is CameraRollCell cameraRollCell)
                return cameraRollCell.Owner;

            control = control.Parent as Control ?? control.GetVisualParent() as Control;
        }

        return null;
    }

    private void OnRequestOpenAccountCreation()
    {
        if (_boundViewModel is null || _accountCreationWindow is not null)
            return;

        _accountCreationWindow = new AccountCreationWindow { DataContext = _boundViewModel };
        _accountCreationWindow.Closed += (_, _) => _accountCreationWindow = null;
        _accountCreationWindow.Show(TopLevel.GetTopLevel(this) as Window);
    }

    private void OnRequestCloseAccountCreation()
    {
        _accountCreationWindow?.Close();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InputPane != null)
        {
            topLevel.InputPane.StateChanged += InputPane_StateChanged;
        }
        topLevel?.AddHandler(KeyDownEvent, MainView_KeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InputPane != null)
        {
            topLevel.InputPane.StateChanged -= InputPane_StateChanged;
        }
        topLevel?.RemoveHandler(KeyDownEvent, MainView_KeyDown);
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