using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Logshot.ViewModels;
using System;
using System.Threading.Tasks;

namespace Logshot.Views.Android;

public partial class AndroidMainView : UserControl
{
    private MainViewModel? _boundViewModel;

    public AndroidMainView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                _boundViewModel = vm;
                vm.InitializeApplicationCommand.Execute(null);
            }
        };
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
}