using System;
using Avalonia.Media;
using DaisyOS.Core.Models;

namespace DaisyOS.Shell.ViewModels;

public class LauncherItemViewModel
{
    public AppEntry App { get; }
    public string Name => App.Name;
    public string Description => string.IsNullOrWhiteSpace(App.Description) ? "Application" : App.Description;
    public string IconName => App.Icon;
    public IImage? IconBitmap { get; }

    public LauncherItemViewModel(AppEntry app, IImage? iconBitmap = null)
    {
        App = app;
        IconBitmap = iconBitmap;
    }
}
