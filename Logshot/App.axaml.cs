using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Logshot.Services;
using Logshot.ViewModels;
using Logshot.Views;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Logshot;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. Configure QuestPDF Community License
        QuestPDF.Settings.License = LicenseType.Community;

        // 2. Load the bundled Roboto Condensed font for the PDF Engine
        try
        {
            using var fontStream = AssetLoader.Open(new Uri("avares://Logshot/Assets/Fonts/RobotoCondensed-VariableFont_wght.ttf"));

            // FIX: Explicitly state we are using QuestPDF's FontManager, not Avalonia's.
            QuestPDF.Drawing.FontManager.RegisterFont(fontStream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load Roboto Condensed for PDF: {ex.Message}");
        }

        var dbService = new DatabaseService();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(dbService)
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel(dbService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}