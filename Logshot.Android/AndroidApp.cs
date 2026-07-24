using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Logshot;

namespace Logshot.Android;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();

        // Note: If you had any other specific configuration lines in your old MainActivity 
        // (like .UseReactiveUI() or third-party modules), chain them right here!
    }
}