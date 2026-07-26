using Avalonia.Android;
using Android.App;
using Android.Content.PM;
using Android.Views;

namespace Logshot.Android;

[Activity(
    Label = "Logshot",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity
{
    // Avalonia 12 no longer uses this class for CustomizeAppBuilder.
    // This file remains empty!
}