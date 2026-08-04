using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Logshot.Services;
using Logshot.ViewModels;
using Logshot.Views;
using Logshot.Views.Android;
using System;

namespace Logshot;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Bring focused text box 160px above the sticky bottom buttons when focused
        Avalonia.Input.InputElement.GotFocusEvent.AddClassHandler<TextBox>(async (tb, e) =>
        {
            await System.Threading.Tasks.Task.Delay(150);
            if (tb.IsFocused)
            {
                tb.BringIntoView(new Rect(0, 0, Math.Max(tb.Bounds.Width, 100), Math.Max(tb.Bounds.Height, 40) + 30));
            }
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
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
            singleViewPlatform.MainView = new AndroidMainView
            {
                DataContext = new MainViewModel(dbService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}