using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Logshot.ViewModels;

namespace Logshot.Views;

public partial class InfoView : UserControl
{
    private MainViewModel? _boundViewModel;

    public InfoView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_boundViewModel != null)
            {
                _boundViewModel.RequestExportDbFilePicker -= OnRequestExportDbFilePicker;
                _boundViewModel.RequestImportDbFilePicker -= OnRequestImportDbFilePicker;
            }

            if (DataContext is MainViewModel vm)
            {
                _boundViewModel = vm;
                vm.RequestExportDbFilePicker += OnRequestExportDbFilePicker;
                vm.RequestImportDbFilePicker += OnRequestImportDbFilePicker;
            }
        };
    }

    private async void OnRequestExportDbFilePicker()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || _boundViewModel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Database",
            DefaultExtension = "db",
            SuggestedFileName = "logshot_backup.db",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("SQLite Database") { Patterns = new[] { "*.db" } }
            }
        });

        if (file != null)
        {
            string? destPath = file.TryGetLocalPath();
            if (!string.IsNullOrEmpty(destPath))
            {
                await _boundViewModel.ProcessExportDbAsync(destPath);
            }
        }
    }

    private async void OnRequestImportDbFilePicker()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || _boundViewModel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Database",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("SQLite Database") { Patterns = new[] { "*.db" } }
            }
        });

        if (files != null && files.Count > 0)
        {
            string? sourcePath = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(sourcePath))
            {
                await _boundViewModel.ProcessImportDbAsync(sourcePath);
            }
        }
    }
}