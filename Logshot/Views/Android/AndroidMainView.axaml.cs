using System;
using Avalonia.Controls;
using Logshot.ViewModels;

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
}